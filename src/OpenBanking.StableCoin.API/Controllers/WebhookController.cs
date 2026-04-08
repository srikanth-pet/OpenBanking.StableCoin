using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Domain.Enums;
using OpenBanking.StableCoin.Infrastructure.Coinbase.Webhook;

namespace OpenBanking.StableCoin.API.Controllers;

[ApiController]
[Route("api/v1/webhooks/coinbase")]
[AllowAnonymous]
public sealed class WebhookController : ControllerBase
{
    private readonly ICoinbaseWebhookValidator _webhookValidator;
    private readonly IStablecoinOrderRepository _orderRepo;
    private readonly IWalletTransferRepository _transferRepo;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        ICoinbaseWebhookValidator webhookValidator,
        IStablecoinOrderRepository orderRepo,
        IWalletTransferRepository transferRepo,
        ILogger<WebhookController> logger)
    {
        _webhookValidator = webhookValidator;
        _orderRepo = orderRepo;
        _transferRepo = transferRepo;
        _logger = logger;
    }

    /// <summary>Receives Coinbase webhook events for order and transfer confirmations</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        // Read raw body — required for HMAC signature verification
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var signature = Request.Headers["CB-SIGNATURE"].FirstOrDefault() ?? string.Empty;
        var timestamp = Request.Headers["CB-TIMESTAMP"].FirstOrDefault() ?? string.Empty;

        // 1. Validate timestamp freshness (replay protection)
        if (!_webhookValidator.IsTimestampFresh(timestamp))
        {
            _logger.LogWarning("Webhook rejected: stale timestamp {Timestamp}", timestamp);
            return BadRequest("Webhook timestamp is stale or invalid.");
        }

        // 2. Validate HMAC signature
        if (!_webhookValidator.ValidateSignature(payload, signature, timestamp))
        {
            _logger.LogWarning("Webhook rejected: invalid HMAC signature");
            return BadRequest("Invalid webhook signature.");
        }

        // 3. Process event — always return 200 so Coinbase doesn't retry valid events
        try
        {
            await ProcessEventAsync(payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process webhook event");
            // Return 200 anyway to prevent unbounded retries from Coinbase
            // Failed processing should be handled via a dead-letter queue in production
        }

        return Ok();
    }

    private async Task ProcessEventAsync(string payload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp)) return;
        var eventType = typeProp.GetString() ?? string.Empty;

        _logger.LogInformation("Processing Coinbase webhook event: {EventType}", eventType);

        switch (eventType)
        {
            case "order.filled":
                await HandleOrderFilledAsync(root, ct);
                break;
            case "order.cancelled":
            case "order.canceled":
                await HandleOrderCancelledAsync(root, ct);
                break;
            case "transfer.completed":
                await HandleTransferCompletedAsync(root, ct);
                break;
            case "transfer.failed":
                await HandleTransferFailedAsync(root, ct);
                break;
            default:
                _logger.LogDebug("Ignoring unhandled webhook event type: {EventType}", eventType);
                break;
        }
    }

    private async Task HandleOrderFilledAsync(JsonElement root, CancellationToken ct)
    {
        var orderId = GetString(root, "order_id");
        if (orderId == null) return;

        var order = await _orderRepo.GetByCoinbaseOrderIdAsync(orderId, ct);
        if (order == null) return;

        order.UpdateStatus(
            OrderStatus.Filled,
            filledAmount: GetDecimal(root, "filled_size"),
            filledValue: GetDecimal(root, "filled_value"),
            totalFees: GetDecimal(root, "total_fees"),
            averageFilledPrice: GetDecimal(root, "average_filled_price"),
            rawResponse: root.GetRawText());

        await _orderRepo.UpdateAsync(order, ct);
        _logger.LogInformation("Order {OrderId} marked as Filled via webhook", orderId);
    }

    private async Task HandleOrderCancelledAsync(JsonElement root, CancellationToken ct)
    {
        var orderId = GetString(root, "order_id");
        if (orderId == null) return;

        var order = await _orderRepo.GetByCoinbaseOrderIdAsync(orderId, ct);
        if (order == null || order.IsTerminal) return;

        order.UpdateStatus(OrderStatus.Cancelled, rawResponse: root.GetRawText());
        await _orderRepo.UpdateAsync(order, ct);
        _logger.LogInformation("Order {OrderId} marked as Cancelled via webhook", orderId);
    }

    private async Task HandleTransferCompletedAsync(JsonElement root, CancellationToken ct)
    {
        var transferId = GetString(root, "id");
        if (transferId == null) return;

        var transfer = await _transferRepo.GetByCoinbaseTransferIdAsync(transferId, ct);
        if (transfer == null) return;

        var txHash = GetString(root, "network.hash") ?? GetString(root, "transaction_hash") ?? string.Empty;
        transfer.MarkConfirmed(txHash);
        await _transferRepo.UpdateAsync(transfer, ct);
        _logger.LogInformation("Transfer {TransferId} confirmed with hash {Hash}", transferId, txHash);
    }

    private async Task HandleTransferFailedAsync(JsonElement root, CancellationToken ct)
    {
        var transferId = GetString(root, "id");
        if (transferId == null) return;

        var transfer = await _transferRepo.GetByCoinbaseTransferIdAsync(transferId, ct);
        if (transfer == null) return;

        var reason = GetString(root, "status_reason") ?? "Transfer failed";
        transfer.MarkFailed(reason);
        await _transferRepo.UpdateAsync(transfer, ct);
        _logger.LogWarning("Transfer {TransferId} failed: {Reason}", transferId, reason);
    }

    private static string? GetString(JsonElement root, string path)
    {
        var parts = path.Split('.');
        JsonElement current = root;
        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current)) return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static decimal? GetDecimal(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var prop)) return null;
        var str = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
        return decimal.TryParse(str, out var val) ? val : null;
    }
}

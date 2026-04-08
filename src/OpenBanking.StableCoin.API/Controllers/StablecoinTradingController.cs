using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenBanking.StableCoin.API.Models;
using OpenBanking.StableCoin.Application.Common;
using OpenBanking.StableCoin.Application.DTOs.Trading;
using OpenBanking.StableCoin.Application.Interfaces.Services;

namespace OpenBanking.StableCoin.API.Controllers;

[ApiController]
[Route("api/v1/stablecoin/trading")]
[Authorize]
[Produces("application/json")]
public sealed class StablecoinTradingController : ControllerBase
{
    private readonly IStablecoinTradingService _tradingService;
    private readonly IValidator<PlaceOrderRequest> _placeOrderValidator;
    private readonly IValidator<OrderPreviewRequest> _previewValidator;

    private string CustomerId =>
        User.FindFirstValue("customer_id")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("customer_id claim is missing from token.");

    public StablecoinTradingController(
        IStablecoinTradingService tradingService,
        IValidator<PlaceOrderRequest> placeOrderValidator,
        IValidator<OrderPreviewRequest> previewValidator)
    {
        _tradingService = tradingService;
        _placeOrderValidator = placeOrderValidator;
        _previewValidator = previewValidator;
    }

    /// <summary>Get a real-time price quote for USDC-USD</summary>
    [HttpGet("quote")]
    [ProducesResponseType<PriceQuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQuote(
        [FromQuery] string productId,
        [FromQuery] Domain.Enums.OrderSide side,
        [FromQuery] decimal amount,
        CancellationToken ct)
    {
        if (amount <= 0) return BadRequest(Error("INVALID_AMOUNT", "Amount must be greater than zero."));
        var result = await _tradingService.GetPriceQuoteAsync(
            CustomerId, new PriceQuoteRequest(productId, side, amount), ct);
        return ToActionResult(result);
    }

    /// <summary>Preview an order — returns fees and estimated fill before committing</summary>
    [HttpPost("orders/preview")]
    [ProducesResponseType<OrderPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewOrder(
        [FromBody] OrderPreviewRequest request, CancellationToken ct)
    {
        var validation = await _previewValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationError(validation);

        var result = await _tradingService.PreviewOrderAsync(CustomerId, request, ct);
        return ToActionResult(result);
    }

    /// <summary>Place a buy or sell order for USDC</summary>
    [HttpPost("orders")]
    [ProducesResponseType<PlaceOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? headerIdempotencyKey,
        CancellationToken ct)
    {
        // Header Idempotency-Key takes precedence over body field
        var effectiveRequest = headerIdempotencyKey != null
            ? request with { IdempotencyKey = headerIdempotencyKey }
            : request;

        var validation = await _placeOrderValidator.ValidateAsync(effectiveRequest, ct);
        if (!validation.IsValid) return ValidationError(validation);

        var result = await _tradingService.PlaceOrderAsync(CustomerId, effectiveRequest, ct);
        if (!result.IsSuccess) return ToActionResult(result);

        return CreatedAtAction(
            nameof(GetOrder),
            new { orderId = result.Data!.InternalOrderId },
            result.Data);
    }

    /// <summary>Get the status of an order by internal ID</summary>
    [HttpGet("orders/{orderId:guid}")]
    [ProducesResponseType<OrderStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
    {
        var result = await _tradingService.GetOrderStatusAsync(CustomerId, orderId, ct);
        return ToActionResult(result);
    }

    /// <summary>List orders with optional filters</summary>
    [HttpGet("orders")]
    [ProducesResponseType<PagedResult<OrderStatusResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOrders(
        [FromQuery] OrderListRequest request, CancellationToken ct)
    {
        var result = await _tradingService.ListOrdersAsync(CustomerId, request, ct);
        return ToActionResult(result);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess) return Ok(result.Data);
        return StatusCode(result.HttpStatusHint,
            Error(result.ErrorCode ?? "ERROR", result.ErrorMessage ?? "An error occurred."));
    }

    private IActionResult ValidationError(FluentValidation.Results.ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return BadRequest(new ApiErrorResponse
        {
            Type = "https://api.openbanking.com/errors/validation-error",
            Title = "Validation Error",
            Status = 400,
            Detail = "One or more validation errors occurred.",
            ErrorCode = "VALIDATION_ERROR",
            ValidationErrors = errors
        });
    }

    private static ApiErrorResponse Error(string errorCode, string detail) => new()
    {
        Type = $"https://api.openbanking.com/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}",
        Title = errorCode.Replace('_', ' ').ToLowerInvariant(),
        Status = 400,
        Detail = detail,
        ErrorCode = errorCode
    };
}

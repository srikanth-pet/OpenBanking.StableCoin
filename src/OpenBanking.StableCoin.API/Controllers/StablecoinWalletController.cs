using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenBanking.StableCoin.API.Models;
using OpenBanking.StableCoin.Application.Common;
using OpenBanking.StableCoin.Application.DTOs.Wallet;
using OpenBanking.StableCoin.Application.Interfaces.Services;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.API.Controllers;

[ApiController]
[Route("api/v1/stablecoin/wallet")]
[Authorize]
[Produces("application/json")]
public sealed class StablecoinWalletController : ControllerBase
{
    private readonly IStablecoinWalletService _walletService;
    private readonly IValidator<TransferRequest> _transferValidator;

    private string CustomerId =>
        User.FindFirstValue("customer_id")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("customer_id claim is missing from token.");

    public StablecoinWalletController(
        IStablecoinWalletService walletService,
        IValidator<TransferRequest> transferValidator)
    {
        _walletService = walletService;
        _transferValidator = transferValidator;
    }

    /// <summary>Get the customer's USDC balance</summary>
    [HttpGet("balance")]
    [ProducesResponseType<WalletBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(CancellationToken ct)
    {
        var result = await _walletService.GetBalanceAsync(CustomerId, ct);
        return ToActionResult(result);
    }

    /// <summary>Get the customer's USDC deposit address for the specified network</summary>
    [HttpGet("address/{network}")]
    [ProducesResponseType<DepositAddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepositAddress(SupportedNetwork network, CancellationToken ct)
    {
        var result = await _walletService.GetDepositAddressAsync(CustomerId, network, ct);
        return ToActionResult(result);
    }

    /// <summary>Send USDC to an external wallet address (on-chain transfer)</summary>
    [HttpPost("transfer")]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? headerIdempotencyKey,
        CancellationToken ct)
    {
        var effectiveRequest = headerIdempotencyKey != null
            ? request with { IdempotencyKey = headerIdempotencyKey }
            : request;

        var validation = await _transferValidator.ValidateAsync(effectiveRequest, ct);
        if (!validation.IsValid) return ValidationError(validation);

        var result = await _walletService.SendAsync(CustomerId, effectiveRequest, ct);
        if (!result.IsSuccess) return ToActionResult(result);

        return Accepted(result.Data);
    }

    /// <summary>List on-chain USDC transfer history</summary>
    [HttpGet("transactions")]
    [ProducesResponseType<PagedResult<TransactionHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] int pageSize = 20,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var result = await _walletService.GetTransactionHistoryAsync(
            CustomerId, new PaginationRequest { PageSize = Math.Clamp(pageSize, 1, 100), Cursor = cursor }, ct);
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

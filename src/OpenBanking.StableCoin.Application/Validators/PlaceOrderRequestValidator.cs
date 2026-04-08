using FluentValidation;
using OpenBanking.StableCoin.Application.DTOs.Trading;

namespace OpenBanking.StableCoin.Application.Validators;

public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    private static readonly string[] AllowedProducts = ["USDC-USD"];

    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.")
            .Must(p => AllowedProducts.Contains(p))
            .WithMessage($"Unsupported product. Allowed: {string.Join(", ", AllowedProducts)}");

        RuleFor(x => x.QuoteSize)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount cannot exceed $1,000,000 per order.");

        RuleFor(x => x.Side)
            .IsInEnum().WithMessage("Side must be Buy or Sell.");

        RuleFor(x => x.LimitPrice)
            .GreaterThan(0).WithMessage("LimitPrice must be greater than zero.")
            .When(x => x.LimitPrice.HasValue);

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(100).WithMessage("IdempotencyKey cannot exceed 100 characters.")
            .When(x => x.IdempotencyKey != null);
    }
}

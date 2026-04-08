using FluentValidation;
using OpenBanking.StableCoin.Application.DTOs.Trading;

namespace OpenBanking.StableCoin.Application.Validators;

public class OrderPreviewRequestValidator : AbstractValidator<OrderPreviewRequest>
{
    private static readonly string[] AllowedProducts = ["USDC-USD"];

    public OrderPreviewRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .Must(p => AllowedProducts.Contains(p))
            .WithMessage($"Unsupported product. Allowed: {string.Join(", ", AllowedProducts)}");

        RuleFor(x => x.QuoteSize)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Side)
            .IsInEnum();

        RuleFor(x => x.LimitPrice)
            .GreaterThan(0)
            .When(x => x.LimitPrice.HasValue);
    }
}

using System.Text.RegularExpressions;
using FluentValidation;
using OpenBanking.StableCoin.Application.DTOs.Wallet;

namespace OpenBanking.StableCoin.Application.Validators;

public class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    private static readonly Regex EvmAddressRegex =
        new(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

    private static readonly string[] AllowedAssets = ["USDC"];

    public TransferRequestValidator()
    {
        RuleFor(x => x.ToAddress)
            .NotEmpty().WithMessage("ToAddress is required.")
            .Matches(EvmAddressRegex).WithMessage("ToAddress must be a valid EVM address (0x followed by 40 hex characters).");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(100_000).WithMessage("Amount cannot exceed 100,000 USDC per transfer.");

        RuleFor(x => x.AssetId)
            .NotEmpty()
            .Must(a => AllowedAssets.Contains(a.ToUpperInvariant()))
            .WithMessage($"Unsupported asset. Allowed: {string.Join(", ", AllowedAssets)}");

        RuleFor(x => x.Network)
            .IsInEnum().WithMessage("Network must be Base, Ethereum, or Polygon.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(100)
            .When(x => x.IdempotencyKey != null);
    }
}

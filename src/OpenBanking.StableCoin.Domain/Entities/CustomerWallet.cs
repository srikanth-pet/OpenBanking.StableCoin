using OpenBanking.StableCoin.Domain.Common;
using OpenBanking.StableCoin.Domain.Enums;
using OpenBanking.StableCoin.Domain.Exceptions;

namespace OpenBanking.StableCoin.Domain.Entities;

public class CustomerWallet : AuditableEntity
{
    public string CustomerId { get; private set; } = string.Empty;
    public string CoinbaseWalletId { get; private set; } = string.Empty;
    public string CoinbaseAccountId { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Dictionary<SupportedNetwork, string> _depositAddresses = new();
    public IReadOnlyDictionary<SupportedNetwork, string> DepositAddresses => _depositAddresses;

    private CustomerWallet() { }

    public static CustomerWallet Create(
        string customerId,
        string coinbaseWalletId,
        string coinbaseAccountId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("CustomerId is required.", "INVALID_CUSTOMER_ID");

        return new CustomerWallet
        {
            CustomerId = customerId,
            CoinbaseWalletId = coinbaseWalletId,
            CoinbaseAccountId = coinbaseAccountId,
            IsActive = true,
            CreatedBy = customerId
        };
    }

    public void AddDepositAddress(SupportedNetwork network, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new DomainException($"Deposit address for {network} cannot be empty.", "INVALID_ADDRESS");
        _depositAddresses[network] = address;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string? GetDepositAddress(SupportedNetwork network) =>
        _depositAddresses.TryGetValue(network, out var address) ? address : null;

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

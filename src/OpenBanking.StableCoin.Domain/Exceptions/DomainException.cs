namespace OpenBanking.StableCoin.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class InsufficientBalanceException : DomainException
{
    public InsufficientBalanceException(string message = "Insufficient balance to complete this operation.")
        : base(message, "INSUFFICIENT_BALANCE") { }
}

public class InvalidWalletAddressException : DomainException
{
    public InvalidWalletAddressException(string address)
        : base($"'{address}' is not a valid EVM wallet address.", "INVALID_WALLET_ADDRESS") { }
}

public class WalletNotFoundException : DomainException
{
    public WalletNotFoundException(string customerId)
        : base($"No wallet found for customer '{customerId}'. Please provision a wallet first.", "WALLET_NOT_FOUND") { }
}

public class OrderNotFoundException : DomainException
{
    public OrderNotFoundException(string orderId)
        : base($"Order '{orderId}' was not found.", "ORDER_NOT_FOUND") { }
}

namespace OpenBanking.StableCoin.Domain.Enums;

public enum OrderStatus
{
    Pending = 1,
    Open = 2,
    Filled = 3,
    Cancelled = 4,
    Failed = 5,
    Expired = 6
}

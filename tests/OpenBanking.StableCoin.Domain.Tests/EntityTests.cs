using FluentAssertions;
using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;
using OpenBanking.StableCoin.Domain.Exceptions;

namespace OpenBanking.StableCoin.Domain.Tests;

public class StablecoinOrderTests
{
    [Fact]
    public void Create_WithValidInputs_CreatesOrderWithPendingStatus()
    {
        var order = StablecoinOrder.Create("cust-1", "USDC-USD", OrderSide.Buy, 100m, "bank-001");

        order.Status.Should().Be(OrderStatus.Pending);
        order.CustomerId.Should().Be("cust-1");
        order.ProductId.Should().Be("USDC-USD");
        order.Side.Should().Be(OrderSide.Buy);
        order.RequestedAmount.Should().Be(100m);
        order.IsTerminal.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithZeroOrNegativeAmount_ThrowsDomainException(decimal amount)
    {
        var act = () => StablecoinOrder.Create("cust-1", "USDC-USD", OrderSide.Buy, amount, "bank-001");
        act.Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void UpdateStatus_ToFilled_SetsCompletedAt()
    {
        var order = StablecoinOrder.Create("cust-1", "USDC-USD", OrderSide.Buy, 100m, "bank-001");
        order.UpdateStatus(OrderStatus.Filled, filledAmount: 100.05m, filledValue: 100m, totalFees: 0.50m);

        order.Status.Should().Be(OrderStatus.Filled);
        order.FilledAmount.Should().Be(100.05m);
        order.TotalFees.Should().Be(0.50m);
        order.CompletedAt.Should().NotBeNull();
        order.IsTerminal.Should().BeTrue();
    }
}

public class WalletTransferTests
{
    [Fact]
    public void Create_WithValidInputs_CreatesPendingTransfer()
    {
        var transfer = WalletTransfer.Create(
            "cust-1", "wallet-abc", "0xAbCd1234567890abcdef1234567890abcdef1234",
            50m, "USDC", SupportedNetwork.Base);

        transfer.Status.Should().Be(TransferStatus.Pending);
        transfer.Amount.Should().Be(50m);
        transfer.Network.Should().Be(SupportedNetwork.Base);
    }

    [Fact]
    public void MarkBroadcast_SetsStatusAndCoinbaseId()
    {
        var transfer = WalletTransfer.Create("c", "w", "0x1234567890123456789012345678901234567890", 1m, "USDC", SupportedNetwork.Ethereum);
        transfer.MarkBroadcast("cb-transfer-999");

        transfer.Status.Should().Be(TransferStatus.Broadcast);
        transfer.CoinbaseTransferId.Should().Be("cb-transfer-999");
    }

    [Fact]
    public void MarkConfirmed_SetsStatusAndTxHash()
    {
        var transfer = WalletTransfer.Create("c", "w", "0x1234567890123456789012345678901234567890", 1m, "USDC", SupportedNetwork.Base);
        transfer.MarkBroadcast("cb-001");
        transfer.MarkConfirmed("0xdeadbeef123");

        transfer.Status.Should().Be(TransferStatus.Confirmed);
        transfer.TransactionHash.Should().Be("0xdeadbeef123");
        transfer.ConfirmedAt.Should().NotBeNull();
    }
}

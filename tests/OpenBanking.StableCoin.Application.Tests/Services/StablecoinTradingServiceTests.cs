using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenBanking.StableCoin.Application.DTOs.Trading;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Application.Models.Coinbase;
using OpenBanking.StableCoin.Application.Services;
using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.Tests.Services;

public class StablecoinTradingServiceTests
{
    private readonly Mock<ICoinbaseAdvancedTradeClient> _coinbaseMock = new();
    private readonly Mock<IStablecoinOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICustomerWalletRepository> _walletRepoMock = new();
    private readonly StablecoinTradingService _sut;

    private const string CustomerId = "customer-123";

    public StablecoinTradingServiceTests()
    {
        _sut = new StablecoinTradingService(
            _coinbaseMock.Object,
            _orderRepoMock.Object,
            _walletRepoMock.Object,
            NullLogger<StablecoinTradingService>.Instance);
    }

    [Fact]
    public async Task PlaceOrderAsync_WhenNoWalletExists_ReturnsNotFound()
    {
        _walletRepoMock.Setup(r => r.GetByCustomerIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerWallet?)null);

        var result = await _sut.PlaceOrderAsync(CustomerId,
            new PlaceOrderRequest("USDC-USD", OrderSide.Buy, 100m));

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusHint.Should().Be(404);
        result.ErrorCode.Should().Be("WALLET_NOT_FOUND");
    }

    [Fact]
    public async Task PlaceOrderAsync_IdempotentReplay_ReturnsCachedOrder()
    {
        const string idempotencyKey = "test-key-123";
        var existingOrder = StablecoinOrder.Create(
            CustomerId, "USDC-USD", OrderSide.Buy, 100m, "bank-001", idempotencyKey);

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        var result = await _sut.PlaceOrderAsync(CustomerId,
            new PlaceOrderRequest("USDC-USD", OrderSide.Buy, 100m, IdempotencyKey: idempotencyKey));

        result.IsSuccess.Should().BeTrue();
        result.Data!.InternalOrderId.Should().Be(existingOrder.Id);
        // Coinbase should NOT be called — idempotent replay
        _coinbaseMock.Verify(c =>
            c.CreateOrderAsync(It.IsAny<CbCreateOrderRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PlaceOrderAsync_SuccessfulBuy_ReturnsOrderWithOpenStatus()
    {
        var wallet = CustomerWallet.Create(CustomerId, "wallet-abc", "account-xyz");

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StablecoinOrder?)null);
        _walletRepoMock.Setup(r => r.GetByCustomerIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<StablecoinOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _orderRepoMock.Setup(r => r.UpdateAsync(It.IsAny<StablecoinOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _coinbaseMock.Setup(c => c.CreateOrderAsync(It.IsAny<CbCreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CbCreateOrderResponse
            {
                Success = true,
                OrderId = "cb-order-999",
                SuccessResponse = new CbOrderSuccessResponse
                {
                    OrderId = "cb-order-999",
                    ProductId = "USDC-USD",
                    Side = "BUY",
                    ClientOrderId = "bank-001"
                }
            });

        var result = await _sut.PlaceOrderAsync(CustomerId,
            new PlaceOrderRequest("USDC-USD", OrderSide.Buy, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(OrderStatus.Open);
        result.Data.CoinbaseOrderId.Should().Be("cb-order-999");
    }

    [Fact]
    public async Task GetPriceQuoteAsync_ReturnsBidAskSpread()
    {
        _coinbaseMock.Setup(c => c.GetProductAsync("USDC-USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CbProductResponse
            {
                ProductId = "USDC-USD",
                Price = "0.9998",
                BestBid = "0.9997",
                BestAsk = "0.9999",
                QuoteCurrencyId = "USD",
                BaseCurrencyId = "USDC"
            });

        var result = await _sut.GetPriceQuoteAsync(CustomerId,
            new PriceQuoteRequest("USDC-USD", OrderSide.Buy, 100m));

        result.IsSuccess.Should().BeTrue();
        result.Data!.BestBidPrice.Should().Be(0.9997m);
        result.Data.BestAskPrice.Should().Be(0.9999m);
        // Buy side: effective price is ask
        result.Data.EffectivePrice.Should().Be(0.9999m);
    }
}

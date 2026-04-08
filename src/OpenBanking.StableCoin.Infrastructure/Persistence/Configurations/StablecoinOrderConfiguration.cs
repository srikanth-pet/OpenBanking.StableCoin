using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Infrastructure.Persistence.Configurations;

public class StablecoinOrderConfiguration : IEntityTypeConfiguration<StablecoinOrder>
{
    public void Configure(EntityTypeBuilder<StablecoinOrder> builder)
    {
        builder.ToTable("stablecoin_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.CoinbaseOrderId).HasMaxLength(256);
        builder.Property(x => x.ClientOrderId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ProductId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Side).IsRequired().HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RequestedAmount).HasColumnType("numeric(20,8)").IsRequired();
        builder.Property(x => x.FilledAmount).HasColumnType("numeric(20,8)");
        builder.Property(x => x.FilledValue).HasColumnType("numeric(20,8)");
        builder.Property(x => x.TotalFees).HasColumnType("numeric(20,8)");
        builder.Property(x => x.AverageFilledPrice).HasColumnType("numeric(20,8)");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.FailureReason).HasMaxLength(2048);
        builder.Property(x => x.RawCoinbaseResponse).HasColumnType("text");
        builder.Property(x => x.CreatedBy).HasMaxLength(128);

        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt })
            .HasDatabaseName("ix_stablecoin_orders_customer_created");
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique().HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ix_stablecoin_orders_idempotency_key");
        builder.HasIndex(x => x.CoinbaseOrderId)
            .HasDatabaseName("ix_stablecoin_orders_coinbase_order_id");
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_stablecoin_orders_status");
    }
}

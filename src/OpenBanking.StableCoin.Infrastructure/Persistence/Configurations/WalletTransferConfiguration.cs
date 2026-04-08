using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Infrastructure.Persistence.Configurations;

public class WalletTransferConfiguration : IEntityTypeConfiguration<WalletTransfer>
{
    public void Configure(EntityTypeBuilder<WalletTransfer> builder)
    {
        builder.ToTable("wallet_transfers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.CoinbaseWalletId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ToAddress).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Amount).HasColumnType("numeric(20,8)").IsRequired();
        builder.Property(x => x.AssetId).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Network).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CoinbaseTransferId).HasMaxLength(256);
        builder.Property(x => x.TransactionHash).HasMaxLength(128);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.FailureReason).HasMaxLength(2048);
        builder.Property(x => x.CreatedBy).HasMaxLength(128);

        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt })
            .HasDatabaseName("ix_wallet_transfers_customer_created");
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique().HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ix_wallet_transfers_idempotency_key");
        builder.HasIndex(x => x.CoinbaseTransferId)
            .HasDatabaseName("ix_wallet_transfers_coinbase_transfer_id");
    }
}

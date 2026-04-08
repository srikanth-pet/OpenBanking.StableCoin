using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Infrastructure.Persistence.Configurations;

public class CustomerWalletConfiguration : IEntityTypeConfiguration<CustomerWallet>
{
    public void Configure(EntityTypeBuilder<CustomerWallet> builder)
    {
        builder.ToTable("customer_wallets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.CoinbaseWalletId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.CoinbaseAccountId).IsRequired().HasMaxLength(256);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(128);

        // Store deposit addresses as JSON column
        builder.Property<Dictionary<SupportedNetwork, string>>("_depositAddresses")
            .HasColumnName("deposit_addresses")
            .HasColumnType("jsonb")
            .HasField("_depositAddresses");

        builder.HasIndex(x => x.CustomerId)
            .IsUnique().HasDatabaseName("ix_customer_wallets_customer_id");
    }
}

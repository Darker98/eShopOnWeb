using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.Property(u => u.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Quantity)
            .HasColumnType("decimal(18,4)");

        // Enforces AC-12 (duplicate usage reports do not double-bill) at the database level, not just in application code.
        builder.HasIndex(u => new { u.ProviderSubscriptionId, u.IdempotencyKey }).IsUnique();
    }
}

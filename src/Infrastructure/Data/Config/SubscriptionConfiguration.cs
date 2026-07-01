using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.Property(s => s.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.ProductHandle)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.State)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.BuyerId);
        builder.HasIndex(s => s.ProviderSubscriptionId).IsUnique();
    }
}

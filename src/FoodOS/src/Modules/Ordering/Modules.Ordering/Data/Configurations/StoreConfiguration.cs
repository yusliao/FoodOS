using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Stores");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerTenantId).HasMaxLength(64);
        builder.HasIndex(x => new { x.CustomerTenantId, x.CustomerOrgId });
        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Address).IsRequired().HasMaxLength(256);
        builder.Property(x => x.DeliveryWindow).HasMaxLength(64);
        builder.HasIndex(x => x.CustomerOrgId);
        builder.Ignore(x => x.DomainEvents);
    }
}

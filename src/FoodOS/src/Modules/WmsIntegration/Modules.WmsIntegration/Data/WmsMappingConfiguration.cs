using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.WmsIntegration.Data;

public sealed class WmsMappingConfiguration : IEntityTypeConfiguration<WmsMapping>
{
    public void Configure(EntityTypeBuilder<WmsMapping> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Mappings", WmsIntegrationDbContext.Schema);
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(40).IsRequired();
        builder.Property(x => x.FoodOsValue).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ExternalValue).HasMaxLength(160).IsRequired();
        builder.Property(x => x.FoodOsQuantityPerExternalUnit).HasPrecision(19, 6);
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.Kind, x.FoodOsValue }).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.Kind, x.ExternalValue }).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.IsActive, x.Kind });
        builder.Ignore(x => x.DomainEvents);
    }
}

using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.WmsIntegration.Data;

public sealed class WmsObjectCursorConfiguration : IEntityTypeConfiguration<WmsObjectCursor>
{
    public void Configure(EntityTypeBuilder<WmsObjectCursor> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ObjectCursors", WmsIntegrationDbContext.Schema);
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ExternalObjectId).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.EntityType, x.ExternalObjectId }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}

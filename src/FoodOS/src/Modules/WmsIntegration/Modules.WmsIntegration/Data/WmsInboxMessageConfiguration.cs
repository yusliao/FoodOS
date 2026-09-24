using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.WmsIntegration.Data;

public sealed class WmsInboxMessageConfiguration : IEntityTypeConfiguration<WmsInboxMessage>
{
    public void Configure(EntityTypeBuilder<WmsInboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InboxMessages", WmsIntegrationDbContext.Schema);
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ExternalEventId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ExternalObjectId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.SchemaVersion).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.ExternalEventId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ReceivedAtUtc });
        builder.Ignore(x => x.DomainEvents);
    }
}

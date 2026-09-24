using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.WmsIntegration.Data;

public sealed class WmsOutboundOperationConfiguration : IEntityTypeConfiguration<WmsOutboundOperation>
{
    public void Configure(EntityTypeBuilder<WmsOutboundOperation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OutboundOperations", WmsIntegrationDbContext.Schema);
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(40).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalOperationId).HasMaxLength(160);
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.Detail).HasMaxLength(1000);
        builder.HasIndex(
                "TenantId",
                nameof(WmsOutboundOperation.Provider),
                nameof(WmsOutboundOperation.ConnectionId),
                nameof(WmsOutboundOperation.IdempotencyKey))
            .IsUnique();
        builder.HasIndex(x => new { x.Status, x.UpdatedAt });
        builder.Ignore(x => x.DomainEvents);
    }
}

using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class TraceEventConfiguration : IEntityTypeConfiguration<TraceEvent>
{
    public void Configure(EntityTypeBuilder<TraceEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TraceEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BizStep).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Disposition).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Uom).IsRequired().HasMaxLength(16);
        builder.Property(x => x.SourceLocation).HasMaxLength(64);
        builder.Property(x => x.DestLocation).HasMaxLength(64);
        builder.Property(x => x.ActorUserId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RefType).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.LotId);
        builder.HasIndex(x => x.OccurredAt);
        builder.Ignore(x => x.DomainEvents);
    }
}

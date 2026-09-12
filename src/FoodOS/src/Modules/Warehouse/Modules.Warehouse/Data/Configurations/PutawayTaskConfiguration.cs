using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class PutawayTaskConfiguration : IEntityTypeConfiguration<PutawayTask>
{
    public void Configure(EntityTypeBuilder<PutawayTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PutawayTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(x => new { x.LotId, x.Status });
        builder.Ignore(x => x.DomainEvents);
    }
}

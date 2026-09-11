using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class PickTaskConfiguration : IEntityTypeConfiguration<PickTask>
{
    public void Configure(EntityTypeBuilder<PickTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PickTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Property(x => x.LotNo).HasMaxLength(64);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.ShortageQty).HasPrecision(18, 4);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(x => x.WaveId);
        builder.HasIndex(x => x.OrderId);
        builder.Ignore(x => x.IsComplete);
        builder.Ignore(x => x.DomainEvents);
    }
}

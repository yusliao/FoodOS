using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Inventory.Data.Configurations;

public sealed class DailyPlanConfiguration : IEntityTypeConfiguration<DailyPlan>
{
    public void Configure(EntityTypeBuilder<DailyPlan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DailyPlans");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WarehouseId, x.BusinessDate }).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(x => x.DomainEvents);
    }
}

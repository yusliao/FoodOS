using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class WaveConfiguration : IEntityTypeConfiguration<Wave>
{
    public void Configure(EntityTypeBuilder<Wave> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Waves");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).IsRequired().HasMaxLength(48);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.DailyPlanId, x.ZoneId }).IsUnique();
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasMany(x => x.Tasks)
            .WithOne()
            .HasForeignKey(t => t.WaveId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Tasks)
            .HasField("_tasks")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}

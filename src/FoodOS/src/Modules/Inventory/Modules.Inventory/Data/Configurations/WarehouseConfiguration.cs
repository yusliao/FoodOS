using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Inventory.Data.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Warehouses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.City).IsRequired().HasMaxLength(128);
        builder.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(64);

        builder.OwnsOne(x => x.Clock, clock =>
        {
            clock.Property(c => c.CutoffLocal).HasColumnName("CutoffLocal").IsRequired();
            clock.Property(c => c.LoadLocal).HasColumnName("LoadLocal").IsRequired();
            clock.Property(c => c.DeliverFromLocal).HasColumnName("DeliverFromLocal").IsRequired();
            clock.Property(c => c.DeliverToLocal).HasColumnName("DeliverToLocal").IsRequired();
            clock.Property(c => c.ReconcileLocal).HasColumnName("ReconcileLocal").IsRequired();
            clock.Property(c => c.TimeZoneId).HasColumnName("ClockTimeZoneId").HasMaxLength(64).IsRequired();
        });
        builder.Navigation(x => x.Clock).IsRequired();

        builder.HasMany(x => x.Zones)
            .WithOne()
            .HasForeignKey(z => z.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Zones).AutoInclude();

        builder.Ignore(x => x.DomainEvents);
    }
}

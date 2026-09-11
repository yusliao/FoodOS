using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Shipments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).IsRequired().HasMaxLength(48);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.RouteId, x.BusinessDate }).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasMany(x => x.Stops)
            .WithOne()
            .HasForeignKey(s => s.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Stops)
            .HasField("_stops")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.HasMany(x => x.Returns)
            .WithOne()
            .HasForeignKey(r => r.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Returns)
            .HasField("_returns")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}

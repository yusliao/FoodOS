using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class ShipmentLineConfiguration : IEntityTypeConfiguration<ShipmentLine>
{
    public void Configure(EntityTypeBuilder<ShipmentLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ShipmentLines");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasMany(x => x.Lots)
            .WithOne()
            .HasForeignKey(l => l.ShipmentLineId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lots)
            .HasField("_lots")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}

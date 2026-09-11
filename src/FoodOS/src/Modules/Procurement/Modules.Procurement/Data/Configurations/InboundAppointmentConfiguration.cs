using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class InboundAppointmentConfiguration : IEntityTypeConfiguration<InboundAppointment>
{
    public void Configure(EntityTypeBuilder<InboundAppointment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InboundAppointments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DockSlot).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VehicleNo).HasMaxLength(32);
        builder.HasIndex(x => x.PurchaseOrderId).IsUnique();
    }
}

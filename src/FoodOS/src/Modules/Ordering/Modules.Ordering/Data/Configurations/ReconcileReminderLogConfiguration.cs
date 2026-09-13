using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class ReconcileReminderLogConfiguration : IEntityTypeConfiguration<ReconcileReminderLog>
{
    public void Configure(EntityTypeBuilder<ReconcileReminderLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReconcileReminderLogs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WarehouseId, x.LocalDate }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}

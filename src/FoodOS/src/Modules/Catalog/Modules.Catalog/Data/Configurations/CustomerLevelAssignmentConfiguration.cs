using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Catalog.Data.Configurations;

public sealed class CustomerLevelAssignmentConfiguration : IEntityTypeConfiguration<CustomerLevelAssignment>
{
    public void Configure(EntityTypeBuilder<CustomerLevelAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomerLevelAssignments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CustomerOrgId).IsUnique();
        builder.HasIndex(x => x.CustomerLevelId);
        builder.HasOne<CustomerLevel>()
            .WithMany()
            .HasForeignKey(x => x.CustomerLevelId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.DomainEvents);
    }
}

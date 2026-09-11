using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class CustomerOrgConfiguration : IEntityTypeConfiguration<CustomerOrg>
{
    public void Configure(EntityTypeBuilder<CustomerOrg> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomerOrgs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Ignore(x => x.DomainEvents);
    }
}

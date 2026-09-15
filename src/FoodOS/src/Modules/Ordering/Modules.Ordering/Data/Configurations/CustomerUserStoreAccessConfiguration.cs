using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class CustomerUserStoreAccessConfiguration : IEntityTypeConfiguration<CustomerUserStoreAccess>
{
    public void Configure(EntityTypeBuilder<CustomerUserStoreAccess> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomerUserStoreAccesses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerTenantId).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.CustomerTenantId, x.UserId, x.StoreId }).IsUnique();
        builder.HasIndex(x => new { x.CustomerTenantId, x.CustomerOrgId, x.IsActive });
        builder.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CustomerOrg>().WithMany().HasForeignKey(x => x.CustomerOrgId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(x => x.DomainEvents);
    }
}

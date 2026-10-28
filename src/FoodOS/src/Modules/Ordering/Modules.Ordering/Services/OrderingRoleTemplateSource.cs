using FSH.Modules.Catalog.Contracts.Authorization;
using FSH.Modules.Identity.Contracts.Authorization;
using FSH.Modules.Ordering.Contracts.Authorization;

namespace FSH.Modules.Ordering.Services;

public sealed class OrderingRoleTemplateSource : IOperatorRoleTemplateSource
{
    public IEnumerable<OperatorRoleTemplate> GetTemplates() =>
    [
        new(
            OrderingRoleTemplates.CustomerServiceRole,
            OrderingRoleTemplates.CustomerServiceDescription,
            // Product read access lets the order screens show product names when amending.
            [.. OrderingRoleTemplates.CustomerServicePermissions, CatalogPermissions.Products.View]),
    ];
}

using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Domain;

namespace FSH.Modules.Catalog.Features.v1.PriceLists;

internal static class PriceListMappings
{
    public static PriceListDto ToDto(this PriceList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new PriceListDto(
            list.Id,
            list.Name,
            list.CustomerOrgId,
            list.Priority,
            list.ValidFrom,
            list.ValidTo,
            list.Lines
                .OrderBy(l => l.ProductId)
                .ThenBy(l => l.MinQty)
                .Select(l => new PriceListLineDto(l.Id, l.ProductId, l.MinQty, l.UnitPrice, l.Currency))
                .ToList());
    }
}

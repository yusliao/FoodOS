using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

/// <summary>
/// Imports the client's product list workbook (.xlsx with Item No. / Item Name / Unit / Sub-Unit columns).
/// New SKUs are created inactive with a zero price until a price list arrives; existing SKUs only get their
/// name refreshed, so prices, activation, brand, category and zone edited by operators are never overwritten.
/// With <paramref name="Apply"/> false nothing is written and the report shows what would happen.
/// </summary>
public sealed record ImportProductListCommand(byte[] Workbook, bool Apply) : ICommand<ProductImportReport>;

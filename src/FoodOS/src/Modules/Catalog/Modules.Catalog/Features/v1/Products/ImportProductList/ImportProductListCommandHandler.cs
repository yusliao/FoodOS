using System.Globalization;
using FSH.Modules.Catalog.Contracts.Dtos;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Products.ImportProductList;

public sealed class ImportProductListCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<ImportProductListCommand, ProductImportReport>
{
    public const string UnassignedBrand = "未指定品牌";

    public async ValueTask<ProductImportReport> Handle(ImportProductListCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var rows = XlsxSheetReader.ReadFirstSheet(command.Workbook);
        if (rows.Count < 2)
        {
            throw new InvalidDataException("The workbook has no data rows.");
        }

        var columns = Columns(rows[0]);
        var skipped = new List<string>();
        var warnings = new List<string>();
        var items = new List<ProductListItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            string itemNo = Cell(row, columns.ItemNo);
            string name = Cell(row, columns.Name);
            if (itemNo.Length == 0 && name.Length == 0)
            {
                continue;
            }

            string unit = Cell(row, columns.Unit);
            string subUnit = Cell(row, columns.SubUnit);
            var item = ProductListMapper.Map(itemNo, name, unit, subUnit, out string? reason);
            if (item is null)
            {
                skipped.Add($"第 {i + 1} 行 {itemNo} {name}：{reason}");
                continue;
            }

            if (!seen.Add(item.Sku))
            {
                skipped.Add($"第 {i + 1} 行 {item.Sku}：编号重复，只导入第一次出现的行");
                continue;
            }

            if (ProductListMapper.Uom(unit) is null)
            {
                warnings.Add($"{item.Sku} 单位「{unit}」无法识别，暂记为 EA");
            }

            items.Add(item);
        }

        var skus = items.Select(item => item.Sku).ToList();
        var existing = await dbContext.Products
            .Where(product => skus.Contains(product.Sku))
            .ToDictionaryAsync(product => product.Sku, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        var categories = await dbContext.Categories
            .ToDictionaryAsync(category => category.Name, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        var newItems = items.Where(item => !existing.ContainsKey(item.Sku)).ToList();
        var categoriesToCreate = newItems.Select(item => item.Category).Distinct(StringComparer.Ordinal)
            .Where(name => !categories.ContainsKey(name)).Order(StringComparer.Ordinal).ToList();
        var renames = items
            .Where(item => existing.TryGetValue(item.Sku, out var product) && product.Name != item.Name)
            .ToList();

        if (command.Apply)
        {
            await ApplyAsync(newItems, renames, existing, categories, categoriesToCreate, cancellationToken).ConfigureAwait(false);
        }

        return new ProductImportReport(
            command.Apply,
            rows.Count - 1,
            newItems.Count,
            renames.Count,
            items.Count - newItems.Count - renames.Count,
            skipped,
            warnings,
            newItems.GroupBy(item => item.Category).OrderByDescending(group => group.Count())
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            categoriesToCreate);
    }

    private async Task ApplyAsync(
        List<ProductListItem> newItems,
        List<ProductListItem> renames,
        Dictionary<string, Product> existing,
        Dictionary<string, Category> categories,
        List<string> categoriesToCreate,
        CancellationToken cancellationToken)
    {
        foreach (string name in categoriesToCreate)
        {
            var category = Category.Create(name, "甲方产品清单导入时按编号段自动创建。", null);
            dbContext.Categories.Add(category);
            categories[name] = category;
        }

        var brand = await dbContext.Brands
            .FirstOrDefaultAsync(item => item.Name == UnassignedBrand, cancellationToken)
            .ConfigureAwait(false);
        if (brand is null && newItems.Count > 0)
        {
            brand = Brand.Create(UnassignedBrand, "甲方产品清单没有品牌信息，导入后请在商品里补充。", null);
            dbContext.Brands.Add(brand);
        }

        foreach (var item in newItems)
        {
            string description = string.Create(
                CultureInfo.InvariantCulture,
                $"导入自甲方产品清单，原始单位：{item.SourceUnit} / {item.SourceSubUnit}。价格待补充。");
            var product = Product.Create(
                item.Sku,
                item.Name,
                description,
                brand!.Id,
                categories[item.Category].Id,
                Money.Zero(),
                stock: 0,
                item.Zone,
                baseUom: item.BaseUom,
                catchWeight: item.CatchWeight);
            // 没有价格前不能让饭店看到
            product.Update(item.Name, description, brand.Id, categories[item.Category].Id, isActive: false);
            dbContext.Products.Add(product);
        }

        // 已有商品只同步名称，价格、上下架、品牌、分类、温层都以运营在系统里的设置为准
        foreach (var item in renames)
        {
            var product = existing[item.Sku];
            product.Update(item.Name, product.Description, product.BrandId, product.CategoryId, product.IsActive);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static (int ItemNo, int Name, int Unit, int SubUnit) Columns(IReadOnlyList<string> header)
    {
        int Find(Func<string, bool> match) => header.Select((text, index) => (text, index))
            .Where(cell => match(cell.text)).Select(cell => cell.index).DefaultIfEmpty(-1).First();

        int itemNo = Find(text => Has(text, "Item No") || Has(text, "編號") || Has(text, "编号"));
        int name = Find(text => Has(text, "Item Name") || Has(text, "名稱") || Has(text, "名称"));
        int subUnit = Find(text => Has(text, "Sub-Unit") || Has(text, "次單位") || Has(text, "次单位"));
        int unit = Find(text => (Has(text, "Unit") || Has(text, "單位") || Has(text, "单位"))
            && !Has(text, "Sub") && !Has(text, "次"));
        if (itemNo < 0 || name < 0 || unit < 0)
        {
            throw new InvalidDataException("Header must contain Item No., Item Name and Unit columns.");
        }

        return (itemNo, name, unit, subUnit);
    }

    private static bool Has(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string Cell(IReadOnlyList<string> row, int index)
        => index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
}

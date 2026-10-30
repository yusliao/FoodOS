using System.Globalization;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Catalog.Contracts.v1.Products;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace FoodOS.DbMigrator.Imports;

/// <summary>
/// `import-products --file &lt;xlsx&gt;`: loads the client's product list into the operator catalog.
/// Preview by default; writes only with --apply-import. Safe to re-run (matched by SKU).
/// </summary>
internal sealed class ProductListImporter(IServiceProvider services)
{
    public async Task RunAsync(string filePath, bool apply, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Product list file not found: {filePath}", filePath);
        }

        var tenantStore = services.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var root = await tenantStore.GetAsync(MultitenancyConstants.Root.Id).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The root tenant is missing. Run the normal migrations first.");

        // 商品目录归运营方（root）所有
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(root);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        byte[] workbook = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        var report = await mediator.Send(new ImportProductListCommand(workbook, apply), ct).ConfigureAwait(false);

        var output = Console.Out;
        await output.WriteLineAsync(apply
            ? "[import-products] 已写入商品目录"
            : "[import-products] 预览（未写入任何数据，确认无误后加 --apply-import 执行）").ConfigureAwait(false);
        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"  数据行 {report.TotalRows} · 新建 {report.Created} · 改名 {report.Renamed} · 不变 {report.Unchanged} · 跳过 {report.Skipped.Count} · 警告 {report.Warnings.Count}"))
            .ConfigureAwait(false);
        if (report.CategoriesToCreate.Count > 0)
        {
            await output.WriteLineAsync("  新建分类：" + string.Join("、", report.CategoriesToCreate)).ConfigureAwait(false);
        }

        foreach (var (category, count) in report.CreatedByCategory)
        {
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"    {category}: {count}")).ConfigureAwait(false);
        }

        foreach (string line in report.Skipped)
        {
            await output.WriteLineAsync("  跳过 " + line).ConfigureAwait(false);
        }

        foreach (string line in report.Warnings)
        {
            await output.WriteLineAsync("  警告 " + line).ConfigureAwait(false);
        }

        if (report.Created > 0)
        {
            await output.WriteLineAsync("  新商品均为下架、价格 0，拿到价格表后再改价上架。").ConfigureAwait(false);
        }
    }
}

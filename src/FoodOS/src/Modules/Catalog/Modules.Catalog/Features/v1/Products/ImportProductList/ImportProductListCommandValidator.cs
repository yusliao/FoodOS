using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.Products;

namespace FSH.Modules.Catalog.Features.v1.Products.ImportProductList;

public sealed class ImportProductListCommandValidator : AbstractValidator<ImportProductListCommand>
{
    // 甲方清单约 1200 行、几百 KB；留足余量但挡住误传的大文件
    private const int MaxBytes = 20 * 1024 * 1024;

    public ImportProductListCommandValidator()
    {
        RuleFor(x => x.Workbook).NotEmpty().Must(bytes => bytes.Length <= MaxBytes)
            .WithMessage("The workbook must be smaller than 20 MB.");
    }
}

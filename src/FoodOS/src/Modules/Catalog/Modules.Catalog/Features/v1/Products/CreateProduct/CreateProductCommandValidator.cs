using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Domain;

namespace FSH.Modules.Catalog.Features.v1.Products.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PriceAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PriceCurrency).NotEmpty().Length(3);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TemperatureZone).NotEmpty().Must(v =>
            Enum.TryParse<TemperatureZone>(v, ignoreCase: true, out _));
        RuleFor(x => x.MinRemainingDaysOnShip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ShelfLifeDays).GreaterThanOrEqualTo(0).When(x => x.ShelfLifeDays.HasValue);
        RuleFor(x => x.BaseUom).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.StorageNote).MaximumLength(512);
    }
}

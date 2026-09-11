using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Domain;

namespace FSH.Modules.Catalog.Features.v1.Products.UpdateProductFulfillment;

public sealed class UpdateProductFulfillmentCommandValidator : AbstractValidator<UpdateProductFulfillmentCommand>
{
    public UpdateProductFulfillmentCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.TemperatureZone).NotEmpty()
            .Must(v => Enum.TryParse<TemperatureZone>(v, ignoreCase: true, out _));
        RuleFor(x => x.MinRemainingDaysOnShip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ShelfLifeDays).GreaterThanOrEqualTo(0).When(x => x.ShelfLifeDays.HasValue);
        RuleFor(x => x.BaseUom).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.StorageNote).MaximumLength(512);
    }
}

using FluentValidation;
using FSH.Framework.Shared.Localization;
using FSH.Modules.Catalog.Contracts.v1.Products;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Catalog.Features.v1.Products.UpsertProductTranslation;

public sealed class UpsertProductTranslationCommandValidator : AbstractValidator<UpsertProductTranslationCommand>
{
    public UpsertProductTranslationCommandValidator(IOptions<LocalizationOptions> localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        LocalizationOptions options = localization.Value;

        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Culture).NotEmpty().MaximumLength(16)
            .Must(c => CultureCatalog.IsSupported(c, options))
            .WithMessage("Culture is not in the supported list.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

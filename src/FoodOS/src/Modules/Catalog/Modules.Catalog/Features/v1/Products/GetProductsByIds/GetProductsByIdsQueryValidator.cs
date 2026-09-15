using FluentValidation;
using FSH.Modules.Catalog.Contracts.v1.Products;

namespace FSH.Modules.Catalog.Features.v1.Products.GetProductsByIds;

public sealed class GetProductsByIdsQueryValidator : AbstractValidator<GetProductsByIdsQuery>
{
    public GetProductsByIdsQueryValidator()
    {
        RuleFor(query => query.ProductIds).NotEmpty().Must(productIds => productIds.Count <= 100);
        RuleForEach(query => query.ProductIds).NotEmpty();
    }
}

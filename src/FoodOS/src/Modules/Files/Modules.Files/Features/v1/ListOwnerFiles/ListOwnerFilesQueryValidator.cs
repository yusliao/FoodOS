using FluentValidation;
using FSH.Modules.Files.Contracts.v1.Queries;

namespace FSH.Modules.Files.Features.v1.ListOwnerFiles;

public sealed class ListOwnerFilesQueryValidator : AbstractValidator<ListOwnerFilesQuery>
{
    public ListOwnerFilesQueryValidator()
    {
        RuleFor(q => q.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(q => q.OwnerId).NotEmpty();
        RuleFor(q => q.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}

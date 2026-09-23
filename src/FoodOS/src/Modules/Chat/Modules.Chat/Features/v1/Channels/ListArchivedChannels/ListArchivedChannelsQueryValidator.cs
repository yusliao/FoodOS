using FluentValidation;
using FSH.Modules.Chat.Contracts.v1.Queries;

namespace FSH.Modules.Chat.Features.v1.Channels.ListArchivedChannels;

public sealed class ListArchivedChannelsQueryValidator : AbstractValidator<ListArchivedChannelsQuery>
{
    public ListArchivedChannelsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200).When(x => x.Search is not null);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

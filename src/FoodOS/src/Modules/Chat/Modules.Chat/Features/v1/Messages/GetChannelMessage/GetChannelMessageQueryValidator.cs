using FluentValidation;
using FSH.Modules.Chat.Contracts.v1.Queries;

namespace FSH.Modules.Chat.Features.v1.Messages.GetChannelMessage;

public sealed class GetChannelMessageQueryValidator : AbstractValidator<GetChannelMessageQuery>
{
    public GetChannelMessageQueryValidator()
    {
        RuleFor(x => x.ChannelId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
    }
}

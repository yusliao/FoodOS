using FSH.Modules.Chat.Contracts.v1.DTOs;
using Mediator;

namespace FSH.Modules.Chat.Contracts.v1.Queries;

public sealed record GetChannelMessageQuery(Guid ChannelId, Guid MessageId) : IQuery<MessageDto>;

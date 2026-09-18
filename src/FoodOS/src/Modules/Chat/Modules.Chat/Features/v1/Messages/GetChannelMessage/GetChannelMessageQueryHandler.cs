using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using FSH.Modules.Chat.Contracts.v1.Queries;
using FSH.Modules.Chat.Data;
using FSH.Modules.Chat.Features.v1.Internal;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Chat.Features.v1.Messages.GetChannelMessage;

public sealed class GetChannelMessageQueryHandler(ChatDbContext db, ICurrentUser currentUser, IMediator mediator)
    : IQueryHandler<GetChannelMessageQuery, MessageDto>
{
    public async ValueTask<MessageDto> Handle(GetChannelMessageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty) throw new UnauthorizedException("no current user");

        var channel = await db.Channels.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.ChannelId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Channel not found.");
        channel.RequireMember(userId.ToString());

        var message = await db.Messages.AsNoTracking()
            .Include(m => m.Attachments)
            .Include(m => m.Reactions)
            .FirstOrDefaultAsync(m => m.Id == query.MessageId && m.ChannelId == query.ChannelId
                && m.DeletedAtUtc == null, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Message not found.");

        var resolved = await ChatAttachmentUrls.ResolveAsync(
            [message.ToDto()], mediator, cancellationToken).ConfigureAwait(false);
        return resolved.Single();
    }
}

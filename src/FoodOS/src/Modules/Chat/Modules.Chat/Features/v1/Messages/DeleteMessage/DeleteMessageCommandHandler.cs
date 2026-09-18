using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Web.Realtime;
using FSH.Modules.Chat.Contracts.Authorization;
using FSH.Modules.Chat.Contracts.v1.Commands;
using FSH.Modules.Chat.Data;
using FSH.Modules.Chat.Features.v1.Internal;
using FSH.Modules.Identity.Contracts.Services;
using Mediator;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Chat.Features.v1.Messages.DeleteMessage;

public sealed class DeleteMessageCommandHandler(
    ChatDbContext db,
    ICurrentUser currentUser,
    IUserPermissionService permissions,
    IHubContext<AppHub> hub)
    : ICommandHandler<DeleteMessageCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteMessageCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty) throw new UnauthorizedException("no current user");
        var currentUserId = userId.ToString();

        // Resolve only the lock scope first; do not retain stale tracked state
        // while waiting for another sender/deleter to commit.
        var channelId = await db.Messages.AsNoTracking()
            .Where(m => m.Id == cmd.MessageId).Select(m => (Guid?)m.ChannelId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Message not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ChatMessageWriteLock.AcquireAsync(db, currentUser.GetTenant(), channelId, cancellationToken).ConfigureAwait(false);

        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == cmd.MessageId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Message not found.");

        var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == message.ChannelId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Message not found.");
        channel.RequireMember(currentUserId);

        bool isModerator = await permissions
            .HasPermissionAsync(currentUserId, ChatPermissions.Messages.DeleteAny, cancellationToken)
            .ConfigureAwait(false);

        // Repeated deletes remain authorized operations, not an ownership bypass.
        if (!isModerator && !string.Equals(message.AuthorUserId, currentUserId, StringComparison.Ordinal))
            throw new ForbiddenException("Only the author or a moderator can delete.");
        if (message.DeletedAtUtc.HasValue) return Unit.Value;

        message.SoftDelete(currentUserId, isModerator);

        // If this was a thread reply, decrement the parent's ReplyCount.
        if (message.ParentMessageId is { } parentId)
        {
            var parent = await db.Messages.FirstOrDefaultAsync(m => m.Id == parentId, cancellationToken)
                .ConfigureAwait(false);
            parent?.DecrementReplyCount();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await hub.Clients.CurrentMembers(channel)
            .SendAsync("ChatMessageDeleted", new { channelId = channel.Id, messageId = message.Id }, cancellationToken)
            .ConfigureAwait(false);
        return Unit.Value;
    }
}

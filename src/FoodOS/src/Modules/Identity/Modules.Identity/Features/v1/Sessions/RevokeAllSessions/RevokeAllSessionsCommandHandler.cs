using FSH.Framework.Core.Context;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Sessions.RevokeAllSessions;
using Mediator;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Identity.Features.v1.Sessions.RevokeAllSessions;

public sealed class RevokeAllSessionsCommandHandler : ICommandHandler<RevokeAllSessionsCommand, int>
{
    private readonly ISessionService _sessionService;
    private readonly ICurrentUser _currentUser;

    public RevokeAllSessionsCommandHandler(ISessionService sessionService, ICurrentUser currentUser)
    {
        _sessionService = sessionService;
        _currentUser = currentUser;
    }

    public async ValueTask<int> Handle(RevokeAllSessionsCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId().ToString();
        var sessions = await _sessionService.GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        var current = sessions.SingleOrDefault(s => s.IsCurrentSession);
        if (current is null || (command.ExceptSessionId.HasValue && command.ExceptSessionId.Value != current.Id))
            throw new ForbiddenException("The current session cannot be verified. Sign in again before revoking other sessions.");
        return await _sessionService.RevokeAllSessionsAsync(
            userId,
            userId,
            current.Id,
            "User requested logout from all devices",
            cancellationToken);
    }
}

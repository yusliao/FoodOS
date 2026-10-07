using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using FSH.Modules.Auditing.Contracts;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Impersonation.EndImpersonation;
using Mediator;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.Modules.Identity.Features.v1.Impersonation.EndImpersonation;

public sealed class EndImpersonationCommandHandler
    : ICommandHandler<EndImpersonationCommand, TokenResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly IImpersonationGrantService _grantService;
    private readonly ILogger<EndImpersonationCommandHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EndImpersonationCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        ICurrentUser currentUser,
        IRequestContext requestContext,
        IImpersonationGrantService grantService,
        ILogger<EndImpersonationCommandHandler> logger,
        IServiceScopeFactory scopeFactory)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _currentUser = currentUser;
        _requestContext = requestContext;
        _grantService = grantService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask<TokenResponse> Handle(
        EndImpersonationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var claims = _currentUser.GetUserClaims()?.ToList()
            ?? throw new UnauthorizedException();

        var actorUserId = claims.FirstOrDefault(c => c.Type == ClaimConstants.ActorSubject)?.Value;
        var actorTenantId = claims.FirstOrDefault(c => c.Type == ClaimConstants.ActorTenant)?.Value;
        var jti = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorTenantId))
        {
            // Signed in but no act_sub claim (End called on a non-impersonation token): client error,
            // must be 4xx not CustomException's default 500.
            throw new CustomException(
                "current session is not an impersonation session",
                errors: null,
                System.Net.HttpStatusCode.BadRequest);
        }

        var impersonatedUserId = _currentUser.GetUserId().ToString();
        var impersonatedTenantId = _currentUser.GetTenant() ?? string.Empty;

        // Mark grant ended BEFORE issuing actor tokens so a racing JWT-hook request sees "ended" (safer than the reverse).
        // If MarkEnded fails we proceed anyway: the grant expires naturally and the hook treats Unknown states as revoked.
        if (!string.IsNullOrWhiteSpace(jti))
        {
            try
            {
                await _grantService.MarkEndedByJtiAsync(jti, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to mark impersonation grant ended for jti={Jti}. Actor swap will still proceed.",
                    jti);
            }
        }

        var actorClaimsResult = await _identityService
            .BuildClaimsForUserAsync(actorUserId, actorTenantId, cancellationToken);

        if (actorClaimsResult is null)
        {
            throw new NotFoundException("original actor not found");
        }

        var (subject, actorClaims) = actorClaimsResult.Value;
        var sessionId = Guid.NewGuid();
        actorClaims = actorClaims.Where(claim => claim.Type != SessionClaimTypes.SessionId)
            .Append(new Claim(SessionClaimTypes.SessionId, sessionId.ToString())).ToList();
        var token = await _tokenService.IssueAsync(subject, actorClaims, actorTenantId, cancellationToken);

        await CreateActorSessionAsync(subject, actorTenantId, sessionId, token, cancellationToken).ConfigureAwait(false);
        await _identityService.StoreRefreshTokenAsync(subject, token.RefreshToken, token.RefreshTokenExpiresAt, cancellationToken);

        await _securityAudit.ImpersonationEndedAsync(
            actorUserId: actorUserId,
            actorTenantId: actorTenantId,
            targetUserId: impersonatedUserId,
            targetTenantId: impersonatedTenantId,
            clientId: _requestContext.ClientId ?? "unknown",
            ct: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Impersonation ended: actor {ActorUserId}@{ActorTenant} returned from {TargetUserId}@{TargetTenant} jti={Jti}",
                actorUserId, actorTenantId, impersonatedUserId, impersonatedTenantId, jti ?? "<missing>");
        }

        return token;
    }

    private async Task CreateActorSessionAsync(string subject, string actorTenantId, Guid sessionId,
        TokenResponse token, CancellationToken cancellationToken)
    {
        // Keep the actor tenant's AsyncLocal context inside this async call. The caller
        // must retain the impersonated tenant for the remaining audit operations.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(actorTenantId).ConfigureAwait(false)
            ?? throw new NotFoundException("original actor tenant not found");
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var refreshHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.RefreshToken)).AsSpan(0, 8));
        await scope.ServiceProvider.GetRequiredService<ISessionService>().CreateSessionAsync(
            subject, refreshHash, _requestContext.IpAddress ?? string.Empty,
            _requestContext.UserAgent ?? string.Empty, token.RefreshTokenExpiresAt,
            sessionId, cancellationToken).ConfigureAwait(false);
    }
}

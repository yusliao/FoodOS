using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Services;
using Microsoft.EntityFrameworkCore;
using FSH.Modules.Identity.Contracts.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FSH.Modules.Identity.Authorization.Jwt;

public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _options;
    private readonly IHostEnvironment _environment;

    public ConfigureJwtBearerOptions(IOptions<JwtOptions> options, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        _options = options.Value;
        _environment = environment;
    }

    public void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Configure(string.Empty, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        byte[] key = Encoding.ASCII.GetBytes(_options.SigningKey);

        options.RequireHttpsMetadata = true;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidIssuer = _options.Issuer,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidAudience = _options.Audience,
            ValidateAudience = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        // Capture the validation failure reason so OnChallenge can include it (in Development).
        // Without this we get a body of `{"error":"Unauthorized"}` with no clue why JwtBearer rejected.
        const string FailureKey = "JwtAuthFailure";
        bool isDev = _environment.IsDevelopment();

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Stash the exception type+message on HttpContext so OnChallenge can surface it.
                context.HttpContext.Items[FailureKey] =
                    $"{context.Exception.GetType().Name}: {context.Exception.Message}";

                // Server-side log so we can also see the rejection reason in the API console.
                var failedLogger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("FSH.Identity.JwtAuth");
                failedLogger.LogWarning(context.Exception,
                    "JwtBearer authentication FAILED for {Method} {Path}: {Reason}",
                    SanitizeForLog(context.HttpContext.Request.Method),
                    SanitizeForLog(context.HttpContext.Request.Path.ToString()),
                    context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    // Was an Authorization header even sent? Helps distinguish "JWT rejected"
                    // from "no token at all" — both produce 401 but for very different reasons.
                    bool hadAuthHeader = !string.IsNullOrEmpty(context.HttpContext.Request.Headers.Authorization);

                    // RFC 9457 ProblemDetails — matches the contract the rest of the API uses
                    // for error responses (via the global exception handler).
                    var problem = new ProblemDetails
                    {
                        Type = "https://datatracker.ietf.org/doc/html/rfc7235#section-3.1",
                        Title = "Unauthorized",
                        Status = StatusCodes.Status401Unauthorized,
                        Detail = "Authentication is required to access this resource.",
                        Instance = context.HttpContext.Request.Path,
                    };

                    // In Development surface the actual JwtBearer rejection reason; in Production keep the
                    // body opaque to avoid leaking validation internals.
                    if (isDev)
                    {
                        if (context.HttpContext.Items[FailureKey] is string reason)
                        {
                            problem.Extensions["reason"] = reason;
                        }
                        else if (!hadAuthHeader)
                        {
                            problem.Extensions["reason"] = "No Authorization header on the request.";
                        }
                        else
                        {
                            // Header present but JwtBearer didn't fire OnAuthenticationFailed —
                            // typically means the bearer scheme didn't match the AuthorizationPolicy.
                            problem.Extensions["reason"] = "Bearer token present but JwtBearer did not validate it (scheme mismatch?).";
                        }

                        var challengeLogger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("FSH.Identity.JwtAuth");
                        challengeLogger.LogWarning(
                            "JwtBearer challenge for {Method} {Path}: hadAuthHeader={HadHeader} reason={Reason}",
                            SanitizeForLog(context.HttpContext.Request.Method),
                            SanitizeForLog(context.HttpContext.Request.Path.ToString()),
                            hadAuthHeader,
                            problem.Extensions["reason"]);
                    }

                    var traceId = context.HttpContext.TraceIdentifier;
                    if (!string.IsNullOrEmpty(traceId))
                    {
                        problem.Extensions["traceId"] = traceId;
                    }

                    var result = System.Text.Json.JsonSerializer.Serialize(problem);
                    return context.Response.WriteAsync(result);
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                // Authentication precedes request tenant resolution. Resolve a child scope from
                // the signed tenant claim so a request DbContext never captures a null tenant.
                var tenantId = context.Principal?.FindFirstValue(ClaimConstants.Tenant);
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
                {
                    context.Fail("missing identity context");
                    return;
                }

                await using var hookScope = context.HttpContext.RequestServices.CreateAsyncScope();
                var services = hookScope.ServiceProvider;
                var tenant = await services.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
                    .GetAsync(tenantId).ConfigureAwait(false);
                if (tenant is null)
                {
                    context.Fail("tenant not found");
                    return;
                }
                services.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
                    new MultiTenantContext<AppTenantInfo>(tenant);
                var db = services.GetRequiredService<IdentityDbContext>();
                var ct = context.HttpContext.RequestAborted;
                var actSub = context.Principal?.FindFirstValue(ClaimConstants.ActorSubject);
                if (string.IsNullOrEmpty(actSub))
                {
                    var sessionClaim = context.Principal?.FindFirstValue(SessionClaimTypes.SessionId);
                    var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                    // Do not let permission caches extend disabled accounts or revoked sessions.
                    if (!Guid.TryParse(sessionClaim, out var sessionId)
                        || !await db.UserSessions.AnyAsync(session => session.Id == sessionId
                            && session.UserId == userId && !session.IsRevoked && session.ExpiresAt > now
                            && session.User != null && session.User.IsActive, ct).ConfigureAwait(false))
                    {
                        context.Fail("user session is no longer active");
                    }
                    return;
                }

                // Impersonation tokens use their grant instead of an ordinary session.
                if (!await db.Users.AnyAsync(user => user.Id == userId && user.IsActive, ct).ConfigureAwait(false))
                {
                    context.Fail("user is no longer active");
                    return;
                }

                var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (string.IsNullOrEmpty(jti))
                {
                    // jti is always minted by Start; reject defensively so a malformed/stripped token
                    // can't bypass the revocation check.
                    context.HttpContext.Items[FailureKey] = "Impersonation token missing jti claim";
                    context.Fail("impersonation token missing jti claim");
                    return;
                }

                var grants = hookScope.ServiceProvider
                    .GetRequiredService<IImpersonationGrantService>();
                var revoked = await grants
                    .IsRevokedOrEndedAsync(jti, context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (revoked)
                {
                    context.HttpContext.Items[FailureKey] = "Impersonation grant revoked or ended";
                    context.Fail("impersonation grant revoked or ended");
                }
            },
            OnForbidden = _ => throw new ForbiddenException(),
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Task.CompletedTask;
                }

                var path = context.HttpContext.Request.Path;
                // Browser EventSource/SignalR can't send an Authorization header, so they use
                // ?access_token=. The narrow path allow-list keeps query-string tokens from leaking elsewhere.
                if (path.StartsWithSegments("/notifications", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/api/v1/realtime/hub", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    }

    // Strip control chars so attacker-controlled request data can't forge log lines
    // (CodeQL cs/log-injection); defence in depth on top of Kestrel's URI validation.
    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            buffer.Append(char.IsControl(c) ? '_' : c);
        }
        return buffer.ToString();
    }
}

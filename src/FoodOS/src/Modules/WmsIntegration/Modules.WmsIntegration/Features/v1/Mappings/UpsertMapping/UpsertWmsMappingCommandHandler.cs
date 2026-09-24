using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Data;
using FSH.Modules.WmsIntegration.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.UpsertMapping;

public sealed class UpsertWmsMappingCommandHandler(
    WmsIntegrationDbContext dbContext,
    IOptions<WmsIntegrationOptions> options)
    : ICommandHandler<UpsertWmsMappingCommand, WmsMappingDto>
{
    public async ValueTask<WmsMappingDto> Handle(
        UpsertWmsMappingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Provider) || string.IsNullOrWhiteSpace(settings.ConnectionId))
        {
            throw new CustomException(
                "The WMS provider and connection must be configured before mappings can be managed.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        string kind = WmsMappingKinds.All.Single(candidate =>
            string.Equals(candidate, command.Kind.Trim(), StringComparison.OrdinalIgnoreCase));
        string foodOsValue = command.FoodOsValue.Trim();
        string externalValue = command.ExternalValue.Trim();
        var mapping = await dbContext.Mappings
            .FirstOrDefaultAsync(x => x.Provider == settings.Provider
                && x.ConnectionId == settings.ConnectionId
                && x.Kind == kind
                && x.FoodOsValue == foodOsValue, cancellationToken)
            .ConfigureAwait(false);

        bool externalValueTaken = await dbContext.Mappings
            .AnyAsync(x => x.Provider == settings.Provider
                && x.ConnectionId == settings.ConnectionId
                && x.Kind == kind
                && x.ExternalValue == externalValue
                && (mapping == null || x.Id != mapping.Id), cancellationToken)
            .ConfigureAwait(false);
        if (externalValueTaken)
        {
            throw Conflict(kind, externalValue);
        }

        if (mapping is null)
        {
            mapping = WmsMapping.Create(
                settings.Provider,
                settings.ConnectionId,
                kind,
                foodOsValue,
                externalValue,
                command.FoodOsQuantityPerExternalUnit,
                command.IsActive);
            dbContext.Mappings.Add(mapping);
        }
        else
        {
            mapping.Update(externalValue, command.FoodOsQuantityPerExternalUnit, command.IsActive);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            throw Conflict(kind, externalValue);
        }

        return mapping.ToDto();
    }

    private static CustomException Conflict(string kind, string externalValue) => new(
        $"External {kind} value '{externalValue}' is already mapped for this WMS connection.",
        (IEnumerable<string>?)null,
        HttpStatusCode.Conflict);
}

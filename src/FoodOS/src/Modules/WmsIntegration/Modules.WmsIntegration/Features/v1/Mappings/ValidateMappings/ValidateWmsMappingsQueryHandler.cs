using FSH.Modules.WmsIntegration.Contracts.v1.Mappings;
using FSH.Modules.WmsIntegration.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.WmsIntegration.Features.v1.Mappings.ValidateMappings;

public sealed class ValidateWmsMappingsQueryHandler(
    WmsIntegrationDbContext dbContext,
    IOptions<WmsIntegrationOptions> options)
    : IQueryHandler<ValidateWmsMappingsQuery, WmsMappingValidationResult>
{
    public async ValueTask<WmsMappingValidationResult> Handle(
        ValidateWmsMappingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var settings = options.Value;
        var requirements = query.Requirements
            .Select(x => new WmsMappingRequirement(
                WmsMappingKinds.All.Single(candidate =>
                    string.Equals(candidate, x.Kind.Trim(), StringComparison.OrdinalIgnoreCase)),
                x.FoodOsValue.Trim()))
            .Distinct()
            .ToList();
        string[] kinds = requirements.Select(x => x.Kind).Distinct(StringComparer.Ordinal).ToArray();
        string[] foodOsValues = requirements.Select(x => x.FoodOsValue).Distinct(StringComparer.Ordinal).ToArray();
        var mappings = await dbContext.Mappings.AsNoTracking()
            .Where(x => x.Provider == settings.Provider
                && x.ConnectionId == settings.ConnectionId
                && kinds.Contains(x.Kind)
                && foodOsValues.Contains(x.FoodOsValue))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byKey = mappings.ToDictionary(x => (x.Kind, x.FoodOsValue));

        var resolved = new List<WmsResolvedMapping>();
        var missing = new List<WmsMissingMapping>();
        foreach (var requirement in requirements)
        {
            if (!byKey.TryGetValue((requirement.Kind, requirement.FoodOsValue), out var mapping))
            {
                missing.Add(new(requirement.Kind, requirement.FoodOsValue, "missing"));
            }
            else if (!mapping.IsActive)
            {
                missing.Add(new(requirement.Kind, requirement.FoodOsValue, "inactive"));
            }
            else
            {
                resolved.Add(new(
                    mapping.Kind,
                    mapping.FoodOsValue,
                    mapping.ExternalValue,
                    mapping.FoodOsQuantityPerExternalUnit));
            }
        }

        return new(missing.Count == 0, resolved, missing);
    }
}

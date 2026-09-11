using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.PriceLists;

public sealed record CreatePriceListCommand(
    string Name,
    Guid? CustomerOrgId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo = null,
    int Priority = 0,
    IReadOnlyList<PriceListLineInput>? Lines = null) : ICommand<Guid>;

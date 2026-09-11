namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record PriceListDto(
    Guid Id,
    string Name,
    Guid? CustomerOrgId,
    int Priority,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    IReadOnlyList<PriceListLineDto> Lines);

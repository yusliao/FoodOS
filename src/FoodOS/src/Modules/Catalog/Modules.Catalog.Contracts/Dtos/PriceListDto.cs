namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record PriceListDto(
    Guid Id,
    string Name,
    Guid? CustomerOrgId,
    int Priority,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    IReadOnlyList<PriceListLineDto> Lines)
{
    /// <summary>Set when the list prices one customer level instead of one customer or the whole catalog.</summary>
    public Guid? CustomerLevelId { get; init; }
}

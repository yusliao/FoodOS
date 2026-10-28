namespace FSH.Modules.Catalog.Contracts.Dtos;

/// <summary>Customer pricing level; <paramref name="DiscountPercent"/> is percent off the catalog price.</summary>
public sealed record CustomerLevelDto(Guid Id, string Code, string Name, decimal DiscountPercent, int CustomerCount);

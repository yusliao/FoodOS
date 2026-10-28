namespace FSH.Modules.Catalog.Contracts.Dtos;

/// <summary>A customer's pricing level; <paramref name="DiscountPercent"/> is percent off the standard price.</summary>
public sealed record CustomerLevelOfCustomerDto(Guid CustomerOrgId, Guid CustomerLevelId, string Code, string Name, decimal DiscountPercent);

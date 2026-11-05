namespace FSH.Modules.Catalog.Contracts.Dtos;

public sealed record ProductCategoryCountDto(Guid Id, string Name, int ActiveProductCount);

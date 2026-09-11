using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.Products;

public sealed record UpsertProductTranslationCommand(
    Guid ProductId,
    string Culture,
    string Name,
    string? Description) : ICommand<Guid>;

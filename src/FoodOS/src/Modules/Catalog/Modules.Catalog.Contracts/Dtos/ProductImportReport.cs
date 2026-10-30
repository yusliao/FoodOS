namespace FSH.Modules.Catalog.Contracts.Dtos;

/// <summary>Outcome of a product list import (or its dry run when <see cref="Applied"/> is false).</summary>
public sealed record ProductImportReport(
    bool Applied,
    int TotalRows,
    int Created,
    int Renamed,
    int Unchanged,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, int> CreatedByCategory,
    IReadOnlyList<string> CategoriesToCreate);

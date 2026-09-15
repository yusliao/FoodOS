using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class Supplier : AggregateRoot<Guid>, IOperatorOwnedEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Categories { get; private set; }
    public int LeadDays { get; private set; }
    public string Status { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private Supplier() { }

    public static Supplier Create(string code, string name, string? categories = null, int leadDays = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (leadDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leadDays), "LeadDays cannot be negative.");
        }

        return new Supplier
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Categories = string.IsNullOrWhiteSpace(categories) ? null : categories.Trim(),
            LeadDays = leadDays,
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

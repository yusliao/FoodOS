namespace FoodOS.DbMigrator;

/// <summary>
/// Lightweight command-line parser. Avoids dragging in System.CommandLine for
/// a handful of flags — keep this honest and minimal.
///
/// Verbs:   apply | seed | seed-demo | seed-showcase | import-products | list-pending  (default: apply)
/// Flags:   --tenant &lt;id&gt;   scope to one tenant id
///          --catalog-only   skip per-tenant migrations
///          --seed           after apply, also run SeedAsync per tenant
///          --help / -h      print help text
/// </summary>
internal sealed record MigratorCommand(
    string Command,
    string? Tenant,
    bool CatalogOnly,
    bool SeedAfter,
    bool ApplyShowcase,
    bool Help)
{
    private static readonly string[] KnownVerbs = ["apply", "seed", "seed-demo", "seed-showcase", "import-products", "list-pending"];

    /// <summary>Path of the workbook for import-products.</summary>
    public string? File { get; init; }

    /// <summary>import-products writes only when this is set; otherwise it previews.</summary>
    public bool ApplyImport { get; init; }

    public static MigratorCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var rawVerb = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "apply";
        // Canonicalise to a known verb via OrdinalIgnoreCase match (CA1308 forbids
        // ToLowerInvariant for security-sensitive normalisation).
        var verb = KnownVerbs.FirstOrDefault(v => string.Equals(v, rawVerb, StringComparison.OrdinalIgnoreCase))
            ?? rawVerb;

        var tenant = ExtractValue(args, "--tenant");
        var catalogOnly = args.Any(a => string.Equals(a, "--catalog-only", StringComparison.OrdinalIgnoreCase));
        var seedAfter = args.Any(a => string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase));
        var applyShowcase = args.Any(a => string.Equals(a, "--apply-showcase", StringComparison.OrdinalIgnoreCase));
        var help = args.Any(a => a is "-h" or "--help");

        return new MigratorCommand(verb, tenant, catalogOnly, seedAfter, applyShowcase, help)
        {
            File = ExtractValue(args, "--file"),
            ApplyImport = args.Any(a => string.Equals(a, "--apply-import", StringComparison.OrdinalIgnoreCase)),
        };
    }

    private static string? ExtractValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
            // Also accept --flag=value form.
            if (args[i].StartsWith($"{flag}=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i][(flag.Length + 1)..];
            }
        }
        return null;
    }

    public const string HelpText = """
        FSH DbMigrator — apply EF Core migrations across the tenant catalog
        and every tenant's per-module databases.

        Usage:
          dotnet run --project src/Host/FoodOS.DbMigrator -- [verb] [options]

        Verbs:
          apply           Apply pending migrations (default). Use --seed to also run SeedAsync.
          seed            Run only the SeedAsync step per tenant.
          seed-demo       Provision the demo tenants (acme, globex) with users, catalog,
                          tickets, and chat. Acme also gets the FoodOS demo chain
                          (1 DC / 3 zones / ~22 food SKUs / 4 stores / 1 route).
                          Dev-only — refuses to run unless DOTNET_ENVIRONMENT=Development.
          seed-showcase   Preview restaurant showcase data for an existing customer tenant.
                          Add --apply-showcase to insert missing records; never resets passwords.
          import-products Preview importing the client's product list (.xlsx) into the operator catalog.
                          Requires --file <path>; add --apply-import to write. New SKUs are created
                          inactive at price 0; existing SKUs only get their name refreshed.
          list-pending    Print pending migrations without applying anything.

        Options:
          --tenant <id>        Restrict to a single tenant id (default: all tenants).
          --catalog-only       Skip the per-tenant pass; only the tenant catalog is migrated.
          --seed               After apply, also call ITenantService.SeedTenantAsync.
          --apply-showcase     Allow seed-showcase to write missing showcase records.
          --file <path>        Workbook for import-products.
          --apply-import       Allow import-products to write to the catalog.
          -h, --help           Print this help text.

        Exit codes:
          0 — success
          1 — failure (see logged exception)
        """;
}

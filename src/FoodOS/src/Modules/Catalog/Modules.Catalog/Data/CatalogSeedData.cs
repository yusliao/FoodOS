using FSH.Modules.Catalog.Domain;

namespace FSH.Modules.Catalog.Data;

/// <summary>
/// Demo seed data for the Catalog module. Called from the DbMigrator's
/// <c>seed-demo</c> command for the demo tenants only; fresh tenants get an
/// empty catalogue and populate via the API / admin UI.
/// </summary>
public static class CatalogSeedData
{
    public sealed record BrandSpec(string Name, string Description);

    public sealed record CategorySpec(string Name, string Description, string? ParentName = null);

    public sealed record ProductSpec(
        string Sku,
        string Name,
        string Description,
        string BrandName,
        string CategoryName,
        decimal Price,
        int Stock,
        TemperatureZone TemperatureZone = TemperatureZone.Ambient,
        int? ShelfLifeDays = null,
        int MinRemainingDaysOnShip = 0);

    public static IReadOnlyList<BrandSpec> BrandSpecs { get; } =
    [
        new("Acme Goods", "Quality essentials for the modern home."),
        new("Northwind", "Outdoor and adventure gear since 1985."),
        new("Contoso Studio", "Design-forward furniture and lighting."),
        new("Fabrikam", "Pro-grade tools for makers and builders."),
        new("FreshLine", "Produce and chilled greens for foodservice."),
        new("Harbor Dairy", "Milk, yogurt, cheese, and eggs."),
        new("Arctic Pack", "Frozen proteins, vegetables, and desserts."),
        new("Pantry Co", "Dry grocery staples in foodservice packs."),
    ];

    public static IReadOnlyList<CategorySpec> CategorySpecs { get; } =
    [
        new("Apparel", "Clothing and accessories."),
        new("Home & Living", "Furniture, decor, and home essentials."),
        new("Outdoor", "Gear for the great outdoors."),
        new("Tools", "Power tools, hand tools, and accessories."),
        new("Produce", "Fresh fruit and vegetables."),
        new("Dairy", "Milk, cultured dairy, cheese, and eggs."),
        new("Frozen Foods", "Frozen proteins, vegetables, and desserts."),
        new("Grocery", "Dry grocery and pantry staples."),
        new("Tops", "Shirts, t-shirts, and tops.", "Apparel"),
        new("Outerwear", "Jackets, coats, and shells.", "Apparel"),
        new("Furniture", "Chairs, tables, and shelving.", "Home & Living"),
        new("Lighting", "Lamps and lighting fixtures.", "Home & Living"),
        new("Camping", "Tents, sleeping bags, and cookware.", "Outdoor"),
        new("Hand Tools", "Hammers, screwdrivers, wrenches.", "Tools"),
        new("Power Tools", "Drills, saws, sanders.", "Tools"),
        new("Leafy Greens", "Lettuce, spinach, and salad mixes.", "Produce"),
        new("Fruit", "Berries, tomatoes, and seasonal fruit.", "Produce"),
        new("Milk & Cultured", "Fluid milk, yogurt, and butter.", "Dairy"),
        new("Cheese & Eggs", "Block cheese and shell eggs.", "Dairy"),
        new("Frozen Produce", "IQF vegetables and fruit.", "Frozen Foods"),
        new("Frozen Protein", "IQF poultry and prepared proteins.", "Frozen Foods"),
        new("Dry Grocery", "Rice, oil, flour, and canned goods.", "Grocery"),
    ];

    public static IReadOnlyList<ProductSpec> ProductSpecs { get; } =
    [
        new("ACM-TS-001", "Classic Cotton Tee", "100% organic cotton crew-neck.", "Acme Goods", "Tops", 24.00m, 150),
        new("ACM-HD-002", "Heavyweight Hoodie", "450gsm fleece pullover hoodie.", "Acme Goods", "Outerwear", 68.00m, 60),
        new("CON-CH-101", "Walnut Lounge Chair", "Mid-century walnut frame, linen seat.", "Contoso Studio", "Furniture", 489.00m, 12),
        new("CON-LP-102", "Brass Pendant Lamp", "Hand-finished brass dome pendant.", "Contoso Studio", "Lighting", 189.00m, 24),
        new("NW-TN-201", "Trailhead 2P Tent", "3-season backpacking tent, 2.4kg.", "Northwind", "Camping", 279.00m, 35),
        new("NW-SB-202", "Summit Sleeping Bag", "Down-filled, comfort to -5°C.", "Northwind", "Camping", 219.00m, 28),
        new("FAB-DR-301", "20V Cordless Drill", "Brushless, 2-speed, 2x batteries.", "Fabrikam", "Power Tools", 159.00m, 80),
        new("FAB-WS-302", "16-piece Wrench Set", "Chrome vanadium, metric + imperial.", "Fabrikam", "Hand Tools", 72.00m, 120),
        new("FAB-CS-303", "7-1/4\" Circular Saw", "15-amp corded, 5800 RPM.", "Fabrikam", "Power Tools", 129.00m, 45),
        new("ACM-JK-003", "All-Weather Shell", "3-layer waterproof breathable shell.", "Acme Goods", "Outerwear", 189.00m, 40),

        new("FL-LT-401", "Romaine Hearts 6ct", "Crisp romaine hearts, foodservice pack.", "FreshLine", "Leafy Greens", 8.40m, 0, TemperatureZone.Chilled, 12, 3),
        new("FL-SP-402", "Baby Spinach 2.5lb", "Washed baby spinach, clamshell.", "FreshLine", "Leafy Greens", 9.80m, 0, TemperatureZone.Chilled, 10, 3),
        new("FL-TM-403", "Vine Tomatoes 5lb", "On-the-vine tomatoes.", "FreshLine", "Fruit", 11.20m, 0, TemperatureZone.Chilled, 14, 4),
        new("FL-ST-404", "Strawberries 1lb", "Fresh strawberries, clamshell.", "FreshLine", "Fruit", 6.50m, 0, TemperatureZone.Chilled, 7, 2),
        new("FL-CU-405", "Cucumber 12ct", "Field cucumbers, case.", "FreshLine", "Fruit", 7.10m, 0, TemperatureZone.Chilled, 14, 4),
        new("HD-ML-501", "Whole Milk 1gal", "Vitamin D whole milk, gallon.", "Harbor Dairy", "Milk & Cultured", 5.49m, 0, TemperatureZone.Chilled, 21, 5),
        new("HD-YG-502", "Greek Yogurt 32oz", "Plain Greek yogurt, tub.", "Harbor Dairy", "Milk & Cultured", 6.20m, 0, TemperatureZone.Chilled, 28, 7),
        new("HD-BT-503", "Unsalted Butter 1lb", "Creamery unsalted butter.", "Harbor Dairy", "Milk & Cultured", 4.80m, 0, TemperatureZone.Chilled, 60, 14),
        new("HD-CH-504", "Cheddar Block 5lb", "Mild cheddar, foodservice block.", "Harbor Dairy", "Cheese & Eggs", 18.40m, 0, TemperatureZone.Chilled, 90, 21),
        new("HD-EG-505", "Large Eggs 18ct", "Grade A large shell eggs.", "Harbor Dairy", "Cheese & Eggs", 4.10m, 0, TemperatureZone.Chilled, 35, 10),
        new("AP-PS-601", "Peas IQF 2.5lb", "IQF green peas.", "Arctic Pack", "Frozen Produce", 4.90m, 0, TemperatureZone.Frozen, 540, 30),
        new("AP-BR-602", "Mixed Berries IQF 2lb", "IQF strawberry, blueberry, raspberry.", "Arctic Pack", "Frozen Produce", 8.70m, 0, TemperatureZone.Frozen, 540, 30),
        new("AP-CK-603", "Chicken Breast IQF 10lb", "Boneless skinless chicken breast.", "Arctic Pack", "Frozen Protein", 32.00m, 0, TemperatureZone.Frozen, 365, 30),
        new("AP-FR-604", "French Fries 5lb", "Straight-cut frozen fries.", "Arctic Pack", "Frozen Produce", 6.40m, 0, TemperatureZone.Frozen, 365, 30),
        new("AP-IC-605", "Vanilla Ice Cream 3gal", "Foodservice vanilla ice cream tub.", "Arctic Pack", "Frozen Protein", 19.50m, 0, TemperatureZone.Frozen, 365, 30),
        new("AP-PZ-606", "Cheese Pizza 12in 8ct", "Frozen cheese pizza, case.", "Arctic Pack", "Frozen Protein", 28.80m, 0, TemperatureZone.Frozen, 270, 21),
        new("PC-RC-701", "Jasmine Rice 25lb", "Long-grain jasmine rice, bag.", "Pantry Co", "Dry Grocery", 22.00m, 0, TemperatureZone.Ambient, 540, 30),
        new("PC-OL-702", "Olive Oil 3L", "Extra virgin olive oil, tin.", "Pantry Co", "Dry Grocery", 24.50m, 0, TemperatureZone.Ambient, 540, 60),
        new("PC-CN-703", "Crushed Tomatoes 6/#10", "Crushed tomatoes, #10 cans.", "Pantry Co", "Dry Grocery", 31.20m, 0, TemperatureZone.Ambient, 720, 90),
        new("PC-PN-704", "Penne Pasta 10lb", "Dry penne, foodservice bag.", "Pantry Co", "Dry Grocery", 11.80m, 0, TemperatureZone.Ambient, 720, 90),
        new("PC-FL-705", "AP Flour 25lb", "All-purpose flour, bag.", "Pantry Co", "Dry Grocery", 14.40m, 0, TemperatureZone.Ambient, 365, 60),
        new("PC-SG-706", "Cane Sugar 25lb", "Granulated cane sugar, bag.", "Pantry Co", "Dry Grocery", 16.10m, 0, TemperatureZone.Ambient, 720, 90),
    ];

    public static IReadOnlyList<Brand> BuildBrands()
        => [.. BrandSpecs.Select(s => Brand.Create(s.Name, s.Description, null))];

    public static (IReadOnlyList<Category> Roots, IReadOnlyList<Category> Children) BuildCategories()
    {
        var roots = CategorySpecs
            .Where(s => s.ParentName is null)
            .Select(s => Category.Create(s.Name, s.Description, null))
            .ToList();
        var rootsByName = roots.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var children = CategorySpecs
            .Where(s => s.ParentName is not null)
            .Select(s => Category.Create(s.Name, s.Description, rootsByName[s.ParentName!].Id))
            .ToList();
        return (roots, children);
    }

    public static IReadOnlyList<Product> BuildProducts(
        IReadOnlyDictionary<string, Brand> brandsByName,
        IReadOnlyDictionary<string, Category> categoriesByName)
    {
        ArgumentNullException.ThrowIfNull(brandsByName);
        ArgumentNullException.ThrowIfNull(categoriesByName);

        return [.. ProductSpecs.Select(s => Product.Create(
            s.Sku,
            s.Name,
            s.Description,
            brandsByName[s.BrandName].Id,
            categoriesByName[s.CategoryName].Id,
            new Money(s.Price, "USD"),
            s.Stock,
            s.TemperatureZone,
            s.ShelfLifeDays,
            s.MinRemainingDaysOnShip))];
    }
}

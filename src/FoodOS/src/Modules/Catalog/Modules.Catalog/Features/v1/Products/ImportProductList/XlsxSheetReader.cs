using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FSH.Modules.Catalog.Features.v1.Products.ImportProductList;

/// <summary>
/// Reads the first worksheet of an .xlsx file into rows of cell text. Deliberately tiny: shared strings,
/// inline strings and numbers only — enough for list exports, without adding a spreadsheet dependency.
/// </summary>
public static partial class XlsxSheetReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<IReadOnlyList<string>> ReadFirstSheet(byte[] workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        var shared = ReadSharedStrings(zip);
        var sheet = LoadXml(zip, FirstSheetPath(zip))
            ?? throw new InvalidDataException("The workbook has no worksheet.");

        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in sheet.Descendants(Main + "row"))
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                int column = ColumnIndex((string?)cell.Attribute("r"));
                while (cells.Count < column)
                {
                    cells.Add(string.Empty);
                }

                cells.Add(CellText(cell, shared));
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static string FirstSheetPath(ZipArchive zip)
    {
        var workbook = LoadXml(zip, "xl/workbook.xml") ?? throw new InvalidDataException("Not an .xlsx workbook.");
        string? relId = (string?)workbook.Descendants(Main + "sheet").FirstOrDefault()?.Attribute(Rel + "id");
        var rels = LoadXml(zip, "xl/_rels/workbook.xml.rels");
        string? target = rels?.Descendants(PackageRel + "Relationship")
            .FirstOrDefault(r => (string?)r.Attribute("Id") == relId)
            ?.Attribute("Target")?.Value;
        if (string.IsNullOrEmpty(target))
        {
            return "xl/worksheets/sheet1.xml";
        }

        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var doc = LoadXml(zip, "xl/sharedStrings.xml");
        return doc is null
            ? []
            : doc.Root!.Elements(Main + "si").Select(si => Clean(string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))).ToList();
    }

    private static string CellText(XElement cell, List<string> shared)
    {
        string? type = (string?)cell.Attribute("t");
        string? value = cell.Element(Main + "v")?.Value;
        return type switch
        {
            "s" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && index < shared.Count => shared[index],
            "inlineStr" => Clean(string.Concat(cell.Descendants(Main + "t").Select(t => t.Value))),
            "str" or "b" or "e" => Clean(value ?? string.Empty),
            _ => FormatNumber(value),
        };
    }

    // 编号这种整数在 xlsx 里可能存成 1.125041E6，统一还原成不带小数的文本
    private static string FormatNumber(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
        {
            return value;
        }

        string format = number == decimal.Truncate(number) ? "0" : "0.############";
        return number.ToString(format, CultureInfo.InvariantCulture);
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return 0;
        }

        int index = 0;
        foreach (char c in reference)
        {
            if (!char.IsAsciiLetter(c))
            {
                break;
            }

            index = (index * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return Math.Max(0, index - 1);
    }

    // Excel 把单元格里的回车写成 _x000D_
    private static string Clean(string text) => EscapedChar().Replace(text, string.Empty);

    private static XDocument? LoadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    [GeneratedRegex("_x[0-9A-Fa-f]{4}_")]
    private static partial Regex EscapedChar();
}

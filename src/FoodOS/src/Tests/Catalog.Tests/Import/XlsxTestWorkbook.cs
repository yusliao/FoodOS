using System.IO.Compression;
using System.Security;
using System.Text;

namespace Catalog.Tests.Import;

/// <summary>Builds a minimal .xlsx in memory (shared strings + numbers) for import tests.</summary>
internal static class XlsxTestWorkbook
{
    public static byte[] Build(params object[][] rows)
    {
        var shared = new List<string>();
        var sheet = new StringBuilder("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        for (int r = 0; r < rows.Length; r++)
        {
            sheet.Append("<row r=\"").Append(r + 1).Append("\">");
            for (int c = 0; c < rows[r].Length; c++)
            {
                string reference = $"{(char)('A' + c)}{r + 1}";
                object value = rows[r][c];
                if (value is string text)
                {
                    shared.Add(text);
                    sheet.Append("<c r=\"").Append(reference).Append("\" t=\"s\"><v>").Append(shared.Count - 1).Append("</v></c>");
                }
                else
                {
                    sheet.Append("<c r=\"").Append(reference).Append("\"><v>")
                        .Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)).Append("</v></c>");
                }
            }

            sheet.Append("</row>");
        }

        sheet.Append("</sheetData></worksheet>");
        var strings = new StringBuilder("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        foreach (string text in shared)
        {
            strings.Append("<si><t>").Append(SecurityElement.Escape(text)).Append("</t></si>");
        }

        strings.Append("</sst>");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "xl/workbook.xml",
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
            Add(zip, "xl/sharedStrings.xml", strings.ToString());
        }

        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}

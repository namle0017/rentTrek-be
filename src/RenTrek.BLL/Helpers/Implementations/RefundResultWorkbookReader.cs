using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Đọc file Excel kế toán gửi lại: cột theo tên header (không phụ thuộc vị trí), ảnh nổi qua ClosedXML và
/// ảnh "Place in Cell" của Excel 365 qua việc tự đọc gói xlsx (metadata / richData).
/// </summary>
public static class RefundResultWorkbookReader
{
    public const string TransferredHeader = "Đã chuyển";
    public const string TransactionHeader = "Mã giao dịch";
    public const string ReasonHeader = "Lý do chưa chuyển";
    public const string ImagesHeader = "Hình ảnh minh chứng";
    public const string CaseIdHeader = "RefundCaseId";
    public const string CodeHeader = "Mã hồ sơ";
    public const string DataSheetName = "Hoàn tiền";

    private const long MaxImageBytes = 5 * 1024 * 1024 + 1;

    private static readonly string[] YesValues = ["co", "yes", "x", "1", "true", "y"];
    private static readonly string[] NoValues = ["khong", "no", "0", "false", "n"];

    public static RefundResultWorkbook Read(Stream xlsx)
    {
        // Cần đọc hai lần (ClosedXML + ZipArchive) nên gom vào bộ nhớ
        using var memory = new MemoryStream();
        xlsx.CopyTo(memory);

        var result = new RefundResultWorkbook();
        memory.Position = 0;
        using var workbook = new XLWorkbook(memory);
        var sheet = FindSheet(workbook);
        if (sheet is null)
        {
            result.MissingHeaders.AddRange([TransferredHeader, TransactionHeader, ReasonHeader]);
            return result;
        }

        var (headerRow, columns) = FindHeaders(sheet);
        if (!columns.ContainsKey(Normalize(TransferredHeader))) result.MissingHeaders.Add(TransferredHeader);
        if (!columns.ContainsKey(Normalize(TransactionHeader))) result.MissingHeaders.Add(TransactionHeader);
        if (!columns.ContainsKey(Normalize(ReasonHeader))) result.MissingHeaders.Add(ReasonHeader);
        if (result.MissingHeaders.Count > 0)
            return result;

        var imagesByRow = new Dictionary<int, List<(int Column, int Order, byte[] Data, string ContentType, string FileName)>>();
        var order = 0;
        foreach (var picture in sheet.Pictures)
        {
            try
            {
                var cell = picture.TopLeftCell;
                using var stream = new MemoryStream();
                var source = picture.ImageStream;
                source.Position = 0;
                CopyLimited(source, stream);
                var data = stream.ToArray();
                Add(imagesByRow, cell.Address.RowNumber, cell.Address.ColumnNumber, order++, data,
                    SniffOrDefault(data, null), string.IsNullOrWhiteSpace(picture.Name) ? $"image{order}" : picture.Name);
            }
            catch
            {
                // Ảnh không đọc được coi như không có ảnh
            }
        }

        memory.Position = 0;
        foreach (var image in ReadInCellImages(memory, sheet.Name))
            Add(imagesByRow, image.Row, image.Column, order++, image.Data, image.ContentType, image.FileName);

        var lastRow = Math.Max(sheet.LastRowUsed()?.RowNumber() ?? headerRow, imagesByRow.Keys.DefaultIfEmpty(0).Max());
        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = new RefundResultRow
            {
                RowNumber = rowNumber,
                RefundCode = Text(sheet, rowNumber, columns, CodeHeader),
                TransferredRaw = Text(sheet, rowNumber, columns, TransferredHeader),
                TransactionId = Text(sheet, rowNumber, columns, TransactionHeader),
                Reason = Text(sheet, rowNumber, columns, ReasonHeader)
            };
            var rawId = Text(sheet, rowNumber, columns, CaseIdHeader);
            if (Guid.TryParse(rawId, out var id) && id != Guid.Empty)
                row.RefundCaseId = id;
            row.Transferred = ParseTransferred(row.TransferredRaw);
            if (imagesByRow.TryGetValue(rowNumber, out var images))
                row.Images = images.OrderBy(x => x.Column).ThenBy(x => x.Order).Select(x => (x.Data, x.ContentType, x.FileName)).ToList();

            var empty = string.IsNullOrWhiteSpace(rawId) && row.RefundCode.Length == 0 && row.TransferredRaw.Length == 0
                && row.TransactionId.Length == 0 && row.Reason.Length == 0 && row.Images.Count == 0;
            if (!empty)
                result.Rows.Add(row);
        }

        return result;
    }

    /// <summary>Có/Co/Yes/X/1 → true; Không/Khong/No/0 → false; còn lại null.</summary>
    public static bool? ParseTransferred(string? raw)
    {
        var value = Normalize(raw);
        if (YesValues.Contains(value)) return true;
        if (NoValues.Contains(value)) return false;
        return null;
    }

    /// <summary>Chuẩn hóa so khớp header: bỏ dấu, hoa thường, khoảng trắng thừa.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var decomposed = text.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }

        return string.Join(' ', builder.ToString().Split([' ', '\t', '\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries));
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook)
    {
        var named = workbook.Worksheets.FirstOrDefault(x => Normalize(x.Name) == Normalize(DataSheetName));
        if (named is not null) return named;
        return workbook.Worksheets.FirstOrDefault(x => FindHeaders(x).Columns.ContainsKey(Normalize(TransferredHeader)))
            ?? workbook.Worksheets.FirstOrDefault(x => Normalize(x.Name) != Normalize("Hướng dẫn"));
    }

    private static (int HeaderRow, Dictionary<string, int> Columns) FindHeaders(IXLWorksheet sheet)
    {
        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 1, 10);
        for (var row = 1; row <= lastRow; row++)
        {
            var columns = new Dictionary<string, int>();
            var lastColumn = sheet.Row(row).LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var column = 1; column <= lastColumn; column++)
            {
                var key = Normalize(CellText(sheet.Cell(row, column)));
                if (key.Length > 0 && !columns.ContainsKey(key))
                    columns[key] = column;
            }

            if (columns.ContainsKey(Normalize(TransferredHeader)) || columns.ContainsKey(Normalize(CaseIdHeader)) || columns.ContainsKey(Normalize(CodeHeader)))
                return (row, columns);
        }

        return (1, []);
    }

    private static string Text(IXLWorksheet sheet, int row, Dictionary<string, int> columns, string header)
        => columns.TryGetValue(Normalize(header), out var column) ? CellText(sheet.Cell(row, column)) : string.Empty;

    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank) return string.Empty;
        if (value.IsText) return value.GetText().Trim();
        if (value.IsNumber) return value.GetNumber().ToString("0.###############", CultureInfo.InvariantCulture);
        if (value.IsBoolean) return value.GetBoolean() ? "1" : "0";
        if (value.IsDateTime) return value.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return string.Empty;
    }

    private static void Add(
        Dictionary<int, List<(int Column, int Order, byte[] Data, string ContentType, string FileName)>> map,
        int row, int column, int order, byte[] data, string contentType, string fileName)
    {
        if (!map.TryGetValue(row, out var list))
            map[row] = list = [];
        list.Add((column, order, data, contentType, fileName));
    }

    private static void CopyLimited(Stream source, Stream destination)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, read);
            total += read;
            if (total >= MaxImageBytes) break;
        }
    }

    private static string SniffOrDefault(byte[] data, string? fileName)
    {
        var sniffed = RefundProofFiles.Sniff(data);
        if (sniffed is not null) return sniffed;
        return Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }

    // ---- Ảnh "Place in Cell" (Excel 365): vm của ô -> metadata.xml -> rdrichvalue.xml -> richValueRel.xml -> rels -> media ----

    private static List<(int Row, int Column, byte[] Data, string ContentType, string FileName)> ReadInCellImages(Stream xlsx, string sheetName)
    {
        var found = new List<(int Row, int Column, byte[] Data, string ContentType, string FileName)>();
        try
        {
            using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read, leaveOpen: true);
            var sheetPath = ResolveSheetPath(zip, sheetName);
            var metadata = Load(zip, "xl/metadata.xml");
            var richValues = Load(zip, "xl/richData/rdrichvalue.xml");
            var relList = Load(zip, "xl/richData/richValueRel.xml");
            var rels = Load(zip, "xl/richData/_rels/richValueRel.xml.rels");
            if (sheetPath is null || metadata is null || richValues is null || relList is null || rels is null)
                return found;
            var sheetXml = Load(zip, sheetPath);
            if (sheetXml is null) return found;

            // valueMetadata.bk[vm-1].rc.v -> futureMetadata[XLRICHVALUE].bk[v].rvb@i -> chỉ số rich value
            var valueBlocks = Children(Child(metadata.Root, "valueMetadata"), "bk")
                .Select(bk => Child(bk, "rc")).ToList();
            var futureBlocks = Children(Child(metadata.Root, "futureMetadata"), "bk")
                .Select(bk => bk.Descendants().FirstOrDefault(e => e.Name.LocalName == "rvb")?.Attribute("i")?.Value).ToList();
            var richValueList = Children(richValues.Root, "rv").ToList();
            var relIds = Children(relList.Root, "rel")
                .Select(e => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value).ToList();
            var targets = Children(rels.Root, "Relationship")
                .Where(e => e.Attribute("Id") is not null && e.Attribute("Target") is not null)
                .ToDictionary(e => e.Attribute("Id")!.Value, e => e.Attribute("Target")!.Value);
            var imageKeyIndexByStructure = ReadImageKeyIndexes(zip);

            foreach (var cell in sheetXml.Descendants().Where(e => e.Name.LocalName == "c" && e.Attribute("vm") is not null))
            {
                try
                {
                    if (!int.TryParse(cell.Attribute("vm")!.Value, out var vm) || vm < 1 || vm > valueBlocks.Count) continue;
                    if (!int.TryParse(valueBlocks[vm - 1]?.Attribute("v")?.Value, out var futureIndex)
                        || futureIndex < 0 || futureIndex >= futureBlocks.Count) continue;
                    if (!int.TryParse(futureBlocks[futureIndex], out var richIndex)
                        || richIndex < 0 || richIndex >= richValueList.Count) continue;

                    var rv = richValueList[richIndex];
                    var keyIndex = imageKeyIndexByStructure.GetValueOrDefault(rv.Attribute("s")?.Value ?? string.Empty, 0);
                    var values = Children(rv, "v").ToList();
                    if (keyIndex >= values.Count || !int.TryParse(values[keyIndex].Value, out var relIndex)
                        || relIndex < 0 || relIndex >= relIds.Count || relIds[relIndex] is null) continue;
                    if (!targets.TryGetValue(relIds[relIndex]!, out var target)) continue;

                    var mediaPath = ResolveRelativePath("xl/richData", target);
                    var entry = zip.GetEntry(mediaPath);
                    if (entry is null) continue;
                    using var entryStream = entry.Open();
                    using var buffer = new MemoryStream();
                    CopyLimited(entryStream, buffer);
                    var data = buffer.ToArray();

                    var reference = cell.Attribute("r")?.Value ?? string.Empty;
                    var (row, column) = ParseAddress(reference);
                    if (row == 0) continue;
                    var fileName = Path.GetFileName(mediaPath);
                    found.Add((row, column, data, SniffOrDefault(data, fileName), fileName));
                }
                catch
                {
                    // Ô có cấu trúc lạ: coi như không có ảnh
                }
            }
        }
        catch
        {
            // Gói không có richData hoặc hỏng một phần: chỉ dùng ảnh nổi
        }

        return found;
    }

    private static Dictionary<string, int> ReadImageKeyIndexes(ZipArchive zip)
    {
        var map = new Dictionary<string, int>();
        var structures = Load(zip, "xl/richData/rdrichvaluestructure.xml");
        if (structures?.Root is null) return map;
        var index = 0;
        foreach (var structure in Children(structures.Root, "s"))
        {
            var keys = Children(structure, "k").ToList();
            var position = keys.FindIndex(k => (k.Attribute("n")?.Value ?? string.Empty).Contains("LocalImageIdentifier", StringComparison.OrdinalIgnoreCase));
            map[index.ToString(CultureInfo.InvariantCulture)] = Math.Max(position, 0);
            index++;
        }

        return map;
    }

    private static string? ResolveSheetPath(ZipArchive zip, string sheetName)
    {
        var workbook = Load(zip, "xl/workbook.xml");
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        if (workbook?.Root is null || rels?.Root is null) return null;
        var sheet = workbook.Descendants().FirstOrDefault(e => e.Name.LocalName == "sheet" && e.Attribute("name")?.Value == sheetName);
        var relId = sheet?.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
        if (relId is null) return null;
        var target = Children(rels.Root, "Relationship").FirstOrDefault(e => e.Attribute("Id")?.Value == relId)?.Attribute("Target")?.Value;
        if (target is null) return null;
        return target.StartsWith('/') ? target.TrimStart('/') : ResolveRelativePath("xl", target);
    }

    private static string ResolveRelativePath(string baseDirectory, string target)
    {
        if (target.StartsWith('/')) return target.TrimStart('/');
        var parts = new List<string>(baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var part in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part != ".") parts.Add(part);
        }

        return string.Join('/', parts);
    }

    private static (int Row, int Column) ParseAddress(string address)
    {
        var letters = new string(address.TakeWhile(char.IsLetter).ToArray());
        if (letters.Length == 0 || !int.TryParse(address[letters.Length..], out var row)) return (0, 0);
        var column = letters.ToUpperInvariant().Aggregate(0, (acc, ch) => (acc * 26) + (ch - 'A' + 1));
        return (row, column);
    }

    private static XDocument? Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null || entry.Length > 20 * 1024 * 1024) return null;
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XElement? Child(XElement? parent, string localName)
        => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static IEnumerable<XElement> Children(XElement? parent, string localName)
        => parent?.Elements().Where(e => e.Name.LocalName == localName) ?? [];
}

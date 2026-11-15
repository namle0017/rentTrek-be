using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace RenTrek.BLL.Tests.Fixtures;

/// <summary>
/// Sinh ảnh PNG hợp lệ (đơn sắc) cho test mà không cần file nhị phân trong repo.
/// </summary>
public static class TestImages
{
    public static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>PNG w x h một màu; màu khác nhau cho ra byte khác nhau để phân biệt ảnh.</summary>
    public static byte[] Png(byte r = 255, byte g = 0, byte b = 0, int width = 4, int height = 4)
    {
        var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter none
            for (var x = 0; x < width; x++)
            {
                raw.WriteByte(r);
                raw.WriteByte(g);
                raw.WriteByte(b);
            }
        }

        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw.ToArray());

        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)width);
        WriteBe(ihdr, 4, (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // RGB

        using var png = new MemoryStream();
        png.Write(PngSignature);
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>PNG hợp lệ có thêm byte rác sau IEND để vượt giới hạn dung lượng (magic bytes vẫn đúng).</summary>
    public static byte[] OversizedPng(int totalBytes = 5 * 1024 * 1024 + 1024)
    {
        var png = Png();
        var padded = new byte[Math.Max(totalBytes, png.Length)];
        Array.Copy(png, padded, png.Length);
        return padded;
    }

    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0xFF, 0xD9];

    public static byte[] NotAnImage() => Encoding.UTF8.GetBytes("This is plain text, not an image.");

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBe(length, 0, (uint)data.Length);
        stream.Write(length);
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typeAndData);
        var crc = new byte[4];
        WriteBe(crc, 0, Crc32(typeAndData));
        stream.Write(crc);
    }

    private static void WriteBe(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }
}

/// <summary>
/// Dựng file xlsx có ảnh kiểu Excel 365 "Place in Cell" (IMAGE in cell) từ một xlsx ClosedXML thường:
/// thêm xl/metadata.xml, xl/richData/*, rels, content types và thuộc tính vm trên ô.
/// Đường đi: ô vm -> metadata.xml (valueMetadata/futureMetadata) -> rdrichvalue.xml -> richValueRel.xml(.rels) -> xl/media.
/// </summary>
public static class PlaceInCellWorkbookBuilder
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Rd = "http://schemas.microsoft.com/office/spreadsheetml/2017/richdata";
    private static readonly XNamespace RvRel = "http://schemas.microsoft.com/office/spreadsheetml/2022/richvaluerel";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <param name="baseWorkbook">xlsx bình thường (ví dụ từ ClosedXML)</param>
    /// <param name="images">địa chỉ ô (vd "O2") -> bytes ảnh; mỗi ô đúng một ảnh theo cơ chế Place in Cell</param>
    /// <param name="sheetPath">phần worksheet trong gói, mặc định sheet đầu tiên của ClosedXML</param>
    public static byte[] AddImages(byte[] baseWorkbook, IReadOnlyDictionary<string, byte[]> images, string sheetPath = "xl/worksheets/sheet1.xml")
    {
        using var input = new MemoryStream(baseWorkbook);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var cells = images.Keys.ToList();
            foreach (var entry in source.Entries)
            {
                var content = ReadAll(entry);
                if (entry.FullName == sheetPath)
                    content = Encoding.UTF8.GetBytes(PatchSheet(Encoding.UTF8.GetString(content), cells));
                else if (entry.FullName == "[Content_Types].xml")
                    content = Encoding.UTF8.GetBytes(PatchContentTypes(Encoding.UTF8.GetString(content)));
                else if (entry.FullName == "xl/_rels/workbook.xml.rels")
                    content = Encoding.UTF8.GetBytes(PatchWorkbookRels(Encoding.UTF8.GetString(content)));
                Write(target, entry.FullName, content);
            }

            var list = images.Values.ToList();
            Write(target, "xl/metadata.xml", Encoding.UTF8.GetBytes(Metadata(list.Count)));
            Write(target, "xl/richData/rdrichvaluestructure.xml", Encoding.UTF8.GetBytes(Structure()));
            Write(target, "xl/richData/rdrichvalue.xml", Encoding.UTF8.GetBytes(RichValues(list.Count)));
            Write(target, "xl/richData/richValueRel.xml", Encoding.UTF8.GetBytes(RichValueRels(list.Count)));
            Write(target, "xl/richData/_rels/richValueRel.xml.rels", Encoding.UTF8.GetBytes(RichValueRelRels(list.Count)));
            for (var i = 0; i < list.Count; i++)
                Write(target, $"xl/media/image{i + 1}.png", list[i]);
        }

        return output.ToArray();
    }

    private static string PatchSheet(string xml, IReadOnlyList<string> cells)
    {
        var doc = XDocument.Parse(xml.TrimStart('﻿'));
        var sheetData = doc.Root!.Element(Main + "sheetData")!;
        for (var i = 0; i < cells.Count; i++)
        {
            var address = cells[i];
            var (column, rowNumber) = Split(address);
            var row = sheetData.Elements(Main + "row").FirstOrDefault(x => (int)x.Attribute("r")! == rowNumber);
            if (row is null)
            {
                row = new XElement(Main + "row", new XAttribute("r", rowNumber));
                var nextRow = sheetData.Elements(Main + "row").FirstOrDefault(x => (int)x.Attribute("r")! > rowNumber);
                if (nextRow is null) sheetData.Add(row); else nextRow.AddBeforeSelf(row);
            }

            var cell = row.Elements(Main + "c").FirstOrDefault(x => (string)x.Attribute("r")! == address);
            if (cell is null)
            {
                cell = new XElement(Main + "c", new XAttribute("r", address));
                var nextCell = row.Elements(Main + "c").FirstOrDefault(x => ColumnIndex(Split((string)x.Attribute("r")!).Column) > ColumnIndex(column));
                if (nextCell is null) row.Add(cell); else nextCell.AddBeforeSelf(cell);
            }

            cell.RemoveNodes();
            cell.SetAttributeValue("t", "e");
            cell.SetAttributeValue("vm", i + 1); // vm là chỉ số 1-based vào valueMetadata
            cell.Add(new XElement(Main + "v", "#VALUE!"));
        }

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string PatchContentTypes(string xml)
    {
        var doc = XDocument.Parse(xml.TrimStart('﻿'));
        var types = doc.Root!;
        if (!types.Elements(Ct + "Default").Any(x => string.Equals((string?)x.Attribute("Extension"), "png", StringComparison.OrdinalIgnoreCase)))
            types.Add(new XElement(Ct + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")));
        Override(types, "/xl/metadata.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheetMetadata+xml");
        Override(types, "/xl/richData/rdrichvalue.xml", "application/vnd.ms-excel.rdrichvalue+xml");
        Override(types, "/xl/richData/rdrichvaluestructure.xml", "application/vnd.ms-excel.rdrichvaluestructure+xml");
        Override(types, "/xl/richData/richValueRel.xml", "application/vnd.ms-excel.richvaluerel+xml");
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static void Override(XElement types, string partName, string contentType)
        => types.Add(new XElement(Ct + "Override", new XAttribute("PartName", partName), new XAttribute("ContentType", contentType)));

    private static string PatchWorkbookRels(string xml)
    {
        var doc = XDocument.Parse(xml.TrimStart('﻿'));
        var rels = doc.Root!;
        Rel(rels, "rIdPicMeta", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sheetMetadata", "metadata.xml");
        Rel(rels, "rIdPicRv", "http://schemas.microsoft.com/office/2022/10/relationships/richValue", "richData/rdrichvalue.xml");
        Rel(rels, "rIdPicRvs", "http://schemas.microsoft.com/office/2022/10/relationships/richValueStructure", "richData/rdrichvaluestructure.xml");
        Rel(rels, "rIdPicRvRel", "http://schemas.microsoft.com/office/2022/10/relationships/richValueRel", "richData/richValueRel.xml");
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static void Rel(XElement rels, string id, string type, string target)
        => rels.Add(new XElement(Pkg + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target)));

    private static string Metadata(int count)
    {
        var future = string.Concat(Enumerable.Range(0, count).Select(i =>
            $"<bk><extLst><ext uri=\"{{3e2802c4-a4d2-4d8b-9148-e3be6c30e623}}\" xmlns:xlrd=\"{Rd}\"><xlrd:rvb i=\"{i}\"/></ext></extLst></bk>"));
        var values = string.Concat(Enumerable.Range(0, count).Select(i => $"<bk><rc t=\"1\" v=\"{i}\"/></bk>"));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               $"<metadata xmlns=\"{Main}\" xmlns:xlrd=\"{Rd}\">" +
               "<metadataTypes count=\"1\"><metadataType name=\"XLRICHVALUE\" minSupportedVersion=\"120000\" copy=\"1\" pasteAll=\"1\" pasteValues=\"1\" merge=\"1\" splitFirst=\"1\" rowColShift=\"1\" clearFormats=\"1\" clearComments=\"1\" assign=\"1\" coerce=\"1\"/></metadataTypes>" +
               $"<futureMetadata name=\"XLRICHVALUE\" count=\"{count}\">{future}</futureMetadata>" +
               $"<valueMetadata count=\"{count}\">{values}</valueMetadata></metadata>";
    }

    private static string Structure()
        => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><rvStructures xmlns=\"{Rd}\" count=\"1\"><s t=\"_localImage\"><k n=\"_rvRel:LocalImageIdentifier\" t=\"i\"/><k n=\"CalcOrigin\" t=\"i\"/></s></rvStructures>";

    private static string RichValues(int count)
        => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><rvData xmlns=\"{Rd}\" count=\"{count}\">" +
           string.Concat(Enumerable.Range(0, count).Select(i => $"<rv s=\"0\"><v>{i}</v><v>5</v></rv>")) + "</rvData>";

    private static string RichValueRels(int count)
        => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><richValueRels xmlns=\"{RvRel}\" xmlns:r=\"{R}\">" +
           string.Concat(Enumerable.Range(0, count).Select(i => $"<rel r:id=\"rId{i + 1}\"/>")) + "</richValueRels>";

    private static string RichValueRelRels(int count)
        => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{Pkg}\">" +
           string.Concat(Enumerable.Range(0, count).Select(i =>
               $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/image{i + 1}.png\"/>")) + "</Relationships>";

    private static (string Column, int Row) Split(string address)
    {
        var index = address.TakeWhile(char.IsLetter).Count();
        return (address[..index], int.Parse(address[index..]));
    }

    private static int ColumnIndex(string column) => column.Aggregate(0, (sum, c) => sum * 26 + (char.ToUpperInvariant(c) - 'A' + 1));

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(content);
    }
}

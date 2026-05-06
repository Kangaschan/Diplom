using System.Text;

namespace Application.Exports;

internal static class ImagePdfBuilder
{
    public static byte[] BuildFromJpegPages(IReadOnlyList<byte[]> jpegPages, int imageWidth, int imageHeight)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var objectOffsets = new List<long> { 0L };
        var objectCount = 3 + jpegPages.Count * 3;

        var pageObjectNumbers = new List<int>(jpegPages.Count);
        var contentObjectNumbers = new List<int>(jpegPages.Count);
        var imageObjectNumbers = new List<int>(jpegPages.Count);

        for (var index = 0; index < jpegPages.Count; index++)
        {
            var baseObject = 4 + index * 3;
            pageObjectNumbers.Add(baseObject);
            contentObjectNumbers.Add(baseObject + 1);
            imageObjectNumbers.Add(baseObject + 2);
        }

        WriteObject(writer, stream, objectOffsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(writer, stream, objectOffsets, 2, $"<< /Type /Pages /Count {jpegPages.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>");
        WriteObject(writer, stream, objectOffsets, 3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (var index = 0; index < jpegPages.Count; index++)
        {
            var pageObjectNumber = pageObjectNumbers[index];
            var contentObjectNumber = contentObjectNumbers[index];
            var imageObjectNumber = imageObjectNumbers[index];

            WriteObject(
                writer,
                stream,
                objectOffsets,
                pageObjectNumber,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im{index + 1} {imageObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");

            var content = $"q\n595 0 0 842 0 0 cm\n/Im{index + 1} Do\nQ";
            WriteStreamObject(writer, stream, objectOffsets, contentObjectNumber, Encoding.ASCII.GetBytes(content), null);

            var imageDictionary = $"<< /Type /XObject /Subtype /Image /Width {imageWidth} /Height {imageHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegPages[index].Length} >>";
            WriteStreamObject(writer, stream, objectOffsets, imageObjectNumber, jpegPages[index], imageDictionary);
        }

        var xrefPosition = stream.Position;
        writer.Write($"xref\n0 {objectCount + 1}\n");
        writer.Write("0000000000 65535 f \n");

        for (var index = 1; index < objectOffsets.Count; index++)
        {
            writer.Write($"{objectOffsets[index]:0000000000} 00000 n \n");
        }

        writer.Write($"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static void WriteObject(StreamWriter writer, Stream stream, List<long> offsets, int objectNumber, string body)
    {
        offsets.Add(stream.Position);
        writer.Write($"{objectNumber} 0 obj\n{body}\nendobj\n");
        writer.Flush();
    }

    private static void WriteStreamObject(
        StreamWriter writer,
        Stream stream,
        List<long> offsets,
        int objectNumber,
        byte[] data,
        string? dictionary)
    {
        offsets.Add(stream.Position);
        writer.Write($"{objectNumber} 0 obj\n");
        writer.Write(dictionary ?? $"<< /Length {data.Length} >>");
        writer.Write("\nstream\n");
        writer.Flush();
        stream.Write(data, 0, data.Length);
        writer.Write("\nendstream\nendobj\n");
        writer.Flush();
    }
}

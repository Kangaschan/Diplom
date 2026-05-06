using System.Text;

namespace Application.Exports;

internal static class SimplePdfBuilder
{
    private const int LinesPerPage = 42;

    public static byte[] BuildDocument(string title, IReadOnlyCollection<string> lines)
    {
        var allPages = Paginate(lines).ToList();
        if (allPages.Count == 0)
        {
            allPages.Add([]);
        }

        var objects = new List<string>();
        objects.Add(string.Empty); // 1 Catalog
        objects.Add(string.Empty); // 2 Pages
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"); // 3 Font

        var pageObjectNumbers = new List<int>();

        foreach (var pageLines in allPages)
        {
            var pageObjectNumber = objects.Count + 1;
            var contentObjectNumber = objects.Count + 2;
            pageObjectNumbers.Add(pageObjectNumber);

            var stream = BuildPageContent(title, pageLines);
            var streamBytes = Encoding.ASCII.GetBytes(stream);

            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
            objects.Add($"<< /Length {streamBytes.Length} >>\nstream\n{stream}\nendstream");
        }

        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[1] = $"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>";

        return BuildPdf(objects);
    }

    private static IEnumerable<List<string>> Paginate(IReadOnlyCollection<string> lines)
    {
        var page = new List<string>(LinesPerPage);

        foreach (var line in lines)
        {
            page.Add(line);

            if (page.Count >= LinesPerPage)
            {
                yield return page;
                page = new List<string>(LinesPerPage);
            }
        }

        if (page.Count > 0)
        {
            yield return page;
        }
    }

    private static string BuildPageContent(string title, IReadOnlyCollection<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 18 Tf");
        builder.AppendLine("48 804 Td");
        builder.Append('(').Append(Escape(title)).AppendLine(") Tj");
        builder.AppendLine("0 -24 Td");
        builder.AppendLine("/F1 10 Tf");

        foreach (var line in lines)
        {
            builder.Append('(').Append(Escape(line)).AppendLine(") Tj");
            builder.AppendLine("0 -14 Td");
        }

        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static byte[] BuildPdf(IReadOnlyList<string> objects)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var offsets = new List<long> { 0L };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            writer.Flush();
        }

        var xrefPosition = stream.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");

        for (var index = 1; index < offsets.Count; index++)
        {
            writer.Write($"{offsets[index]:0000000000} 00000 n \n");
        }

        writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}

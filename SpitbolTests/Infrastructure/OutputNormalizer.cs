using System.Text;
using System.Text.RegularExpressions;

namespace SpitbolTests;

/// <summary>
/// Makes two output strings comparable across platforms: CRLF/CR -> LF,
/// trailing whitespace stripped per line, trailing blank lines dropped.
/// Optional <c>dropPatterns</c> remove nondeterministic lines (timings,
/// addresses, statement counts) before comparison.
/// </summary>
public static class OutputNormalizer
{
    public static string Normalize(string text, IReadOnlyList<Regex>? dropPatterns = null)
    {
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        var lines = text.Split('\n').Select(l => l.TrimEnd());
        if (dropPatterns is { Count: > 0 })
            lines = lines.Where(l => !dropPatterns.Any(r => r.IsMatch(l)));

        var list = lines.ToList();
        while (list.Count > 0 && list[^1].Length == 0)
            list.RemoveAt(list.Count - 1);

        return string.Join("\n", list);
    }

    /// <summary>Reads a "<name>.filter" file: one regex per line, blank lines
    /// and lines starting with '#' ignored. Matching output lines are dropped
    /// before golden comparison.</summary>
    public static IReadOnlyList<Regex> LoadFilters(string? filterFile)
    {
        if (filterFile is null || !File.Exists(filterFile))
            return Array.Empty<Regex>();

        return File.ReadAllLines(filterFile)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => new Regex(l, RegexOptions.Compiled))
            .ToList();
    }

    /// <summary>First <paramref name="max"/> line-level differences, for a
    /// readable assertion message.</summary>
    public static string Diff(string expected, string actual, int max = 25)
    {
        var e = expected.Split('\n');
        var a = actual.Split('\n');
        var sb = new StringBuilder();
        int n = Math.Max(e.Length, a.Length), shown = 0;

        for (int i = 0; i < n && shown < max; i++)
        {
            var el = i < e.Length ? e[i] : "<missing>";
            var al = i < a.Length ? a[i] : "<missing>";
            if (el != al)
            {
                sb.AppendLine($"  line {i + 1}:");
                sb.AppendLine($"    expected: {el}");
                sb.AppendLine($"    actual  : {al}");
                shown++;
            }
        }
        if (shown == 0)
            sb.AppendLine("  (no line-level differences after normalization)");
        else if (shown == max)
            sb.AppendLine($"  ... (showing first {max} differences)");
        return sb.ToString();
    }
}

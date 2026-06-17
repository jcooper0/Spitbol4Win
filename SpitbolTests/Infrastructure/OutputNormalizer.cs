using System.Text;
using System.Text.RegularExpressions;

namespace SpitbolTests;

/// <summary>
/// One rule parsed from a "&lt;name&gt;.filter" file. A rule is either a
/// <c>drop</c> (matching lines are removed) or a <c>sub</c> (a regex
/// substitution applied in-place to surviving lines). <see cref="Replacement"/>
/// is <c>null</c> for a drop rule and non-null for a substitution.
/// </summary>
public sealed record FilterRule(Regex Pattern, string? Replacement)
{
    public bool IsDrop => Replacement is null;
}

/// <summary>
/// Makes two output strings comparable across platforms: CRLF/CR -> LF,
/// trailing whitespace stripped per line, trailing blank lines dropped.
/// Optional filter rules remove nondeterministic lines (timings, addresses,
/// statement counts) and/or rewrite volatile fragments (e.g. compile-error
/// source positions) before comparison.
/// </summary>
public static class OutputNormalizer
{
    public static string Normalize(string text, IReadOnlyList<FilterRule>? rules = null)
    {
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        var dropRules = rules?.Where(r => r.IsDrop).ToList();
        var subRules  = rules?.Where(r => !r.IsDrop).ToList();

        var list = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd();

            // Drop decision is made on the original (post-trim) line, so a
            // substitution can never resurrect or hide a line that should go.
            if (dropRules is { Count: > 0 } && dropRules.Any(r => r.Pattern.IsMatch(line)))
                continue;

            if (subRules is { Count: > 0 })
                foreach (var r in subRules)
                    line = r.Pattern.Replace(line, r.Replacement!);

            list.Add(line);
        }

        while (list.Count > 0 && list[^1].Length == 0)
            list.RemoveAt(list.Count - 1);

        return string.Join("\n", list);
    }

    /// <summary>
    /// Reads a "&lt;name&gt;.filter" file. Blank lines and lines starting with
    /// '#' are ignored. Each remaining line is one rule:
    /// <list type="bullet">
    /// <item><description>
    /// <c>sub:&lt;delim&gt;&lt;regex&gt;&lt;delim&gt;&lt;replacement&gt;&lt;delim&gt;</c>
    /// — a regex substitution (sed-style). The first character after the
    /// <c>sub:</c> prefix is the delimiter; pick one that does not occur in the
    /// pattern or replacement. The replacement may use <c>$1</c>, <c>$2</c>, ...
    /// for captured groups (.NET <see cref="Regex.Replace(string,string)"/>).
    /// Example: <c>sub:~(\.sbl)\(\d+\)~$1(POS)~</c>.
    /// </description></item>
    /// <item><description>
    /// <c>drop:&lt;regex&gt;</c> — an explicit drop rule.
    /// </description></item>
    /// <item><description>
    /// any other line — a drop regex (the original, prefix-free form; kept for
    /// backward compatibility with existing filters).
    /// </description></item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<FilterRule> LoadFilters(string? filterFile)
    {
        if (filterFile is null || !File.Exists(filterFile))
            return Array.Empty<FilterRule>();

        var rules = new List<FilterRule>();
        int lineNo = 0;
        foreach (var rawLine in File.ReadAllLines(filterFile))
        {
            lineNo++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("sub:", StringComparison.Ordinal))
            {
                var (pattern, replacement) = ParseSub(line.Substring(4).Trim(), filterFile, lineNo);
                rules.Add(new FilterRule(new Regex(pattern, RegexOptions.Compiled), replacement));
            }
            else if (line.StartsWith("drop:", StringComparison.Ordinal))
            {
                rules.Add(new FilterRule(new Regex(line.Substring(5).Trim(), RegexOptions.Compiled), null));
            }
            else
            {
                rules.Add(new FilterRule(new Regex(line, RegexOptions.Compiled), null));
            }
        }
        return rules;
    }

    /// <summary>Parses the body of a sed-style <c>sub:</c> rule:
    /// <c>&lt;delim&gt;pattern&lt;delim&gt;replacement&lt;delim&gt;</c>.</summary>
    private static (string Pattern, string Replacement) ParseSub(string body, string file, int lineNo)
    {
        if (body.Length < 3)
            throw new FormatException(
                $"{Path.GetFileName(file)}:{lineNo}: malformed 'sub:' rule (too short). " +
                "Expected sub:<delim><regex><delim><replacement><delim>.");

        char delim = body[0];
        int second = body.IndexOf(delim, 1);
        if (second < 0)
            throw new FormatException(
                $"{Path.GetFileName(file)}:{lineNo}: 'sub:' rule missing second '{delim}' delimiter.");

        int third = body.IndexOf(delim, second + 1);
        if (third < 0)
            throw new FormatException(
                $"{Path.GetFileName(file)}:{lineNo}: 'sub:' rule missing third '{delim}' delimiter. " +
                $"Pick a delimiter that does not appear in the pattern or replacement.");

        if (third != body.Length - 1)
            throw new FormatException(
                $"{Path.GetFileName(file)}:{lineNo}: 'sub:' rule has trailing text after the third '{delim}'.");

        var pattern = body.Substring(1, second - 1);
        var replacement = body.Substring(second + 1, third - second - 1);
        if (pattern.Length == 0)
            throw new FormatException($"{Path.GetFileName(file)}:{lineNo}: 'sub:' rule has an empty pattern.");
        return (pattern, replacement);
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

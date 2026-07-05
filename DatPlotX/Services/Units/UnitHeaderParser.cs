using System.Text.RegularExpressions;

namespace DatPlotX.Services.Units;

/// <summary>
/// Extracts (display-name, unit) from a CSV column header. Recognizes the common engineering
/// conventions documented in <see cref="Docs/Curve-Analysis-Tools-Plan.md"/> §12.2:
/// <list type="bullet">
///   <item><c>Altitude [ft]</c>  → (<c>Altitude</c>, <c>ft</c>)</item>
///   <item><c>EGT (°C)</c>       → (<c>EGT</c>,      <c>°C</c>)</item>
///   <item><c>AoA, deg</c>       → (<c>AoA</c>,      <c>deg</c>)</item>
///   <item><c>Altitude_ft</c>    → (<c>Altitude</c>, <c>ft</c>)   when "ft" is in the known-unit set</item>
///   <item><c>pressure-psi</c>   → (<c>pressure</c>, <c>psi</c>)  when "psi" is in the known-unit set</item>
///   <item><c>count</c>          → (<c>count</c>,    <c>null</c>)</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>When multiple bracket / paren groups are present, the <b>trailing</b> group wins:
/// <c>my [bracket] column [ft]</c> → unit is <c>ft</c>.</para>
/// <para>Underscore / hyphen suffix patterns are conservative: the suffix is only treated as
/// a unit if it appears in <see cref="UnitRegistry.IsKnown"/>. This avoids stealing the
/// trailing token from columns like <c>velocity_x</c> or <c>thrust-port</c>.</para>
/// </remarks>
public static class UnitHeaderParser
{
    // Trailing group: "name [unit]" or "name (unit)" — captured non-greedily on the inside.
    // We use a manual right-anchor scan instead of a regex to handle nested brackets correctly.
    private static readonly Regex CommaSuffixRx = new(@"^(?<name>.+?)\s*,\s*(?<unit>[^\s,][^,]*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex DelimSuffixRx = new(@"^(?<name>.+?)[_\-](?<unit>[^_\-\s]+)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses <paramref name="header"/> into (display-name, unit). Returns the original
    /// trimmed header as the display name with a null unit when nothing matches.
    /// </summary>
    public static UnitInfo Parse(string header, IUnitRegistry? registry = null)
    {
        if (string.IsNullOrWhiteSpace(header))
            return new UnitInfo(header ?? string.Empty, null);

        registry ??= UnitRegistry.Default;
        var trimmed = header.Trim();

        // 1. Trailing bracket group: "name [unit]"
        if (TryExtractTrailingDelimited(trimmed, '[', ']', out var name, out var unit))
            return new UnitInfo(name, NormalizeOrNull(unit, registry));

        // 2. Trailing paren group: "name (unit)"
        if (TryExtractTrailingDelimited(trimmed, '(', ')', out name, out unit))
            return new UnitInfo(name, NormalizeOrNull(unit, registry));

        // 3. Comma suffix: "name, unit" — only if unit is known
        var commaMatch = CommaSuffixRx.Match(trimmed);
        if (commaMatch.Success)
        {
            var candidate = commaMatch.Groups["unit"].Value;
            if (registry.IsKnown(candidate))
                return new UnitInfo(commaMatch.Groups["name"].Value.Trim(), registry.Normalize(candidate));
        }

        // 4. Underscore / hyphen suffix: "altitude_ft" — only if suffix is known
        var delimMatch = DelimSuffixRx.Match(trimmed);
        if (delimMatch.Success)
        {
            var candidate = delimMatch.Groups["unit"].Value;
            if (registry.IsKnown(candidate))
                return new UnitInfo(delimMatch.Groups["name"].Value.Trim(), registry.Normalize(candidate));
        }

        return new UnitInfo(trimmed, null);
    }

    /// <summary>
    /// Scans from the right for a balanced <paramref name="open"/>/<paramref name="close"/> pair
    /// at the end of the string. Returns the body of that final group as <paramref name="unit"/>
    /// and everything before it as <paramref name="name"/>.
    /// </summary>
    private static bool TryExtractTrailingDelimited(
        string input, char open, char close, out string name, out string unit)
    {
        name = string.Empty;
        unit = string.Empty;

        var trimmed = input.TrimEnd();
        if (trimmed.Length < 3 || trimmed[^1] != close)
            return false;

        // Walk backwards from the closing bracket, tracking nesting depth.
        int depth = 1;
        int openIdx = -1;
        for (int i = trimmed.Length - 2; i >= 0; i--)
        {
            char c = trimmed[i];
            if (c == close) depth++;
            else if (c == open)
            {
                depth--;
                if (depth == 0) { openIdx = i; break; }
            }
        }
        if (openIdx <= 0)
            return false;

        unit = trimmed.Substring(openIdx + 1, trimmed.Length - openIdx - 2).Trim();
        name = trimmed.Substring(0, openIdx).TrimEnd();

        // Reject "[]" / "()" with empty body, and reject when there's no name before it.
        if (unit.Length == 0 || name.Length == 0)
            return false;

        return true;
    }

    private static string? NormalizeOrNull(string raw, IUnitRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return registry.IsKnown(raw) ? registry.Normalize(raw) : raw;
    }
}

using System.Collections.Frozen;

namespace DatPlotX.Services.Units;

/// <summary>
/// Default <see cref="IUnitRegistry"/>. Knows the ~30 most common engineering units across
/// length / time / velocity / angle / pressure / temperature / force / mass / current /
/// voltage / frequency / acceleration. Intentionally small — anything outside this set is
/// preserved verbatim by <see cref="UnitHeaderParser"/> rather than being stripped.
/// </summary>
public sealed class UnitRegistry : IUnitRegistry
{
    public static UnitRegistry Default { get; } = new();

    // Canonical form keyed by every recognized alias (case-insensitive lookups via TryGetValue
    // after ToLowerInvariant; canonical forms preserve case where it matters, e.g. "°C").
    private static readonly FrozenDictionary<string, string> Aliases = BuildAliases();

    private static FrozenDictionary<string, string> BuildAliases()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Length
        // Altitude variants (MSL / AGL / pressure / radio) are still "feet" dimensionally —
        // alias them to "ft" so the slope→rate conversion (ft/s → ft/min) fires for them.
        Add(map, "ft", "ft", "feet", "foot", "ftMSL", "ft-MSL", "ftAGL", "ft-AGL", "ftHAE");
        Add(map, "m", "m", "meter", "meters", "metre", "metres");
        Add(map, "km", "km", "kilometer", "kilometers");
        Add(map, "mi", "mi", "mile", "miles");
        Add(map, "nm", "nm", "nmi", "nautical-mile", "nauticalmile");
        Add(map, "in", "in", "inch", "inches");
        Add(map, "cm", "cm", "centimeter", "centimeters");
        Add(map, "mm", "mm", "millimeter", "millimeters");

        // Time
        // "time" is X-Plane's unit token for its elapsed-time columns (e.g. "_totl,_time"),
        // which are always seconds — alias it so altitude slope → ft/min fires on that X axis.
        Add(map, "s", "s", "sec", "secs", "second", "seconds", "time");
        Add(map, "ms", "ms", "millisecond", "milliseconds");
        Add(map, "min", "min", "minute", "minutes");
        Add(map, "hr", "hr", "h", "hour", "hours");

        // Velocity
        Add(map, "kt", "kt", "kts", "knot", "knots");
        Add(map, "mph", "mph");
        Add(map, "kph", "km/h", "kph", "km/h");
        Add(map, "ft/s", "ft/s", "fps");
        Add(map, "m/s", "m/s", "mps");
        Add(map, "ft/min", "ft/min", "fpm");

        // Angle
        Add(map, "deg", "deg", "degree", "degrees", "°");
        Add(map, "rad", "rad", "radian", "radians");

        // Pressure
        Add(map, "psi", "psi");
        Add(map, "Pa", "Pa", "pascal", "pascals");
        Add(map, "kPa", "kPa", "kilopascal", "kilopascals");
        Add(map, "hPa", "hPa", "hectopascal", "hectopascals");
        Add(map, "bar", "bar");
        Add(map, "inHg", "inHg");

        // Temperature
        Add(map, "°C", "°C", "degC", "deg-C", "deg C", "C");
        Add(map, "°F", "°F", "degF", "deg-F", "deg F", "F");
        Add(map, "K", "K", "kelvin", "kelvins");

        // Force
        Add(map, "N", "N", "newton", "newtons");
        Add(map, "lbf", "lbf", "lbs-f");
        Add(map, "kN", "kN", "kilonewton", "kilonewtons");

        // Mass
        Add(map, "kg", "kg", "kilogram", "kilograms");
        Add(map, "lb", "lb", "lbs", "pound", "pounds", "lbm");
        Add(map, "g", "g", "gram", "grams");

        // Acceleration / G
        Add(map, "G", "G", "g-force", "g's");
        Add(map, "m/s²", "m/s²", "m/s2", "m/s^2");
        Add(map, "ft/s²", "ft/s²", "ft/s2", "ft/s^2");

        // Electrical
        Add(map, "V", "V", "volt", "volts");
        Add(map, "mV", "mV", "millivolt", "millivolts");
        Add(map, "A", "A", "amp", "amps", "ampere", "amperes");
        Add(map, "mA", "mA", "milliamp", "milliamps");
        Add(map, "Ω", "Ω", "ohm", "ohms");
        Add(map, "W", "W", "watt", "watts");
        Add(map, "kW", "kW", "kilowatt", "kilowatts");
        Add(map, "Hz", "Hz", "hertz");
        Add(map, "kHz", "kHz", "kilohertz");

        // Dimensionless-ish
        Add(map, "%", "%", "pct", "percent");
        Add(map, "RPM", "RPM", "rpm");

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static void Add(Dictionary<string, string> map, string canonical, params string[] aliases)
    {
        foreach (var a in aliases)
            map[a] = canonical;
    }

    public bool IsKnown(string unit)
        => !string.IsNullOrWhiteSpace(unit) && Aliases.ContainsKey(unit.Trim());

    public string Normalize(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return unit;
        return Aliases.TryGetValue(unit.Trim(), out var canonical) ? canonical : unit;
    }

    public DerivedRate? PreferredDerivedRate(string baseUnit, string xUnit)
    {
        if (string.IsNullOrWhiteSpace(baseUnit) || string.IsNullOrWhiteSpace(xUnit))
            return null;

        var y = Normalize(baseUnit);
        var x = Normalize(xUnit);

        // Altitude rate (vertical speed): ft/s is the raw slope unit; engineers want ft/min.
        if (y == "ft" && x == "s") return new DerivedRate(60.0, "ft/min");

        // Horizontal speed: m/s is the raw slope unit on position-in-meters; engineers want kt.
        if (y == "m" && x == "s") return new DerivedRate(1.94384449, "kt");
        if (y == "km" && x == "hr") return new DerivedRate(0.539957, "kt");
        if (y == "mi" && x == "hr") return new DerivedRate(0.868976, "kt");

        return null;
    }
}

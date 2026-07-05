namespace DatPlotX.Services.Analysis;

/// <summary>
/// Y-array plus X access for one curve. Supports both <em>periodic</em> data (Signal plots —
/// X is implicit at <c>i * Period</c>) and <em>scatter</em> data (X array is provided).
/// The service materializes a real X array for the segment slice when handing the data to
/// metric implementations.
/// </summary>
public sealed class AnalysisCurveData
{
    public string CurveId { get; }
    public double[] Y { get; }

    /// <summary>For scatter data, the explicit X array (same length as <see cref="Y"/>). Null for periodic data.</summary>
    public double[]? XArray { get; }

    /// <summary>For periodic data, the sample period. Zero when <see cref="XArray"/> is set.</summary>
    public double Period { get; }

    public bool IsPeriodic => XArray is null;

    public int Length => Y.Length;

    /// <summary>Construct periodic-data view (X[i] = i × period).</summary>
    public AnalysisCurveData(string curveId, double[] y, double period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentNullException.ThrowIfNull(y);
        CurveId = curveId;
        Y = y;
        XArray = null;
        Period = period;
    }

    /// <summary>Construct scatter-data view (explicit X[]).</summary>
    public AnalysisCurveData(string curveId, double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Length != y.Length)
            throw new ArgumentException("X and Y must have equal length.");
        CurveId = curveId;
        Y = y;
        XArray = x;
        Period = 0;
    }

    public double XAt(int i) => XArray is { } arr ? arr[i] : i * Period;

    /// <summary>
    /// Indices [start, end] (inclusive) covering the X range. Uses index math for periodic data
    /// and binary search for scatter. Returns (start &gt; end) when the range is empty.
    /// </summary>
    public (int Start, int End) SliceIndices(double xMin, double xMax)
    {
        if (Length == 0) return (1, 0);

        if (IsPeriodic)
        {
            // Period > 0 invariant ⇒ no /0.
            int start = Math.Max(0, (int)Math.Ceiling(xMin / Period));
            int end = Math.Min(Length - 1, (int)Math.Floor(xMax / Period));
            return (start, end);
        }

        // Scatter — binary search.
        var arr = XArray!;
        int s = LowerBound(arr, xMin);
        int e = UpperBound(arr, xMax) - 1;
        return (s, e);
    }

    private static int LowerBound(double[] arr, double v)
    {
        int lo = 0, hi = arr.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (arr[mid] < v) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static int UpperBound(double[] arr, double v)
    {
        int lo = 0, hi = arr.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (arr[mid] <= v) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Materializes (X, Y) spans for the inclusive index range. For periodic data, X is
    /// computed on-demand into a freshly-allocated array — fine for typical segment sizes;
    /// callers that compute many metrics on the same segment should reuse the result.
    /// </summary>
    public (double[] X, double[] Y) Slice(int start, int end)
    {
        if (start > end) return (Array.Empty<double>(), Array.Empty<double>());

        int n = end - start + 1;
        var yOut = new double[n];
        Array.Copy(Y, start, yOut, 0, n);

        double[] xOut;
        if (XArray is { } xa)
        {
            xOut = new double[n];
            Array.Copy(xa, start, xOut, 0, n);
        }
        else
        {
            xOut = new double[n];
            double p = Period;
            for (int i = 0; i < n; i++) xOut[i] = (start + i) * p;
        }
        return (xOut, yOut);
    }
}

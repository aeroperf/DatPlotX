namespace DatPlotX.Helpers;

/// <summary>
/// Helper methods for file dialogs
/// </summary>
public static class FileDialogHelper
{
    public static class Filters
    {
        public const string Csv = "CSV Files|*.csv";
        public const string Tab = "Tab Files|*.tab";
        public const string Txt = "Text Files|*.txt";
        public const string AllData = "Data Files|*.csv;*.tab;*.txt";
        public const string Dpx = "DatPlot Projects|*.dpx";
        public const string Json = "JSON Files|*.json";
        public const string Png = "PNG Images|*.png";
        public const string Jpeg = "JPEG Images|*.jpg;*.jpeg";
        public const string Bmp = "BMP Images|*.bmp";
        public const string Svg = "SVG Images|*.svg";
        public const string AllImages = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.svg";
        public const string All = "All Files|*.*";
    }

    public static string CombineFilters(params string[] filters)
    {
        return string.Join("|", filters);
    }

    public static string GetDataFileFilter()
    {
        return CombineFilters(Filters.AllData, Filters.Csv, Filters.Tab, Filters.Txt, Filters.All);
    }

    public static string GetProjectFileFilter()
    {
        return CombineFilters(Filters.Dpx, Filters.Json, Filters.All);
    }

    public static string GetImageFileFilter()
    {
        return CombineFilters(Filters.AllImages, Filters.Png, Filters.Jpeg, Filters.Bmp, Filters.Svg, Filters.All);
    }

    public static string GetDefaultFileName(string baseName, string extension)
    {
        // SECURITY: Validate base name to prevent invalid file names (CWE-20)
        baseName = InputValidator.ValidateFileName(baseName);
        extension = extension.TrimStart('.');

        return $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
    }

    public static string GetDefaultFileNameWithoutExtension(string baseName)
    {
        baseName = InputValidator.ValidateFileName(baseName);
        return $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}";
    }
}

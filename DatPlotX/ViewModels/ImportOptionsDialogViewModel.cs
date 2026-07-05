using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;
using DatPlotX.Services;
using System.Collections.ObjectModel;

namespace DatPlotX.ViewModels;

public enum PreviewRowKind
{
    Skipped,
    Header,
    Unit,
    DataStart,
    DataBody,
}

public partial class PreviewLineViewModel : ObservableObject
{
    public int Number { get; }
    public string Text { get; }

    [ObservableProperty]
    private PreviewRowKind _kind = PreviewRowKind.Skipped;

    public PreviewLineViewModel(int number, string text)
    {
        Number = number;
        Text = text;
    }
}

public partial class ImportOptionsDialogViewModel : ObservableObject
{
    private readonly IFilePreviewService? _previewService;
    private readonly string? _filePath;

    public ImportOptionsDialogViewModel()
    {
        FileName = string.Empty;
        PreviewLines = new ObservableCollection<PreviewLineViewModel>();
    }

    public ImportOptionsDialogViewModel(string filePath, IFilePreviewService previewService)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        FileName = Path.GetFileName(filePath);
        PreviewLines = new ObservableCollection<PreviewLineViewModel>();
    }

    public string FileName { get; }

    public ObservableCollection<PreviewLineViewModel> PreviewLines { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDecimalFormatEnabled))]
    [NotifyPropertyChangedFor(nameof(AreLineSelectorsEnabled))]
    private string _selectedDelimiter = ",";

    [ObservableProperty]
    private string _selectedDecimalFormat = "Period (.)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(ValidationHint))]
    private int _headerLine = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(ValidationHint))]
    private int _unitLine; // 0 = none

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(ValidationHint))]
    private int _dataStartLine = 2;

    [ObservableProperty]
    private bool _isLoadingPreview;

    public bool IsXPlaneFormat => SelectedDelimiter == "X-Plane (.txt)";
    public bool IsDecimalFormatEnabled => !IsXPlaneFormat;
    public bool AreLineSelectorsEnabled => !IsXPlaneFormat;

    public bool CanImport => GetValidationError() is null;
    public string? ValidationHint => GetValidationError();

    public List<string> Delimiters { get; } = new() { ",", ";", "|", ":", "Tab", "X-Plane (.txt)" };
    public List<string> DecimalFormats { get; } = new() { "Period (.)", "Comma (,)" };

    public async Task LoadPreviewAsync()
    {
        if (_previewService is null || _filePath is null) return;
        IsLoadingPreview = true;
        try
        {
            var lines = await _previewService.ReadFirstLinesAsync(_filePath).ConfigureAwait(true);
            PreviewLines.Clear();
            for (int i = 0; i < lines.Count; i++)
                PreviewLines.Add(new PreviewLineViewModel(i + 1, lines[i]));
            RetagPreviewLines();
        }
        finally
        {
            IsLoadingPreview = false;
        }
    }

    partial void OnHeaderLineChanged(int value)
    {
        EnsureDataStartLineFollows();
        RetagPreviewLines();
    }
    partial void OnUnitLineChanged(int value)
    {
        EnsureDataStartLineFollows();
        RetagPreviewLines();
    }
    partial void OnDataStartLineChanged(int value) => RetagPreviewLines();

    private void EnsureDataStartLineFollows()
    {
        var floor = Math.Max(HeaderLine, UnitLine);
        if (floor > 0 && DataStartLine <= floor)
            DataStartLine = floor + 1;
    }

    private void RetagPreviewLines()
    {
        foreach (var line in PreviewLines)
        {
            line.Kind = ClassifyLine(line.Number);
        }
    }

    private PreviewRowKind ClassifyLine(int number)
    {
        if (HeaderLine > 0 && number == HeaderLine) return PreviewRowKind.Header;
        if (UnitLine > 0 && number == UnitLine) return PreviewRowKind.Unit;
        if (number == DataStartLine) return PreviewRowKind.DataStart;
        if (number > DataStartLine) return PreviewRowKind.DataBody;
        return PreviewRowKind.Skipped;
    }

    private string? GetValidationError()
    {
        if (IsXPlaneFormat) return null;

        if (HeaderLine < 0) return "Header line must be 0 or greater.";
        if (UnitLine < 0) return "Unit line must be 0 or greater.";
        if (DataStartLine < 1) return "Data start line must be at least 1.";

        if (HeaderLine > 0 && UnitLine > 0 && HeaderLine == UnitLine)
            return "Header and unit lines must differ.";
        if (HeaderLine > 0 && DataStartLine <= HeaderLine)
            return "Data start line must be greater than the header line.";
        if (UnitLine > 0 && DataStartLine <= UnitLine)
            return "Data start line must be greater than the unit line.";
        return null;
    }

    public ImportOptionsModel GetImportOptions()
    {
        var isXPlaneFormat = IsXPlaneFormat;
        return new ImportOptionsModel
        {
            Delimiter = isXPlaneFormat ? "|" : (SelectedDelimiter == "Tab" ? "\t" : SelectedDelimiter),
            CultureName = isXPlaneFormat ? "en-US" : (SelectedDecimalFormat == "Period (.)" ? "en-US" : "de-DE"),
            IsXPlaneFormat = isXPlaneFormat,
            HeaderLine = isXPlaneFormat ? 1 : HeaderLine,
            UnitLine = isXPlaneFormat ? 0 : UnitLine,
            DataStartLine = isXPlaneFormat ? 2 : DataStartLine,
        };
    }
}

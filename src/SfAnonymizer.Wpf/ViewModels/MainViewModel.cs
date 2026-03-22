using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SfAnonymizer.Core.Detectors;
using SfAnonymizer.Core.Models;
using SfAnonymizer.Core.Services;
using SfAnonymizer.Wpf.Services;

namespace SfAnonymizer.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFileParser _parser;
    private readonly IAnonymizationEngine _engine;
    private readonly IFileWriter _writer;
    private readonly ISensitiveColumnDetector _detector;

    // Parsed state
    private List<string> _headers = [];
    private List<Dictionary<string, string>> _rows = [];
    private AnonymizationResult? _lastResult;

    /// <summary>
    /// All available categories (built-in + user-defined). Bound to each column's ComboBox.
    /// </summary>
    public ObservableCollection<CategoryOption> AvailableCategories { get; }

    public MainViewModel(
        IFileParser parser,
        IAnonymizationEngine engine,
        IFileWriter writer,
        ISensitiveColumnDetector detector)
    {
        _parser = parser;
        _engine = engine;
        _writer = writer;
        _detector = detector;

        // Seed with built-ins, then append any saved custom categories
        AvailableCategories = new(CategoryHelper.BuiltIns);
        foreach (var def in CategoryStorage.Load())
            AvailableCategories.Add(new CategoryOption(def));

        // Auto-save whenever the list changes
        AvailableCategories.CollectionChanged += OnAvailableCategoriesChanged;
    }

    private void OnAvailableCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CategoryStorage.Save(AvailableCategories
            .Where(o => o.IsCustom)
            .Select(o => o.Custom!));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ParseFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    private string _inputFilePath = string.Empty;

    public bool HasFile => !string.IsNullOrEmpty(InputFilePath);

    [ObservableProperty]
    private string _statusMessage = "Ready. Drop or select a CSV/Excel file to begin.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isFileLoaded;

    [ObservableProperty]
    private bool _isAnonymized;

    [ObservableProperty]
    private int _totalRows;

    [ObservableProperty]
    private int _totalReplacements;

    /// <summary>
    /// Detected sensitive columns — user can toggle them before anonymizing.
    /// </summary>
    public ObservableCollection<ColumnClassificationViewModel> DetectedColumns { get; } = [];

    /// <summary>
    /// Transcode table entries for display.
    /// </summary>
    public ObservableCollection<TranscodeEntry> TranscodeEntries { get; } = [];

    /// <summary>
    /// DataView for the preview DataGrid — DataTable binds cleanly with auto-generated columns.
    /// </summary>
    [ObservableProperty]
    private DataView? _previewData;

    [ObservableProperty]
    private string _previewTabHeader = "📄 Original Data";

    // ── Commands ──

    public event EventHandler? ManageCategoriesRequested;

    [RelayCommand]
    private void ManageCategories() => ManageCategoriesRequested?.Invoke(this, EventArgs.Empty);

    private bool CanParseFile() =>
        !string.IsNullOrWhiteSpace(InputFilePath) && File.Exists(InputFilePath);

    private bool CanClear() => HasFile;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        InputFilePath = string.Empty;
        _headers = [];
        _rows = [];
        _lastResult = null;
        DetectedColumns.Clear();
        TranscodeEntries.Clear();
        PreviewData = null;
        TotalRows = 0;
        TotalReplacements = 0;
        IsFileLoaded = false;
        IsAnonymized = false;
        StatusMessage = "Ready. Drop or select a CSV/Excel file to begin.";
    }

    [RelayCommand(CanExecute = nameof(CanParseFile))]
    private async Task ParseFileAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Parsing file...";

            (_headers, _rows) = await _parser.ParseAsync(InputFilePath, ct);
            TotalRows = _rows.Count;

            // Auto-detect sensitive columns
            var classifications = _detector.Classify(_headers, _rows);

            DetectedColumns.Clear();
            foreach (var header in _headers)
            {
                var match = classifications.FirstOrDefault(c =>
                    string.Equals(c.ColumnName, header, StringComparison.OrdinalIgnoreCase));

                var defaultOption = match is not null
                    ? AvailableCategories.FirstOrDefault(o => o.BuiltIn == match.Category)
                      ?? AvailableCategories.First()
                    : AvailableCategories.First();

                DetectedColumns.Add(new ColumnClassificationViewModel
                {
                    ColumnName = header,
                    IsSelected = match is not null,
                    Category = defaultOption,
                });
            }

            IsFileLoaded = true;
            IsAnonymized = false;
            TranscodeEntries.Clear();
            PreviewData = BuildOriginalPreviewTable().DefaultView;
            PreviewTabHeader = "📄 Original Data";

            StatusMessage = $"Loaded {_rows.Count} rows, {_headers.Count} columns. " +
                           $"{classifications.Count} sensitive column(s) auto-detected.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error parsing file: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Anonymize()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Anonymizing...";

            // Build classification list from user selection
            var selectedClassifications = DetectedColumns
                .Where(c => c.IsSelected)
                .Select(c => new ColumnClassification(
                    c.ColumnName,
                    c.Category.BuiltIn ?? SensitiveDataCategory.Custom,
                    IsAutoDetected: false,
                    c.Category.Custom))
                .ToList();

            _lastResult = _engine.Anonymize(_headers, _rows, selectedClassifications);

            // Update transcode table
            TranscodeEntries.Clear();
            foreach (var entry in _lastResult.TranscodeTable)
            {
                TranscodeEntries.Add(entry);
            }

            // Build DataTable for preview (first 100 rows)
            PreviewData = BuildPreviewTable(_lastResult).DefaultView;

            TotalReplacements = _lastResult.TotalReplacements;
            IsAnonymized = true;
            PreviewTabHeader = "📄 Anonymized Preview";

            StatusMessage = $"Done! {_lastResult.TotalReplacements} replacements across " +
                           $"{_lastResult.AffectedRows} rows, {_lastResult.AffectedColumns} columns.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private DataTable BuildOriginalPreviewTable()
    {
        var table = new DataTable();
        foreach (var header in _headers)
            table.Columns.Add(header, typeof(string));
        foreach (var row in _rows.Take(100))
        {
            var dr = table.NewRow();
            foreach (var header in _headers)
                dr[header] = row.GetValueOrDefault(header, string.Empty);
            table.Rows.Add(dr);
        }
        return table;
    }

    private static DataTable BuildPreviewTable(AnonymizationResult result)
    {
        var table = new DataTable();

        foreach (var header in result.Headers)
        {
            table.Columns.Add(header, typeof(string));
        }

        foreach (var row in result.Rows.Take(100))
        {
            var dr = table.NewRow();
            foreach (var header in result.Headers)
            {
                dr[header] = row.GetValueOrDefault(header, string.Empty);
            }
            table.Rows.Add(dr);
        }

        return table;
    }

    [RelayCommand]
    private async Task ExportAnonymizedAsync(string outputPath, CancellationToken ct)
    {
        if (_lastResult is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Exporting anonymized file...";

            await _writer.WriteAnonymizedCsvAsync(outputPath, _lastResult, ct);

            StatusMessage = $"Anonymized file saved to: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAnonymizedXlsxAsync(string outputPath, CancellationToken ct)
    {
        if (_lastResult is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Exporting anonymized file...";

            await _writer.WriteAnonymizedXlsxAsync(outputPath, _lastResult, ct);

            StatusMessage = $"Anonymized file saved to: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportTranscodeTableAsync(string outputPath, CancellationToken ct)
    {
        if (_lastResult is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Exporting transcode table...";

            await _writer.WriteTranscodeTableAsync(outputPath, _lastResult.TranscodeTable, ct);

            StatusMessage = $"Transcode table saved to: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportTranscodeTableXlsxAsync(string outputPath, CancellationToken ct)
    {
        if (_lastResult is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Exporting transcode table...";

            await _writer.WriteTranscodeTableXlsxAsync(outputPath, _lastResult.TranscodeTable, ct);

            StatusMessage = $"Transcode table saved to: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// Wrapper for column classification with user-toggleable selection.
/// </summary>
public partial class ColumnClassificationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _columnName = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private CategoryOption _category = null!;
}

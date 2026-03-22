using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using SfAnonymizer.Wpf.ViewModels;

namespace SfAnonymizer.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.ManageCategoriesRequested += (_, _) =>
        {
            var dialog = new ManageCategoriesDialog(_vm.AvailableCategories) { Owner = this };
            dialog.ShowDialog();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.IsBusy))
                Mouse.OverrideCursor = _vm.IsBusy ? Cursors.Wait : null;
        };
    }

    // ── DataGrid column binding fix for dotted column names (e.g. "Account.Name") ──

    internal void PreviewDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // WPF interprets dots in property paths, so "Account.Name" would try to bind to Account.Name property.
        // Using indexed binding [ColumnName] bypasses path parsing and binds directly by column name.
        if (e.Column is DataGridTextColumn col)
        {
            col.Binding = new Binding($"[{e.PropertyName}]");
        }
    }

    // ── File Browse ──

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Salesforce Export File",
            Filter = "Supported Files (*.csv;*.xlsx)|*.csv;*.xlsx|CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            _vm.InputFilePath = dialog.FileName;
        }
    }

    // ── Export Dialogs ──

    private async void ExportAnonymized_Click(object sender, RoutedEventArgs e)
    {
        var inputName = Path.GetFileNameWithoutExtension(_vm.InputFilePath);
        var dialog = new SaveFileDialog
        {
            Title = "Save Anonymized File",
            Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
            FileName = $"{inputName}_anonymized.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            if (Path.GetExtension(dialog.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                await _vm.ExportAnonymizedXlsxCommand.ExecuteAsync(dialog.FileName);
            else
                await _vm.ExportAnonymizedCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private async void ExportTranscode_Click(object sender, RoutedEventArgs e)
    {
        var inputName = Path.GetFileNameWithoutExtension(_vm.InputFilePath);
        var dialog = new SaveFileDialog
        {
            Title = "Save Transcode Table",
            Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
            FileName = $"{inputName}_transcode.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            if (Path.GetExtension(dialog.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                await _vm.ExportTranscodeTableXlsxCommand.ExecuteAsync(dialog.FileName);
            else
                await _vm.ExportTranscodeTableCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private async void ExportAnonymizedXlsx_Click(object sender, RoutedEventArgs e)
    {
        var inputName = Path.GetFileNameWithoutExtension(_vm.InputFilePath);
        var dialog = new SaveFileDialog
        {
            Title = "Save Anonymized File as Excel",
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"{inputName}_anonymized.xlsx"
        };

        if (dialog.ShowDialog() == true)
            await _vm.ExportAnonymizedXlsxCommand.ExecuteAsync(dialog.FileName);
    }

    // ── Drag & Drop ──

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var ext = Path.GetExtension(files?.FirstOrDefault() ?? "").ToLowerInvariant();
            if (ext is ".csv" or ".xlsx")
            {
                e.Effects = DragDropEffects.Copy;
                DropOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
                return;
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var file = files?.FirstOrDefault();

        if (file is null) return;

        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is not (".csv" or ".xlsx")) return;

        _vm.InputFilePath = file;
    }

}

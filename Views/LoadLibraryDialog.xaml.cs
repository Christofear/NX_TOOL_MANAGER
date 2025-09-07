using Microsoft.Win32;
using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace NX_TOOL_MANAGER
{
    public partial class LoadLibraryDialog : Window, INotifyPropertyChanged
    {
        public string ToolsPath { get => _toolsPath; set { _toolsPath = value; OnPropertyChanged(nameof(ToolsPath)); } }
        public string HoldersPath { get => _holdersPath; set { _holdersPath = value; OnPropertyChanged(nameof(HoldersPath)); } }
        public string ShanksPath { get => _shanksPath; set { _shanksPath = value; OnPropertyChanged(nameof(ShanksPath)); } }
        // FIX: Added a new property for the trackpoints file path.
        public string TrackpointsPath { get => _trackpointsPath; set { _trackpointsPath = value; OnPropertyChanged(nameof(TrackpointsPath)); } }

        private string _toolsPath, _holdersPath, _shanksPath, _trackpointsPath;

        public LoadLibraryDialog()
        {
            InitializeComponent();
            DataContext = this;

            var mgr = LibraryManager.Instance;
            ToolsPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Tools)?.FullPath ?? "";
            HoldersPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Holders)?.FullPath ?? "";
            ShanksPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Shanks)?.FullPath ?? "";
            // FIX: Prefill the trackpoints path from the LibraryManager.
            TrackpointsPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Trackpoints)?.FullPath ?? "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && Path.GetExtension(files[0]).Equals(".dat", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effects = DragDropEffects.Copy;
                }
            }
        }

        private void TextBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0) return;

            string path = files[0];
            if (!Path.GetExtension(path).Equals(".dat", StringComparison.OrdinalIgnoreCase))
            {
                ShowInvalidFileError("The dragged item is not a valid .dat file.");
                return;
            }

            if (sender is TextBox tb)
            {
                var expectedKind = GetExpectedKindForTextBox(tb);
                if (!VerifyFileContent(path, expectedKind))
                {
                    ShowInvalidFileError($"The dragged file does not appear to be a valid '{expectedKind}' library.");
                    return;
                }

                switch (tb.Name)
                {
                    case "ToolsTextBox": ToolsPath = path; break;
                    case "HoldersTextBox": HoldersPath = path; break;
                    case "ShanksTextBox": ShanksPath = path; break;
                    // FIX: Handle the drop for the new trackpoints textbox.
                    case "TrackpointsTextBox": TrackpointsPath = path; break;
                }
            }
        }

        private string PickFile(FileKind expectedKind)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "NX ASCII DB (*.dat)|*.dat",
                Title = $"Select {expectedKind} .dat file"
            };

            if (dlg.ShowDialog() == true)
            {
                var path = dlg.FileName;
                if (!VerifyFileContent(path, expectedKind))
                {
                    ShowInvalidFileError($"The selected file does not appear to be a valid '{expectedKind}' library.");
                    return null;
                }
                return path;
            }
            return null;
        }

        private void PickTools_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile(FileKind.Tools);
            if (!string.IsNullOrWhiteSpace(path)) ToolsPath = path;
        }
        private void PickHolders_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile(FileKind.Holders);
            if (!string.IsNullOrWhiteSpace(path)) HoldersPath = path;
        }
        private void PickShanks_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile(FileKind.Shanks);
            if (!string.IsNullOrWhiteSpace(path)) ShanksPath = path;
        }
        // FIX: Added a click handler for the new trackpoints button.
        private void PickTrackpoints_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile(FileKind.Trackpoints);
            if (!string.IsNullOrWhiteSpace(path)) TrackpointsPath = path;
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if ((!string.IsNullOrEmpty(ToolsPath) && !File.Exists(ToolsPath)) ||
                (!string.IsNullOrEmpty(HoldersPath) && !File.Exists(HoldersPath)) ||
                (!string.IsNullOrEmpty(ShanksPath) && !File.Exists(ShanksPath)) ||
                // FIX: Added a check for the new trackpoints path.
                (!string.IsNullOrEmpty(TrackpointsPath) && !File.Exists(TrackpointsPath)))
            {
                MessageBox.Show(this, "One or more of the specified files does not exist.",
                    "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // FIX: Pass the new trackpoints path to the LibraryManager.
            LibraryManager.Instance.ApplySelection(ToolsPath, HoldersPath, ShanksPath, TrackpointsPath);
            DialogResult = true;
        }

        private void ClearTools_Click(object sender, RoutedEventArgs e) => ToolsPath = string.Empty;
        private void ClearHolders_Click(object sender, RoutedEventArgs e) => HoldersPath = string.Empty;
        private void ClearShanks_Click(object sender, RoutedEventArgs e) => ShanksPath = string.Empty;
        // FIX: Added a click handler for the new clear button.
        private void ClearTrackpoints_Click(object sender, RoutedEventArgs e) => TrackpointsPath = string.Empty;

        private FileKind GetExpectedKindForTextBox(TextBox tb)
        {
            return tb.Name switch
            {
                "ToolsTextBox" => FileKind.Tools,
                "HoldersTextBox" => FileKind.Holders,
                "ShanksTextBox" => FileKind.Shanks,
                // FIX: Added a case for the new trackpoints textbox.
                "TrackpointsTextBox" => FileKind.Trackpoints,
                _ => FileKind.Tools
            };
        }

        private bool VerifyFileContent(string path, FileKind expectedKind)
        {
            try
            {
                var lines = File.ReadLines(path).Take(500).ToList();
                var content = string.Join("\n", lines).ToLowerInvariant();

                switch (expectedKind)
                {
                    case FileKind.Tools:
                        bool hasHeader = content.Contains("tool_database.dat");
                        bool hasClass = content.Contains("#class");
                        bool hasFormat = content.Contains("format");
                        bool hasUnits = content.Contains("english") || content.Contains("metric");
                        return hasHeader && hasClass && hasFormat && hasUnits;

                    case FileKind.Holders:
                        bool hasHolderHeader = content.Contains("holder_ascii.dat");
                        bool hasRtype = content.Contains("rtype");
                        bool hasStype = content.Contains("stype");
                        bool hasHtype = content.Contains("htype");
                        return hasHolderHeader && hasRtype && hasStype && hasHtype;

                    case FileKind.Shanks:
                        bool hasShankHeader = content.Contains("shank_ascii.dat");
                        bool hasRtypeField = content.Contains("rtype");
                        bool hasStypeField = content.Contains("stype");
                        return hasShankHeader && hasRtypeField && hasStypeField;
                    case FileKind.Trackpoints:
                        return content.Contains("trackpoint_database.dat");
                }
            }
            catch (Exception) { return false; }
            return false;
        }

        private void ShowInvalidFileError(string message) =>
            MessageBox.Show(this, message, "Invalid File Type", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}


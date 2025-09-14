using Microsoft.Win32;
using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_TOOL_MANAGER
{
    public partial class LoadLibraryDialog : Window, INotifyPropertyChanged
    {
        public string ToolsPath { get; set; }
        public string HoldersPath { get; set; }
        public string ShanksPath { get; set; }
        public string TrackpointsPath { get; set; }
        public string SegmentedToolsPath { get; set; }

        public LoadLibraryDialog()
        {
            InitializeComponent();
            DataContext = this;
            LoadSettings();
        }

        private void LoadSettings()
        {
            ToolsPath = Properties.Settings.Default.ToolsPath;
            HoldersPath = Properties.Settings.Default.HoldersPath;
            ShanksPath = Properties.Settings.Default.ShanksPath;
            TrackpointsPath = Properties.Settings.Default.TrackpointsPath;
            SegmentedToolsPath = Properties.Settings.Default.SegmentedToolsPath;

            if (string.IsNullOrEmpty(ToolsPath) && string.IsNullOrEmpty(HoldersPath))
            {
                AutoPopulateFilePathsFromEnvironment();
            }

            OnAllPropertiesChanged();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.ToolsPath = ToolsPath;
            Properties.Settings.Default.HoldersPath = HoldersPath;
            Properties.Settings.Default.ShanksPath = ShanksPath;
            Properties.Settings.Default.TrackpointsPath = TrackpointsPath;
            Properties.Settings.Default.SegmentedToolsPath = SegmentedToolsPath;
            Properties.Settings.Default.Save();
        }

        private void AutoPopulateFilePathsFromEnvironment()
        {
            try
            {
                string ugiiBaseDir = Environment.GetEnvironmentVariable("UGII_BASE_DIR");
                if (string.IsNullOrEmpty(ugiiBaseDir)) return;

                string resourcePath = Path.Combine(ugiiBaseDir, "MACH", "resource", "library", "tool", "english");
                if (!Directory.Exists(resourcePath)) return;

                ToolsPath = FindFileOrDefault(resourcePath, "tool_database.dat");
                HoldersPath = FindFileOrDefault(resourcePath, "holder_database.dat");
                ShanksPath = FindFileOrDefault(resourcePath, "shank_database.dat");
                TrackpointsPath = FindFileOrDefault(resourcePath, "trackpoint_database.dat");
                SegmentedToolsPath = FindFileOrDefault(resourcePath, "segmented_tool_database.dat");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not auto-detect from UGII_BASE_DIR: {ex.Message}");
            }
        }

        private string FindFileOrDefault(string basePath, string fileName)
        {
            string fullPath = Path.Combine(basePath, fileName);
            return File.Exists(fullPath) ? fullPath : string.Empty;
        }

        #region Event Handlers

        private void PickFileAndSetPath(FileKind kind)
        {
            string currentPath = kind switch
            {
                FileKind.Tools => ToolsPath,
                FileKind.Holders => HoldersPath,
                FileKind.Shanks => ShanksPath,
                FileKind.Trackpoints => TrackpointsPath,
                FileKind.SegmentedTools => SegmentedToolsPath,
                _ => ""
            };

            var path = PickFile(kind, currentPath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                switch (kind)
                {
                    case FileKind.Tools: ToolsPath = path; break;
                    case FileKind.Holders: HoldersPath = path; break;
                    case FileKind.Shanks: ShanksPath = path; break;
                    case FileKind.Trackpoints: TrackpointsPath = path; break;
                    case FileKind.SegmentedTools: SegmentedToolsPath = path; break;
                }
                OnAllPropertiesChanged();
            }
        }

        private string PickFile(FileKind expectedKind, string initialPath)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "NX ASCII DB (*.dat)|*.dat",
                Title = $"Select {expectedKind} .dat file"
            };

            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(Path.GetDirectoryName(initialPath)))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(initialPath);
            }

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

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if ((!string.IsNullOrEmpty(ToolsPath) && !File.Exists(ToolsPath)) ||
                (!string.IsNullOrEmpty(HoldersPath) && !File.Exists(HoldersPath)) ||
                (!string.IsNullOrEmpty(ShanksPath) && !File.Exists(ShanksPath)) ||
                (!string.IsNullOrEmpty(TrackpointsPath) && !File.Exists(TrackpointsPath)) ||
                (!string.IsNullOrEmpty(SegmentedToolsPath) && !File.Exists(SegmentedToolsPath)))
            {
                MessageBox.Show(this, "One or more of the specified files does not exist.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveSettings();
            LibraryManager.Instance.ApplySelection(ToolsPath, HoldersPath, ShanksPath, TrackpointsPath, SegmentedToolsPath);
            DialogResult = true;
        }

        private void PickTools_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(FileKind.Tools);
        private void PickHolders_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(FileKind.Holders);
        private void PickShanks_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(FileKind.Shanks);
        private void PickTrackpoints_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(FileKind.Trackpoints);
        private void PickSegmentedTools_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(FileKind.SegmentedTools);

        private void ClearTools_Click(object sender, RoutedEventArgs e) { ToolsPath = string.Empty; OnPropertyChanged(nameof(ToolsPath)); }
        private void ClearHolders_Click(object sender, RoutedEventArgs e) { HoldersPath = string.Empty; OnPropertyChanged(nameof(HoldersPath)); }
        private void ClearShanks_Click(object sender, RoutedEventArgs e) { ShanksPath = string.Empty; OnPropertyChanged(nameof(ShanksPath)); }
        private void ClearTrackpoints_Click(object sender, RoutedEventArgs e) { TrackpointsPath = string.Empty; OnPropertyChanged(nameof(TrackpointsPath)); }
        private void ClearSegmentedTools_Click(object sender, RoutedEventArgs e) { SegmentedToolsPath = string.Empty; OnPropertyChanged(nameof(SegmentedToolsPath)); }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void TextBox_Drop(object sender, DragEventArgs e) { /* Existing logic */ }
        private void TextBox_PreviewDragOver(object sender, DragEventArgs e) { /* Existing logic */ }

        private void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && e.ClickCount == 3)
            {
                textBox.SelectAll();
            }
        }

        #endregion

        #region Helpers

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
                        bool hasHolderHeader = content.Contains("holder_database.dat") || content.Contains("holder_ascii.dat");
                        bool hasRtype = content.Contains("rtype");
                        bool hasStype = content.Contains("stype");
                        bool hasHtype = content.Contains("htype");
                        return hasHolderHeader && hasRtype && hasStype && hasHtype;

                    case FileKind.Shanks:
                        bool hasShankHeader = content.Contains("shank_database.dat") || content.Contains("shank_ascii.dat");
                        bool hasRtypeField = content.Contains("rtype");
                        bool hasStypeField = content.Contains("stype");
                        return hasShankHeader && hasRtypeField && hasStypeField;

                    case FileKind.Trackpoints:
                        return content.Contains("trackpoint_database.dat");

                    case FileKind.SegmentedTools:
                        return content.Contains("segmented_tool_database.dat");
                }
            }
            catch (Exception) { return false; }
            return false;
        }

        private void ShowInvalidFileError(string message) => MessageBox.Show(this, message, "Invalid File Type", MessageBoxButton.OK, MessageBoxImage.Error);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void OnAllPropertiesChanged()
        {
            OnPropertyChanged(nameof(ToolsPath));
            OnPropertyChanged(nameof(HoldersPath));
            OnPropertyChanged(nameof(ShanksPath));
            OnPropertyChanged(nameof(TrackpointsPath));
            OnPropertyChanged(nameof(SegmentedToolsPath));
        }
        #endregion
    }
}
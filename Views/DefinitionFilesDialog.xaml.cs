using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NX_TOOL_MANAGER.Views
{
    public partial class DefinitionFilesDialog : Window, INotifyPropertyChanged
    {
        public string ToolsDefPath { get; set; }
        public string HoldersDefPath { get; set; }
        public string ShanksDefPath { get; set; }
        public string TrackpointsDefPath { get; set; }
        public string SegmentedDefPath { get; set; }

        public DefinitionFilesDialog()
        {
            InitializeComponent();
            DataContext = this;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Try to auto-populate first if settings are empty
            if (string.IsNullOrEmpty(Properties.Settings.Default.ToolsDefPath))
            {
                AutoPopulateFilePathsFromEnvironment();
            }

            LoadIndividualFileSettings();
            OnAllPropertiesChanged();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.ToolsDefPath = ToolsDefPath;
            Properties.Settings.Default.HoldersDefPath = HoldersDefPath;
            Properties.Settings.Default.ShanksDefPath = ShanksDefPath;
            Properties.Settings.Default.TrackpointsDefPath = TrackpointsDefPath;
            Properties.Settings.Default.SegmentedDefPath = SegmentedDefPath;
            Properties.Settings.Default.Save();
        }

        private void AutoPopulateFilePathsFromEnvironment()
        {
            try
            {
                string ugiiBaseDir = Environment.GetEnvironmentVariable("UGII_BASE_DIR");
                if (string.IsNullOrEmpty(ugiiBaseDir)) return;

                string resourcePath = Path.Combine(ugiiBaseDir, "MACH", "resource", "library", "tool", "ascii");
                if (!Directory.Exists(resourcePath)) return;

                ToolsDefPath = FindFileOrDefault(resourcePath, "dbc_tool_ascii.def");
                HoldersDefPath = FindFileOrDefault(resourcePath, "holder_ascii.def");
                ShanksDefPath = FindFileOrDefault(resourcePath, "shank_ascii.def");
                TrackpointsDefPath = FindFileOrDefault(resourcePath, "trackpoint_ascii.def");
                SegmentedDefPath = FindFileOrDefault(resourcePath, "segmented_tool_ascii.def");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not auto-detect from UGII_BASE_DIR: {ex.Message}");
            }
        }

        private void LoadIndividualFileSettings()
        {
            // REVERTED: Directly load the full path from settings.
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ToolsDefPath))
                ToolsDefPath = Properties.Settings.Default.ToolsDefPath;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.HoldersDefPath))
                HoldersDefPath = Properties.Settings.Default.HoldersDefPath;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ShanksDefPath))
                ShanksDefPath = Properties.Settings.Default.ShanksDefPath;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TrackpointsDefPath))
                TrackpointsDefPath = Properties.Settings.Default.TrackpointsDefPath;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.SegmentedDefPath))
                SegmentedDefPath = Properties.Settings.Default.SegmentedDefPath;
        }

        private string FindFileOrDefault(string basePath, string fileName)
        {
            string fullPath = Path.Combine(basePath, fileName);
            // REVERTED: Return the full path directly.
            return File.Exists(fullPath) ? fullPath : string.Empty;
        }

        #region Event Handlers

        private void PickFileAndSetPath(string currentPath, Action<string> setPathAction)
        {
            var path = PickDefFile(currentPath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                // REVERTED: Set the full path from the dialog directly.
                setPathAction(path);
            }
        }

        private string PickDefFile(string initialPath)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Definition Files (*.def)|*.def|All files (*.*)|*.*",
                Title = "Select Definition File"
            };

            // REVERTED: Use the path directly without expanding it.
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(Path.GetDirectoryName(initialPath)))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(initialPath);
            }

            return (dlg.ShowDialog() == true) ? dlg.FileName : null;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
        }

        private void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && e.ClickCount == 3)
            {
                textBox.SelectAll();
            }
        }

        private void PickToolsDef_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(ToolsDefPath, (p) => { ToolsDefPath = p; OnPropertyChanged(nameof(ToolsDefPath)); });
        private void PickHoldersDef_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(HoldersDefPath, (p) => { HoldersDefPath = p; OnPropertyChanged(nameof(HoldersDefPath)); });
        private void PickShanksDef_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(ShanksDefPath, (p) => { ShanksDefPath = p; OnPropertyChanged(nameof(ShanksDefPath)); });
        private void PickTrackpointsDef_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(TrackpointsDefPath, (p) => { TrackpointsDefPath = p; OnPropertyChanged(nameof(TrackpointsDefPath)); });
        private void PickSegmentedDef_Click(object sender, RoutedEventArgs e) => PickFileAndSetPath(SegmentedDefPath, (p) => { SegmentedDefPath = p; OnPropertyChanged(nameof(SegmentedDefPath)); });

        private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        #endregion

        #region Helpers

        // REVERTED: ExpandPath and CollapsePath methods have been removed.

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void OnAllPropertiesChanged()
        {
            OnPropertyChanged(nameof(ToolsDefPath));
            OnPropertyChanged(nameof(HoldersDefPath));
            OnPropertyChanged(nameof(ShanksDefPath));
            OnPropertyChanged(nameof(TrackpointsDefPath));
            OnPropertyChanged(nameof(SegmentedDefPath));
        }
        #endregion
    }
}
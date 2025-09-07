using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace NX_TOOL_MANAGER.Views
{
    public partial class DefinitionFilesDialog : Window, INotifyPropertyChanged
    {
        // Properties are now backed by the application settings
        public string ToolsDefPath { get; set; }
        public string HoldersDefPath { get; set; }
        public string ShanksDefPath { get; set; }
        public string TrackpointsDefPath { get; set; }

        public DefinitionFilesDialog()
        {
            InitializeComponent();
            DataContext = this;
            LoadSettings(); // Load settings on startup
        }

        private void LoadSettings()
        {
            // 1. Get the default paths from the environment variable first as a fallback.
            PopulatePathsFromEnvironment();

            // 2. Override with saved settings if they exist and are valid.
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ToolsDefPath) && File.Exists(Properties.Settings.Default.ToolsDefPath))
            {
                ToolsDefPath = Properties.Settings.Default.ToolsDefPath;
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.HoldersDefPath) && File.Exists(Properties.Settings.Default.HoldersDefPath))
            {
                HoldersDefPath = Properties.Settings.Default.HoldersDefPath;
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ShanksDefPath) && File.Exists(Properties.Settings.Default.ShanksDefPath))
            {
                ShanksDefPath = Properties.Settings.Default.ShanksDefPath;
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TrackpointsDefPath) && File.Exists(Properties.Settings.Default.TrackpointsDefPath))
            {
                TrackpointsDefPath = Properties.Settings.Default.TrackpointsDefPath;
            }

            // Notify the UI that the properties may have been updated from settings.
            OnPropertyChanged(nameof(ToolsDefPath));
            OnPropertyChanged(nameof(HoldersDefPath));
            OnPropertyChanged(nameof(ShanksDefPath));
            OnPropertyChanged(nameof(TrackpointsDefPath));
        }

        private void SaveSettings()
        {
            // Save the current paths from the textboxes into the application settings.
            Properties.Settings.Default.ToolsDefPath = ToolsDefPath;
            Properties.Settings.Default.HoldersDefPath = HoldersDefPath;
            Properties.Settings.Default.ShanksDefPath = ShanksDefPath;
            Properties.Settings.Default.TrackpointsDefPath = TrackpointsDefPath;

            // Persist the changes to disk.
            Properties.Settings.Default.Save();
        }

        private void PopulatePathsFromEnvironment()
        {
            try
            {
                string ugiiBaseDir = Environment.GetEnvironmentVariable("UGII_BASE_DIR");
                if (string.IsNullOrEmpty(ugiiBaseDir)) return;

                string resourcePath = Path.Combine(ugiiBaseDir, "MACH", "resource", "library", "tool", "ascii");
                if (!Directory.Exists(resourcePath)) return;

                string toolsFile = Path.Combine(resourcePath, "dbc_tool_ascii.def");
                if (File.Exists(toolsFile)) ToolsDefPath = toolsFile;

                string holdersFile = Path.Combine(resourcePath, "holder_ascii.def");
                if (File.Exists(holdersFile)) HoldersDefPath = holdersFile;

                string shanksFile = Path.Combine(resourcePath, "shank_ascii.def");
                if (File.Exists(shanksFile)) ShanksDefPath = shanksFile;

                string trackpointsFile = Path.Combine(resourcePath, "trackpoint_ascii.def");
                if (File.Exists(trackpointsFile)) TrackpointsDefPath = trackpointsFile;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not automatically locate definition files.\nError: {ex.Message}", "Auto-detection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string PickDefFile(string currentPath)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Definition Files (*.def)|*.def|All files (*.*)|*.*",
                Title = "Select Definition File"
            };

            if (!string.IsNullOrEmpty(currentPath))
            {
                string directory = Path.GetDirectoryName(currentPath);
                if (Directory.Exists(directory))
                {
                    dlg.InitialDirectory = directory;
                }
            }

            return (dlg.ShowDialog() == true) ? dlg.FileName : null;
        }

        // --- Event Handlers ---
        private void PickToolsDef_Click(object sender, RoutedEventArgs e)
        {
            string path = PickDefFile(ToolsDefPath);
            if (!string.IsNullOrEmpty(path)) ToolsDefPath = path;
            OnPropertyChanged(nameof(ToolsDefPath));
        }

        private void PickHoldersDef_Click(object sender, RoutedEventArgs e)
        {
            string path = PickDefFile(HoldersDefPath);
            if (!string.IsNullOrEmpty(path)) HoldersDefPath = path;
            OnPropertyChanged(nameof(HoldersDefPath));
        }

        private void PickShanksDef_Click(object sender, RoutedEventArgs e)
        {
            string path = PickDefFile(ShanksDefPath);
            if (!string.IsNullOrEmpty(path)) ShanksDefPath = path;
            OnPropertyChanged(nameof(ShanksDefPath));
        }

        private void PickTrackpointsDef_Click(object sender, RoutedEventArgs e)
        {
            string path = PickDefFile(TrackpointsDefPath);
            if (!string.IsNullOrEmpty(path)) TrackpointsDefPath = path;
            OnPropertyChanged(nameof(TrackpointsDefPath));
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings(); // Save the settings when the user clicks OK.
            DialogResult = true;
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


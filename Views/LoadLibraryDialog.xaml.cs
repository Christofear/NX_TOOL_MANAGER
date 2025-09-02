using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using NX_TOOL_MANAGER.Services;   // LibraryManager
using NX_TOOL_MANAGER.Models;     // FileKind (if needed elsewhere)

namespace NX_TOOL_MANAGER
{
    public partial class LoadLibraryDialog : Window, INotifyPropertyChanged
    {
        public string ToolsPath { get => _toolsPath; set { _toolsPath = value; OnPropertyChanged(nameof(ToolsPath)); } }
        public string HoldersPath { get => _holdersPath; set { _holdersPath = value; OnPropertyChanged(nameof(HoldersPath)); } }
        public string ShanksPath { get => _shanksPath; set { _shanksPath = value; OnPropertyChanged(nameof(ShanksPath)); } }

        private string _toolsPath, _holdersPath, _shanksPath;

        public LoadLibraryDialog()
        {
            InitializeComponent();
            DataContext = this;

            // Prefill from LibraryManager (current loaded files)
            var mgr = LibraryManager.Instance;
            ToolsPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Tools)?.FullPath ?? "";
            HoldersPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Holders)?.FullPath ?? "";
            ShanksPath = mgr.Libraries.FirstOrDefault(x => x.Kind == FileKind.Shanks)?.FullPath ?? "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // --- Drag & Drop validation ---
        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && IsValidDatFile(files[0]))
                    e.Effects = DragDropEffects.Copy;
            }
        }

        private void TextBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0 || !IsValidDatFile(files[0])) { ShowInvalidFileError(); return; }

            if (sender is TextBox tb)
            {
                switch (tb.Name)
                {
                    case "ToolsTextBox": ToolsPath = files[0]; break;
                    case "HoldersTextBox": HoldersPath = files[0]; break;
                    case "ShanksTextBox": ShanksPath = files[0]; break;
                }
            }
        }

        // --- File picking ---
        private string PickFile()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "NX ASCII Tool DB (*.dat)|*.dat",
                Title = "Select .dat file"
            };

            if (dlg.ShowDialog() == true)
            {
                if (IsValidDatFile(dlg.FileName)) return dlg.FileName;
                ShowInvalidFileError();
            }
            return null;
        }

        private void PickTools_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile();
            if (!string.IsNullOrWhiteSpace(path)) ToolsPath = path;
        }
        private void PickHolders_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile();
            if (!string.IsNullOrWhiteSpace(path)) HoldersPath = path;
        }
        private void PickShanks_Click(object sender, RoutedEventArgs e)
        {
            var path = PickFile();
            if (!string.IsNullOrWhiteSpace(path)) ShanksPath = path;
        }

        // --- OK / Apply ---
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            // Existence checks only for non-empty paths
            if ((!string.IsNullOrEmpty(ToolsPath) && !File.Exists(ToolsPath)) ||
                (!string.IsNullOrEmpty(HoldersPath) && !File.Exists(HoldersPath)) ||
                (!string.IsNullOrEmpty(ShanksPath) && !File.Exists(ShanksPath)))
            {
                MessageBox.Show(this, "One or more of the specified files does not exist.",
                    "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 🔗 Apply atomically to LibraryManager (loads + unloads)
            LibraryManager.Instance.ApplySelection(ToolsPath, HoldersPath, ShanksPath);

            DialogResult = true;
        }

        // --- Clear buttons (do not unload yet; unload happens on OK via ApplySelection) ---
        private void ClearTools_Click(object sender, RoutedEventArgs e) => ToolsPath = string.Empty;
        private void ClearHolders_Click(object sender, RoutedEventArgs e) => HoldersPath = string.Empty;
        private void ClearShanks_Click(object sender, RoutedEventArgs e) => ShanksPath = string.Empty;

        // --- Helpers ---
        private static bool IsValidDatFile(string path) =>
            !string.IsNullOrEmpty(path) &&
            string.Equals(Path.GetExtension(path), ".dat", StringComparison.OrdinalIgnoreCase);

        private void ShowInvalidFileError() =>
            MessageBox.Show(this, "The selected item is not a valid .dat file.",
                "Invalid File Type", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // XAML helper for showing the ✖ button only when a path exists
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}

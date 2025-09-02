using NX_TOOL_MANAGER.Helpers;
using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using NX_TOOL_MANAGER.Views;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace NX_TOOL_MANAGER.Views
{
    public enum LogType { Warning, Error }
    public enum PageKind { Viewer, BulkEditor, Merger }

    public class LogEntry
    {
        public LogType Type { get; set; }
        public string Message { get; set; }
    }
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // === Navigation state ===
        private PageKind _currentPage = PageKind.Viewer;
        public PageKind CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); }
        }

        // === Views ===
        private readonly ViewerView _viewerView;
        private readonly BulkEditorView _bulkEditorView;
        private readonly MergerView _mergerView;

        // === Log ===
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();
        public int ErrorCount => LogEntries.Count(l => l.Type == LogType.Error);
        public int WarningCount => LogEntries.Count(l => l.Type == LogType.Warning);

        private bool _isLogExpanded;
        public bool IsLogExpanded
        {
            get => _isLogExpanded;
            set { _isLogExpanded = value; OnPropertyChanged(nameof(IsLogExpanded)); }
        }

        // === INotifyPropertyChanged ===
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Views
            _viewerView = new ViewerView();
            _bulkEditorView = new BulkEditorView();
            _mergerView = new MergerView();
            PageHost.Content = _viewerView;

            // Keep counts fresh whenever items are added/removed
            LogEntries.CollectionChanged += LogEntries_CollectionChanged;

            // Seed sample log rows
            LogEntries.Add(new LogEntry { Type = LogType.Warning, Message = "Tool T01 has no assigned holder." });
            LogEntries.Add(new LogEntry { Type = LogType.Error, Message = "Failed to parse Shank_Database.dat: Invalid character at line 52." });
        }

        private void LogEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
        }

        // === Title bar, window chrome ===
        private void ToggleLogPanel_Click(object sender, RoutedEventArgs e) => IsLogExpanded = !IsLogExpanded;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        // === Menu/toolbar ===
        private void LoadLibrary_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LoadLibraryDialog { Owner = this };
            dlg.ShowDialog();
        }

        private void Viewer_Click(object sender, RoutedEventArgs e)
        {
            PageHost.Content = _viewerView;
            CurrentPage = PageKind.Viewer;
        }

        private void BulkEditor_Click(object sender, RoutedEventArgs e)
        {
            PageHost.Content = _bulkEditorView;
            CurrentPage = PageKind.BulkEditor;
        }

        private void Merger_Click(object sender, RoutedEventArgs e)
        {
            PageHost.Content = _mergerView;
            CurrentPage = PageKind.Merger;
        }

        private void SaveLibrary_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void Exit_Click(object sender, RoutedEventArgs e) => Close();
        private void Convert_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void Import_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void Export_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void New_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void SaveAs_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void SaveAll_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void ApplicationSettings_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void LayoutSettings_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void HelpTopics_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void About_Click(object sender, RoutedEventArgs e) { /* TODO */ }
    }
}

using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using static NX_TOOL_MANAGER.Helpers.WindowInterop;
using System.Windows.Shell;

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
        private DatDocumentRef _currentlyMonitoredDoc;

        private PageKind _currentPage = PageKind.Viewer;
        public PageKind CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); }
        }

        private readonly EditorView _editorView;
        private readonly BulkEditorView _bulkEditorView;
        private readonly MergerView _mergerView;

        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();
        public int ErrorCount => LogEntries.Count(l => l.Type == LogType.Error);
        public int WarningCount => LogEntries.Count(l => l.Type == LogType.Warning);

        private bool _isLogExpanded;
        public bool IsLogExpanded
        {
            get => _isLogExpanded;
            set { _isLogExpanded = value; OnPropertyChanged(nameof(IsLogExpanded)); }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _editorView = new EditorView();
            _bulkEditorView = new BulkEditorView();
            _mergerView = new MergerView();
            PageHost.Content = _editorView;

            LogEntries.CollectionChanged += LogEntries_CollectionChanged;

            LibraryManager.Instance.PropertyChanged += LibraryManager_PropertyChanged;
            UpdateSaveButtonStates();
        }

        #region Dynamic Save Button Properties
        private string _saveMenuItemHeader = "Save";
        public string SaveMenuItemHeader
        {
            get => _saveMenuItemHeader;
            set { _saveMenuItemHeader = value; OnPropertyChanged(); }
        }

        private bool _isSaveEnabled;
        public bool IsSaveEnabled
        {
            get => _isSaveEnabled;
            set { _isSaveEnabled = value; OnPropertyChanged(); }
        }

        private bool _isSaveAsEnabled;
        public bool IsSaveAsEnabled
        {
            get => _isSaveAsEnabled;
            set { _isSaveAsEnabled = value; OnPropertyChanged(); }
        }

        private bool _isSaveAllEnabled;
        public bool IsSaveAllEnabled
        {
            get => _isSaveAllEnabled;
            set { _isSaveAllEnabled = value; OnPropertyChanged(); }
        }
        #endregion

        #region Event Handling for Save States
        private void LibraryManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryManager.Instance.SelectedDocument) || e.PropertyName == nameof(LibraryManager.Instance.Libraries))
            {
                if (_currentlyMonitoredDoc != null)
                {
                    _currentlyMonitoredDoc.PropertyChanged -= SelectedDocument_PropertyChanged;
                }
                _currentlyMonitoredDoc = LibraryManager.Instance.SelectedDocument;
                if (_currentlyMonitoredDoc != null)
                {
                    _currentlyMonitoredDoc.PropertyChanged += SelectedDocument_PropertyChanged;
                }
                UpdateSaveButtonStates();
            }
        }

        private void SelectedDocument_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DatDocumentRef.IsDirty))
            {
                UpdateSaveButtonStates();
            }
        }

        private void UpdateSaveButtonStates()
        {
            var manager = LibraryManager.Instance;
            var doc = manager.SelectedDocument;

            if (doc == null)
            {
                SaveMenuItemHeader = "Save";
                IsSaveEnabled = false;
                IsSaveAsEnabled = false;
            }
            else
            {
                SaveMenuItemHeader = $"Save {doc.DisplayFileName.Replace("_", "__")}";
                IsSaveAsEnabled = true;
                IsSaveEnabled = doc.IsDirty;
            }

            IsSaveAllEnabled = manager.Libraries.Any(d => d.IsDirty);
        }
        #endregion

        #region Maximize Fix

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr mWindowHandle = (new WindowInteropHelper(this)).Handle;
            HwndSource.FromHwnd(mWindowHandle).AddHook(new HwndSourceHook(WindowProc));
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                WmGetMinMaxInfo(hwnd, lParam);
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            GetCursorPos(out POINT lMousePosition);
            IntPtr lPrimaryScreen = MonitorFromPoint(lMousePosition, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            var lPrimaryScreenInfo = new MONITORINFO();
            if (GetMonitorInfo(lPrimaryScreen, lPrimaryScreenInfo))
            {
                var lMmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                lMmi.ptMaxPosition.X = lPrimaryScreenInfo.rcWork.Left;
                lMmi.ptMaxPosition.Y = lPrimaryScreenInfo.rcWork.Top;
                lMmi.ptMaxSize.X = lPrimaryScreenInfo.rcWork.Right - lPrimaryScreenInfo.rcWork.Left;
                lMmi.ptMaxSize.Y = lPrimaryScreenInfo.rcWork.Bottom - lPrimaryScreenInfo.rcWork.Top;
                Marshal.StructureToPtr(lMmi, lParam, true);
            }
        }

        // THE FIX: This event handler now also adjusts the WindowChrome properties.
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            var chrome = WindowChrome.GetWindowChrome(this);

            if (WindowState == WindowState.Maximized)
            {
                // Remove the visual border and the invisible resize border to eliminate the gap
                RootBorder.BorderThickness = new Thickness(0);
                chrome.ResizeBorderThickness = new Thickness(0);
            }
            else
            {
                // Restore the borders when not maximized
                RootBorder.BorderThickness = new Thickness(1);
                chrome.ResizeBorderThickness = new Thickness(6);
            }
        }
        #endregion

        #region Existing Event Handlers

        private void LogEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
        }

        public void AddLogEntry(LogType type, string message)
        {
            Dispatcher.Invoke(() => { LogEntries.Add(new LogEntry { Type = type, Message = message }); });
        }

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

        private void LoadLibrary_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LoadLibraryDialog { Owner = this };
            dlg.ShowDialog();
        }

        private void MainToolbar_PageNavigation(object sender, RoutedPropertyChangedEventArgs<PageKind> e)
        {
            CurrentPage = e.NewValue;
            PageHost.Content = CurrentPage switch
            {
                PageKind.Viewer => _editorView,
                PageKind.BulkEditor => _bulkEditorView,
                PageKind.Merger => _mergerView,
                _ => PageHost.Content
            };
        }

        private void SaveLibrary_Click(object sender, RoutedEventArgs e)
        {
            var manager = LibraryManager.Instance;
            var selectedDoc = manager.SelectedDocument;
            if (selectedDoc != null && manager.SaveLibraryCommand.CanExecute(selectedDoc))
            {
                manager.SaveLibraryCommand.Execute(selectedDoc);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var manager = LibraryManager.Instance;
            var selectedDoc = manager.SelectedDocument;
            if (selectedDoc != null && manager.SaveAsLibraryCommand.CanExecute(selectedDoc))
            {
                manager.SaveAsLibraryCommand.Execute(selectedDoc);
            }
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            var manager = LibraryManager.Instance;
            foreach (var doc in manager.Libraries.Where(d => d.IsDirty).ToList())
            {
                if (manager.SaveLibraryCommand.CanExecute(doc))
                {
                    manager.SaveLibraryCommand.Execute(doc);
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();
        private void New_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void ApplicationSettings_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void LayoutSettings_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void HelpTopics_Click(object sender, RoutedEventArgs e) { /* TODO */ }
        private void About_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        private void FieldDefinitions_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DefinitionFilesDialog { Owner = this };
            dlg.ShowDialog();
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}


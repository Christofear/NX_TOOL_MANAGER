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
using System.IO;

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
        #region Commands for Shortcuts
        public static readonly RoutedUICommand SaveFileCommand = new RoutedUICommand("Save File", "SaveFile", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });
        public static readonly RoutedUICommand SaveAsCommand = new RoutedUICommand("Save As", "SaveAs", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift) });
        public static readonly RoutedUICommand SaveAllCommand = new RoutedUICommand("Save All", "SaveAll", typeof(MainWindow));
        public static readonly RoutedUICommand CloseFileCommand = new RoutedUICommand("Close File", "CloseFile", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.W, ModifierKeys.Control) });
        public static readonly RoutedUICommand CloseAllFilesCommand = new RoutedUICommand("Close All Files", "CloseAllFiles", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.W, ModifierKeys.Control | ModifierKeys.Shift) });
        public static readonly RoutedUICommand ToggleLibraryTreeCommand = new RoutedUICommand("Toggle Library Tree", "ToggleLibraryTree", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.D1, ModifierKeys.Control) });
        public static readonly RoutedUICommand ToggleToolPreviewCommand = new RoutedUICommand("Toggle Tool Preview", "ToggleToolPreview", typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.D2, ModifierKeys.Control) });
        #endregion

        private DatDocumentRef _currentlyMonitoredDoc;

        private PageKind _currentPage = PageKind.Viewer;
        public PageKind CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); }
        }

        private readonly ViewerView _viewerView;
        public ViewerView TheViewerView => _viewerView;
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

            _viewerView = new ViewerView();
            _bulkEditorView = new BulkEditorView();
            _mergerView = new MergerView();
            PageHost.Content = _viewerView;

            LogEntries.CollectionChanged += LogEntries_CollectionChanged;

            LibraryManager.Instance.PropertyChanged += LibraryManager_PropertyChanged;
            LibraryManager.Instance.Libraries.CollectionChanged += Libraries_CollectionChanged;
            UpdateSaveButtonStates();

            #region Command Bindings
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Close()));
            CommandBindings.Add(new CommandBinding(SaveFileCommand, SaveLibrary_Click, (s, e) => e.CanExecute = IsSaveEnabled));
            CommandBindings.Add(new CommandBinding(SaveAsCommand, SaveAs_Click, (s, e) => e.CanExecute = IsSaveAsEnabled));
            CommandBindings.Add(new CommandBinding(SaveAllCommand, SaveAll_Click, (s, e) => e.CanExecute = IsSaveAllEnabled));
            CommandBindings.Add(new CommandBinding(CloseFileCommand, CloseLibrary_Click, (s, e) => e.CanExecute = IsCloseEnabled));
            CommandBindings.Add(new CommandBinding(CloseAllFilesCommand, CloseAll_Click, (s, e) => e.CanExecute = IsCloseAllEnabled));
            CommandBindings.Add(new CommandBinding(ToggleLibraryTreeCommand, (s, e) => TheViewerView.IsClassTreeExpanded = !TheViewerView.IsClassTreeExpanded));
            CommandBindings.Add(new CommandBinding(ToggleToolPreviewCommand, (s, e) => TheViewerView.IsPreviewExpanded = !TheViewerView.IsPreviewExpanded));
            #endregion

            // THE FIX: Subscribe to the Loaded event instead of calling the method directly.
            this.Loaded += MainWindow_Loaded;
        }

        // THE FIX: New event handler that runs AFTER the window is shown.
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Now it is safe to call the auto-load logic.
            AutoLoadLibraryOnStartup();
        }

        private void AutoLoadLibraryOnStartup()
        {
            var settings = Properties.Settings.Default;
            string toolsPath = settings.ToolsPath;
            string holdersPath = settings.HoldersPath;
            string shanksPath = settings.ShanksPath;
            string trackpointsPath = settings.TrackpointsPath;
            string segmentedPath = settings.SegmentedToolsPath;

            // Check if essential paths are not configured (first run)
            if (string.IsNullOrEmpty(toolsPath) && string.IsNullOrEmpty(holdersPath))
            {
                // This will now work because the MainWindow is loaded and visible.
                var configDialog = new LoadLibraryDialog { Owner = this };
                if (configDialog.ShowDialog() != true)
                {
                    Application.Current.Shutdown();
                    return;
                }
                // Re-read settings after the dialog is closed
                toolsPath = settings.ToolsPath;
                holdersPath = settings.HoldersPath;
                shanksPath = settings.ShanksPath;
                trackpointsPath = settings.TrackpointsPath;
                segmentedPath = settings.SegmentedToolsPath;
            }

            // Validate that all configured files actually exist
            if ((!string.IsNullOrEmpty(toolsPath) && !File.Exists(toolsPath)) ||
                (!string.IsNullOrEmpty(holdersPath) && !File.Exists(holdersPath)) ||
                (!string.IsNullOrEmpty(shanksPath) && !File.Exists(shanksPath)) ||
                (!string.IsNullOrEmpty(trackpointsPath) && !File.Exists(trackpointsPath)) ||
                (!string.IsNullOrEmpty(segmentedPath) && !File.Exists(segmentedPath)))
            {
                MessageBox.Show("One or more library files could not be found at the saved location. Please correct the paths.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                var configDialog = new LoadLibraryDialog { Owner = this };
                if (configDialog.ShowDialog() != true)
                {
                    Application.Current.Shutdown();
                    return;
                }
                // Re-read settings after correction
                toolsPath = settings.ToolsPath;
                holdersPath = settings.HoldersPath;
                shanksPath = settings.ShanksPath;
                trackpointsPath = settings.TrackpointsPath;
                segmentedPath = settings.SegmentedToolsPath;
            }

            LibraryManager.Instance.ApplySelection(toolsPath, holdersPath, shanksPath, trackpointsPath, segmentedPath);
        }

        #region Dynamic Menu Properties
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
        public string CloseMenuItemHeader => $"Close {LibraryManager.Instance.SelectedDocument?.FileName}";
        public bool IsCloseEnabled => LibraryManager.Instance.SelectedDocument != null;
        public bool IsCloseAllEnabled => LibraryManager.Instance.Libraries.Any();
        #endregion

        #region Event Handling for Menu States
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
                OnPropertyChanged(nameof(CloseMenuItemHeader));
                OnPropertyChanged(nameof(IsCloseEnabled));
            }
        }

        private void Libraries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsCloseAllEnabled));
            UpdateSaveButtonStates();
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
                IsSaveEnabled = doc.IsDirty && !doc.IsReadOnly;
            }

            IsSaveAllEnabled = manager.Libraries.Any(d => d.IsDirty && !d.IsReadOnly);
        }
        #endregion

        #region New Menu Click Handlers
        private void CloseLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryManager.Instance.SelectedDocument != null)
            {
                LibraryManager.Instance.Unload(LibraryManager.Instance.SelectedDocument.Kind);
            }
        }

        private void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            LibraryManager.Instance.UnloadAll();
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
            if (msg == 0x0024) { WmGetMinMaxInfo(hwnd, lParam); }
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

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            var chrome = WindowChrome.GetWindowChrome(this);
            if (WindowState == WindowState.Maximized)
            {
                RootBorder.BorderThickness = new Thickness(0);
                chrome.ResizeBorderThickness = new Thickness(0);
            }
            else
            {
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
                PageKind.Viewer => _viewerView,
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


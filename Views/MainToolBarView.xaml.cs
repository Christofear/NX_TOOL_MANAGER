using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    public partial class MainToolbarView : UserControl
    {
        // Define routed events that the MainWindow can listen for.
        public static readonly RoutedEvent LoadLibraryClickEvent = EventManager.RegisterRoutedEvent(
            "LoadLibraryClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MainToolbarView));

        public static readonly RoutedEvent SaveLibraryClickEvent = EventManager.RegisterRoutedEvent(
            "SaveLibraryClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MainToolbarView));

        public static readonly RoutedEvent PageNavigationEvent = EventManager.RegisterRoutedEvent(
            "PageNavigation", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<PageKind>), typeof(MainToolbarView));

        // Expose them as standard .NET events.
        public event RoutedEventHandler LoadLibraryClick
        {
            add { AddHandler(LoadLibraryClickEvent, value); }
            remove { RemoveHandler(LoadLibraryClickEvent, value); }
        }
        public event RoutedEventHandler SaveLibraryClick
        {
            add { AddHandler(SaveLibraryClickEvent, value); }
            remove { RemoveHandler(SaveLibraryClickEvent, value); }
        }
        public event RoutedPropertyChangedEventHandler<PageKind> PageNavigation
        {
            add { AddHandler(PageNavigationEvent, value); }
            remove { RemoveHandler(PageNavigationEvent, value); }
        }

        public MainToolbarView()
        {
            InitializeComponent();
        }

        // --- Event Handlers ---
        private void LoadLibrary_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(LoadLibraryClickEvent));
        }

        private void SaveLibrary_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(SaveLibraryClickEvent));
        }

        private void Viewer_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedPropertyChangedEventArgs<PageKind>(PageKind.Viewer, PageKind.Viewer, PageNavigationEvent));
        }

        private void BulkEditor_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedPropertyChangedEventArgs<PageKind>(PageKind.BulkEditor, PageKind.BulkEditor, PageNavigationEvent));
        }

        private void Merger_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedPropertyChangedEventArgs<PageKind>(PageKind.Merger, PageKind.Merger, PageNavigationEvent));
        }

        // Placeholders for other buttons
        private void Convert_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Convert Clicked");
        private void Import_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Import Clicked");
        private void Export_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Export Clicked");
    }
}

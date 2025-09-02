using NX_TOOL_MANAGER.Models;        // DatDocumentRef
using NX_TOOL_MANAGER.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NX_TOOL_MANAGER.Views
{
    /// <summary>
    /// Interaction logic for ViewerView.xaml
    /// </summary>
    public partial class ViewerView : UserControl
    {
        public ViewerView()
        {
            InitializeComponent();
            DataContext = this;

            // Handle clicks inside the TreeView (clear if empty space)
            Tree.PreviewMouseDown += OnTreePreviewMouseDown;

        }

        // ====== Dependency Properties ======

        /// <summary>
        /// Collection of loaded libraries (Tools, Holders, Shanks).
        /// Bound to the TreeView in XAML.
        /// </summary>
        public ObservableCollection<DatDocumentRef> Libraries
        {
            get => (ObservableCollection<DatDocumentRef>)GetValue(LibrariesProperty);
            set => SetValue(LibrariesProperty, value);
        }

        public static readonly DependencyProperty LibrariesProperty =
            DependencyProperty.Register(
                nameof(Libraries),
                typeof(ObservableCollection<DatDocumentRef>),
                typeof(ViewerView),
                new PropertyMetadata(new ObservableCollection<DatDocumentRef>()));

        // ====== Event Handlers ======

        /// <summary>
        /// Click inside TreeView background → clear selection.
        /// </summary>
        private void OnTreePreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var container = ItemsControl.ContainerFromElement(Tree, dep);
                if (container == null)
                    ClearTreeSelection(Tree);
            }
        }

        /// <summary>
        /// Click anywhere in window but outside TreeView → clear selection.
        /// </summary>
        private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (!IsDescendantOf(dep, Tree))
                    ClearTreeSelection(Tree);
            }
        }

        // ====== Helpers ======

        /// <summary>
        /// Check if node is inside ancestor, walking visuals, visuals3D, or logical tree.
        /// </summary>
        private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
        {
            while (node != null)
            {
                if (node == ancestor) return true;
                node = GetParentSafe(node);
            }
            return false;
        }

        private static DependencyObject GetParentSafe(DependencyObject node)
        {
            if (node == null) return null;

            // Visual / Visual3D
            if (node is Visual || node is Visual3D)
                return VisualTreeHelper.GetParent(node);

            // Flow content elements (e.g., Run, Span)
            if (node is FrameworkContentElement fce)
                return fce.Parent;

            // Fallback: logical parent
            return LogicalTreeHelper.GetParent(node);
        }

        /// <summary>
        /// Recursively clear selection in this TreeView or nested items.
        /// </summary>
        private void ClearTreeSelection(ItemsControl parent)
        {
            foreach (var item in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                {
                    tvi.IsSelected = false;
                    ClearTreeSelection(tvi);
                }
            }
        }


        private void Tree_SelectedItemChanged(object s, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is DatClass cls) LibraryManager.Instance.SelectedClass = cls;
            else if (e.NewValue is DatDocumentRef) LibraryManager.Instance.SelectedClass = null;
        }


        // ====== Commands for context menu ======

        public static readonly DependencyProperty UnloadLibraryCommandProperty =
            DependencyProperty.Register(nameof(UnloadLibraryCommand),
                typeof(ICommand),
                typeof(ViewerView));

        public ICommand UnloadLibraryCommand
        {
            get => (ICommand)GetValue(UnloadLibraryCommandProperty);
            set => SetValue(UnloadLibraryCommandProperty, value);
        }

        public static readonly DependencyProperty SaveLibraryCommandProperty =
            DependencyProperty.Register(nameof(SaveLibraryCommand),
                typeof(ICommand),
                typeof(ViewerView));

        public ICommand SaveLibraryCommand
        {
            get => (ICommand)GetValue(SaveLibraryCommandProperty);
            set => SetValue(SaveLibraryCommandProperty, value);
        }

        public static readonly DependencyProperty SaveAsLibraryCommandProperty =
            DependencyProperty.Register(nameof(SaveAsLibraryCommand),
                typeof(ICommand),
                typeof(ViewerView));

        public ICommand SaveAsLibraryCommand
        {
            get => (ICommand)GetValue(SaveAsLibraryCommandProperty);
            set => SetValue(SaveAsLibraryCommandProperty, value);
        }



    }
}

using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    public partial class ClassTree : UserControl
    {
        // This custom event will allow the parent (ViewerView) to know when the selection changes.
        public event RoutedPropertyChangedEventHandler<object> SelectedItemChanged;

        public ClassTree()
        {
            InitializeComponent();
        }

        // This is the original event handler from the TreeView.
        private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // When the selection changes, we raise our custom event to notify the parent.
            SelectedItemChanged?.Invoke(this, e);
        }

        // --- Context Menu Handlers ---
        // This logic now correctly lives inside the control that owns the menu.
        private void UnloadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: DatDocumentRef docRef })
            {
                LibraryManager.Instance.Unload(docRef.Kind);
            }
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // FIX: Implemented Save Logic
            if (sender is MenuItem { DataContext: DatDocumentRef docRef })
            {
                // Check if the command can be executed (it's good practice)
                if (LibraryManager.Instance.SaveLibraryCommand.CanExecute(docRef))
                {
                    // Execute the command, which is already defined in the LibraryManager
                    LibraryManager.Instance.SaveLibraryCommand.Execute(docRef);
                }
            }
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // FIX: Implement Save As Logic
            if (sender is MenuItem { DataContext: DatDocumentRef docRef })
            {
                if (LibraryManager.Instance.SaveAsLibraryCommand.CanExecute(docRef))
                {
                    LibraryManager.Instance.SaveAsLibraryCommand.Execute(docRef);
                }
            }
        }
    }
}


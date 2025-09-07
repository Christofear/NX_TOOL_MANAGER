using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    // THE FIX: The class name now correctly matches the file name.
    public partial class EditorView : UserControl, INotifyPropertyChanged
    {
        public EditorView()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        #region Pane Collapse Logic

        private bool _isClassTreeExpanded = true;
        public bool IsClassTreeExpanded
        {
            get => _isClassTreeExpanded;
            set { _isClassTreeExpanded = value; OnPropertyChanged(); }
        }

        private bool _isPreviewExpanded = true;
        public bool IsPreviewExpanded
        {
            get => _isPreviewExpanded;
            set { _isPreviewExpanded = value; OnPropertyChanged(); }
        }

        private void ToggleClassTree_Click(object sender, RoutedEventArgs e)
        {
            IsClassTreeExpanded = !IsClassTreeExpanded;
        }

        private void TogglePreview_Click(object sender, RoutedEventArgs e)
        {
            IsPreviewExpanded = !IsPreviewExpanded;
        }

        #endregion

        private void ClassTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue;
            var manager = LibraryManager.Instance;

            // Update the DataContext for the LibraryGridView.
            GridView.DataContext = selectedItem;

            // THE FIX: Also update the PreviewPane with the selection from the tree.
            // This is crucial for displaying the correct editor for Holders, Shanks, and Trackpoints.
            ThePreviewPane.SelectedObject = selectedItem;

            // Update the central LibraryManager's state for the main window's save buttons.
            if (selectedItem is DatDocumentRef docRef)
            {
                manager.SelectedDocument = docRef;
            }
            else if (selectedItem is DatClass datClass)
            {
                manager.SelectedDocument = manager.Libraries.FirstOrDefault(d => d.Document == datClass.ParentDocument);
                manager.SelectedClass = datClass;
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}

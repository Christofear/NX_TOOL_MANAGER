using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    public partial class ViewerView : UserControl, INotifyPropertyChanged
    {
        public ViewerView()
        {
            InitializeComponent();
            this.DataContext = this;

            // This logic correctly connects the code-behind to the PreviewPane and its events.
            ThePreviewPane.RequestCollapse += ThePreviewPane_RequestCollapse;
            ThePreviewPane.RequestExpand += ThePreviewPane_RequestExpand;
        }

        #region State Properties
        private bool _isClassTreeExpanded = true;
        public bool IsClassTreeExpanded { get => _isClassTreeExpanded; set { _isClassTreeExpanded = value; OnPropertyChanged(); } }

        private bool _isPreviewExpanded = true;
        public bool IsPreviewExpanded { get => _isPreviewExpanded; set { _isPreviewExpanded = value; OnPropertyChanged(); } }

        // This property is used to pass data to the LibraryGridView
        private object _parameterDataContext;
        public object ParameterDataContext { get => _parameterDataContext; set { _parameterDataContext = value; OnPropertyChanged(); } }
        #endregion

        private void ClassTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue;
            var manager = LibraryManager.Instance;

            if (selectedItem is DatDocumentRef docRef) { manager.SelectedDocument = docRef; }
            else if (selectedItem is DatClass datClass)
            {
                manager.SelectedDocument = manager.Libraries.FirstOrDefault(d => d.Document == datClass.ParentDocument);
                manager.SelectedClass = datClass;
            }

            // Pass the selected item to the main grid and the preview pane
            ParameterDataContext = selectedItem;
            ThePreviewPane.SelectedObject = selectedItem;
        }

        private void GridView_SelectedRowChanged(object sender, RoutedPropertyChangedEventArgs<DatRow> e)
        {
            // When a row is selected in the grid, pass it to the preview pane
            ThePreviewPane.SelectedRow = e.NewValue;
        }

        #region Event Handlers for Pane Toggling
        private void ToggleClassTree_Click(object sender, RoutedEventArgs e) => IsClassTreeExpanded = !IsClassTreeExpanded;
        private void TogglePreview_Click(object sender, RoutedEventArgs e) => IsPreviewExpanded = !IsPreviewExpanded;

        // These handlers are required for the PreviewPane to communicate back to this view
        private void ThePreviewPane_RequestCollapse(object sender, EventArgs e)
        {
            IsPreviewExpanded = false;
        }

        private void ThePreviewPane_RequestExpand(object sender, EventArgs e)
        {
            IsPreviewExpanded = true;
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}


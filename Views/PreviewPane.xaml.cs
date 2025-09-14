using NX_TOOL_MANAGER.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NX_TOOL_MANAGER.Views
{
    public partial class PreviewPane : UserControl
    {
        public event EventHandler RequestCollapse;
        public event EventHandler RequestExpand;

        public PreviewPane()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        // Receives the selected item from the Class Tree
        public object SelectedObject
        {
            get { return GetValue(SelectedObjectProperty); }
            set { SetValue(SelectedObjectProperty, value); }
        }
        public static readonly DependencyProperty SelectedObjectProperty =
            DependencyProperty.Register(nameof(SelectedObject), typeof(object), typeof(PreviewPane), new PropertyMetadata(null, OnSelectionChanged));

        // Receives the selected row from the LibraryGridView
        public DatRow SelectedRow
        {
            get { return (DatRow)GetValue(SelectedRowProperty); }
            set { SetValue(SelectedRowProperty, value); }
        }
        public static readonly DependencyProperty SelectedRowProperty =
            DependencyProperty.Register(nameof(SelectedRow), typeof(DatRow), typeof(PreviewPane), new PropertyMetadata(null, OnSelectionChanged));

        // Controls visibility of the Parameter Editor
        public bool IsParameterEditorVisible
        {
            get { return (bool)GetValue(IsParameterEditorVisibleProperty); }
            set { SetValue(IsParameterEditorVisibleProperty, value); }
        }
        public static readonly DependencyProperty IsParameterEditorVisibleProperty =
            DependencyProperty.Register(nameof(IsParameterEditorVisible), typeof(bool), typeof(PreviewPane), new PropertyMetadata(false));

        // Controls visibility of the Shape Editor
        public bool IsShapeEditorVisible
        {
            get { return (bool)GetValue(IsShapeEditorVisibleProperty); }
            set { SetValue(IsShapeEditorVisibleProperty, value); }
        }
        public static readonly DependencyProperty IsShapeEditorVisibleProperty =
            DependencyProperty.Register(nameof(IsShapeEditorVisible), typeof(bool), typeof(PreviewPane), new PropertyMetadata(false));

        #endregion

        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pane = (PreviewPane)d;
            pane.UpdateViewVisibility();
        }

        private void UpdateViewVisibility()
        {
            var kind = GetKindFromObject(SelectedObject);

            IsParameterEditorVisible = (kind == FileKind.Tools);
            IsShapeEditorVisible = (kind == FileKind.Holders || kind == FileKind.Shanks);

            if (kind == FileKind.Trackpoints)
            {
                RequestCollapse?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                RequestExpand?.Invoke(this, EventArgs.Empty);
            }
        }

        private static FileKind? GetKindFromObject(object obj)
        {
            if (obj is DatDocumentRef docRef) return docRef.Kind;
            if (obj is DatClass datClass) return datClass.ParentDocument?.ParentRef?.Kind;
            // This handles the case where a category node is selected in the tree
            if (obj is Services.CategoryNode catNode)
            {
                return catNode.Classes.FirstOrDefault()?.ParentDocument?.ParentRef?.Kind;
            }
            return null;
        }
    }

    // A standard converter to change a boolean value into a Visibility value.
    // Placed here to avoid creating a new file.
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


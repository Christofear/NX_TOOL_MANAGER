using NX_TOOL_MANAGER.Models;
using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    /// <summary>
    /// This is now a simple "host" control. Its only job is to receive the
    /// selected row from the main view and pass it down to the editor control.
    /// </summary>
    public partial class PreviewPane : UserControl
    {
        public PreviewPane()
        {
            InitializeComponent();
        }

        // This DependencyProperty is still needed to receive the selection from LibraryGridView.
        public DatRow SelectedRow
        {
            get => (DatRow)GetValue(SelectedRowProperty);
            set => SetValue(SelectedRowProperty, value);
        }
        public static readonly DependencyProperty SelectedRowProperty =
            DependencyProperty.Register(nameof(SelectedRow), typeof(DatRow), typeof(PreviewPane), new PropertyMetadata(null));
    }
}


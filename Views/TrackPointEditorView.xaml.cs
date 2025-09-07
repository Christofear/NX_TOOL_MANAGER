using NX_TOOL_MANAGER.Models;
using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    /// <summary>
    /// A dedicated editor that hosts the parameter list for a Trackpoint file.
    /// </summary>
    public partial class TrackpointEditorView : UserControl
    {
        public TrackpointEditorView()
        {
            InitializeComponent();
        }

        // This property receives the DatClass object (e.g., "MILLING_DRILLING")
        // from the host PreviewPane.
        public DatClass DataContextClass
        {
            get { return (DatClass)GetValue(DataContextClassProperty); }
            set { SetValue(DataContextClassProperty, value); }
        }

        public static readonly DependencyProperty DataContextClassProperty =
            DependencyProperty.Register("DataContextClass", typeof(DatClass), typeof(TrackpointEditorView), new PropertyMetadata(null));
    }
}

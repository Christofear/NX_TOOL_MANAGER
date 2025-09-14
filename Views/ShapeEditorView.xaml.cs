using NX_TOOL_MANAGER.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace NX_TOOL_MANAGER.Views
{
    public class SimpleParameter : INotifyPropertyChanged
    {
        private readonly DatRow _datRow;
        public string Key { get; }

        public string Value
        {
            get => _datRow.Get(Key);
            set { if (_datRow.Get(Key) != value) { _datRow.Set(Key, value); OnPropertyChanged(); } }
        }

        public SimpleParameter(DatRow datRow, string key)
        {
            _datRow = datRow;
            Key = key;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ShapeEditorView : UserControl, INotifyPropertyChanged
    {
        public ShapeEditorView()
        {
            InitializeComponent();
            this.DataContext = this;
            // THE FIX: Instantiate the separate SegmentEditor control, just like in ParameterEditorView
            SegmentEditorControl = new SegmentEditor();
        }

        // Property for the Parameters list (Tab 1)
        public ObservableCollection<SimpleParameter> Parameters { get; } = new ObservableCollection<SimpleParameter>();

        // THE FIX: Property to hold the instance of the separate SegmentEditor control (for Tab 2)
        public SegmentEditor SegmentEditorControl { get; }

        public DatRow DataContextRow
        {
            get => (DatRow)GetValue(DataContextRowProperty);
            set => SetValue(DataContextRowProperty, value);
        }
        public static readonly DependencyProperty DataContextRowProperty =
            DependencyProperty.Register(nameof(DataContextRow), typeof(DatRow), typeof(ShapeEditorView), new PropertyMetadata(null, OnDataContextRowChanged));

        private static void OnDataContextRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editorView = (ShapeEditorView)d;
            var newRow = e.NewValue as DatRow;

            editorView.RebuildParameters(newRow);
            editorView.UpdateSegmentsTab(newRow);
        }

        private void RebuildParameters(DatRow currentRow)
        {
            Parameters.Clear();
            if (currentRow?.ParentClass != null)
            {
                foreach (var key in currentRow.ParentClass.FormatFields.OrderBy(k => k))
                {
                    Parameters.Add(new SimpleParameter(currentRow, key));
                }
            }
        }

        /// <summary>
        /// This method now finds the RTYPE 2 rows and passes them to the
        /// SegmentEditorControl's DataContext, following the blueprint's pattern.
        /// </summary>
        private void UpdateSegmentsTab(DatRow selectedRow)
        {
            var parentDocument = selectedRow?.ParentClass?.ParentDocument;

            if (selectedRow == null || parentDocument == null)
            {
                SegmentEditorControl.DataContext = null;
                return;
            }

            string targetLibRf = selectedRow.Get("LIBRF");

            if (string.IsNullOrEmpty(targetLibRf))
            {
                SegmentEditorControl.DataContext = null;
                return;
            }

            var segmentRows = parentDocument.Classes
                .SelectMany(c => c.Rows)
                .Where(row => row.Get("RTYPE") == "2" && row.Get("LIBRF") == targetLibRf)
                .ToList();

            if (segmentRows.Any())
            {
                var tempClass = new DatClass { Name = "Segment Profile", ParentDocument = parentDocument };
                tempClass.FormatFields.AddRange(segmentRows.First().ParentClass.FormatFields);
                foreach (var row in segmentRows)
                {
                    tempClass.Rows.Add(row);
                }

                // THE FIX: Pass the discovered segments to the separate control's DataContext
                SegmentEditorControl.DataContext = tempClass;
            }
            else
            {
                SegmentEditorControl.DataContext = null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
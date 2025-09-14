using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NX_TOOL_MANAGER.Views
{
    public partial class SegmentEditor : UserControl
    {
        private DatClass _currentClass;

        public SegmentEditor()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        public string MasterLibRef { get; set; }
        public string MasterClassName { get; set; }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_currentClass?.Rows != null)
            {
                _currentClass.Rows.CollectionChanged -= Rows_CollectionChanged;
            }

            _currentClass = e.NewValue as DatClass;
            DataGrid.ItemsSource = _currentClass?.Rows;
            BuildColumns(_currentClass);

            if (_currentClass?.Rows != null)
            {
                _currentClass.Rows.CollectionChanged += Rows_CollectionChanged;
                ResequenceRows();
            }
        }

        private void BuildColumns(DatClass cls)
        {
            DataGrid.Columns.Clear();
            if (cls == null) return;

            var hiddenColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                { "LIBRF", "T", "STYPE", "SEQ", "RTYPE" };

            var keys = cls.FormatFields.Any()
                ? cls.FormatFields
                : cls.Rows.FirstOrDefault()?.Map.Keys ?? Enumerable.Empty<string>();

            foreach (var key in keys)
            {
                if (hiddenColumns.Contains(key)) continue;

                var definition = FieldManager.GetDefinition(key);
                if (definition != null && !definition.Visible) continue;

                var binding = new Binding($"Map[{key}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
                string headerText = definition?.Description ?? key;

                // THE FIX: Create a plain column. All styling and width is now controlled by the XAML.
                var column = new DataGridTextColumn
                {
                    Header = headerText,
                    Binding = binding
                };

                if (definition != null)
                {
                    ToolTipService.SetToolTip(column, definition.Description);
                }

                DataGrid.Columns.Add(column);
            }
        }

        #region Toolbar Button Logic
        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentClass == null) return;
            var newRow = new DatRow { ParentClass = _currentClass };

            if (!string.IsNullOrEmpty(MasterLibRef))
            {
                newRow.Set("LIBRF", MasterLibRef);
                if (MasterClassName == "HOLDER" || MasterClassName == "SHANK")
                {
                    newRow.Set("RTYPE", "2");
                }
                else
                {
                    newRow.Set("T", "1");
                    string stype = MasterClassName.ToUpperInvariant() switch
                    {
                        "MILL_FORM" => "0",
                        "STEP_DRILL" => "1",
                        "TURN_FORM" => "2",
                        _ => "0"
                    };
                    newRow.Set("STYPE", stype);
                }
            }
            _currentClass.Rows.Add(newRow);
            DataGrid.SelectedItem = newRow;
            DataGrid.ScrollIntoView(newRow);
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentClass == null || DataGrid.SelectedItems.Count == 0) return;
            var selectedRows = DataGrid.SelectedItems.Cast<DatRow>().ToList();
            foreach (var row in selectedRows)
            {
                _currentClass.Rows.Remove(row);
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_currentClass == null || DataGrid.SelectedItem == null) return;
            if (DataGrid.SelectedItem is DatRow selectedRow)
            {
                int currentIndex = _currentClass.Rows.IndexOf(selectedRow);
                if (currentIndex > 0)
                {
                    _currentClass.Rows.Move(currentIndex, currentIndex - 1);
                }
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_currentClass == null || DataGrid.SelectedItem == null) return;
            if (DataGrid.SelectedItem is DatRow selectedRow)
            {
                int currentIndex = _currentClass.Rows.IndexOf(selectedRow);
                if (currentIndex < _currentClass.Rows.Count - 1)
                {
                    _currentClass.Rows.Move(currentIndex, currentIndex + 1);
                }
            }
        }
        #endregion

        #region Automatic Sequencing
        private void Rows_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ResequenceRows();
        }

        private void ResequenceRows()
        {
            if (_currentClass == null) return;
            for (int i = 0; i < _currentClass.Rows.Count; i++)
            {
                _currentClass.Rows[i].Set("SEQ", (i + 1).ToString());
            }
        }
        #endregion
    }
}

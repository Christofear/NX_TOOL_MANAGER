using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace NX_TOOL_MANAGER.Views
{
    public partial class LibraryGridView : UserControl
    {
        public LibraryGridView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => Rebind();
            DataGrid.LoadingRow += DataGrid_LoadingRow;
            DataGrid.CurrentCellChanged += DataGrid_CurrentCellChanged;
            DataGrid.PreparingCellForEdit += DataGrid_PreparingCellForEdit;
            DataGrid.CellEditEnding += DataGrid_CellEditEnding;
        }

        private DatClass _current;
        private INotifyCollectionChanged _rowsINCC;

        public string MasterLibRef { get; set; }
        public string MasterClassName { get; set; }

        private void Rebind()
        {
            var cls = ResolveClassFromContext(DataContext);
            if (ReferenceEquals(_current, cls)) return;

            if (_rowsINCC != null) _rowsINCC.CollectionChanged -= Rows_CollectionChanged;
            if (_current?.Rows != null)
            {
                foreach (var row in _current.Rows) row.PropertyChanged -= DatRow_PropertyChanged;
            }

            _current = cls;
            _rowsINCC = _current?.Rows as INotifyCollectionChanged;

            if (_rowsINCC != null) _rowsINCC.CollectionChanged += Rows_CollectionChanged;
            if (_current?.Rows != null)
            {
                foreach (var row in _current.Rows) row.PropertyChanged += DatRow_PropertyChanged;
            }

            BuildColumns(_current);
            DataGrid.ItemsSource = _current?.Rows;
            UpdateHeaderVisibility();
        }

        public static readonly DependencyProperty SelectedRowProperty =
            DependencyProperty.Register(
                nameof(SelectedRow),
                typeof(DatRow),
                typeof(LibraryGridView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedRowChanged));

        public DatRow SelectedRow
        {
            get => (DatRow)GetValue(SelectedRowProperty);
            set => SetValue(SelectedRowProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<DatRow> SelectedRowChanged;

        private static void OnSelectedRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridView = (LibraryGridView)d;
            gridView.SelectedRowChanged?.Invoke(gridView, new RoutedPropertyChangedEventArgs<DatRow>((DatRow)e.OldValue, (DatRow)e.NewValue));
        }

        private void BuildColumns(DatClass cls)
        {
            DataGrid.Columns.Clear();
            if (cls == null) return;

            bool isShapeProfile = (cls.Name == "Shape Profile" || cls.Name == "Segment Profile");
            var hiddenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIBRF", "T", "STYPE", "SEQ", "RTYPE" };

            IEnumerable<string> keys = cls.FormatFields.Any()
                ? cls.FormatFields
                : cls.Rows.FirstOrDefault()?.Map.Keys ?? Enumerable.Empty<string>();

            foreach (var key in keys)
            {
                if (isShapeProfile && hiddenColumns.Contains(key))
                {
                    continue;
                }

                var definition = FieldManager.GetDefinition(key);
                if (definition != null && !definition.Visible) continue;

                var binding = new Binding($"Map[{key}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
                var headerStyle = new Style(typeof(DataGridColumnHeader), DataGrid.ColumnHeaderStyle);

                // --- THE FIX: Changed DisplayName to Description ---
                string headerText = definition?.Description ?? key;

                if (definition != null)
                {
                    headerStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, definition.Description));
                }

                DataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = headerText,
                    Binding = binding,
                    Width = DataGridLength.Auto,
                    MinWidth = 100,
                    CanUserSort = false,
                    IsReadOnly = false,
                    HeaderStyle = headerStyle
                });
            }
        }

        #region Event Handlers
        private void Rows_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) { foreach (DatRow item in e.OldItems) item.PropertyChanged -= DatRow_PropertyChanged; }
            if (e.NewItems != null) { foreach (DatRow item in e.NewItems) item.PropertyChanged += DatRow_PropertyChanged; }
            UpdateHeaderVisibility();
        }

        private void UpdateHeaderVisibility()
        {
            DataGrid.HeadersVisibility = (_current != null && _current.Rows.Any()) ? DataGridHeadersVisibility.All : DataGridHeadersVisibility.None;
        }

        private void DatRow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) => DataGrid.Items.Refresh();
        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (DataGrid.CurrentCell.Item is DatRow row) { SelectedRow = row; }
        }

        private void DataGridRowHeader_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement header && header.DataContext is DatRow row)
            {
                DataGrid.SelectedCells.Clear();
                foreach (var column in DataGrid.Columns) { DataGrid.SelectedCells.Add(new DataGridCellInfo(row, column)); }
            }
        }

        private void DataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox textBox)
            {
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is DatRow row)
            {
                row.ParentClass?.ParentDocument?.ParentRef?.SetDirty();
            }
        }
        #endregion

        #region Excel-like and Context Menu Functionality
        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.C: ExecuteCopy(); e.Handled = true; break;
                    case Key.X: ExecuteCopy(); ExecuteDelete(); e.Handled = true; break;
                    case Key.V: ExecutePaste(); e.Handled = true; break;
                }
            }
            else if (e.Key == Key.Delete) { ExecuteDelete(); e.Handled = true; }
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            var newRow = new DatRow { ParentClass = _current };

            if (!string.IsNullOrEmpty(MasterLibRef))
            {
                newRow.Set("LIBRF", MasterLibRef);

                if (MasterClassName == "HOLDER")
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

            _current.Rows.Add(newRow);
            DataGrid.SelectedItem = newRow;
            DataGrid.ScrollIntoView(newRow);
        }

        private void Copy_Click(object sender, RoutedEventArgs e) => ExecuteCopy();
        private void Paste_Click(object sender, RoutedEventArgs e) => ExecutePaste();

        private void ExecuteCopy()
        {
            var selectedCells = DataGrid.SelectedCells;
            if (selectedCells.Count == 0) return;
            var sb = new StringBuilder();
            var groupedByRow = selectedCells.GroupBy(c => c.Item).OrderBy(g => DataGrid.Items.IndexOf(g.Key));
            foreach (var rowGroup in groupedByRow)
            {
                sb.AppendLine(string.Join("\t", rowGroup.OrderBy(c => c.Column.DisplayIndex).Select(GetCellText)));
            }
            Clipboard.SetText(sb.ToString().TrimEnd());
        }

        private void ExecuteDelete()
        {
            foreach (var cellInfo in DataGrid.SelectedCells) { SetCellValue(cellInfo, string.Empty); }
            DataGrid.Items.Refresh();
        }

        private void ExecutePaste()
        {
            if (DataGrid.SelectedCells.Count == 0) return;
            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText)) return;
            var clipboardRows = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var clipboardData = clipboardRows.Select(row => row.Split('\t')).ToArray();
            if (clipboardData.Length == 0) return;
            var startCell = DataGrid.SelectedCells.OrderBy(c => DataGrid.Items.IndexOf(c.Item)).ThenBy(c => c.Column.DisplayIndex).First();
            int startRowIndex = DataGrid.Items.IndexOf(startCell.Item);
            int startColIndex = startCell.Column.DisplayIndex;
            if (clipboardData.Length == 1 && clipboardData[0].Length == 1)
            {
                foreach (var cell in DataGrid.SelectedCells) { SetCellValue(cell, clipboardData[0][0]); }
            }
            else
            {
                for (int r = 0; r < clipboardData.Length; r++)
                {
                    int targetRowIndex = startRowIndex + r;
                    if (targetRowIndex >= DataGrid.Items.Count) break;
                    for (int c = 0; c < clipboardData[r].Length; c++)
                    {
                        int targetColIndex = startColIndex + c;
                        if (targetColIndex >= DataGrid.Columns.Count) break;
                        var cell = new DataGridCellInfo(DataGrid.Items[targetRowIndex], DataGrid.Columns[targetColIndex]);
                        SetCellValue(cell, clipboardData[r][c]);
                    }
                }
            }
            DataGrid.Items.Refresh();
        }

        private void SetCellValue(DataGridCellInfo cellInfo, string value)
        {
            if (cellInfo.Item is DatRow row && cellInfo.Column is DataGridTextColumn column)
            {
                if (column.Binding is Binding binding && binding.Path.Path.Contains("Map"))
                {
                    string key = binding.Path.Path.Substring(binding.Path.Path.IndexOf('[') + 1).TrimEnd(']');
                    row.Set(key, value);
                }
            }
        }
        private string GetCellText(DataGridCellInfo cellInfo)
        {
            return (cellInfo.Column.GetCellContent(cellInfo.Item) as TextBlock)?.Text ?? string.Empty;
        }
        #endregion

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            return (parentObject is T parent) ? parent : FindVisualParent<T>(parentObject);
        }

        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) { /* Placeholder */ }

        private static DatClass ResolveClassFromContext(object ctx)
        {
            if (ctx is DatClass c) return c;
            if (ctx is DatDocumentRef doc)
            {
                return doc.Document?.Classes.FirstOrDefault();
            }
            return null;
        }
    }
}


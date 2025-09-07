using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
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
            DataGrid.CellEditEnding += DataGrid_CellEditEnding; // THE FIX: Hook into this event
        }

        private DatClass _current;
        private INotifyCollectionChanged _rowsINCC;

        private void Rebind()
        {
            var cls = ResolveClassFromContext(DataContext);

            if (!ReferenceEquals(_current, cls))
            {
                if (_rowsINCC != null)
                    _rowsINCC.CollectionChanged -= Rows_CollectionChanged;
                if (_current?.Rows != null)
                {
                    foreach (var row in _current.Rows)
                        row.PropertyChanged -= DatRow_PropertyChanged;
                }

                _current = cls;
                _rowsINCC = _current?.Rows as INotifyCollectionChanged;

                if (_rowsINCC != null)
                    _rowsINCC.CollectionChanged += Rows_CollectionChanged;
                if (_current?.Rows != null)
                {
                    foreach (var row in _current.Rows)
                        row.PropertyChanged += DatRow_PropertyChanged;
                }

                BuildColumns(_current);
                DataGrid.ItemsSource = _current?.Rows;
            }
            UpdateHeaderVisibility();
        }

        public static readonly DependencyProperty SelectedRowProperty =
            DependencyProperty.Register(
                nameof(SelectedRow),
                typeof(DatRow),
                typeof(LibraryGridView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public DatRow SelectedRow
        {
            get => (DatRow)GetValue(SelectedRowProperty);
            set => SetValue(SelectedRowProperty, value);
        }

        private void Rows_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DatRow item in e.OldItems)
                    item.PropertyChanged -= DatRow_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (DatRow item in e.NewItems)
                    item.PropertyChanged += DatRow_PropertyChanged;
            }
            UpdateHeaderVisibility();
        }

        private void UpdateHeaderVisibility()
        {
            bool hasData = _current != null;
            DataGrid.HeadersVisibility = hasData ? DataGridHeadersVisibility.All : DataGridHeadersVisibility.None;
        }

        private void DatRow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // This is now primarily for the PreviewPane to update the grid.
            DataGrid.Items.Refresh();
        }

        private static DatClass ResolveClassFromContext(object ctx)
        {
            if (ctx is DatClass c) return c;
            var docProp = ctx?.GetType().GetProperty("Document", BindingFlags.Public | BindingFlags.Instance);
            var doc = docProp?.GetValue(ctx);
            var classesProp = doc?.GetType().GetProperty("Classes", BindingFlags.Public | BindingFlags.Instance);
            var classes = classesProp?.GetValue(doc) as IEnumerable;
            return classes?.Cast<DatClass>().FirstOrDefault();
        }

        private void BuildColumns(DatClass cls)
        {
            DataGrid.Columns.Clear();
            if (cls == null) return;

            IEnumerable<string> keys = Enumerable.Empty<string>();
            if (cls.FormatFields != null && cls.FormatFields.Count > 0)
            {
                keys = cls.FormatFields;
            }
            else if (cls.Rows.FirstOrDefault()?.Map != null)
            {
                keys = cls.Rows.FirstOrDefault().Map.Keys;
            }

            foreach (var key in keys)
            {
                var definition = FieldManager.GetDefinition(key);

                if (definition != null && !definition.Visible)
                {
                    continue;
                }

                var binding = new Binding($"Map[{key}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    FallbackValue = "",
                    TargetNullValue = ""
                };

                // THE FIX: Create a new style instance for each column to avoid both the
                // "sealed" error and the white header bug.
                var headerStyle = new Style(typeof(DataGridColumnHeader), DataGrid.ColumnHeaderStyle);
                string headerText = key;

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

        // --- EVENT HANDLERS ---

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (DataGrid.CurrentCell.Item is DatRow row)
            {
                SelectedRow = row;
            }
        }

        private void DataGridRowHeader_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement header && header.DataContext is DatRow row)
            {
                DataGrid.SelectedCells.Clear();
                foreach (var column in DataGrid.Columns)
                {
                    DataGrid.SelectedCells.Add(new DataGridCellInfo(row, column));
                }
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

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridCell cell && !cell.IsEditing)
            {
                var dataGrid = FindVisualParent<DataGrid>(cell);
                if (dataGrid != null)
                {
                    dataGrid.Focus();
                    if (!cell.IsFocused)
                    {
                        cell.Focus();
                    }
                    dataGrid.BeginEdit();
                }
            }
        }

        // THE FIX: This event handler ensures that edits made directly in the grid
        // correctly mark the file as modified.
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Row.Item is DatRow row)
                {
                    // Manually trigger the dirty flag logic.
                    row.ParentClass?.ParentDocument?.ParentRef?.SetDirty();
                }
            }
        }

        #region Excel-like Functionality

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
            else if (e.Key == Key.Delete)
            {
                ExecuteDelete();
                e.Handled = true;
            }
        }

        private void ExecuteCopy()
        {
            var selectedCells = DataGrid.SelectedCells;
            if (selectedCells.Count == 0) return;
            var sb = new StringBuilder();
            var groupedByRow = selectedCells.GroupBy(c => c.Item).OrderBy(g => DataGrid.Items.IndexOf(g.Key));
            foreach (var rowGroup in groupedByRow)
            {
                var orderedCells = rowGroup.OrderBy(c => c.Column.DisplayIndex);
                sb.AppendLine(string.Join("\t", orderedCells.Select(GetCellText)));
            }
            Clipboard.SetText(sb.ToString().TrimEnd());
        }

        private void ExecuteDelete()
        {
            foreach (var cellInfo in DataGrid.SelectedCells)
            {
                SetCellValue(cellInfo, string.Empty);
            }
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
                string valueToPaste = clipboardData[0][0];
                foreach (var cell in DataGrid.SelectedCells)
                {
                    SetCellValue(cell, valueToPaste);
                }
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
                    string path = binding.Path.Path;
                    string key = path.Substring(path.IndexOf('[') + 1).TrimEnd(']');
                    // Use the custom Set method to ensure the dirty flag is triggered.
                    row.Set(key, value);
                }
            }
        }

        private string GetCellText(DataGridCellInfo cellInfo)
        {
            if (cellInfo.Column.GetCellContent(cellInfo.Item) is TextBlock tb)
            {
                return tb.Text;
            }
            return string.Empty;
        }

        #endregion

        // --- Helper Methods ---
        public static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            return parent ?? FindVisualParent<T>(parentObject);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCopy();
        }
        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            ExecutePaste();
        }
        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Placeholder for your existing logic
        }
    }
}


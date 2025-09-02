using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
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
        }

        private DatClass _current;
        private INotifyCollectionChanged _rowsINCC;
        private DataGridColumn _rcColumn;
        private object _rcItem;

        private void Rebind()
        {
            var cls = ResolveClassFromContext(DataContext);

            if (!ReferenceEquals(_current, cls))
            {
                // Unsubscribe from old events to prevent memory leaks
                if (_rowsINCC != null)
                    _rowsINCC.CollectionChanged -= Rows_CollectionChanged;
                if (_current?.Rows != null)
                {
                    foreach (var row in _current.Rows)
                    {
                        row.PropertyChanged -= DatRow_PropertyChanged;
                    }
                }

                _current = cls;
                _rowsINCC = _current?.Rows as INotifyCollectionChanged;

                // Subscribe to new events
                if (_rowsINCC != null)
                    _rowsINCC.CollectionChanged += Rows_CollectionChanged;
                if (_current?.Rows != null)
                {
                    foreach (var row in _current.Rows)
                    {
                        row.PropertyChanged += DatRow_PropertyChanged;
                    }
                }

                BuildColumns(_current);
                DataGrid.ItemsSource = _current?.Rows;
            }

            UpdateHeaderVisibility();
            RefreshRowNumbers();
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
            // Subscribe/unsubscribe when rows are added or removed from the collection
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
            RefreshRowNumbers();
        }

        private void UpdateHeaderVisibility()
        {
            bool hasData = _current != null && _current.Rows?.Count > 0;

            DataGrid.HeadersVisibility = hasData
                ? DataGridHeadersVisibility.All
                : DataGridHeadersVisibility.Column;

            DataGrid.RowHeaderWidth = hasData ? 40 : 0;
        }


        private void DatRow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // This is the direct command that forces the DataGrid to update its display.
            DataGrid.Items.Refresh();
        }

        // Tree sends either a DatClass (child) or DatDocumentRef (file/root);
        // for a file, show its first class.
        private static DatClass ResolveClassFromContext(object ctx)
        {
            if (ctx is DatClass c) return c;

            // Try to avoid a hard dependency: DatDocumentRef has Document.Classes
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

            // (Your existing exclude logic is preserved)
            var exclude = new HashSet<string>(
                new[] { "t", "st", "ugt", "ugst", "tlnum", "adjreg", "hld", "cutcomreg", "matref", "zoff",
        "sharef", "thrds", "cx1", "cy1", "cx2", "cy2", "desi", "rampangle", "helicaldia",
        "minramplen", "maxcutwidth", "hldref", "tpref", "ha", "drot" },
                StringComparer.OrdinalIgnoreCase
            );

            // (Your existing logic for getting the keys is preserved)
            IEnumerable<string> keys = Enumerable.Empty<string>();
            if (cls.FormatFields != null && cls.FormatFields.Count > 0)
            {
                keys = cls.FormatFields;
            }
            else
            {
                var first = cls.Rows.FirstOrDefault();
                if (first?.Map != null)
                    keys = first.Map.Keys;
            }

            foreach (var key in keys.Where(k => !exclude.Contains(k)))
            {
                var binding = new Binding($"Map[{key}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    FallbackValue = "",
                    TargetNullValue = ""
                };

                // --- THIS IS THE NEW LOGIC ---
                // 1. Get the field definition from our manager service.
                var definition = FieldManager.GetDefinition(key);

                // 2. Create a new style for this specific header that inherits the existing dark theme style.
                var headerStyle = new Style(typeof(DataGridColumnHeader), DataGrid.ColumnHeaderStyle);

                // 3. Add the tooltip to the new style.
                headerStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, definition.Description));
                // --- END OF NEW LOGIC ---

                DataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = key,
                    Binding = binding,
                    MinWidth = 80,
                    CanUserSort = false,
                    IsReadOnly = true,
                    // 4. Apply the new style (with the tooltip) to this specific column.
                    HeaderStyle = headerStyle
                });
            }
        }

        // Row numbering that works with virtualization
        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void RefreshRowNumbers()
        {
            for (int i = 0; i < DataGrid.Items.Count; i++)
            {
                if (DataGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                    row.Header = (row.GetIndex() + 1).ToString();
            }
        }

        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _rcColumn = null;
            _rcItem = null;

            var dep = (DependencyObject)e.OriginalSource;
            var cell = FindAncestor<DataGridCell>(dep);
            if (cell != null)
            {
                var row = FindAncestor<DataGridRow>(cell);
                if (row != null)
                {
                    _rcColumn = cell.Column;
                    _rcItem = row.Item;

                    // keep UI state sane (don’t touch SelectedCells if FullRow)
                    DataGrid.ScrollIntoView(row.Item, cell.Column);
                    DataGrid.UpdateLayout();
                    DataGrid.CurrentCell = new DataGridCellInfo(row.Item, cell.Column);
                    DataGrid.Focus();

                    if (DataGrid.SelectionUnit != DataGridSelectionUnit.FullRow)
                    {
                        DataGrid.SelectedCells.Clear();
                        DataGrid.SelectedCells.Add(DataGrid.CurrentCell);
                    }
                    else
                    {
                        if (!row.IsSelected)
                        {
                            DataGrid.SelectedItems.Clear();
                            row.IsSelected = true;
                        }
                    }
                    return;
                }
            }

            // Fallback: right-clicked a row but not a specific cell
            var r = FindAncestor<DataGridRow>(dep);
            if (r != null)
            {
                _rcItem = r.Item;
                DataGrid.ScrollIntoView(r.Item);
                DataGrid.UpdateLayout();
                DataGrid.CurrentCell = new DataGridCellInfo(r.Item, DataGrid.Columns.FirstOrDefault());
                DataGrid.Focus();
                if (!r.IsSelected)
                {
                    DataGrid.SelectedItems.Clear();
                    r.IsSelected = true;
                }
            }
        }


        // ===== Copy cell (no headers) =====
        private void CopyCell_Click(object sender, RoutedEventArgs e)
        {
            var col = _rcColumn ?? DataGrid.CurrentCell.Column;
            var item = _rcItem ?? DataGrid.CurrentCell.Item;
            if (col == null || item == null) return;

            // First: read via bindings (works even when virtualized)
            var text = ReadCellText(item, col);

            // If blank, try realized visual
            if (string.IsNullOrEmpty(text))
            {
                DataGrid.ScrollIntoView(item, col);
                DataGrid.UpdateLayout();
                var content = col.GetCellContent(item);
                var tb = FindDescendant<TextBlock>(content);
                if (tb != null) text = tb.Text;
                else if (content is ContentPresenter cp && cp.Content != null) text = cp.Content.ToString();
            }

            if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
            else Clipboard.Clear();
        }



        // ===== Copy row (no headers) =====
        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            // Use current row if present, otherwise the first selected item
            var item = DataGrid.CurrentCell.Item ?? DataGrid.SelectedItem;
            if (item == null) return;

            // Ensure the row is realized so built-in copy has visuals to consult if needed
            DataGrid.ScrollIntoView(item);
            DataGrid.UpdateLayout();

            var prev = DataGrid.ClipboardCopyMode;
            try
            {
                DataGrid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader; // no headers
                ApplicationCommands.Copy.Execute(null, DataGrid);
            }
            finally
            {
                DataGrid.ClipboardCopyMode = prev;
            }
        }


        // --- Helpers to read cell text reliably for most column types --- //
        private string ReadCellText(object item, DataGridColumn column)
        {
            switch (column)
            {
                case DataGridTextColumn txt:
                    return ReadBound(txt.Binding as Binding, item);

                case DataGridCheckBoxColumn chk:
                    return ReadBound(chk.Binding as Binding, item);

                case DataGridComboBoxColumn cbo:
                    // Prefer SelectedValueBinding, then SelectedItemBinding, else SortMemberPath
                    var b = cbo.SelectedValueBinding as Binding
                            ?? cbo.SelectedItemBinding as Binding;
                    var val = ReadBound(b, item);
                    if (!string.IsNullOrEmpty(val)) return val;
                    if (!string.IsNullOrEmpty(column.SortMemberPath))
                        return GetPropertyValue(item, column.SortMemberPath)?.ToString() ?? string.Empty;
                    return string.Empty;

                default:
                    // Template or custom column: try SortMemberPath first
                    if (!string.IsNullOrEmpty(column.SortMemberPath))
                        return GetPropertyValue(item, column.SortMemberPath)?.ToString() ?? string.Empty;
                    return string.Empty;
            }
        }

        private string ReadBound(Binding binding, object item)
        {
            if (binding?.Path?.Path is not string path || string.IsNullOrEmpty(path))
                return string.Empty;

            var raw = GetPropertyValue(item, path);
            if (raw == null) return string.Empty;

            if (!string.IsNullOrEmpty(binding.StringFormat))
                return string.Format(binding.StringFormat, raw);

            if (binding.Converter != null)
                return binding.Converter.Convert(raw, typeof(string), binding.ConverterParameter, binding.ConverterCulture)?.ToString() ?? string.Empty;

            return raw.ToString();
        }

        private static object GetPropertyValue(object obj, string path)
        {
            if (obj == null || string.IsNullOrWhiteSpace(path)) return null;
            object cur = obj;
            foreach (var part in path.Split('.'))
            {
                if (cur == null) return null;
                var t = cur.GetType();
                var prop = t.GetProperty(part, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop == null) return null;
                cur = prop.GetValue(cur);
            }
            return cur;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && current is not T)
                current = VisualTreeHelper.GetParent(current);
            return current as T;
        }

        private static TDesc FindDescendant<TDesc>(DependencyObject root) where TDesc : DependencyObject
        {
            if (root == null) return null;
            for (int i = 0, n = VisualTreeHelper.GetChildrenCount(root); i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TDesc t) return t;
                var deeper = FindDescendant<TDesc>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }


    }
}

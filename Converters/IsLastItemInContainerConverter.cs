using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace NX_TOOL_MANAGER.Converters
{
    /// <summary>
    /// Returns true if the given TreeViewItem is the last item in its parent's collection.
    /// Usage in XAML (inside a TreeViewItem ControlTemplate):
    ///   <DataTrigger Value="True">
    ///     <DataTrigger.Binding>
    ///       <Binding RelativeSource="{RelativeSource TemplatedParent}"
    ///                Converter="{StaticResource IsLastItemInContainerConverter}"/>
    ///     </DataTrigger.Binding>
    ///     <!-- setters -->
    ///   </DataTrigger>
    /// </summary>
    public sealed class IsLastItemInContainerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Expect a TreeViewItem passed in via TemplatedParent
            if (value is not TreeViewItem tvi)
                return false;

            // Locate the parent ItemsControl (TreeView or a TreeViewItem's ItemsPresenter)
            var parent = ItemsControl.ItemsControlFromItemContainer(tvi);
            if (parent is null)
                return false;

            // Try the most accurate route first: container generator index
            var gen = parent.ItemContainerGenerator;
            int idx = gen.IndexFromContainer(tvi);
            if (idx >= 0)
                return idx == parent.Items.Count - 1;

            // If virtualization or timing means the container index isn't available yet,
            // fall back to comparing the data item reference.
            var dataItem = tvi.DataContext;
            if (dataItem is null || parent.Items.Count == 0)
                return false;

            int dataIndex = parent.Items.IndexOf(dataItem);
            return dataIndex >= 0 && dataIndex == parent.Items.Count - 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

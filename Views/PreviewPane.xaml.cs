using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization; // ADDED: Required for NumberStyles and CultureInfo
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;

namespace NX_TOOL_MANAGER.Views
{
    public class ParameterViewModel : INotifyPropertyChanged
    {
        private readonly DatRow _datRow;
        public string Key { get; }
        public FieldDefinition Definition { get; }

        public string Value
        {
            get => _datRow.Get(Key);
            set { _datRow.Set(Key, value); OnPropertyChanged(); }
        }

        public ParameterViewModel(DatRow datRow, string key)
        {
            _datRow = datRow;
            Key = key;
            Definition = FieldManager.GetDefinition(key);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class PreviewPane : UserControl
    {
        public PreviewPane()
        {
            InitializeComponent();
            (this.Content as FrameworkElement).DataContext = this;
        }

        public ObservableCollection<ParameterViewModel> Parameters { get; } = new ObservableCollection<ParameterViewModel>();

        public DatRow SelectedRow
        {
            get => (DatRow)GetValue(SelectedRowProperty);
            set => SetValue(SelectedRowProperty, value);
        }
        public static readonly DependencyProperty SelectedRowProperty =
            DependencyProperty.Register(nameof(SelectedRow), typeof(DatRow), typeof(PreviewPane), new PropertyMetadata(null, OnSelectedRowChanged));

        private static void OnSelectedRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var previewPane = (PreviewPane)d;
            previewPane.Parameters.Clear();

            if (e.NewValue is DatRow newRow)
            {
                var hiddenKeys = new HashSet<string> { "T", "ST", "UGT", "UGST" };

                // UPDATED: Removed the .OrderBy() to preserve the original order
                var visibleParams = newRow.Map.Where(kvp => !hiddenKeys.Contains(kvp.Key));

                foreach (var kvp in visibleParams)
                {
                    previewPane.Parameters.Add(new ParameterViewModel(newRow, kvp.Key));
                }
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var param = textBox?.DataContext as ParameterViewModel;
            if (param == null) return;

            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            switch (param.Definition.Type.ToLower())
            {
                case "integer":
                    if (param.Key == "DROT" && (newText != "3" && newText != "4"))
                    {
                        e.Handled = true;
                        return;
                    }
                    if (!int.TryParse(newText, out _)) e.Handled = true;
                    break;

                case "double":
                    // THIS IS THE FIX: Using NumberStyles.Float and CultureInfo.InvariantCulture
                    // allows for decimals that start with a "."
                    if (!double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        e.Handled = true;
                    }
                    break;

                case "boolean":
                    if (newText != "0" && newText != "1") e.Handled = true;
                    break;
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var currentTextBox = sender as TextBox;
                if (currentTextBox == null) return;
                var itemsControl = FindVisualParent<ItemsControl>(currentTextBox);
                if (itemsControl == null) return;
                var currentContainer = FindVisualParent<ContentPresenter>(currentTextBox);
                if (currentContainer == null) return;
                int currentIndex = itemsControl.ItemContainerGenerator.IndexFromContainer(currentContainer);
                int nextIndex = currentIndex + 1;
                if (nextIndex < itemsControl.Items.Count)
                {
                    var nextContainer = itemsControl.ItemContainerGenerator.ContainerFromIndex(nextIndex) as ContentPresenter;
                    if (nextContainer != null)
                    {
                        nextContainer.ApplyTemplate();
                        var nextTextBox = FindVisualChild<TextBox>(nextContainer);
                        if (nextTextBox != null)
                        {
                            nextTextBox.Focus();
                            nextTextBox.CaretIndex = nextTextBox.Text.Length;
                        }
                    }
                }
                e.Handled = true;
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            return (parentObject is T parent) ? parent : FindVisualParent<T>(parentObject);
        }
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}


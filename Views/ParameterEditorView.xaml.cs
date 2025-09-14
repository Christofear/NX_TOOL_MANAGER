using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        public void RefreshValue()
        {
            OnPropertyChanged(nameof(Value));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ParameterCategoryViewModel : INotifyPropertyChanged
    {
        public string CategoryName { get; set; }
        public int DisplayOrder { get; set; }
        public ObservableCollection<ParameterViewModel> Parameters { get; } = new ObservableCollection<ParameterViewModel>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ParameterEditorView : UserControl, INotifyPropertyChanged
    {
        public ParameterEditorView()
        {
            InitializeComponent();
            this.DataContext = this;
            // Create an instance of our SegmentEditor to be hosted in the new tab.
            SegmentEditorControl = new SegmentEditor();
        }

        // --- NEW PROPERTIES to manage the dynamic tab ---
        public SegmentEditor SegmentEditorControl { get; }

        private Visibility _segmentProfileTabVisibility = Visibility.Collapsed;
        public Visibility SegmentProfileTabVisibility
        {
            get => _segmentProfileTabVisibility;
            set { _segmentProfileTabVisibility = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ParameterCategoryViewModel> ParameterCategories { get; } = new ObservableCollection<ParameterCategoryViewModel>();

        public DatRow DataContextRow
        {
            get => (DatRow)GetValue(DataContextRowProperty);
            set => SetValue(DataContextRowProperty, value);
        }
        public static readonly DependencyProperty DataContextRowProperty =
            DependencyProperty.Register(nameof(DataContextRow), typeof(DatRow), typeof(ParameterEditorView), new PropertyMetadata(null, OnDataContextRowChanged));

        private static void OnDataContextRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editorView = (ParameterEditorView)d;
            if (e.OldValue is DatRow oldRow)
            {
                oldRow.PropertyChanged -= editorView.SelectedRow_PropertyChanged;
            }
            if (e.NewValue is DatRow newRow)
            {
                newRow.PropertyChanged += editorView.SelectedRow_PropertyChanged;
            }
            editorView.RebuildParameters();
            // This new method now controls the logic for the dynamic tab.
            editorView.UpdateSegmentProfileTab();
        }

        // --- NEW METHOD to handle the visibility and data for the new tab ---
        private void UpdateSegmentProfileTab()
        {
            var selectedRow = DataContextRow;
            if (selectedRow == null)
            {
                SegmentProfileTabVisibility = Visibility.Collapsed;
                return;
            }

            var parentClass = selectedRow.ParentClass;
            bool isSpecialTool = CategoryService.IsSpecialToolType(parentClass?.Name);

            if (isSpecialTool)
            {
                SegmentProfileTabVisibility = Visibility.Visible;

                string libRef = selectedRow.Get("LIBRF");
                var segmentedToolsDoc = LibraryManager.Instance.Libraries.FirstOrDefault(doc => doc.Kind == FileKind.SegmentedTools);

                if (segmentedToolsDoc?.Document == null || string.IsNullOrEmpty(libRef))
                {
                    SegmentEditorControl.DataContext = null;
                    return;
                }

                var shapeRows = segmentedToolsDoc.Document.Classes
                    .SelectMany(c => c.Rows)
                    .Where(r => r.Get("LIBRF") == libRef)
                    .ToList();

                if (shapeRows.Any())
                {
                    var tempClass = new DatClass { Name = "Segment Profile", ParentDocument = segmentedToolsDoc.Document };
                    tempClass.FormatFields.AddRange(shapeRows.First().ParentClass.FormatFields);
                    foreach (var row in shapeRows) { tempClass.Rows.Add(row); }

                    // Pass the data and context to the SegmentEditor control instance
                    SegmentEditorControl.DataContext = tempClass;
                    SegmentEditorControl.MasterLibRef = libRef;
                    SegmentEditorControl.MasterClassName = parentClass.Name;
                }
                else
                {
                    SegmentEditorControl.DataContext = null;
                }
            }
            else
            {
                SegmentProfileTabVisibility = Visibility.Collapsed;
            }
        }

        private void SelectedRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null && e.PropertyName.StartsWith("Map[") && e.PropertyName.EndsWith("]"))
            {
                string key = e.PropertyName.Substring(4, e.PropertyName.Length - 5);
                foreach (var category in ParameterCategories)
                {
                    var paramVM = category.Parameters.FirstOrDefault(p => p.Key == key);
                    if (paramVM != null)
                    {
                        paramVM.RefreshValue();
                        return;
                    }
                }
            }
        }

        private void RebuildParameters()
        {
            ParameterCategories.Clear();
            var currentRow = DataContextRow;

            if (currentRow?.ParentClass == null) return;

            var classFields = currentRow.ParentClass.FormatFields;
            var allViewModels = classFields
                .Select(key => new ParameterViewModel(currentRow, key))
                .Where(p => p.Definition != null && p.Definition.Visible)
                .ToList();

            var groupedViewModels = allViewModels
                .GroupBy(p => p.Definition.Category)
                .Select(g => new
                {
                    CategoryName = string.IsNullOrWhiteSpace(g.Key) ? "General" : g.Key,
                    Parameters = g.ToList()
                });

            var categoryVMs = new List<ParameterCategoryViewModel>();
            foreach (var group in groupedViewModels)
            {
                var categorySettings = FieldManager.GetCategorySettings(group.CategoryName);
                if (!categorySettings.Visible) continue;

                var categoryVM = new ParameterCategoryViewModel
                {
                    CategoryName = group.CategoryName,
                    DisplayOrder = categorySettings.DisplayOrder,
                    IsExpanded = categorySettings.DefaultExpanded
                };

                foreach (var param in group.Parameters.OrderBy(p => p.Definition.Description))
                {
                    categoryVM.Parameters.Add(param);
                }

                categoryVMs.Add(categoryVM);
            }

            foreach (var vm in categoryVMs.OrderBy(c => c.DisplayOrder))
            {
                ParameterCategories.Add(vm);
            }
        }

        #region Input Validation and Event Handlers
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var param = textBox?.DataContext as ParameterViewModel;
            if (param == null) return;
            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            switch (param.Definition.Type.ToLower())
            {
                case "integer":
                    if (param.Key == "DROT" && (newText != "3" && newText != "4")) { e.Handled = true; return; }
                    if (!int.TryParse(newText, out _)) e.Handled = true;
                    break;
                case "double":
                    if (!double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) e.Handled = true;
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
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}


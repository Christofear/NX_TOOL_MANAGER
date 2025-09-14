using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NX_TOOL_MANAGER.Views
{
    public partial class ToolVisual : UserControl
    {
        private Matrix _transformMatrix;
        private Point _lastMousePosition;

        public ToolVisual()
        {
            InitializeComponent();

            // THE MAJOR CHANGE: The control now reacts to its DataContext being changed.
            this.DataContextChanged += OnDataContextChanged;

            this.MouseWheel += OnMouseWheel;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
            this.MouseMove += OnMouseMove;

            ResetTransform();
            Loaded += (_, __) => Redraw();
            SizeChanged += (_, __) => Redraw();
        }

        private void Redraw()
        {
            if (BackgroundCanvas == null || DrawingCanvas == null || Placeholder == null) return;

            BackgroundCanvas.Children.Clear();
            DrawingCanvas.Children.Clear();

            DrawGrid();

            // It now gets its data directly from the DataContext property.
            var toolData = DataContext as DatRow;

            if (toolData == null)
            {
                ShowPlaceholder("No selection");
                ToolTypeLabel.Text = string.Empty;
                return;
            }

            HidePlaceholder();
            DrawCsys();
            ApplyTransform();

            string ugt = toolData.Get("UGT") ?? "0";
            string ugst = toolData.Get("UGST") ?? "0";
            var definition = ToolTypeRegistry.Find(ugt, ugst);

            if (definition != null)
            {
                ToolTypeLabel.Text = $"{definition.UgTypeName} - {definition.UgSubtypeName}";
            }
            else
            {
                ToolTypeLabel.Text = "Unknown Tool Type";
            }

            bool wasDrawn = false;
            if (definition?.Drawer != null)
            {
                wasDrawn = definition.Drawer.Draw(DrawingCanvas, toolData);
            }

            if (!wasDrawn) wasDrawn = ToolDrawerHelpers.DrawCylindricalTool(DrawingCanvas, toolData, "DIAMETER", "LENGTH");

            if (!wasDrawn)
            {
                ShowPlaceholder("Preview not available");
                ToolTypeLabel.Text = string.Empty;
            }
        }

        // --- PAN AND ZOOM LOGIC (Unchanged) ---
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(this);
            double zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            var scaleMatrix = _transformMatrix;
            scaleMatrix.ScaleAt(zoomFactor, zoomFactor, mousePos.X, mousePos.Y);
            _transformMatrix = scaleMatrix;
            ApplyTransform();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ResetTransform();
                Redraw();
                return;
            }
            _lastMousePosition = e.GetPosition(this);
            this.CaptureMouse();
            Cursor = Cursors.Hand;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (this.IsMouseCaptured)
            {
                Point currentMousePos = e.GetPosition(this);
                Vector delta = currentMousePos - _lastMousePosition;
                var translateMatrix = _transformMatrix;
                translateMatrix.Translate(delta.X, delta.Y);
                _transformMatrix = translateMatrix;
                ApplyTransform();
                _lastMousePosition = currentMousePos;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        private void ApplyTransform()
        {
            if (DrawingCanvas != null)
            {
                DrawingCanvas.RenderTransform = new MatrixTransform(_transformMatrix);
            }
        }

        private void ResetTransform()
        {
            _transformMatrix = Matrix.Identity;
        }

        // --- HELPER FUNCTIONS (Unchanged) ---
        private void DrawGrid()
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(0xCF, 0xD8, 0xDC));
            for (double x = 0; x < this.ActualWidth; x += 10) { BackgroundCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = this.ActualHeight, Stroke = gridBrush, StrokeThickness = 0.5 }); }
            for (double y = 0; y < this.ActualHeight; y += 10) { BackgroundCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = this.ActualWidth, Y2 = y, Stroke = gridBrush, StrokeThickness = 0.5 }); }
        }

        private void DrawCsys()
        {
            double csysSize = 25;
            double originX = 25;
            double originY = this.ActualHeight - 25;
            var labelBrush = new SolidColorBrush(Color.FromRgb(0x49, 0x50, 0x57));

            var zAxis = new Line { X1 = originX, Y1 = originY, X2 = originX, Y2 = originY - csysSize, Stroke = Brushes.Blue, StrokeThickness = 2 };
            var zArrow = new Polygon { Points = { new(originX, originY - csysSize - 5), new(originX - 3, originY - csysSize), new(originX + 3, originY - csysSize) }, Fill = Brushes.Blue };
            var zLabel = new TextBlock { Text = "Z", Foreground = labelBrush, FontWeight = FontWeights.Bold, FontSize = 12 };
            Canvas.SetLeft(zLabel, originX - 12);
            Canvas.SetTop(zLabel, originY - csysSize - 18);

            var xAxis = new Line { X1 = originX, Y1 = originY, X2 = originX + csysSize, Y2 = originY, Stroke = Brushes.Red, StrokeThickness = 2 };
            var xArrow = new Polygon { Points = { new(originX + csysSize + 5, originY), new(originX + csysSize, originY - 3), new(originX + csysSize, originY + 3) }, Fill = Brushes.Red };
            var xLabel = new TextBlock { Text = "X", Foreground = labelBrush, FontWeight = FontWeights.Bold, FontSize = 12 };
            Canvas.SetLeft(xLabel, originX + csysSize + 5);
            Canvas.SetTop(xLabel, originY - 8);

            BackgroundCanvas.Children.Add(zAxis);
            BackgroundCanvas.Children.Add(zArrow);
            BackgroundCanvas.Children.Add(zLabel);
            BackgroundCanvas.Children.Add(xAxis);
            BackgroundCanvas.Children.Add(xArrow);
            BackgroundCanvas.Children.Add(xLabel);
        }

        private void ShowPlaceholder(string text)
        {
            Placeholder.Text = text;
            Placeholder.Visibility = Visibility.Visible;
        }

        private void HidePlaceholder()
        {
            Placeholder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Handles switching the PropertyChanged listener when the DataContext (the selected DatRow) changes.
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Detach from the old row's property change notifications
            if (e.OldValue is DatRow oldRow)
                oldRow.PropertyChanged -= OnCurrentRowPropertyChanged;

            // Attach to the new row's property change notifications
            if (e.NewValue is DatRow newRow)
                newRow.PropertyChanged += OnCurrentRowPropertyChanged;

            // Redraw with the new tool data
            Redraw();
        }

        /// <summary>
        /// This method is called whenever a property on the *currently selected* DatRow changes.
        /// </summary>
        private void OnCurrentRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Redraw to reflect the change in the tool's parameters.
            Redraw();
        }
    }
}

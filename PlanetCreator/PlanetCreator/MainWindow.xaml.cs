using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlanetCreator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Height = 1080;
            ImageView.MouseWheel += ImageView_MouseWheel;
            BorderView.MouseLeftButtonDown += ImageView_MouseLeftButtonDown; ;
            BorderView.MouseLeftButtonUp += ImageView_MouseLeftButtonUp; ;
            BorderView.MouseRightButtonDown += BorderView_MouseRightButtonDown;
            BorderView.MouseMove += ImageView_MouseMove;

            Loaded += MainWindow_Loaded;
        }

        private void BorderView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!ViewModel.AddingLake) return;
            if (_isMouseOnFace)
            {
                ViewModel.HandleLakeAddClick(_lastMouseOverCubeMapFace, _lastMouseOverCubeMapX, _lastMouseOverCubeMapY);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            CurvePreviewCanvas.Visibility = Visibility.Collapsed;
            _previewOffTime = DateTime.UtcNow;
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        DateTime _previewOffTime;
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (CurvePreviewCanvas.Visibility != Visibility.Collapsed && DateTime.UtcNow > _previewOffTime)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurvePreviewCanvas.Visibility = Visibility.Collapsed;
                });
            }
        }

        System.Timers.Timer _timer = new(1000);

        void ShowPreviewCanvas(List<Point> points, Brush? brush = null)
        {
            var b = brush ?? Brushes.Red;
            var polyLine = new Polyline
            {
                Stroke = b,
                StrokeThickness = 2,
                Points = new(points)
            };
            if (brush == null)
            {
                CurvePreviewCanvas.Children.Clear();
            }
            CurvePreviewCanvas.Children.Add(polyLine);
            CurvePreviewCanvas.Visibility = Visibility.Visible;
            _previewOffTime = DateTime.UtcNow.AddSeconds(5);
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Curve Preview
            if (e.PropertyName == nameof(ViewModel.FlattenLowsAmount) ||
                e.PropertyName == nameof(ViewModel.FlattenPeaksAmount))
            {
                var gen = new PlanetGenerator();
                List<Point> points = new();
                for (double x=0;x<=1.0;x+=.01)
                {
                    points.Add(new Point(x*100, 100-gen.FlattenHistogramSingle(x, ViewModel.FlattenLowsAmount, ViewModel.FlattenPeaksAmount)*100));
                }
                ShowPreviewCanvas(points);
            }
            if (e.PropertyName == nameof(ViewModel.ExponentialFlattenStrength) ||
                e.PropertyName == nameof(ViewModel.EquatorFlatWidth) ||
                e.PropertyName == nameof(ViewModel.FlattenEquator))
            {
                var gen = new PlanetGenerator();
                gen.TileWidth = ViewModel.TileWidth;
                CurvePreviewCanvas.Children.Clear();
                foreach (var ii in new List<(int,Brush)> { (ViewModel.TileWidth/2,Brushes.Red),(ViewModel.TileWidth/4, Brushes.Orange), (0,Brushes.Green)})
                {
                    List<Point> points = new();
                    for (double x = 0; x <= 1.0; x += .01)
                    {
                        points.Add(new Point(x * 100, 100 - gen.ExponentialStretchSingle(
                            x, ViewModel.ExponentialFlattenStrength,
                            ViewModel.FlattenEquator ? ViewModel.EquatorFlatWidth : -1,
                            false, ii.Item1) * 100));
                    }
                    ShowPreviewCanvas(points, ii.Item2);
                    if (!ViewModel.FlattenEquator) break;
                }
            }
            if (e.PropertyName == nameof(ViewModel.EquatorMaxHeight) ||
                e.PropertyName == nameof(ViewModel.EquatorHeightRoundness))
            {
                var gen = new PlanetGenerator();
                gen.TileWidth = ViewModel.TileWidth;
                List<Point> points = new();
                for (double x = 0; x <= ViewModel.TileWidth; x += ViewModel.TileWidth*0.01)
                {
                    points.Add(new Point(
                        x * 100.0 / ViewModel.TileWidth,
                        100 - gen.LowerEquatorSingle(1.0, ViewModel.EquatorMaxHeight/100.0, ViewModel.EquatorHeightRoundness/10.0, x) * 100));
                }
                CurvePreviewCanvas.Children.Clear();
                ShowPreviewCanvas(points, Brushes.Yellow);
            }

            if (e.PropertyName == nameof(ViewModel.PreviewMode) ||
                e.PropertyName == nameof(ViewModel.LimitedPreview) ||
                e.PropertyName == nameof(ViewModel.PreviewTile))
            {
                LargePreviewBorder.Visibility = ViewModel.PreviewMode ? Visibility.Visible : Visibility.Collapsed;
                PreviewBorder.Visibility = (ViewModel.PreviewMode && ViewModel.LimitedPreview) ? Visibility.Visible : Visibility.Collapsed;
                switch (ViewModel.PreviewTile)
                {
                    case SpaceEngineersToolsShared.CubeMapFace.Up:
                        Canvas.SetLeft(LargePreviewBorder, Canvas.GetLeft(TUp));
                        Canvas.SetTop(LargePreviewBorder, Canvas.GetTop(TUp));
                        Canvas.SetLeft(PreviewBorder, Canvas.GetLeft(TUp) + 512);
                        Canvas.SetTop(PreviewBorder, Canvas.GetTop(TUp) + 512);
                        break;
                    case SpaceEngineersToolsShared.CubeMapFace.Down:
                        Canvas.SetLeft(LargePreviewBorder, Canvas.GetLeft(TDown));
                        Canvas.SetTop(LargePreviewBorder, Canvas.GetTop(TDown));
                        Canvas.SetLeft(PreviewBorder, Canvas.GetLeft(TDown) + 512);
                        Canvas.SetTop(PreviewBorder, Canvas.GetTop(TDown) + 512);
                        break;
                    case SpaceEngineersToolsShared.CubeMapFace.Front:
                        Canvas.SetLeft(LargePreviewBorder, Canvas.GetLeft(TFront));
                        Canvas.SetTop(LargePreviewBorder, Canvas.GetTop(TFront));
                        Canvas.SetLeft(PreviewBorder, Canvas.GetLeft(TFront) + 512);
                        Canvas.SetTop(PreviewBorder, Canvas.GetTop(TFront) + 512);
                        break;
                    case SpaceEngineersToolsShared.CubeMapFace.Back:
                        Canvas.SetLeft(LargePreviewBorder, Canvas.GetLeft(TBack));
                        Canvas.SetTop(LargePreviewBorder, Canvas.GetTop(TBack));
                        Canvas.SetLeft(PreviewBorder, Canvas.GetLeft(TBack) + 512);
                        Canvas.SetTop(PreviewBorder, Canvas.GetTop(TBack) + 512);
                        break;
                    case SpaceEngineersToolsShared.CubeMapFace.Left:
                        Canvas.SetLeft(LargePreviewBorder, Canvas.GetLeft(TLeft));
                        Canvas.SetTop(LargePreviewBorder, Canvas.GetTop(TLeft));
                        Canvas.SetLeft(PreviewBorder, Canvas.GetLeft(TLeft) + 512);
                        Canvas.SetTop(PreviewBorder, Canvas.GetTop(TLeft) + 512);
                        break;
                    case SpaceEngineersToolsShared.CubeMapFace.Right:
                        Canvas.SetLeft(LargePreviewBorder, Canvas.GetLeft(TRight));
                        Canvas.SetTop(LargePreviewBorder, Canvas.GetTop(TRight));
                        Canvas.SetLeft(PreviewBorder, Canvas.GetLeft(TRight) + 512);
                        Canvas.SetTop(PreviewBorder, Canvas.GetTop(TRight) + 512);
                        break;
                }
            }
        }

        private Point _origin;
        private Point _start;
        bool _moved = false;

        bool _isMouseOnFace;
        CubeMapFace _lastMouseOverCubeMapFace;
        int _lastMouseOverCubeMapX;
        int _lastMouseOverCubeMapY;

        bool IsWithin(FrameworkElement elem, int x, int y)
        {
            var left = (int)(Canvas.GetLeft(elem)+.5);
            var top = (int)(Canvas.GetTop(elem) +.5);
            var within =
                 x >= left && x < (left + 2048) &&
                 y >= top  && y < (top  + 2048);

            // Update offsets
            if (within)
            {
                _lastMouseOverCubeMapX = x - left;
                _lastMouseOverCubeMapY = y - top;
            }
            return within;
        }

        void UpdateMapCoordinates(Point p)
        {
            _isMouseOnFace = false;
            Matrix m = ImageView.RenderTransform.Value;
            if (p.X >= 0 && p.Y >= 0 && p.X < BorderView.ActualWidth && p.Y < BorderView.ActualHeight)
            {
                double top = (p.Y - m.OffsetY) / m.M11;
                double left = (p.X - m.OffsetX) / m.M11;
                if (left < 0) return;
                if (top < 0) return;
                if (left > ImageView.Width) return;
                if (top > ImageView.Height) return;

                int x = (int)(left+.5);
                int y = (int)(top+.5);

                _isMouseOnFace = true;
                // Determine Face
                if (IsWithin(TUp, x, y))
                {
                    _lastMouseOverCubeMapFace = CubeMapFace.Up;
                }
                else if (IsWithin(TFront, x, y))
                {
                    _lastMouseOverCubeMapFace = CubeMapFace.Front;
                }
                else if (IsWithin(TRight, x, y))
                {
                    _lastMouseOverCubeMapFace = CubeMapFace.Right;
                }
                else if (IsWithin(TBack, x, y))
                {
                    _lastMouseOverCubeMapFace = CubeMapFace.Back;
                }
                else if (IsWithin(TLeft, x, y))
                {
                    _lastMouseOverCubeMapFace = CubeMapFace.Left;
                }
                else if (IsWithin(TDown, x, y))
                {
                    _lastMouseOverCubeMapFace = CubeMapFace.Down;
                }
                else _isMouseOnFace = false;
            }
        }

        private void ImageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!BorderView.IsMouseCaptured)
            {
                UpdateMapCoordinates(e.MouseDevice.GetPosition(BorderView));
                return;
            }
            if (!_moved)
            {
                // For some reason we get a mouse moved event uppon first mouse_down.
                // Ignore this or image will jump
                _moved = true;
                return;
            }
            Point p = e.MouseDevice.GetPosition(BorderView);
            var dx = (p.X - _start.X);
            var dy = (p.Y - _start.Y);
            //Debug.WriteLine("Moved " + dx + ";" + dy);
            Matrix m = ImageView.RenderTransform.Value;
            m.OffsetX = _origin.X + (p.X - _start.X);
            m.OffsetY = _origin.Y + (p.Y - _start.Y);
            ImageView.RenderTransform = new MatrixTransform(m);
        }

        private void ImageView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            BorderView.ReleaseMouseCapture();
            _moved = false;
        }

        private void ImageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (BorderView.IsMouseCaptured) return;
            BorderView.CaptureMouse();
            _moved = false;
            //Debug.WriteLine("Down, Mouse captured");
            _start = e.GetPosition(BorderView);
            _origin.X = ImageView.RenderTransform.Value.OffsetX;
            _origin.Y = ImageView.RenderTransform.Value.OffsetY;
        }

        private void ImageView_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point p = e.MouseDevice.GetPosition(ImageView);
            Matrix m = ImageView.RenderTransform.Value;
            if (e.Delta > 0)
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            else
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            ImageView.RenderTransform = new MatrixTransform(m);
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            var sInfo = new System.Diagnostics.ProcessStartInfo("https://github.com/michi84o/SpaceEngineersTools")
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            BorderView.MouseMove += ImageView_MouseMove;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.LoadPictures();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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

        private void ImageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!BorderView.IsMouseCaptured) { return; }
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

    }
}

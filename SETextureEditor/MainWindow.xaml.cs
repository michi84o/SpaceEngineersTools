using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SETextureEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool _moved;
        Point _start;
        Point _origin;

        public MainWindow()
        {
            InitializeComponent();

            MyBorderRgb.MouseWheel += MyBorderRgb_MouseWheel;
            MyBorderRgb.MouseLeftButtonDown += MyBorderRgb_MouseLeftButtonDown;
            MyBorderRgb.MouseLeftButtonUp += MyBorderRgb_MouseLeftButtonUp;
            MyBorderRgb.MouseMove += MyBorderRgb_MouseMove;
            MyBorderRgb.MouseRightButtonUp += MyBorderRgb_MouseRightButtonUp;

            ViewModel.AutoscaleAction = AutoScale;
        }

        private void MyBorderRgb_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            AutoScale();
            InvalidateVisual();
        }

        void AutoScale()
        {
            var scale = Math.Min(
                    512 / MyCanvasRgb.Width,
                    512 / MyCanvasRgb.Height);
            var mat = new Matrix();
            mat.ScaleAtPrepend(scale, scale, 0, 0);
            MyCanvasRgb.RenderTransform = new MatrixTransform(mat);
        }

        private void MyBorderRgb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!MyBorderRgb.IsMouseCaptured)
            {
                return;
            }
            if (!_moved)
            {
                // For some reason we get a mouse moved event uppon first mouse_down.
                // Ignore this or image will jump
                _moved = true;
                return;
            }
            Point p = e.MouseDevice.GetPosition(MyBorderRgb);
            var dx = (p.X - _start.X);
            var dy = (p.Y - _start.Y);

            Matrix m = MyCanvasRgb.RenderTransform.Value;
            m.OffsetX = _origin.X + (p.X - _start.X);
            m.OffsetY = _origin.Y + (p.Y - _start.Y);
            MyCanvasRgb.RenderTransform = new MatrixTransform(m);
        }

        private void MyBorderRgb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MyBorderRgb.ReleaseMouseCapture();
            _moved = false;
        }

        private void MyBorderRgb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MyBorderRgb.IsMouseCaptured) return;
            MyBorderRgb.CaptureMouse();
            _moved = false;
            //Debug.WriteLine("Down, Mouse captured");
            _start = e.GetPosition(MyBorderRgb);
            _origin.X = MyCanvasRgb.RenderTransform.Value.OffsetX;
            _origin.Y = MyCanvasRgb.RenderTransform.Value.OffsetY;
        }

        private void MyBorderRgb_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point p = e.MouseDevice.GetPosition(MyCanvasRgb);
            Matrix m = MyCanvasRgb.RenderTransform.Value;
            if (e.Delta > 0)
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            else
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            MyCanvasRgb.RenderTransform = new MatrixTransform(m);
        }
    }
}
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

            //MyBorderRgbXZ.MouseWheel += MyBorderRgb_MouseWheel;
            //MyBorderRgbXZ.MouseLeftButtonDown += MyBorderRgb_MouseLeftButtonDown;
            //MyBorderRgbXZ.MouseLeftButtonUp += MyBorderRgb_MouseLeftButtonUp;
            //MyBorderRgbXZ.MouseMove += MyBorderRgb_MouseMove;
            //MyBorderRgbXZ.MouseRightButtonUp += MyBorderRgb_MouseRightButtonUp;

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
                    512 / MyCanvasRgbXZ.Width,
                    512 / MyCanvasRgbXZ.Height);
            var mat = new Matrix();
            mat.ScaleAtPrepend(scale, scale, 0, 0);
            MyCanvasRgbXZ.RenderTransform = new MatrixTransform(mat);
            MyCanvasNormalXZ.RenderTransform = new MatrixTransform(mat);

            MyCanvasRgbY.RenderTransform = new MatrixTransform(mat);
            MyCanvasNormalY.RenderTransform = new MatrixTransform(mat);

            scale = Math.Min(
                    256 / MyCanvasRgbXZ.Width,
                    256 / MyCanvasRgbXZ.Height);
            mat = new Matrix();
            mat.ScaleAtPrepend(scale, scale, 0, 0);


            MyCanvasEmissivenessXZ.RenderTransform = new MatrixTransform(mat);
            MyCanvasMetalnessXZ.RenderTransform = new MatrixTransform(mat);
            MyCanvasGlossXZ.RenderTransform = new MatrixTransform(mat);
            MyCanvasPainabilityXZ.RenderTransform = new MatrixTransform(mat);
            MyCanvasOcclusionXZ.RenderTransform = new MatrixTransform(mat);

            MyCanvasEmissivenessY.RenderTransform = new MatrixTransform(mat);
            MyCanvasMetalnessY.RenderTransform = new MatrixTransform(mat);
            MyCanvasGlossY.RenderTransform = new MatrixTransform(mat);
            MyCanvasPainabilityY.RenderTransform = new MatrixTransform(mat);
            MyCanvasOcclusionY.RenderTransform = new MatrixTransform(mat);
        }

        private void MyBorderRgb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!MyBorderRgbXZ.IsMouseCaptured)
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
            Point p = e.MouseDevice.GetPosition(MyBorderRgbXZ);
            var dx = (p.X - _start.X);
            var dy = (p.Y - _start.Y);

            Matrix m = MyCanvasRgbXZ.RenderTransform.Value;
            m.OffsetX = _origin.X + (p.X - _start.X);
            m.OffsetY = _origin.Y + (p.Y - _start.Y);
            MyCanvasRgbXZ.RenderTransform = new MatrixTransform(m);
        }

        private void MyBorderRgb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MyBorderRgbXZ.ReleaseMouseCapture();
            _moved = false;
        }

        private void MyBorderRgb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MyBorderRgbXZ.IsMouseCaptured) return;
            MyBorderRgbXZ.CaptureMouse();
            _moved = false;
            //Debug.WriteLine("Down, Mouse captured");
            _start = e.GetPosition(MyBorderRgbXZ);
            _origin.X = MyCanvasRgbXZ.RenderTransform.Value.OffsetX;
            _origin.Y = MyCanvasRgbXZ.RenderTransform.Value.OffsetY;
        }

        private void MyBorderRgb_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point p = e.MouseDevice.GetPosition(MyCanvasRgbXZ);
            Matrix m = MyCanvasRgbXZ.RenderTransform.Value;
            if (e.Delta > 0)
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            else
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            MyCanvasRgbXZ.RenderTransform = new MatrixTransform(m);
        }
    }
}
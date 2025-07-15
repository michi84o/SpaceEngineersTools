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

namespace SETextureEditor
{
    /// <summary>
    /// Interaction logic for TextureView.xaml
    /// </summary>
    public partial class TextureView : UserControl
    {
        private Point _origin;
        private Point _start;
        bool _moved = false;

        public TextureView()
        {
            InitializeComponent();

            MyBorder.MouseWheel += MyBorder_MouseWheel;
            MyBorder.MouseLeftButtonDown += MyBorder_MouseLeftButtonDown;
            MyBorder.MouseLeftButtonUp += MyBorder_MouseLeftButtonUp;
            MyBorder.MouseMove += MyBorder_MouseMove;

            DataContextChanged += TextureView_DataContextChanged;
        }

        private void TextureView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is TextureViewModel model)
            {
                MyCanvas.Width = model.Width;
                MyCanvas.Height = model.Height;
                MyCanvas.RenderTransform = new MatrixTransform();
            }
        }

        private void MyBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!MyBorder.IsMouseCaptured)
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
            Point p = e.MouseDevice.GetPosition(MyBorder);
            var dx = (p.X - _start.X);
            var dy = (p.Y - _start.Y);

            Matrix m = MyCanvas.RenderTransform.Value;
            m.OffsetX = _origin.X + (p.X - _start.X);
            m.OffsetY = _origin.Y + (p.Y - _start.Y);
            MyCanvas.RenderTransform = new MatrixTransform(m);
        }

        private void MyBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MyBorder.ReleaseMouseCapture();
            _moved = false;
        }

        private void MyBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MyBorder.IsMouseCaptured) return;
            MyBorder.CaptureMouse();
            _moved = false;
            //Debug.WriteLine("Down, Mouse captured");
            _start = e.GetPosition(MyBorder);
            _origin.X = MyCanvas.RenderTransform.Value.OffsetX;
            _origin.Y = MyCanvas.RenderTransform.Value.OffsetY;
        }

        private void MyBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point p = e.MouseDevice.GetPosition(MyCanvas);
            Matrix m = MyCanvas.RenderTransform.Value;
            if (e.Delta > 0)
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            else
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            MyCanvas.RenderTransform = new MatrixTransform(m);
        }
    }
}

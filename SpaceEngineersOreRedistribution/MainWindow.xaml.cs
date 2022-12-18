using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
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

namespace SpaceEngineersOreRedistribution
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point _origin;
        private Point _start;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;

            BorderView.MouseWheel += ImageView_MouseWheel;
            ImageView.MouseLeftButtonDown += ImageView_MouseLeftButtonDown; ;
            ImageView.MouseLeftButtonUp += ImageView_MouseLeftButtonUp; ;
            ImageView.MouseMove += ImageView_MouseMove; ;
        }

        private void ImageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ImageView.IsMouseCaptured) return;
            Point p = e.MouseDevice.GetPosition(BorderView);
            Matrix m = ImageView.RenderTransform.Value;
            m.OffsetX = _origin.X + (p.X - _start.X);
            m.OffsetY = _origin.Y + (p.Y - _start.Y);
            ImageView.RenderTransform = new MatrixTransform(m);
        }

        private void ImageView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageView.ReleaseMouseCapture();
        }

        private void ImageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ImageView.IsMouseCaptured) return;
            ImageView.CaptureMouse();
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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenPlanetDefinition(@"C:\Users\Michael\AppData\Roaming\SpaceEngineers\Mods\OreRedistribution\Data\PlanetDataFiles\PlanetGeneratorDefinitions.sbc");



        }
    }
}

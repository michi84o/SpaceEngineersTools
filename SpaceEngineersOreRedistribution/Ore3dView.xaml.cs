using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace SpaceEngineersOreRedistribution
{
    /// <summary>
    /// Interaction logic for Ore3dView.xaml
    /// </summary>
    public partial class Ore3dView : Window
    {
        public Ore3dView()
        {
            InitializeComponent();
            ViewModel.Cuboids.CollectionChanged += Cuboids_CollectionChanged;

            MyCam.UpDirection = new Vector3D(0, 0, 1);
            // Idea for mouse controls:
            // Camera is on virtual sphere:
            // - Camera always looks towards center
            // - Mouse Wheel controls sphere radius
            // - Mouse left right moves it longitude
            // - Mouse up down moves it latitude

            MyCanvas.MouseDown += MyViewPort_MouseDown;
            MyCanvas.MouseUp += MyViewPort_MouseUp; ;
            MyCanvas.MouseMove += MyViewPort_MouseMove;
            MyCanvas.MouseWheel += MyViewPort_MouseWheel;
        }

        private void MyViewPort_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MyCanvas.ReleaseMouseCapture();
            _dragCount = 0;
        }

        private void MyViewPort_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var delta = e.Delta / 20.0;
            var radius = ViewModel.Radius + delta;
            if (radius < 50) radius = 50;
            if (radius > 300) radius = 300;
            ViewModel.Radius = radius;
        }

        private void MyViewPort_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // There is a bug that causes first deltas to be too large
                if (_dragCount < 3)
                {
                    _previousPosition = e.GetPosition(MyCanvas);
                    ++_dragCount;
                    return;
                }

                // Panning
                Vector delta = e.GetPosition(MyCanvas) - _previousPosition;

                var lo = ViewModel.Longitude - delta.X / 2;
                var la = ViewModel.Latitude + delta.Y / 2;

                if (lo < 0) lo = 0;
                if (lo > 360) lo = 360;
                if (la < -89) la = -89;
                if (la > 89) la = 89;

                ViewModel.Longitude = lo;
                ViewModel.Latitude = la;

                _previousPosition = e.GetPosition(MyCanvas);
            }
        }

        Point _previousPosition;
        int _dragCount = 0;
        private void MyViewPort_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MyCanvas.CaptureMouse();
            _dragCount = 0;
        }

        private void Cuboids_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // TODO: Can be done more effective. Analyse what changed.
            MyViewPort.Children.Clear();

            DirectionalLight myDirectionalLight = new DirectionalLight();
            myDirectionalLight.Color = Colors.White;
            myDirectionalLight.Direction = new Vector3D(-0.2, -0.2, -0.5);

            AmbientLight ambientLight = new AmbientLight();
            ambientLight.Color = Color.FromRgb(80, 80, 80); //Colors.White;

            MyViewPort.Children.Add(new ModelVisual3D() { Content = ambientLight });
            MyViewPort.Children.Add(new ModelVisual3D() { Content = myDirectionalLight });

            foreach (var model in ViewModel.Cuboids)
            {
                MyViewPort.Children.Add(model);
            }

            // Transparent surface must come after cuboids
            MyViewPort.Children.Add(My3dHelper.CreateSurface());
        }



    }
}

using SixLabors.ImageSharp.PixelFormats;
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

            // Idea for mouse controls:
            // Camera is on virtual sphere:
            // - Camera always looks towards center
            // - Mouse Wheel controls sphere radius
            // - Mouse left right moves it longitude
            // - Mouse up down moves it latitude

            //MyViewPort.MouseDown += MyViewPort_MouseDown;
            //MyViewPort.MouseMove += MyViewPort_MouseMove;
            //MyViewPort.MouseWheel += MyViewPort_MouseWheel;
        }

        // TODO: Zoom, Pan, Rotate with mouse
        //private void MyViewPort_MouseWheel(object sender, MouseWheelEventArgs e)
        //{
        //    // Zooming
        //    double zoomFactor = 1.1;
        //    if (e.Delta < 0)
        //        zoomFactor = 1 / zoomFactor;

        //    // Berechne neuen Kameraposition für Zoom
        //    MyCam.Position = new Point3D(
        //        MyCam.Position.X * zoomFactor,
        //        MyCam.Position.Y * zoomFactor,
        //        MyCam.Position.Z * zoomFactor);
        //}

        //private void MyViewPort_MouseMove(object sender, MouseEventArgs e)
        //{
        //    if (e.LeftButton == MouseButtonState.Pressed)
        //    {
        //        // Panning
        //        Vector delta = e.GetPosition(MyViewPort) - _previousPosition;
        //        MyCam.Position += new Vector3D(-delta.X, delta.Y, 0);
        //        _previousPosition = e.GetPosition(MyViewPort);
        //    }
        //    // TODO: Rotation
        //}

        //Point _previousPosition;
        //private void MyViewPort_MouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    _previousPosition = e.GetPosition(MyViewPort);
        //}

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

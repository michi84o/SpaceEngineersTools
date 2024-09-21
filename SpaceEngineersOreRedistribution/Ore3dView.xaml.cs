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

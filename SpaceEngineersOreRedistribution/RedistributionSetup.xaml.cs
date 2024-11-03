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
using System.Windows.Shapes;

namespace SpaceEngineersOreRedistribution
{
    /// <summary>
    /// Interaction logic for RedistributionSetup.xaml
    /// </summary>
    public partial class RedistributionSetup : Window
    {
        public RedistributionSetup()
        {
            InitializeComponent();
            ViewModel.ConfirmAction = ConfirmAction;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedInfo))
            {
                UpdateDiagramClick(null, null);
            }
        }

        private void ButtonAbort_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmAction()
        {
            DialogResult = true;
            Close();
        }

        private void UpdateDiagramClick(object sender, RoutedEventArgs e)
        {
            List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>
            {
                Colors.LawnGreen,
                Colors.DeepSkyBlue,
                Colors.Red,
                Colors.Orange,
                Colors.BlueViolet,
                Colors.Magenta,
                Colors.Green,
                Colors.Gold,
                Colors.MediumPurple,
                Colors.Blue,
            };
            ViewModel.FinalizeList();

            MyCanvas.Children.Clear();
            if (ViewModel.SelectedInfo == null) return;
            double depthMax = 0;
            foreach (var mapping in ViewModel.SelectedInfo.OreMappings)
            {
                if (mapping.Start + mapping.Depth > depthMax) depthMax = mapping.Start + mapping.Depth;
            }
            int i = -1;
            // Canvas is 320x240
            // Each rect must be 32 pixels
            foreach (var mapping in ViewModel.SelectedInfo.OreMappings)
            {
                ++i;
                if (i > 9) break;
                if (mapping.Value < 1)
                {
                    continue;
                }
                var rect = new Rectangle();
                rect.Width = 32;
                rect.Height = 240 * mapping.Depth / depthMax;
                rect.Fill = new SolidColorBrush(colors[i]);
                MyCanvas.Children.Add(rect);
                Canvas.SetLeft(rect, i * 32);
                Canvas.SetTop(rect, 240 * mapping.Start / depthMax);
            }
        }
    }
}

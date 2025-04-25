using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace ComplexMaterialViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ViewModel.UpdateCanvasAction = UpdateCanvas;
        }


        void UpdateCanvas()
        {
            MyCanvas.Children.Clear();

            // Width is 360 pixels. Max values are 0...90.
            // Scaling factor: 4
            var scale = 4d;

            var rnd = new Random();
            foreach (var rule in ViewModel.CanvasRules)
            {
                var x = rule.Slope.Min;
                var width = rule.Slope.Max - rule.Slope.Min;
                var y = 90 - rule.Latitude.Max;
                var height = rule.Latitude.Max - rule.Latitude.Min;

                if (rule.Height.Min > ViewModel.MaxHeight) continue;
                if (rule.Height.Max < ViewModel.MinHeight) continue;

                x *= scale;
                y *= scale;
                width *= scale;
                height *= scale;

                var rect = new Border()
                {
                    Width = width,
                    Height = height,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.5),
                    Opacity = .5
                };

                var name = rule.Layers.FirstOrDefault()?.Material ?? "";

                rect.Background = Brushes.LightGreen;
                var randSat = 0.2 + rnd.NextDouble() * 0.8;
                var randVal = 0.2 + rnd.NextDouble() * 0.8;
                if (name.ToLower().Contains("rock"))
                    rect.Background = new SolidColorBrush(ColorExtensions.FromHSV(30, randSat, randVal));
                else if (name.ToLower().Contains("desert"))
                    rect.Background = new SolidColorBrush(ColorExtensions.FromHSV(60, randSat, randVal));
                else if (name.ToLower().Contains("grass"))
                    rect.Background = new SolidColorBrush(ColorExtensions.FromHSV(120, randSat, randVal));
                else if (name.ToLower().Contains("snow"))
                    rect.Background = new SolidColorBrush(ColorExtensions.FromHSV(195, randSat, randVal));

                rect.ToolTip = name + " H:" + rule.Height.Min.ToString(CultureInfo.InvariantCulture) + "," + rule.Height.Max.ToString(CultureInfo.InvariantCulture);
                MyCanvas.Children.Add(rect);
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);

                rule.Actions.Add(r =>
                {
                    if (r.IsHighlighted)
                    {
                        foreach (var child in MyCanvas.Children)
                            Canvas.SetZIndex((Border)child, 0);

                        rect.BorderBrush = Brushes.Red;
                        rect.BorderThickness = new Thickness(4);
                        Canvas.SetZIndex(rect, 1);
                        rect.Opacity = 1;
                    }
                    else
                    {
                        rect.BorderBrush = Brushes.Gray;
                        rect.BorderThickness = new Thickness(.5);
                        rect.Opacity = 0.5;
                        Canvas.SetZIndex(rect, 0);
                    }
                });
            }
        }
    }
}

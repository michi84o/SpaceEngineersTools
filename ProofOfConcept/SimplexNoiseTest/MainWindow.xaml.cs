using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoiseTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        class NoiseMaker
        {
            OpenSimplexNoise _noise;
            public double ResolutionScale;
            public double Weight;
            public double GetValue(double x, double y)
            {
                return _noise.Evaluate(x*ResolutionScale, y*ResolutionScale)*Weight;
            }
            public NoiseMaker(int seed, double resolutionScale, double weight)
            {
                _noise = new OpenSimplexNoise(seed);
                ResolutionScale = resolutionScale;
                Weight = weight;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            List<NoiseMaker> list = new()
            {
                new(0, 1.0 / 200, 1),
                new(1, 1.0 / 100, .6),
                new(2, 1.0 / 50, .2),
            };

            long xMax = 2048;
            long yMax = 2048;

            var image = new Image<Rgb48>((int)xMax, (int)yMax);

            double min = 0;
            double max = 0;

            int i = 0;
            long arraySize = xMax * yMax;
            double[] nums = new double[arraySize];
            for (int x=0;x<xMax;++x)
            {
                for (int y=0;y<yMax;++y)
                {
                    double value = 0;
                    foreach (var n in list)
                    {
                        var v = n.GetValue(x, y); ;
                        value += v;
                    }
                    nums[i++] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            double offset = -1*min;
            double stretch = Math.Abs(max - min);

            i = 0;
            for (int x = 0; x < xMax; x++)
            {
                for (int y = 0; y < yMax; y++)
                {
                    double value = nums[i++];
                    value += offset;
                    value /= stretch;
                    ushort v = (ushort)(value * 65535);
                    image[x, y] = new Rgb48(v, v, v);
                }
            }

            image.SaveAsPng("tmp.png");
            MyImage.Source = GetImage("tmp.png");
        }

        string GetCurrentDir()
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            return System.IO.Path.GetDirectoryName(loc);
        }
        string GetLocalFileName(string filename)
        {
            return System.IO.Path.Combine(GetCurrentDir(), filename);
        }
        ImageSource GetImage(string filename)
        {
            return new BitmapImage(new Uri(GetLocalFileName(filename)));
        }
    }


}

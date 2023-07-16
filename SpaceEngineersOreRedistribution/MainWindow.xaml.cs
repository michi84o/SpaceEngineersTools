using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
        private System.Windows.Point _origin;
        private System.Windows.Point _start;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;

            ImageView.MouseWheel += ImageView_MouseWheel;
            BorderView.MouseLeftButtonDown += ImageView_MouseLeftButtonDown; ;
            BorderView.MouseLeftButtonUp += ImageView_MouseLeftButtonUp; ;
            BorderView.MouseMove += ImageView_MouseMove; ;
        }

        bool _moved = false;
        private void ImageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!BorderView.IsMouseCaptured) { return; }
            if (!_moved)
            {
                // For some reason we get a mouse moved event uppon first mouse_down.
                // Ignore this or image will jump
                    _moved = true;
                return;
            }
            System.Windows.Point p = e.MouseDevice.GetPosition(BorderView);
            var dx = (p.X - _start.X);
            var dy = (p.Y - _start.Y);
            //Debug.WriteLine("Moved " + dx + ";" + dy);
            Matrix m = ImageView.RenderTransform.Value;
            m.OffsetX = _origin.X + (p.X - _start.X);
            m.OffsetY = _origin.Y + (p.Y - _start.Y);
            ImageView.RenderTransform = new MatrixTransform(m);
        }

        private void ImageView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            BorderView.ReleaseMouseCapture();
            Debug.WriteLine("Mouse capture released");
            _moved = false;
        }

        private void ImageView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (BorderView.IsMouseCaptured) return;
            BorderView.CaptureMouse();
            _moved = false;
            //Debug.WriteLine("Down, Mouse captured");
            _start = e.GetPosition(BorderView);
            _origin.X = ImageView.RenderTransform.Value.OffsetX;
            _origin.Y = ImageView.RenderTransform.Value.OffsetY;
        }

        private void ImageView_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            System.Windows.Point p = e.MouseDevice.GetPosition(ImageView);
            Matrix m = ImageView.RenderTransform.Value;
            if (e.Delta > 0)
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            else
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            ImageView.RenderTransform = new MatrixTransform(m);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            ViewModel.OpenPlanetDefinition(@"C:\Users\Michael\AppData\Roaming\SpaceEngineers\Mods\OreRedistribution\Data\PlanetGeneratorDefinitions.sbc");
#endif

            bool test = false;
            // Testcode for material maps:
            bool makeLatitudeLines = false;
            bool makeBiomeStatistics = false;
            if (test)
            {
                if (makeLatitudeLines)
                {
                    foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
                    {
                        var faceName = face.ToString().ToLower();

                        StringBuilder sb = new();
                        Dictionary<int, int> counts = new();
                        var image = new SixLabors.ImageSharp.Image<Rgb24>(2048, 2048);
                        for (int x = 0; x < 2048; ++x)
                            for (int y = 0; y < 2048; ++y)
                            {
                                // Latitude lines
                                var point = CoordinateHelper.GetNormalizedSphereCoordinates(face, x, y);
                                var lolat = CoordinateHelper.ToLongitudeLatitude(point);
                                var rest = Math.Abs(lolat.latitude) % 10;
                                if (rest >= 9.5) rest = 10 - rest;
                                if (rest <= 0.5)
                                {
                                    var val = (byte)((1 - rest) * 255 + 0.5);
                                    image[x, y] = new Rgb24(val, val, val);
                                }
                            }
                        image.SaveAsPng(faceName +  "_lines.png");
                    } // foreach
                }
                if (makeBiomeStatistics)
                {
                    Dictionary<int, int> countsGlobal = new();
                    foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
                    {
                        var faceName = face.ToString().ToLower();
                        dynamic image = SixLabors.ImageSharp.Image.Load(faceName + "_mat.png");

                        StringBuilder sb = new();
                        Dictionary<int, int> counts = new();
                        for (int x = 0; x < 2048; ++x)
                            for (int y = 0; y < 2048; ++y)
                            {
                                var val = image[x, y].G;
                                if (!counts.ContainsKey(val)) counts[val] = 1;
                                else counts[val]++;

                                if (!countsGlobal.ContainsKey(val)) countsGlobal[val] = 1;
                                else countsGlobal[val]++;
                            }
                        HashSet<int> keyHashes = new();
                        foreach (var key in counts.Keys)
                            keyHashes.Add(key);
                        var sorted = keyHashes.ToList();
                        sorted.Sort();
                        foreach (var key in sorted)
                        {
                            sb.Append("" + key + ": " + counts[key] + "\r\n");
                        }
                        System.IO.File.WriteAllText(faceName + "_counts.txt", sb.ToString());
                    } // foreach
                    StringBuilder sbGlobal = new();
                    HashSet<int> keyHashesG = new();
                    foreach (var key in countsGlobal.Keys)
                        keyHashesG.Add(key);
                    var sortedG = keyHashesG.ToList();
                    sortedG.Sort();
                    foreach (var key in sortedG)
                    {
                        sbGlobal.Append("" + key + ": " + countsGlobal[key] + "\r\n");
                    }
                    System.IO.File.WriteAllText("counts.txt", sbGlobal.ToString());
                }

            }
        }
    }
}

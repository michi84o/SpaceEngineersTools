using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
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
            BorderView.MouseLeftButtonUp += ImageView_MouseLeftButtonUp;
            BorderView.MouseMove += ImageView_MouseMove;
            BorderView.MouseRightButtonUp += BorderView_MouseRightButtonUp;
            OreInspectorCb.Checked += OreInspectorCb_Checked;
            OreInspectorCb.Unchecked += OreInspectorCb_Checked;
        }

        private void OreInspectorCb_Checked(object sender, RoutedEventArgs e)
        {
            UpdateOreInspectRect(new System.Windows.Point(0, 0));
            ImageView.Focus();
        }

        void UpdateOreInspectRect(System.Windows.Point p)
        {
            Matrix m = ImageView.RenderTransform.Value;
            if (p.X >= 0 && p.Y >= 0 && p.X < BorderView.ActualWidth && p.Y < BorderView.ActualHeight && OreInspectorCb.IsChecked
                && ViewModel.SelectedPlanetDefinition != null)
            {
                OreInspectorRect.Visibility = Visibility.Visible;
                // Canvas Top/Left ist based relative to Canvas. Canvas is render transformed.
                // Need to convert mouse coordinates
                // Offset in matrix is not scaled
                double top = (p.Y - m.OffsetY) / m.M11 - OreInspectorRect.Height;
                double left = (p.X - m.OffsetX) / m.M11 - OreInspectorRect.Width;
                if (left < 0) left = 0;
                if (top < 0) top = 0;
                if (left > ImageView.Width - OreInspectorRect.Width) left = ImageView.Width - OreInspectorRect.Width;
                if (top > ImageView.Height - OreInspectorRect.Height) top = ImageView.Height - OreInspectorRect.Height;
                Canvas.SetTop(OreInspectorRect,top);
                Canvas.SetLeft(OreInspectorRect,left);
            }
            else
            {
                OreInspectorRect.Visibility = Visibility.Collapsed;
            }
        }

        private void BorderView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (BorderView.IsMouseCaptured || !OreInspectorCb.IsChecked ||
                ViewModel.SelectedPlanetDefinition == null) return;
            var x = (int)(Canvas.GetLeft(OreInspectorRect) + 0.5);
            var y = (int)(Canvas.GetTop(OreInspectorRect) + 0.5);
            // Check which image:
            CubeMapFace face = CubeMapFace.Up;
            if (x >= 0 && y >= 0 && x <= 2048 && y <= 2048)
            {
                face = CubeMapFace.Up;
            }
            else if (x >= 0 && y >= 2048 && x <= 2048 && y <= 4096)
            {
                face = CubeMapFace.Front;
                y -= 2048;
            }
            else if (x >= 2048 && y >= 2048 && x <= 4096 && y <= 4096)
            {
                face = CubeMapFace.Right;
                x -= 2048;
                y -= 2048;
            }
            else if (x >= 4096 && y >= 2048 && x <= 6144 && y <= 4096)
            {
                face = CubeMapFace.Back;
                x -= 4096;
                y -= 2048;
            }
            else if (x >= 6144 && y >= 2048 && x <= 8192 && y <= 4096)
            {
                face = CubeMapFace.Left;
                x -= 6144;
                y -= 2048;
            }
            else if (x >= 4096 && y >= 4096 && x <= 6144 && y <= 6144)
            {
                face = CubeMapFace.Down;
                x -= 4096;
                y -= 4096;
            }
            else
                return;

            // Copy pixels into matrix
            var width = (int)(OreInspectorRect.Width + .5);
            var height = (int)(OreInspectorRect.Height + .5);
            OreMapping[,] oreMap = new OreMapping[width, height];

            //var types = ViewModel.OreTypes;

            var info = ViewModel.GetInfo(face, CancellationToken.None);
            for (int xx = 0; xx < width; ++xx)
            {
                for (int yy = 0; yy < height; ++yy)
                {
                    var xxx = xx + x;
                    var yyy = yy + y;
                    if (xxx >= 0 && xxx < 2048 && yyy >= 0 && yyy <= 2048)
                    {
                        var color = info.B[xxx, yyy];
                        var mapping = ViewModel.SelectedPlanetDefinition.OreMappings.FirstOrDefault(o => o.Value == color);
                        oreMap[xx, yy] = mapping;
                    }
                }
            }

            var win = new Ore3dView();
            win.SetRectSize(ViewModel.OreInspectorSize);
            win.ViewModel.AddCuboids(oreMap);
            win.ShowDialog();
        }

        bool _moved = false;
        private void ImageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!BorderView.IsMouseCaptured)
            {
                UpdateOreInspectRect(e.MouseDevice.GetPosition(BorderView));
                return;
            }
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
            UpdateOreInspectRect(p);
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
            {
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            }
            else
            {
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            }
            ImageView.RenderTransform = new MatrixTransform(m);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //var n = new Normal(50,10,0);
            //for (int i = 0; i < 20; ++i)
            //    Debug.WriteLine(n.Next(5, 5*20.0/100));
#if DEBUG
            ViewModel.OpenPlanetDefinition(@"C:\Users\Michael\AppData\Roaming\SpaceEngineers\Mods\SeamsFixedDeluxeOreRedist\Data\PlanetGeneratorDefinitions.sbc");
#endif

            bool test = false;
            // Testcode for material maps:
            bool makeLatitudeLines = true;
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
                        var climateZone = new SixLabors.ImageSharp.Image<Rgb24>(2048, 2048);
                        for (int x = 0; x < 2048; ++x)
                            for (int y = 0; y < 2048; ++y)
                            {
                                // Latitude lines
                                var point = CoordinateHelper.GetNormalizedSphereCoordinates(face, x, y);
                                var lolat = CoordinateHelper.ToLongitudeLatitude(point);
                                var latAbs = Math.Abs(lolat.latitude);
                                var rest = latAbs % 5;
                                if (rest >= 4.9) rest = 5 - rest;
                                if (rest <= 0.1)
                                {
                                    var val = (byte)((1 - rest) * 255 + 0.5);
                                    image[x, y] = new Rgb24(val, val, val);
                                }
                                for (int ii = 5; ii < 100; ii += 5)
                                {
                                    if (latAbs < ii)
                                    {
                                        climateZone[x, y] = new Rgb24((byte)(95+ii), (byte)(95 + ii), (byte)(95 + ii));
                                        break;
                                    }
                                }

                            }
                        image.SaveAsPng(faceName +  "_lines.png");
                        climateZone.SaveAsPng(faceName + "_climatezones.png");
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

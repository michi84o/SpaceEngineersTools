using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
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


            var bmap = new Bitmap(2048, 2048);
            var lockBmap = new LockBitmap(bmap);
            lockBmap.LockBits();

            double min = 0;
            double max = 0;

            int i = 0;
            long arraySize = 2048L * 2048L;
            double[] nums = new double[arraySize];
            for (int x=0;x<2048;++x)
            {
                for (int y=0;y<2048;++y)
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
            for (int x = 0; x < 2048; x++)
            {
                for (int y = 0; y < 2048; y++)
                {
                    double value = nums[i++];
                    value += offset;
                    value /= stretch;
                    int v = (int)(value * 255);
                    var color = Color.FromArgb(v, v, v);
                    lockBmap.SetPixel(x, y, color);
                }
            }

            lockBmap.UnlockBits();
            MyImage.Source = FromBitmap(bmap);

            bmap.Save("noise.png", ImageFormat.Png);

            bmap.Dispose();
        }

        public static BitmapImage FromBitmap(Bitmap bitmap)
        {
            using MemoryStream memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            return bitmapImage;
        }
    }

    public class LockBitmap
    {
        Bitmap _source = null;
        IntPtr _iptr = IntPtr.Zero;
        BitmapData _bitmapData = null;

        public byte[] Pixels { get; set; }
        public int Depth { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public LockBitmap(Bitmap source)
        {
            _source = source;
        }

        /// <summary>
        /// Lock bitmap data
        /// </summary>
        public void LockBits()
        {
            // Get width and height of bitmap
            Width = _source.Width;
            Height = _source.Height;

            // get total locked pixels count
            int PixelCount = Width * Height;

            // Create rectangle to lock
            Rectangle rect = new Rectangle(0, 0, Width, Height);

            // get source bitmap pixel format size
            Depth = Bitmap.GetPixelFormatSize(_source.PixelFormat);

            // Check if bpp (Bits Per Pixel) is 8, 24, or 32
            if (Depth != 8 && Depth != 24 && Depth != 32)
            {
                throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
            }

            // Lock bitmap and return bitmap data
            _bitmapData = _source.LockBits(rect, ImageLockMode.ReadWrite,
                                            _source.PixelFormat);

            // create byte array to copy pixel values
            int step = Depth / 8;
            Pixels = new byte[PixelCount * step];
            _iptr = _bitmapData.Scan0;

            // Copy data from pointer to array
            Marshal.Copy(_iptr, Pixels, 0, Pixels.Length);
        }

        /// <summary>
        /// Unlock bitmap data
        /// </summary>
        public void UnlockBits()
        {
            try
            {
                // Copy data from byte array to pointer
                Marshal.Copy(Pixels, 0, _iptr, Pixels.Length);

                // Unlock bitmap data
                _source.UnlockBits(_bitmapData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Get the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Color GetPixel(int x, int y)
        {
            Color clr = Color.Empty;

            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (i > Pixels.Length - cCount)
                throw new IndexOutOfRangeException();

            if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                byte a = Pixels[i + 3]; // a
                clr = Color.FromArgb(a, r, g, b);
            }
            if (Depth == 24) // For 24 bpp get Red, Green and Blue
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                clr = Color.FromArgb(r, g, b);
            }
            if (Depth == 8)
            // For 8 bpp get color value (Red, Green and Blue values are the same)
            {
                byte c = Pixels[i];
                clr = Color.FromArgb(c, c, c);
            }
            return clr;
        }

        /// <summary>
        /// Set the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="color"></param>
        public void SetPixel(int x, int y, Color color)
        {
            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
                Pixels[i + 3] = color.A;
            }
            if (Depth == 24) // For 24 bpp set Red, Green and Blue
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
            }
            if (Depth == 8)
            // For 8 bpp set color value (Red, Green and Blue values are the same)
            {
                Pixels[i] = color.B;
            }
        }
    }
}

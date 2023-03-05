using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlanetCreator
{
    interface IDebugImage
    {
        void DebugDrawPixel(int x, int y, byte a, byte r, byte g, byte b);
    }

    class MainWindowViewModel : PropChangeNotifier, IDebugImage
    {
        BitmapImage _tileUp;
        public BitmapImage TileUp
        {
            get => _tileUp;
            set => SetProp(ref _tileUp, value);
        }

        BitmapImage _tileFront;
        public BitmapImage TileFront
        {
            get => _tileFront;
            set => SetProp(ref _tileFront, value);
        }

        BitmapImage _tileRight;
        public BitmapImage TileRight
        {
            get => _tileRight;
            set => SetProp(ref _tileRight, value);
        }
        BitmapImage _tileBack;
        public BitmapImage TileBack
        {
            get => _tileBack;
            set => SetProp(ref _tileBack, value);
        }
        BitmapImage _tileLeft;
        public BitmapImage TileLeft
        {
            get => _tileLeft;
            set => SetProp(ref _tileLeft, value);
        }
        BitmapImage _tileDown;
        public BitmapImage TileDown
        {
            get => _tileDown;
            set => SetProp(ref _tileDown, value);
        }

        public WriteableBitmap DebugBitmap { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);

        public void DebugDrawPixel(int x, int y, byte a, byte r, byte g, byte b)
        {
            Application.Current.Dispatcher.BeginInvoke(() => _DebugDrawPixel(x, y, a, r, g, b));
        }
        void _DebugDrawPixel(int x, int y, byte a, byte r, byte g, byte b)
        {
            try
            {
                // Reserve the back buffer for updates.
                DebugBitmap.Lock();

                unsafe
                {
                    // Get a pointer to the back buffer.
                    IntPtr pBackBuffer = DebugBitmap.BackBuffer;

                    // Find the address of the pixel to draw.
                    pBackBuffer += y * DebugBitmap.BackBufferStride;
                    pBackBuffer += x * 4;

                    // Compute the pixel's color.
                    int color_data = b;    // B
                    color_data |= g << 8;  // G
                    color_data |= r << 16; // R
                    color_data |= a << 24; // A

                    // Assign the color data to the pixel.
                    *((int*)pBackBuffer) = color_data;
                }

                // Specify the area of the bitmap that changed.
                DebugBitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                DebugBitmap.Unlock();
            }
        }

        int _progress;
        public int Progress
        {
            get => _progress;
            set => SetProp(ref _progress, value);
        }

        bool _previewMode;
        public bool PreviewMode
        {
            get => _previewMode;
            set => SetProp(ref _previewMode, value);
        }

        bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProp(ref _isBusy, value))
                    OnPropertyChanged(nameof(NotBusy));
            }
        }
        public bool NotBusy => !IsBusy;

        int _seed = 0;
        public int Seed
        {
            get => _seed;
            set => SetProp(ref _seed, value);
        }
        int _noiseScale = 100;
        public int NoiseScale
        {
            get => _noiseScale;
            set => SetProp(ref _noiseScale, value);
        }
        int _octaves = 4;
        public int Octaves
        {
            get => _octaves;
            set => SetProp(ref _octaves, value);
        }
        int _erosionIterations = 1000000;
        public int ErosionIterations
        {
            get => _erosionIterations;
            set => SetProp(ref _erosionIterations, value);
        }
        int _erosionMaxDropletLifeTime = 100;
        public int ErosionMaxDropletLifeTime
        {
            get => _erosionMaxDropletLifeTime;
            set => SetProp(ref _erosionMaxDropletLifeTime, value);
        }
        double _erosionInteria = 0.01;
        public double ErosionInteria
        {
            get => _erosionInteria;
            set => SetProp(ref _erosionInteria, value);
        }
        double _erosionSedimentCapacityFactor = 30;
        public double ErosionSedimentCapacityFactor
        {
            get => _erosionSedimentCapacityFactor;
            set => SetProp(ref _erosionSedimentCapacityFactor, value);
        }
        double _erosionDepositSpeed = 0.1;
        public double ErosionDepositSpeed
        {
            get => _erosionDepositSpeed;
            set => SetProp(ref _erosionDepositSpeed, value);
        }
        double _erosionErodeSpeed = 0.3;
        public double ErosionErodeSpeed
        {
            get => _erosionErodeSpeed;
            set => SetProp(ref _erosionErodeSpeed, value);
        }
        double _erosionDepositBrush = 3;
        public double ErosionDepositBrush
        {
            get => _erosionDepositBrush;
            set => SetProp(ref _erosionDepositBrush, value);
        }
        double _erosionErodeBrush = 3;
        public double ErosionErodeBrush
        {
            get => _erosionErodeBrush;
            set => SetProp(ref _erosionErodeBrush, value);
        }
        bool _enableErosion = true;
        public bool EnableErosion
        {
            get => _enableErosion;
            set => SetProp(ref _enableErosion, value);
        }
        double _gravity = 10;
        public double Gravity
        {
            get => _gravity;
            set => SetProp(ref _gravity, value);
        }
        double _evaporateSpeed = 0.01;
        public double EvaporateSpeed
        {
            get => _evaporateSpeed;
            set => SetProp(ref _evaporateSpeed, value);
        }
        bool _enableLakeGeneration = true;
        public bool EnableLakeGeneration
        {
            get => _enableLakeGeneration;
            set => SetProp(ref _enableLakeGeneration, value);
        }
        ushort _lakesPerTile = 40;
        public ushort LakesPerTile
        {
            get => _lakesPerTile;
            set => SetProp(ref _lakesPerTile, value);
        }
        double _lakeVolumeMultiplier = 1.0;
        public double LakeVolumeMultiplier
        {
            get => _lakeVolumeMultiplier;
            set => SetProp(ref _lakeVolumeMultiplier, value);
        }

        public ICommand GenerateCommand => new RelayCommand(async o =>
        {
            TileUp = TileDown = TileRight = TileLeft = TileFront = TileBack = TileDown = null;

            var generator = new PlanetGenerator();
            generator.DebugImage = this;
            generator.DebugMode = PreviewMode;
            generator.Seed = Seed;
            if (NoiseScale < 1 || NoiseScale > 65535) NoiseScale = 100;
            generator.NoiseScale = NoiseScale;
            if (Octaves < 1 || Octaves > 64) Octaves = 4;
            generator.Octaves = Octaves;
            generator.EnableErosion = EnableErosion;
            if (ErosionIterations < 1) { ErosionIterations = 1; }
            generator.ErosionIterations = ErosionIterations;
            if (ErosionMaxDropletLifeTime < 1) { ErosionMaxDropletLifeTime = 1; }
            generator.ErosionMaxDropletLifeTime = ErosionMaxDropletLifeTime;
            if (ErosionInteria < 0) ErosionInteria = 0;
            generator.ErosionInteria = ErosionInteria;
            if (ErosionSedimentCapacityFactor < 0) ErosionSedimentCapacityFactor = 0;
            generator.ErosionSedimentCapacityFactor = ErosionSedimentCapacityFactor;
            if (ErosionDepositSpeed < 0) ErosionDepositSpeed = 0;
            if (ErosionDepositSpeed > 1) ErosionDepositSpeed = 1;
            generator.ErosionDepositSpeed = ErosionDepositSpeed;
            if (ErosionErodeSpeed < 0) ErosionErodeSpeed = 0;
            if (ErosionErodeSpeed > 1) ErosionErodeSpeed = 1;
            generator.ErosionErodeSpeed = ErosionErodeSpeed;
            if (ErosionDepositBrush < 0) ErosionDepositBrush = 0;
            generator.ErosionDepositBrush = ErosionDepositBrush;
            if (ErosionErodeBrush < 0) ErosionErodeBrush = 0;
            generator.ErosionErodeBrush = ErosionErodeBrush;
            if (Gravity < 0) Gravity = 0;
            generator.Gravity = Gravity;
            if (EvaporateSpeed < 0) EvaporateSpeed = 0;
            if (EvaporateSpeed > 1) EvaporateSpeed = 1;
            generator.EvaporateSpeed = EvaporateSpeed;
            generator.LakesPerTile = LakesPerTile;

            generator.GenerateLakes = EnableLakeGeneration;
            if (LakeVolumeMultiplier <= 0)
            {
                generator.GenerateLakes = false;
            }
            if (LakeVolumeMultiplier > 100000)
            {
                LakeVolumeMultiplier = 100000;
            }
            generator.LakeVolumeMultiplier = LakeVolumeMultiplier;

            Progress = 1;
            generator.ProgressChanged += Generator_ProgressChanged;
            IsBusy = true;
            try { _tcs?.Cancel(); } catch { }
            _tcs = new CancellationTokenSource();
            try
            {
                await Task.Run(() =>
                {
                    generator.GeneratePlanet(_tcs.Token);
                });
            }
            finally
            {
                IsBusy = false;
                generator.ProgressChanged -= Generator_ProgressChanged;
                _tcs.Dispose();
            }
            LoadPictures();
            Progress = 0;
        }, o=> !IsBusy);
        CancellationTokenSource _tcs;

        public ICommand AbortCommand => new RelayCommand(o =>
        {
            try { _tcs?.Cancel(); } catch { }
        }, o=> IsBusy);

        private void Generator_ProgressChanged(object sender, ProgressEventArgs e)
        {
            Progress = e.Progress;
        }

        public void LoadPictures()
        {
            try { TileUp = GetImage("up.png"); } catch { }
            try { TileDown = GetImage("down.png"); } catch { }
            try { TileLeft = GetImage("left.png"); } catch { }
            try { TileRight = GetImage("right.png"); } catch { }
            try { TileFront = GetImage("front.png"); } catch { }
            try { TileBack = GetImage("back.png"); } catch { }
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
        BitmapImage GetImage(string filename)
        {
            try
            {
                var imageBytes = System.IO.File.ReadAllBytes(GetLocalFileName(filename));

                var stream = new MemoryStream(imageBytes);
                var img = new System.Windows.Media.Imaging.BitmapImage();

                img.BeginInit();
                img.StreamSource = stream;
                img.EndInit();
                return img;
            }
            catch { return null; }
        }

    }
}

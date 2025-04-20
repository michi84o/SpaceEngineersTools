using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
    public interface IDebugOverlay
    {
        void DebugCollectPixel(CubeMapFace face, int x, int y, byte a, byte r, byte g, byte b);
        void DebugDrawCollectedPixels();
        void DebugClearPixels(CubeMapFace face);
    }

    public class MainWindowViewModel : PropChangeNotifier, IDebugOverlay
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

        public WriteableBitmap OverlayBitmapUp { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);
        public WriteableBitmap OverlayBitmapFront { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);
        public WriteableBitmap OverlayBitmapRight { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);
        public WriteableBitmap OverlayBitmapBack { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);
        public WriteableBitmap OverlayBitmapLeft { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);
        public WriteableBitmap OverlayBitmapDown { get; } = new WriteableBitmap(2048, 2048, 96, 96, PixelFormats.Bgra32, null);

        WriteableBitmap GetOverlay(CubeMapFace face)
        {
            WriteableBitmap bitmap;
            switch (face)
            {
                case CubeMapFace.Back:
                    bitmap = OverlayBitmapBack;
                    break;
                case CubeMapFace.Left:
                    bitmap = OverlayBitmapLeft;
                    break;
                case CubeMapFace.Right:
                    bitmap = OverlayBitmapRight;
                    break;
                case CubeMapFace.Front:
                    bitmap = OverlayBitmapFront;
                    break;
                case CubeMapFace.Up:
                    bitmap = OverlayBitmapUp;
                    break;
                case CubeMapFace.Down:
                    bitmap = OverlayBitmapDown;
                    break;
                default:
                    return null;
            }
            return bitmap;
        }

        Dictionary<CubeMapFace, List<object[]>> _collectedPixels = new();
        public void DebugCollectPixel(CubeMapFace face, int x, int y, byte a, byte r, byte g, byte b)
        {
            lock (_collectedPixels)
            {
                if (!_collectedPixels.ContainsKey(face))
                    _collectedPixels.Add(face, new List<object[]>());
                _collectedPixels[face].Add(new object[] { x, y, a, r, g, b });
            }
        }

        public void DebugDrawCollectedPixels()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                lock (_collectedPixels)
                {
                    foreach (var face in _collectedPixels.Keys)
                    {
                        var bitmap = GetOverlay(face);
                        if (bitmap == null) return;
                        try
                        {
                            // Reserve the back buffer for updates.
                            bitmap.Lock();

                            foreach (var pix in _collectedPixels[face])
                            {
                                int x = (int)pix[0];
                                int y = (int)pix[1];
                                byte a = (byte)pix[2];
                                byte r = (byte)pix[3];
                                byte g = (byte)pix[4];
                                byte b = (byte)pix[5];

                                unsafe
                                {
                                    // Get a pointer to the back buffer.
                                    IntPtr pBackBuffer = bitmap.BackBuffer;

                                    // Find the address of the pixel to draw.
                                    pBackBuffer += y * bitmap.BackBufferStride;
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
                                bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
                            }
                        }
                        finally
                        {
                            // Release the back buffer and make it available for display.
                            bitmap.Unlock();
                        }
                    }
                }
            });
        }

        public void DebugDrawPixel(CubeMapFace face, int x, int y, byte a, byte r, byte g, byte b)
        {
            Application.Current.Dispatcher.BeginInvoke(() => _DebugDrawPixel(face, x, y, a, r, g, b));
        }

        public void DebugClearPixels(CubeMapFace face)
        {
            _collectedPixels.Clear();
            Application.Current.Dispatcher.BeginInvoke(() => _DebugClearPixels(face));
        }

        void _DebugClearPixels(CubeMapFace face)
        {
            var bitmap = GetOverlay(face);
            if (bitmap == null) return;
            try
            {
                // Reserve the back buffer for updates.
                bitmap.Lock();

                unsafe
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        for (int y = 0; y < bitmap.Height; y++)
                        {
                            // Get a pointer to the back buffer.
                            IntPtr pBackBuffer = bitmap.BackBuffer;

                            // Find the address of the pixel to draw.
                            pBackBuffer += y * bitmap.BackBufferStride;
                            pBackBuffer += x * 4;

                            // Compute the pixel's color.
                            int color_data = 0;
                            // Assign the color data to the pixel.
                            *((int*)pBackBuffer) = color_data;
                        }
                    }
                }

                // Specify the area of the bitmap that changed.
                bitmap.AddDirtyRect(new Int32Rect(0, 0, 2048, 2048));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                bitmap.Unlock();
            }
        }
        void _DebugDrawPixel(CubeMapFace face, int x, int y, byte a, byte r, byte g, byte b)
        {
            var bitmap = GetOverlay(face);
            if (bitmap == null) return;
            try
            {
                // Reserve the back buffer for updates.
                bitmap.Lock();

                unsafe
                {
                    // Get a pointer to the back buffer.
                    IntPtr pBackBuffer = bitmap.BackBuffer;

                    // Find the address of the pixel to draw.
                    pBackBuffer += y * bitmap.BackBufferStride;
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
                bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                bitmap.Unlock();
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
            set
            {
                if (!SetProp(ref _previewMode, value)) return;
                if (!value) ExtendedPreview = false;
            }
        }

        bool _extendedPreview;
        public bool ExtendedPreview
        {
            get => _extendedPreview;
            set
            {
                if (!SetProp(ref _extendedPreview, value)) return;
                PreviewMode = true;
            }
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
        int _noiseScale = 200;
        public int NoiseScale
        {
            get => _noiseScale;
            set => SetProp(ref _noiseScale, value);
        }
        int _octaves = 5;
        public int Octaves
        {
            get => _octaves;
            set
            {
                if (SetProp(ref _octaves, value))
                {
                    if (value > 25) Octaves = 25;
                }
            }
        }

        bool _applySCurve;
        public bool ApplySCurve
        {
            get => _applySCurve;
            set => SetProp(ref _applySCurve, value);
        }

        int _flattenFactor = 10;
        public int FlattenFactor
        {
            get => _flattenFactor;
            set
            {
                if (SetProp(ref _flattenFactor, value))
                {
                    if (value > 100)
                        EquatorFlatSigma = 100;
                }
            }
        }

        bool _flattenEquator;
        public bool FlattenEquator
        {
            get => _flattenEquator;
            set => SetProp(ref _flattenEquator, value);
        }
        int _equatorFlatSigma = 200;
        public int EquatorFlatSigma
        {
            get => _equatorFlatSigma;
            set
            {
                if (SetProp(ref _equatorFlatSigma, value))
                {
                    if (value > 250)
                        EquatorFlatSigma = 250;
                }
            }
        }

        int _erosionIterations = 2500000;
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
        double _erosionSedimentCapacityFactor = 35;
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
        double _erosionDepositBrush = 2;
        public double ErosionDepositBrush
        {
            get => _erosionDepositBrush;
            set => SetProp(ref _erosionDepositBrush, value);
        }
        double _erosionErodeBrush = 2;
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

        double _brushPointiness = 0.25;
        public double BrushPointiness
        {
            get => _brushPointiness;
            set => SetProp(ref _brushPointiness, value);
        }

        string _materialSource = "Enter Source Material PNG Folder here";//"G:\\Steam\\steamapps\\common\\SpaceEngineers\\Content\\Data\\PlanetDataFiles\\EarthLike";
        public string MaterialSource
        {
            get => _materialSource;
            set => SetProp(ref _materialSource, value);
        }
        public ICommand GenerateCommand => new RelayCommand(async o =>
        {
            var faceValues = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToArray();
            foreach (var face in faceValues)
                DebugClearPixels(face);

            TileUp = TileDown = TileRight = TileLeft = TileFront = TileBack = TileDown = null;
            var generator = new PlanetGenerator();
            generator.DebugOverlay = this;
            generator.DebugMode = PreviewMode;
            generator.ExtendedDebugMode = ExtendedPreview;
            generator.Seed = Seed;
            if (NoiseScale < 1 || NoiseScale > 65535) NoiseScale = 100;
            generator.NoiseScale = NoiseScale;
            if (Octaves < 1 || Octaves > 25) Octaves = 5;
            generator.Octaves = Octaves;
            generator.ApplySCurve = ApplySCurve;
            if (FlattenFactor < 0) FlattenFactor = 0;
            generator.FlattenFactor = FlattenFactor;
            generator.FlattenEquator = FlattenEquator;
            if (EquatorFlatSigma < 1) EquatorFlatSigma = 250;
            generator.EquatorFlatSigma = EquatorFlatSigma;
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
            if (BrushPointiness < 0) BrushPointiness = 0;
            if (BrushPointiness > 1) BrushPointiness = 1;
            generator.BrushPointiness = BrushPointiness;
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
            var tcs = new CancellationTokenSource();
            var token = tcs.Token;
            _tcs = tcs;
            try
            {
                await Task.Run(() =>
                {
                    generator.GeneratePlanet(token);
                });
            }
            catch { } // TaskCancelledException
            finally
            {
                IsBusy = false;
                generator.ProgressChanged -= Generator_ProgressChanged;
                _tcs.Dispose();
            }
            if (token.IsCancellationRequested) return;

            LoadPictures();

            if (MaterialSource != null)
            {
                if (Directory.Exists(MaterialSource))
                {
                    foreach (var face in faceValues)
                    {
                        try
                        {
                            if (token.IsCancellationRequested) return;
                            var file = face.ToString().ToLower() + "_lakes.png";
                            var remoteFile = face.ToString().ToLower() + "_mat.png";
                            var remotePath = System.IO.Path.Combine(MaterialSource, remoteFile);
                            if (File.Exists(remotePath) && File.Exists(file))
                            {
                                // TODO: dynamic is slow!
                                dynamic localImg = Image.Load(file);
                                dynamic remoteImg = Image.Load(remotePath);
                                var image = new Image<Rgb24>(2048, 2048);
                                if (remoteImg != null && localImg != null)
                                {
                                    Parallel.For(0, 2048, generator.POptions(token), x =>
                                    {
                                        for (int y = 0; y < 2048; ++y)
                                        {
                                            // Local image is lake only
                                            var localImgVal = localImg[x, y];
                                            var imgVal = remoteImg[x, y];
                                            image[x, y] = new Rgb24((byte)localImgVal.R, (byte)imgVal.G, (byte)imgVal.B);
                                        }
                                    });
                                    image.Save(face.ToString().ToLower() + "_mat.png");
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            Progress = 0;
        }, o => !IsBusy);
        CancellationTokenSource _tcs;

        public ICommand AbortCommand => new RelayCommand(o =>
        {
            try { _tcs?.Cancel(); } catch { }
        }, o => IsBusy);

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

        public ICommand TripletFixCommand => new RelayCommand(o =>
        {
            EdgeFixCommand.Execute("triplet");
        });

        public ICommand EdgeFixCommand => new RelayCommand(o =>
        {
            MessageBox.Show("Select one of the files to be fixed.\r\nI will fix all 6 cube map files that belong to this file.\r\nWarning! It will override your files!\r\nClose the next dialog to abort.");
            var dlg = new OpenFileDialog();
            dlg.Filter = "PNG Files|*.png";
            if (dlg.ShowDialog() != true) return;
            var folder = System.IO.Path.GetDirectoryName(dlg.FileName);

            // Check if all files exist:
            string[] files = new[]
            {
                "back.png",
                "down.png",
                "front.png",
                "left.png",
                "right.png",
                "up.png",
            };
            string filesList = "";
            foreach (var file in files)
            {
                var fileName = Path.Combine(folder, file);
                if (!File.Exists(fileName))
                {
                    MessageBox.Show(file + " is missing!", "Missing file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else filesList += fileName + "\r\n";
            }

            if (MessageBox.Show("Please confirm that these files should be overriden:\r\n" + filesList.TrimEnd(), "Question", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            Dictionary<CubeMapFace, double[,]> images = new();
            CubeMapFace[] faces = new[]
            {
                CubeMapFace.Back,
                CubeMapFace.Down,
                CubeMapFace.Front,
                CubeMapFace.Left,
                CubeMapFace.Right,
                CubeMapFace.Up,
            };
            // Load images
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    var fileName = Path.Combine(folder, files[i]);
                    var face = faces[i];
                    var image = SixLabors.ImageSharp.Image.Load(fileName) as Image<L16>;
                    double[,] imageData = new double[2048, 2048];
                    for (int x = 0; x < 2048; ++x)
                        for (int y = 0; y < 2048; ++y)
                        {
                            imageData[x, y] = image[x, y].PackedValue / 65535.0;
                        }
                    images[face] = imageData;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Error loading files", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            // Apply fix
            try
            {
                if (o is string s && s == "triplet")
                {
                    //EdgeFixer.MakeSeamless(images, CancellationToken.None, true);
                    //EdgeFixer.MakeSeamless(images, CancellationToken.None, true);
                    EdgeFixer.MakeSeamless(images, CancellationToken.None, true, true);
                }
                else
                    EdgeFixer.MakeSeamless(images, CancellationToken.None);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error applying edge fix", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Save images
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    var fileName = Path.Combine(folder, files[i]);
                    var face = faces[i];
                    var image = new SixLabors.ImageSharp.Image<L16>(2048, 2048);
                    var imageData = images[face];
                    for (int x = 0; x < 2048; ++x)
                        for (int y = 0; y < 2048; ++y)
                        {
                            var val = imageData[x, y] * 65535;
                            if (val < 0) val = 0;
                            if (val > 65535) val = 65535;
                            image[x, y] = new L16 { PackedValue = (ushort)val };
                        }
                    image.Save(fileName);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Error loading files", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            MessageBox.Show("Finished!");
        });

        public ICommand SetProfile1Command => new RelayCommand(o =>
        {
            Seed = 0;
            NoiseScale = 200;
            Octaves = 5;
            FlattenFactor = 10;
            FlattenEquator = true;
            EquatorFlatSigma = 150;
            ErosionIterations = 5000000;
            ErosionMaxDropletLifeTime = 100;
            ErosionInteria = 0.005;
            ErosionSedimentCapacityFactor = 10;
            ErosionDepositSpeed = 0.01;
            ErosionErodeSpeed = .03;
            ErosionDepositBrush = 5;
            ErosionErodeBrush = 5;
            BrushPointiness = 0.5;
            EnableErosion = true;
            Gravity = 10;
            EvaporateSpeed = 0.025;
            EnableLakeGeneration = true;
            LakesPerTile = 40;
            LakeVolumeMultiplier = 1;
        });

        public ICommand SetProfile2Command => new RelayCommand(o=>
        {
            Seed = 0;
            NoiseScale = 200;
            Octaves = 5;
            FlattenFactor = 10;
            FlattenEquator = true;
            EquatorFlatSigma = 150;
            ErosionIterations = 2500000;
            ErosionMaxDropletLifeTime = 100;
            ErosionInteria = 0.005;
            ErosionSedimentCapacityFactor = 20;
            ErosionDepositSpeed = 0.01;
            ErosionErodeSpeed = .03;
            ErosionDepositBrush = 3;
            ErosionErodeBrush = 3;
            BrushPointiness = 0.1;
            EnableErosion = true;
            Gravity = 10;
            EvaporateSpeed = 0.025;
            EnableLakeGeneration = true;
            LakesPerTile = 40;
            LakeVolumeMultiplier = 1;
        });

    }
}

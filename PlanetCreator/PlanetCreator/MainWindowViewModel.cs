using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public List<int> TileWidths { get; } = new List<int> { 2048, 4096 };

        int _tileWidth = 2048;
        public int TileWidth
        {
            get => _tileWidth;
            set => SetProp(ref _tileWidth, value);
        }

        bool _sessionLocked;
        public bool SessionLocked
        {
            get => _sessionLocked;
            set
            {
                if (SetProp(ref _sessionLocked, value))
                    OnPropertyChanged(nameof(SessionLockString));
            }
        }

        string _workingDir;
        public string WorkingDir
        {
            get => _workingDir;
            set => SetProp(ref _workingDir, value);
        }

        public string SessionLockString => SessionLocked ? "Unlock Session" : "Lock Session";

        public ICommand WorkingDirCommand => new RelayCommand(o =>
        {
            var dlg = new SaveFileDialog();
            dlg.FileName = "output.png";
            if (dlg.ShowDialog() == true)
            {
                WorkingDir = Path.GetDirectoryName(dlg.FileName);
            }
        }, o=> !SessionLocked);

        public ICommand LockSessionCommand => new RelayCommand(o =>
        {
            if (!Directory.Exists(WorkingDir))
            {
                var res = MessageBox.Show("Working Directory doesn't exist. Try to create it?", "Questing", MessageBoxButton.YesNo);
                if (res != MessageBoxResult.Yes) return;
                try
                {
                    Directory.CreateDirectory(WorkingDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            OnPropertyChanged(nameof(SessionLockString));
            SessionLocked = !SessionLocked;

            SelectedBackup = null;
            Backups.Clear();
            if (SessionLocked)
            {
                // Update Backups List
                var dirs = Directory.GetDirectories(WorkingDir,"Backup_*").Select(Path.GetFileName);
                foreach (var dir in dirs)
                {
                    Path.GetDirectoryName(dir);
                    if (dir.StartsWith("Backup_"))
                    {
                        Backups.Add(dir.Substring("Backup_".Length));
                    }
                }

                if (OverlayBitmapUp == null || OverlayBitmapUp.Width != TileWidth)
                {
                    OverlayBitmapUp = new WriteableBitmap(TileWidth, TileWidth, 96, 96, PixelFormats.Bgra32, null);
                    OnPropertyChanged(nameof(OverlayBitmapUp));
                }
                if (OverlayBitmapDown == null || OverlayBitmapDown.Width != TileWidth)
                {
                    OverlayBitmapDown = new WriteableBitmap(TileWidth, TileWidth, 96, 96, PixelFormats.Bgra32, null);
                    OnPropertyChanged(nameof(OverlayBitmapDown));
                }
                if (OverlayBitmapLeft == null || OverlayBitmapLeft.Width != TileWidth)
                {
                    OverlayBitmapLeft = new WriteableBitmap(TileWidth, TileWidth, 96, 96, PixelFormats.Bgra32, null);
                    OnPropertyChanged(nameof(OverlayBitmapLeft));
                }
                if (OverlayBitmapRight == null || OverlayBitmapRight.Width != TileWidth)
                {
                    OverlayBitmapRight = new WriteableBitmap(TileWidth, TileWidth, 96, 96, PixelFormats.Bgra32, null);
                    OnPropertyChanged(nameof(OverlayBitmapRight));
                }
                if (OverlayBitmapFront == null || OverlayBitmapFront.Width != TileWidth)
                {
                    OverlayBitmapFront = new WriteableBitmap(TileWidth, TileWidth, 96, 96, PixelFormats.Bgra32, null);
                    OnPropertyChanged(nameof(OverlayBitmapFront));
                }
                if (OverlayBitmapBack == null || OverlayBitmapBack.Width != TileWidth)
                {
                    OverlayBitmapBack = new WriteableBitmap(TileWidth, TileWidth, 96, 96, PixelFormats.Bgra32, null);
                    OnPropertyChanged(nameof(OverlayBitmapBack));
                }
            }
        }, o => !IsBusy);

        public ObservableCollection<string> Backups { get; } = new();

        string _selectedBackup;
        public string SelectedBackup
        {
            get => _selectedBackup;
            set => SetProp(ref _selectedBackup, value);
        }

        string _newBackupName;
        public string NewBackupName
        {
            get => _newBackupName;
            set => SetProp(ref _newBackupName, value);
        }

        public ICommand LoadBackupCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var dir = Path.Combine(WorkingDir, "Backup_" + SelectedBackup);
                    if (!Directory.Exists(dir))
                    {
                        MessageBox.Show("This backup does not exist anymore!");
                        Backups.Remove(SelectedBackup);
                        SelectedBackup = null;
                        return;
                    }
                    var faces = Enum.GetValues<CubeMapFace>().ToList();
                    foreach (var face in faces)
                    {
                        if (IsCancellationRequested) return;
                        try
                        {
                            var source = Path.Combine(dir, (face + ".png").ToLower());
                            var destination = Path.Combine(WorkingDir, (face + ".png").ToLower());
                            if (File.Exists(source))
                                File.Copy(source, destination, true);

                            source = Path.Combine(dir, (face + "_mat.png").ToLower());
                            destination = Path.Combine(WorkingDir, (face + "_mat.png").ToLower());
                            if (File.Exists(source))
                                File.Copy(source, destination, true);

                            source = Path.Combine(dir, (face + "_lakes.png").ToLower());
                            destination = Path.Combine(WorkingDir, (face + "_lakes.png").ToLower());
                            if (File.Exists(source))
                                File.Copy(source, destination, true);

                            source = Path.Combine(dir, (face + "_worley.png").ToLower());
                            destination = Path.Combine(WorkingDir, (face + "_worley.png").ToLower());
                            if (File.Exists(source))
                                File.Copy(source, destination, true);

                            source = Path.Combine(dir, (face + "_sediments.png").ToLower());
                            destination = Path.Combine(WorkingDir, (face + "_sediments.png").ToLower());
                            if (File.Exists(source))
                                File.Copy(source, destination, true);

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        LoadPictures(WorkingDir, faces);
                    }));
                    try
                    {
                        var sedimentTxt = Path.Combine(dir, "sediments.txt");
                        if (File.Exists(sedimentTxt))
                            File.Copy(sedimentTxt, Path.Combine(WorkingDir, "sediments.txt"), true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                finally { IsBusy = false; }
            });
        }, o => SelectedBackup != null);

        public ICommand SaveBackupCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var dir = Path.Combine(WorkingDir, "Backup_" + NewBackupName);
                    Directory.CreateDirectory(dir);
                    var faces = Enum.GetValues<CubeMapFace>().ToList();
                    int hMapCount = 0;
                    foreach (var face in faces)
                    {
                        var destination = Path.Combine(dir, (face + ".png").ToLower());
                        var source = Path.Combine(WorkingDir, face + ".png");
                        if (File.Exists(source))
                        {
                            File.Copy(source, destination, true);
                            ++hMapCount;
                        }

                        destination = Path.Combine(dir, (face + "_mat.png").ToLower());
                        source = Path.Combine(WorkingDir, face + "_mat.png");
                        if (File.Exists(source))
                            File.Copy(source, destination, true);

                        destination = Path.Combine(dir, (face + "_lakes.png").ToLower());
                        source = Path.Combine(WorkingDir, face + "_lakes.png");
                        if (File.Exists(source))
                            File.Copy(source, destination, true);

                        destination = Path.Combine(dir, (face + "_worley.png").ToLower());
                        source = Path.Combine(WorkingDir, face + "_worley.png");
                        if (File.Exists(source))
                            File.Copy(source, destination, true);

                        destination = Path.Combine(dir, (face + "_sediments.png").ToLower());
                        source = Path.Combine(WorkingDir, face + "_sediments.png");
                        if (File.Exists(source))
                            File.Copy(source, destination, true);
                    }

                    var sedimentTxt = Path.Combine(WorkingDir, "sediments.txt");
                    if (File.Exists(sedimentTxt))
                        File.Copy(sedimentTxt, Path.Combine(dir, "sediments.txt"), true);

                    if (hMapCount > 0)
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            Backups.Add(NewBackupName);
                        }));
                    }
                    if (Directory.GetFiles(dir).Length == 0)
                        Directory.Delete(dir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                finally { IsBusy = false; }
            });
        }, o=> SessionLocked && !string.IsNullOrEmpty(NewBackupName) &&
        !Directory.Exists(Path.Combine(WorkingDir, NewBackupName)) );

        public void LoadPictures()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    LoadPictures(WorkingDir, Enum.GetValues<CubeMapFace>().ToList());
                }));
            }
            else
                LoadPictures(WorkingDir, Enum.GetValues<CubeMapFace>().ToList());
        }

        void LoadPictures(string dir, List<CubeMapFace> faces)
        {
            foreach (var face in faces)
            {
                if (_cts.Token.IsCancellationRequested) return;
                var tilePath = Path.Combine(dir, face + ".png");
                var overlayPath = Path.Combine(dir, face + "_overlay.png");
                switch (face)
                {
                    case CubeMapFace.Up:
                        try { TileUp = GetImage(tilePath); } catch { }
                        try { OverlayBitmapUp = GetWritableBitmap(overlayPath); } catch { }
                        break;
                    case CubeMapFace.Down:
                        try { TileDown = GetImage(tilePath); } catch { }
                        try { OverlayBitmapDown = GetWritableBitmap(overlayPath); } catch { }
                        break;
                    case CubeMapFace.Left:
                        try { TileLeft = GetImage(tilePath); } catch { }
                        try { OverlayBitmapLeft = GetWritableBitmap(overlayPath); } catch { }
                        break;
                    case CubeMapFace.Right:
                        try { TileRight = GetImage(tilePath); } catch { }
                        try { OverlayBitmapRight = GetWritableBitmap(overlayPath); } catch { }
                        break;
                    case CubeMapFace.Front:
                        try { TileFront = GetImage(tilePath); } catch { }
                        try { OverlayBitmapFront = GetWritableBitmap(overlayPath); } catch { }
                        break;
                    case CubeMapFace.Back:
                        try { TileBack = GetImage(tilePath); } catch { }
                        try { OverlayBitmapBack = GetWritableBitmap(overlayPath); } catch { }
                        break;
                }
            }
        }

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

        WriteableBitmap _overlayBitmapUp;
        public WriteableBitmap OverlayBitmapUp
        {
            get => _overlayBitmapUp;
            set => SetProp(ref _overlayBitmapUp, value);
        }
        private WriteableBitmap _overlayBitmapFront;
        public WriteableBitmap OverlayBitmapFront
        {
            get => _overlayBitmapFront;
            set => SetProp(ref _overlayBitmapFront, value);
        }

        private WriteableBitmap _overlayBitmapRight;
        public WriteableBitmap OverlayBitmapRight
        {
            get => _overlayBitmapRight;
            set => SetProp(ref _overlayBitmapRight, value);
        }

        private WriteableBitmap _overlayBitmapBack;
        public WriteableBitmap OverlayBitmapBack
        {
            get => _overlayBitmapBack;
            set => SetProp(ref _overlayBitmapBack, value);
        }

        private WriteableBitmap _overlayBitmapLeft;
        public WriteableBitmap OverlayBitmapLeft
        {
            get => _overlayBitmapLeft;
            set => SetProp(ref _overlayBitmapLeft, value);
        }

        private WriteableBitmap _overlayBitmapDown;
        public WriteableBitmap OverlayBitmapDown
        {
            get => _overlayBitmapDown;
            set => SetProp(ref _overlayBitmapDown, value);
        }

        public MainWindowViewModel()
        {
            WorkingDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Output");
        }

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
            _DebugClearPixels(face);
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
                bitmap.AddDirtyRect(new Int32Rect(0, 0, TileWidth, TileWidth));
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

        public List<CubeMapFace> CubeMapFaces => Enum.GetValues<CubeMapFace>().ToList();
        CubeMapFace _previewTile = CubeMapFace.Back;
        public CubeMapFace PreviewTile
        {
            get => _previewTile;
            set => SetProp(ref _previewTile, value);
        }

        bool _previewMode;
        public bool PreviewMode
        {
            get => _previewMode;
            set
            {
                if (SetProp(ref _previewMode, value) && !value)
                    LimitedPreview = false;
            }
        }

        bool _limitedPreview;
        public bool LimitedPreview
        {
            get => _limitedPreview;
            set
            {
                if (SetProp(ref _limitedPreview, value) && value)
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
                {
                    if (value)
                    {
                        _cts = new CancellationTokenSource();
                        Progress = 0;
                    }
                    OnPropertyChanged(nameof(NotBusy));
                }
            }
        }
        public bool NotBusy => !IsBusy;

        int _seed = 0;
        public int Seed
        {
            get => _seed;
            set => SetProp(ref _seed, value);
        }
        int _noiseScale = 150;
        public int NoiseScale
        {
            get => _noiseScale;
            set => SetProp(ref _noiseScale, value);
        }
        int _octaves = 8;
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

        //int _flattenFactor = 10;
        //public int FlattenFactor
        //{
        //    get => _flattenFactor;
        //    set
        //    {
        //        if (SetProp(ref _flattenFactor, value))
        //        {
        //            if (value > 100)
        //                EquatorFlatSigma = 100;
        //        }
        //    }
        //}

        bool _flattenEquator;
        public bool FlattenEquator
        {
            get => _flattenEquator;
            set => SetProp(ref _flattenEquator, value);
        }


        double _exponentialFlattenStrength = 1.4;
        public double ExponentialFlattenStrength
        {
            get => _exponentialFlattenStrength;
            set => SetProp(ref _exponentialFlattenStrength, value);
        }

        int _frecklesPerTile = 6;
        public int FrecklesPerTile
        {
            get => _frecklesPerTile;
            set => SetProp(ref _frecklesPerTile, value);
        }
        int _freckleRadius = 40;
        public int FreckleRadius
        {
            get => _freckleRadius;
            set => SetProp(ref _freckleRadius, value);
        }
        int _freckleSegments = 6;
        public int FreckleSegments
        {
            get => _freckleSegments;
            set => SetProp(ref _freckleSegments, value);
        }
        int _equatorFlatWidth = 10; // 5-50%
        public int EquatorFlatWidth
        {
            get => _equatorFlatWidth;
            set => SetProp(ref _equatorFlatWidth, value);
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

        //"G:\\Steam\\steamapps\\common\\SpaceEngineers\\Content\\Data\\PlanetDataFiles\\EarthLike";
        string _materialSource = "Enter Source PNG Folder";
        public string MaterialSource
        {
            get => _materialSource;
            set => SetProp(ref _materialSource, value);
        }

        int _lakeStampDepth = 100;
        public int LakeStampDepth
        {
            get => _lakeStampDepth;
            set => SetProp(ref _lakeStampDepth, value);
        }

        byte _lakeMatMapValue = 82;
        public byte LakeMatMapValue
        {
            get => _lakeMatMapValue;
            set => SetProp(ref _lakeMatMapValue, value);
        }

        int _lakeMode = 0;
        public bool LakeModeReplace
        {
            get => (_lakeMode == 0);
            set
            {
                if (value == LakeModeReplace) return;
                if (value)
                    _lakeMode = 0;
                else
                    _lakeMode = 1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LakeModeAdd));
            }
        }
        public bool LakeModeAdd
        {
            get => (_lakeMode == 1);
            set
            {
                if (value == LakeModeAdd) return;
                if (value)
                    _lakeMode = 1;
                else
                    _lakeMode = 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LakeModeReplace));
            }
        }

        int _worleyCells = 55;
        public int WorleyCells
        {
            get => _worleyCells;
            set => SetProp(ref _worleyCells, value);
        }
        bool _invertWorley;
        public bool InvertWorley
        {
            get => _invertWorley;
            set => SetProp(ref _invertWorley, value);
        }

        int _flattenLowsAmount = 100;
        public int FlattenLowsAmount
        {
            get => _flattenLowsAmount;
            set => SetProp(ref _flattenLowsAmount, value);
        }

        int _flattenPeaksAmount = 100;
        public int FlattenPeaksAmount
        {
            get => _flattenPeaksAmount;
            set => SetProp(ref _flattenPeaksAmount, value);
        }

        double _stretchHistogramMin = 0d;
        public double StretchHistogramMin
        {
            get => _stretchHistogramMin;
            set
            {
                if (SetProp(ref _stretchHistogramMin, value))
                {
                    if (StretchHistogramMax <= value)
                        StretchHistogramMax = value + 0.1;
                }
            }
        }

        int _freckleSeed = 0;
        public int FreckleSeed
        {
            get => _freckleSeed;
            set => SetProp(ref _freckleSeed, value);
        }

        double _stretchHistogramMax = 1d;
        public double StretchHistogramMax
        {
            get => _stretchHistogramMax;
            set
            {
                if (SetProp(ref _stretchHistogramMax, value))
                {
                    if (StretchHistogramMin >= value)
                        StretchHistogramMin = value - 0.1;
                }
            }
        }

        int _sedimentPlateCount = 4500;
        public int SedimentPlateCount
        {
            get => _sedimentPlateCount;
            set => SetProp(ref _sedimentPlateCount, value);
        }

        int _sedimentTypes = 64;
        public int SedimentTypes
        {
            get => _sedimentTypes;
            set => SetProp(ref _sedimentTypes, value);
        }

        bool _useSedimentLayers = false;
        public bool UseSedimentLayers
        {
            get => _useSedimentLayers;
            set => SetProp(ref _useSedimentLayers, value);
        }

        PlanetGenerator GetGenerator()
        {
            var generator = new PlanetGenerator();
            generator.DebugOverlay = this;
            generator.DebugMode = PreviewMode;
            generator.LimitedDebugMode = LimitedPreview;
            if (NoiseScale < 1 || NoiseScale > 65535) NoiseScale = 150;
            if (Octaves < 1 || Octaves > 25) Octaves = 8;
            if (ErosionIterations < 1) { ErosionIterations = 1; }
            if (ErosionMaxDropletLifeTime < 1) { ErosionMaxDropletLifeTime = 1; }
            if (ErosionInteria < 0) ErosionInteria = 0;
            if (ErosionSedimentCapacityFactor < 0) ErosionSedimentCapacityFactor = 0;
            if (ErosionDepositSpeed < 0) ErosionDepositSpeed = 0;
            if (ErosionDepositSpeed > 1) ErosionDepositSpeed = 1;
            if (ErosionErodeSpeed < 0) ErosionErodeSpeed = 0;
            if (ErosionErodeSpeed > 1) ErosionErodeSpeed = 1;
            if (ErosionDepositBrush < 0) ErosionDepositBrush = 0;
            if (ErosionErodeBrush < 0) ErosionErodeBrush = 0;
            if (BrushPointiness < 0) BrushPointiness = 0;
            if (BrushPointiness > 1) BrushPointiness = 1;
            if (Gravity < 0) Gravity = 0;
            if (EvaporateSpeed < 0) EvaporateSpeed = 0;
            if (EvaporateSpeed > 1) EvaporateSpeed = 1;
            if (LakeStampDepth < 0) LakeStampDepth = 0;
            if (LakeVolumeMultiplier > 100000) LakeVolumeMultiplier = 100000;
            if (FreckleRadius > 200) FreckleRadius = 200;
            if (FreckleRadius < 10) FreckleRadius = 10;
            if (FrecklesPerTile > 32) FrecklesPerTile = 32;
            if (FrecklesPerTile < 1) FrecklesPerTile = 1;
            generator.TileWidth = TileWidth;
            generator.WorkingDirectory = WorkingDir;
            generator.PreviewFace = PreviewTile;

            generator.ProgressChanged += (s, a) => { Progress = a.Progress; };

            return generator;
        }

        public ICommand SimplexNoiseFillMapCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            foreach (var face in CubeMapFaces)
            {
                Application.Current.Dispatcher.Invoke(() =>
                { DebugClearPixels(face); });
            }
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.GenerateSimplexHeightMaps(Seed, Octaves, NoiseScale, _cts.Token);
                    LoadPictures();
                }
                finally { IsBusy = false; }
            });

        });

        public ICommand WorleyNoiseFillMapCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            foreach (var face in CubeMapFaces)
                DebugClearPixels(face);
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.GenerateWorleyHeightMaps(Seed, WorleyCells, InvertWorley, _cts.Token);
                    LoadPictures();
                }
                finally
                { IsBusy = false; }
            });
        });

        public ICommand WorleyAddFrecklesCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.LimitedDebugMode = false;
                    gen.AddWorleyFreckles(FreckleSeed, FrecklesPerTile, WorleyCells, FreckleSegments, FreckleRadius, _cts.Token);
                    LoadPictures();
                }
                finally
                { IsBusy = false; }
            });
        });

        public ICommand HistomgramFlattenCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.FlattenHistogram(FlattenLowsAmount, FlattenPeaksAmount, _cts.Token);
                    LoadPictures();
                }
                finally
                { IsBusy = false; }
            });
        });

        public ICommand EquatorFlattenCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.ExponentialStretch(ExponentialFlattenStrength,
                        FlattenEquator ? EquatorFlatWidth : -1, _cts.Token);
                    LoadPictures();
                }
                finally
                { IsBusy = false; }
            });
        });

        public ICommand HistomgramStretchCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.StrechHistogram(StretchHistogramMin, StretchHistogramMax, _cts.Token);
                    LoadPictures();
                }
                finally
                { IsBusy = false; }
            });
        });

        public ICommand InvertCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.Invert(_cts.Token);
                    LoadPictures();
                }
                finally
                { IsBusy = false; }
            });
        });

        public ICommand GenerateSedimentLayersCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    var gen = GetGenerator();
                    gen.GenerateSedimentLayers(Seed, SedimentTypes, SedimentPlateCount, _cts.Token);
                }
                finally
                { IsBusy = false; }
            });

        });

        public ICommand ErodeHydraulicCommand => new RelayCommand(o =>
        {
            var gen = GetGenerator();
            IsBusy = true;
            Task.Run(() =>
            {
                try
                {
                    gen.InitErosion(UseSedimentLayers, _cts.Token);
                    int iterationCount = 0;
                    Task.Run(async () =>
                    {
                        while (IsBusy && !_cts.Token.IsCancellationRequested && iterationCount < ErosionIterations)
                        {
                            try
                            {
                                await Task.Delay(100, _cts.Token);
                                Progress = (int)(.5 + (100.0 * iterationCount) / ErosionIterations);
                            }
                            catch
                            {
                                Progress = 100;
                                break;
                            }
                        }
                    });
                    Parallel.For(0, ErosionIterations, gen.POptions(_cts.Token), pit =>
                    {
                        gen.Erode(_cts.Token);
                        Interlocked.Increment(ref iterationCount);
                    });
                    gen.FinishErode(_cts.Token);
                    LoadPictures();
                }
                catch { return; } // Cancelled
                finally
                { IsBusy = false; }
            });
        });

        public ICommand AddLakesCommand => new RelayCommand(o =>
        {
            IsBusy = true;
            foreach (var face in CubeMapFaces)
            {
                Application.Current.Dispatcher.Invoke(() =>
                { DebugClearPixels(face); });
            }
            Task.Run(() =>
            {
                try
                {
                    var faces = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToList();
                    if (PreviewMode) faces = new List<CubeMapFace> { PreviewTile };
                    var gen = GetGenerator();
                    gen.AddLakes(LakesPerTile, LakeVolumeMultiplier, LakeStampDepth, MaterialSource, LakeMatMapValue, LakeModeAdd, _cts.Token);
                    LoadPictures();
                }
                catch { }
                finally
                { IsBusy = false; }
            });
        });

        bool _addingLake;
        public bool AddingLake
        {
            get => _addingLake;
            set
            {
                if (SetProp(ref _addingLake, value))
                {
                    IsBusy = value;
                    BusyText = value ? "Right click on a dark spot on the map.\r\nLeft click to abort." : null;
                }
            }
        }

        string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProp(ref _busyText, value);
        }

        public ICommand AddLakeByClickCommand => new RelayCommand(o =>
        {
            AddingLake = true;
        });

        public void HandleLakeAddClick(CubeMapFace face, int x, int y)
        {
            Debug.WriteLine(face + "," +x + "," +y);
            MessageBox.Show("Not implemented, WIP");
            IsBusy = false;
        }

        bool IsCancellationRequested => _cts.IsCancellationRequested == true;
        CancellationTokenSource _cts = new CancellationTokenSource();
        public ICommand AbortCommand => new RelayCommand(o =>
        {
            try { _cts?.Cancel(); } catch { }
        }, o => IsBusy);

        private void Generator_ProgressChanged(object sender, ProgressEventArgs e)
        {
            Progress = e.Progress;
        }

        BitmapImage GetImage(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                    return null;

                var imageBytes = System.IO.File.ReadAllBytes(filename);
                var stream = new MemoryStream(imageBytes);
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.StreamSource = stream;
                img.EndInit();
                return img;
            }
            catch { return null; }
        }

        WriteableBitmap GetWritableBitmap(string fileName)
        {
            try
            {
                if (!File.Exists(fileName)) return null;
                var image = Image.Load(fileName);
                if (image.Width != TileWidth)
                {
                    image.Mutate(k => k.Resize(TileWidth, TileWidth, KnownResamplers.NearestNeighbor));
                }
                Image<Rgba32> rgba = image as Image<Rgba32>;
                if (rgba == null)
                {
                    rgba = image.CloneAs<Rgba32>();
                    image.Dispose();
                }
                var bmp = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);
                bmp.Lock();
                try
                {
                    rgba.ProcessPixelRows(accessor =>
                    {
                        var backBuffer = bmp.BackBuffer;

                        for (var y = 0; y < rgba.Height; y++)
                        {
                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                            for (var x = 0; x < rgba.Width; x++)
                            {
                                var backBufferPos = backBuffer + (y * rgba.Width + x) * 4;
                                var val = pixelRow[x];
                                var color = val.A << 24 | val.R << 16 | val.G << 8 | val.B;

                                System.Runtime.InteropServices.Marshal.WriteInt32(backBufferPos, color);
                            }
                        }
                    });
                    bmp.AddDirtyRect(new Int32Rect(0, 0, rgba.Width, rgba.Height));
                }
                finally
                {
                    bmp.Unlock();
                }
                return bmp;
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
                    var image = Image.Load(fileName) as Image<L16>;
                    double[,] imageData = new double[image.Width, image.Width];
                    ImageHelper.ExtractPixels(image, imageData);
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
                    var imageData = images[face];
                    var width = imageData.GetLength(0);
                    var image = new SixLabors.ImageSharp.Image<L16>(width, width);
                    ImageHelper.InsertPixels(image, imageData);
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

    }
}

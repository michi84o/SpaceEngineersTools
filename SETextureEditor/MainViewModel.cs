using MathNet.Numerics.Distributions;
using MathNet.Numerics.Providers.LinearAlgebra;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using static System.Reflection.Metadata.BlobBuilder;

namespace SETextureEditor
{
    public class MainViewModel : PropChangeNotifier
    {
        string _lastFileName;
        string _tempDir;

        int _textureWidth = 2048;
        public int TextureWidth
        {
            get => _textureWidth;
            set => SetProp(ref _textureWidth, value);
        }
        int _textureHeight = 2048;
        public int TextureHeight
        {
            get => _textureHeight;
            set => SetProp(ref _textureHeight, value);
        }

        #region XZ
        WriteableBitmap _textureRgbXZ;
        public WriteableBitmap TextureRgbXZ
        {
            get => _textureRgbXZ;
            set
            {
                if (SetProp(ref _textureRgbXZ, value))
                {
                    if (value != null)
                    {
                        TextureWidth = value.PixelWidth;
                        TextureHeight = value.PixelHeight;
                    }
                    else
                    {
                        TextureWidth = 2048;
                        TextureHeight = 2048;
                    }
                }
            }
        }

        WriteableBitmap _textureMetalnessXZ;
        public WriteableBitmap TextureMetalnessXZ
        {
            get => _textureMetalnessXZ;
            set => SetProp(ref _textureMetalnessXZ, value);
        }

        WriteableBitmap _textureAmbientOcclusionXZ;
        public WriteableBitmap TextureAmbientOcclusionXZ
        {
            get => _textureAmbientOcclusionXZ;
            set => SetProp(ref _textureAmbientOcclusionXZ, value);
        }

        WriteableBitmap _textureEmissivenessXZ;
        public WriteableBitmap TextureEmissivenessXZ
        {
            get => _textureEmissivenessXZ;
            set => SetProp(ref _textureEmissivenessXZ, value);
        }

        WriteableBitmap _texturePaintabilityXZ;
        public WriteableBitmap TexturePaintabilityXZ
        {
            get => _texturePaintabilityXZ;
            set => SetProp(ref _texturePaintabilityXZ, value);
        }

        WriteableBitmap _textureNormalXZ;
        public WriteableBitmap TextureNormalXZ
        {
            get => _textureNormalXZ;
            set => SetProp(ref _textureNormalXZ, value);
        }

        WriteableBitmap _textureGlossXZ;
        public WriteableBitmap TextureGlossXZ
        {
            get => _textureGlossXZ;
            set => SetProp(ref _textureGlossXZ, value);
        }
        #endregion

        #region Y
        WriteableBitmap _textureRgbY;
        public WriteableBitmap TextureRgbY
        {
            get => _textureRgbY;
            set
            {
                if (SetProp(ref _textureRgbY, value))
                {
                    if (value != null)
                    {
                        TextureWidth = value.PixelWidth;
                        TextureHeight = value.PixelHeight;
                    }
                    else
                    {
                        TextureWidth = 2048;
                        TextureHeight = 2048;
                    }
                }
            }
        }

        WriteableBitmap _textureMetalnessY;
        public WriteableBitmap TextureMetalnessY
        {
            get => _textureMetalnessY;
            set => SetProp(ref _textureMetalnessY, value);
        }

        WriteableBitmap _textureAmbientOcclusionY;
        public WriteableBitmap TextureAmbientOcclusionY
        {
            get => _textureAmbientOcclusionY;
            set => SetProp(ref _textureAmbientOcclusionY, value);
        }

        WriteableBitmap _textureEmissivenessY;
        public WriteableBitmap TextureEmissivenessY
        {
            get => _textureEmissivenessY;
            set => SetProp(ref _textureEmissivenessY, value);
        }

        WriteableBitmap _texturePaintabilityY;
        public WriteableBitmap TexturePaintabilityY
        {
            get => _texturePaintabilityY;
            set => SetProp(ref _texturePaintabilityY, value);
        }

        WriteableBitmap _textureNormalY;
        public WriteableBitmap TextureNormalY
        {
            get => _textureNormalY;
            set => SetProp(ref _textureNormalY, value);
        }

        WriteableBitmap _textureGlossY;
        public WriteableBitmap TextureGlossY
        {
            get => _textureGlossY;
            set => SetProp(ref _textureGlossY, value);
        }
        #endregion

        bool _skipYAxis = false;
        public bool SkipYAxis
        {
            get => _skipYAxis;
            set => SetProp(ref _skipYAxis, value);
        }

        bool _loadDistanceTextures = false;
        public bool LoadDistanceTextures
        {
            get => _loadDistanceTextures;
            set => SetProp(ref _loadDistanceTextures, value);
        }

        public Action AutoscaleAction { get; set; }

        public MainViewModel()
        {
            #region Initialize Temp Directory
            var exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            try
            {
                var settingsFile = System.IO.Path.Combine(exeDir, "Settings.xml");
                XDocument doc = XDocument.Load(settingsFile);
                var tempDirValue = doc.Root.Element("tempDir").Value;
                if (!Path.IsPathRooted(tempDirValue))
                {
                    _tempDir = Path.Combine(exeDir, tempDirValue);
                }
                if (!Directory.Exists(_tempDir))
                {
                    Directory.CreateDirectory(_tempDir);
                }
            }
            catch
            {
                try
                {
                    _tempDir = System.IO.Path.Combine(exeDir, "temp");
                    if (!Directory.Exists(_tempDir))
                    {
                        Directory.CreateDirectory(_tempDir);
                    }
                }
                catch
                {
                    MessageBox.Show("Failed to load path of temporary folder from settings file!\r\nTrying to use local temp directory failed!\r\nPlease make sure you have write privileges", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
            #endregion
        }

        public ICommand OpenFileCommand => new RelayCommand(o =>
        {
            _fileNamePatterns = new string[]
            {
                // XZ axis
                "_ForAxisXZ_add.dds",
                "_ForAxisXZ_cm.dds",
                "_ForAxisXZ_distance_add.dds",
                "_ForAxisXZ_distance_cm.dds",
                "_ForAxisXZ_distance_ng.dds",
                "_ForAxisXZ_ng.dds",
                // Y axis
                "_ForAxisY_add.dds",
                "_ForAxisY_cm.dds",
                "_ForAxisY_distance_add.dds",
                "_ForAxisY_distance_cm.dds",
                "_ForAxisY_distance_ng.dds",
                "_ForAxisY_ng.dds"
            };

            var localDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var texconvPath = System.IO.Path.Combine(localDir, "texconv.exe");
            if (!System.IO.File.Exists(texconvPath))
            {
                MessageBox.Show("texconv.exe not found in the application directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DDS Files (*.dds)|*.dds|All Files (*.*)|*.*",
                Title = "Open Texture File"
            };
            if (openFileDialog.ShowDialog() != true)
                return;

            TextureRgbXZ = null;
            TexturePaintabilityXZ = null;
            TextureAmbientOcclusionXZ = null;
            TextureEmissivenessXZ = null;
            TextureMetalnessXZ = null;
            TextureGlossXZ = null;
            TextureNormalXZ = null;

            TextureRgbY = null;
            TexturePaintabilityY = null;
            TextureAmbientOcclusionY = null;
            TextureEmissivenessY = null;
            TextureMetalnessY = null;
            TextureGlossY = null;
            TextureNormalY = null;

            var fullFileName = openFileDialog.FileName;
            var fileName = System.IO.Path.GetFileName(fullFileName);
            _lastFileName = fullFileName;

            // Try to match group of files
            string groupName = null;
            foreach (var pattern in _fileNamePatterns)
            {
                if (fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    // Found a matching file
                    groupName = fileName.Substring(0, fileName.Length - pattern.Length);
                    break;
                }
            }

            bool fallback = false;
            if (groupName == null)
            {
                // Fallback. Here are some example file names:
                // AlienGreenGrass_cm.dds --> CM file
                // EarthIce_Far1_cm.dds --> distance file

                if (fileName.EndsWith("_cm.dds", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("_ng.dds", StringComparison.OrdinalIgnoreCase))
                {
                    groupName = fileName.Substring(0, fileName.Length - "_cm.dds".Length);
                    SkipYAxis = true;
                }
                else if (fileName.EndsWith("_add.dds", StringComparison.OrdinalIgnoreCase))
                {
                    groupName = fileName.Substring(0, fileName.Length - "_add.dds".Length);
                    SkipYAxis = true;
                }
                if (groupName != null)
                {
                    var list = _fileNamePatterns.ToList();
                    list.RemoveAll(p => p.Contains("ForAxisY"));
                    for (int i = 0; i < list.Count; ++i)
                    {
                        list[i] = list[i].Replace("_ForAxisXZ", "");
                    }
                    _fileNamePatterns = list.ToArray();
                    fallback = true;
                }
            }

            if (groupName == null)
            {
                MessageBox.Show("Selected file does not match any known texture group patterns.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var folder = System.IO.Path.GetDirectoryName(fullFileName);
            foreach (var pattern in _fileNamePatterns)
            {
                var fileToLoad = System.IO.Path.Combine(folder, groupName + pattern);
                if (File.Exists(fileToLoad))
                {
                    if (LoadDistanceTextures != pattern.Contains("distance")) continue;
                    if (SkipYAxis && !pattern.Contains("XZ") && !fallback) continue;

                    if (pattern.Contains("add")) // ADDITIVE
                    {
                        // R: Ambient Occlusion
                        // G: Emmissiveness
                        // B: unused
                        // A: Paintablity
                        var textures = LoadDDS(fileToLoad, new TextureLoadMode[] { TextureLoadMode.R, TextureLoadMode.G, TextureLoadMode.A });
                        if (textures != null)
                        {
                            if (pattern.Contains("XZ") || fallback)
                            {
                                TextureAmbientOcclusionXZ = textures[0];
                                TextureEmissivenessXZ = textures[1];
                                TexturePaintabilityXZ = textures[2];
                            }
                            else if (pattern.Contains("Y"))
                            {
                                TextureAmbientOcclusionY = textures[0];
                                TextureEmissivenessY = textures[1];
                                TexturePaintabilityY = textures[2];
                            }
                        }
                    }
                    else if (pattern.Contains("cm")) // COLOR METALNESS
                    {
                        // RGB: Material Color
                        // A: Metalness
                        var textures = LoadDDS(fileToLoad, new TextureLoadMode[] { TextureLoadMode.RGB, TextureLoadMode.A });
                        if (textures != null)
                        {
                            if (pattern.Contains("XZ") || fallback)
                            {
                                TextureRgbXZ = textures[0];
                                TextureMetalnessXZ = textures[1];
                            }
                            else if (pattern.Contains("Y"))
                            {
                                TextureRgbY = textures[0];
                                TextureMetalnessY = textures[1];
                            }
                        }
                    }
                    else if (pattern.Contains("ng")) // NORMAL GLOSS
                    {
                        // RGB: Normal Map
                        // A: Gloss
                        var textures = LoadDDS(fileToLoad, new TextureLoadMode[] { TextureLoadMode.RGB, TextureLoadMode.A });
                        if (textures != null)
                        {
                            if (pattern.Contains("XZ") || fallback)
                            {
                                TextureNormalXZ = textures[0];
                                TextureGlossXZ = textures[1];
                            }
                            else if (pattern.Contains("Y"))
                            {
                                TextureNormalY = textures[0];
                                TextureGlossY = textures[1];
                            }
                        }
                    }
                }
            }
            AutoscaleAction?.Invoke();

        });

        string[] _fileNamePatterns = new string[]
        {
            // XZ axis
            "_ForAxisXZ_add.dds",
            "_ForAxisXZ_cm.dds",
            "_ForAxisXZ_distance_add.dds",
            "_ForAxisXZ_distance_cm.dds",
            "_ForAxisXZ_distance_ng.dds",
            "_ForAxisXZ_ng.dds",
            // Y axis
            "_ForAxisY_add.dds",
            "_ForAxisY_cm.dds",
            "_ForAxisY_distance_add.dds",
            "_ForAxisY_distance_cm.dds",
            "_ForAxisY_distance_ng.dds",
            "_ForAxisY_ng.dds"
        };

        enum TextureLoadMode
        {
            RGBA,
            RGB,
            R,
            G,
            B,
            A
        }

        WriteableBitmap[] LoadDDS(string filePath, TextureLoadMode[] loadModes)
        {
            try
            {
                var localDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var texconvPath = System.IO.Path.Combine(localDir, "texconv.exe");
                // Step 1 convert using texconv
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = texconvPath;
                process.StartInfo.Arguments = $"\"{filePath}\" -y -ft png -o \"{_tempDir}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                var targetFileName = System.IO.Path.Combine(_tempDir, System.IO.Path.GetFileNameWithoutExtension(filePath) + ".png");
                var loaded = SixLabors.ImageSharp.Image.Load(targetFileName);
                var image = loaded as Image<Rgba32>;
                if (image == null)
                {
                    image = loaded.CloneAs<Rgba32>();
                    loaded.Dispose();
                }

                List<WriteableBitmap> bitmaps = new List<WriteableBitmap>();
                foreach (var loadMode in loadModes)
                {
                    var bmp = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);
                    bitmaps.Add(bmp);
                    bmp.Lock();
                }
                try
                {
                    image.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < image.Height; y++)
                        {
                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                            for (var x = 0; x < image.Width; x++)
                            {
                                for (int i = 0; i < loadModes.Length; i++)
                                {
                                    var loadMode = loadModes[i];
                                    var bmp = bitmaps[i];
                                    var backBuffer = bmp.BackBuffer;
                                    var backBufferPos = backBuffer + (y * image.Width + x) * 4;
                                    var val = pixelRow[x];
                                    int color = 0;
                                    switch (loadMode)
                                    {
                                        case TextureLoadMode.RGBA:
                                            color = val.R << 16 | val.G << 8 | val.B | val.A << 24;
                                            break;
                                        case TextureLoadMode.RGB:
                                            color = val.R << 16 | val.G << 8 | val.B | 0xFF << 24;
                                            break;
                                        case TextureLoadMode.R:
                                            color = val.R << 16 | val.R << 8 | val.R | 0xFF << 24;
                                            break;
                                        case TextureLoadMode.G:
                                            color = val.G << 16 | val.G << 8 | val.G | 0xFF << 24;
                                            break;
                                        case TextureLoadMode.B:
                                            color = val.B << 16 | val.B << 8 | val.B | 0xFF << 24;
                                            break;
                                        case TextureLoadMode.A:
                                            color = val.A << 16 | val.A << 8 | val.A | 0xFF << 24;
                                            break;
                                    }
                                    System.Runtime.InteropServices.Marshal.WriteInt32(backBufferPos, color);
                                }
                            }
                        }
                    });
                    image.Dispose();
                }
                finally
                {
                    foreach (WriteableBitmap bmp in bitmaps)
                    {
                        bmp.AddDirtyRect(new Int32Rect(0, 0, image.Width, image.Height));
                        bmp.Unlock();
                    }
                }

                return bitmaps.ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load texture: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }


        #region XZ Commands
        public ICommand CopyRgbXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureRgbXZ);
        }, o => TextureRgbXZ != null);

        public ICommand CopyMetalnessXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureMetalnessXZ);
        }, o => TextureMetalnessXZ != null);

        public ICommand CopyAmbientOcclusionXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureAmbientOcclusionXZ);
        }, o => TextureAmbientOcclusionXZ != null);

        public ICommand CopyEmissivenessXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureEmissivenessXZ);
        }, o => TextureEmissivenessXZ != null);

        public ICommand CopyPaintabilityXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TexturePaintabilityXZ);
        }, o => TexturePaintabilityXZ != null);

        public ICommand CopyNormalXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureNormalXZ);
        }, o => TextureNormalXZ != null);

        public ICommand CopyGlossXZCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureGlossXZ);
        }, o => TextureGlossXZ != null);
        #endregion

        #region Y Commands
        public ICommand CopyRgbYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureRgbY);
        }, o => TextureRgbY != null);

        public ICommand CopyMetalnessYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureMetalnessY);
        }, o => TextureMetalnessY != null);

        public ICommand CopyAmbientOcclusionYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureAmbientOcclusionY);
        }, o => TextureAmbientOcclusionY != null);

        public ICommand CopyEmissivenessYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureEmissivenessY);
        }, o => TextureEmissivenessY != null);

        public ICommand CopyPaintabilityYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TexturePaintabilityY);
        }, o => TexturePaintabilityY != null);

        public ICommand CopyNormalYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureNormalY);
        }, o => TextureNormalY != null);

        public ICommand CopyGlossYCommand => new RelayCommand(o =>
        {
            Clipboard.SetImage(TextureGlossY);
        }, o => TextureGlossY != null);
        #endregion

        DateTime _lastChecked = DateTime.Now;
        bool _lastCheck = false;
        bool ContainsImage()
        {
            if ((DateTime.Now - _lastChecked).TotalMilliseconds < 100)
                return _lastCheck; // Prevent too frequent checks
            _lastChecked = DateTime.Now;
            _lastCheck = Clipboard.ContainsImage();
            return _lastCheck;
        }

        WriteableBitmap GetFromClipBoard(TextureLoadMode mode)
        {
            try
            {
                if (!Clipboard.ContainsImage())
                    return null;

                var image = Clipboard.GetImage();
                if (image == null)
                    return null;

                FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap(
                    image,
                    PixelFormats.Bgra32,
                    null, // Standard-Palette
                    0);   // Dithering-Options

                WriteableBitmap bitmap = new WriteableBitmap(convertedBitmap);

                if (mode == TextureLoadMode.RGBA)
                    return bitmap;

                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                int stride = bitmap.BackBufferStride;

                bitmap.Lock();

                unsafe
                {
                    byte* pStart = (byte*)bitmap.BackBuffer;

                    for (int y = 0; y < height; y++)
                    {
                        byte* pLine = pStart + y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            var g = pLine[x * 4 + 1]; // Use green channel as gray value
                            switch (mode)
                            {
                                case TextureLoadMode.RGB:
                                    pLine[x * 4 + 3] = 0xFF; // Alpha
                                    break;
                                case TextureLoadMode.R:
                                case TextureLoadMode.G:
                                case TextureLoadMode.B:
                                case TextureLoadMode.A:
                                    pLine[x * 4] = g; // Blue
                                    pLine[x * 4 + 2] = g; // Red
                                    pLine[x * 4 + 3] = 0xFF; // Alpha
                                    break;
                            }
                        }
                    }
                }
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                bitmap.Unlock();

                return bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to get image from clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

        }

        #region XZ Commands Paste
        public ICommand PasteRgbXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.RGB);
            if (img != null)
            {
                TextureRgbXZ = img;
            }
        }, o=> ContainsImage());

        public ICommand PasteMetalnessXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.A);
            if (img != null)
            {
                TextureMetalnessXZ = img;
            }
        }, o => ContainsImage());

        public ICommand PasteAmbientOcclusionXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.R);
            if (img != null)
            {
                TextureAmbientOcclusionXZ = img;
            }
        }, o => ContainsImage());

        public ICommand PasteEmissivenessXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.G);
            if (img != null)
            {
                TextureEmissivenessXZ = img;
            }
        }, o => ContainsImage());

        public ICommand PastePaintabilityXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.A);
            if (img != null)
            {
                TexturePaintabilityXZ = img;
            }
        }, o => ContainsImage());

        public ICommand PasteNormalXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.RGB);
            if (img != null)
            {
                TextureNormalXZ = img;
            }
        }, o => ContainsImage());

        public ICommand PasteGlossXZCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.A);
            if (img != null)
            {
                TextureGlossXZ = img;
            }
        }, o => ContainsImage());
        #endregion

        #region Y Commands Paste
        public ICommand PasteRgbYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.RGB);
            if (img != null)
            {
                TextureRgbY = img;
            }
        }, o => ContainsImage());

        public ICommand PasteMetalnessYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.A);
            if (img != null)
            {
                TextureMetalnessY = img;
            }
        }, o => ContainsImage());

        public ICommand PasteAmbientOcclusionYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.R);
            if (img != null)
            {
                TextureAmbientOcclusionY = img;
            }
        }, o => ContainsImage());

        public ICommand PasteEmissivenessYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.G);
            if (img != null)
            {
                TextureEmissivenessY = img;
            }
        }, o => ContainsImage());

        public ICommand PastePaintabilityYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.A);
            if (img != null)
            {
                TexturePaintabilityY = img;
            }
        }, o => ContainsImage());

        public ICommand PasteNormalYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.RGB);
            if (img != null)
            {
                TextureNormalY = img;
            }
        }, o => ContainsImage());

        public ICommand PasteGlossYCommand => new RelayCommand(o =>
        {
            var img = GetFromClipBoard(TextureLoadMode.A);
            if (img != null)
            {
                TextureGlossY = img;
            }
        }, o => ContainsImage());
        #endregion

        public ICommand SaveCommand => new RelayCommand(o =>
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "DDS Files (*.dds)|*.dds|All Files (*.*)|*.*",
                Title = "Save Texture Files",
                FileName = _lastFileName,
            };
            if (saveFileDialog.ShowDialog() != true)
                return;

            var fullFileName = saveFileDialog.FileName;
            var fileName = System.IO.Path.GetFileName(fullFileName);

            string groupName = null;
            foreach (var pattern in _fileNamePatterns)
            {
                if (fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    // Found a matching file
                    groupName = fileName.Substring(0, fileName.Length - pattern.Length);
                    break;
                }
            }

            if (groupName == null)
            {
                MessageBox.Show("Selected file does not match any known texture group patterns.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dir = Path.GetDirectoryName(fullFileName);

            #region XZ
            // _ForAxisXZ_add.dds
            // Additive
            if (TextureAmbientOcclusionXZ != null &&
                TextureEmissivenessXZ != null &&
                TexturePaintabilityXZ != null)
            {
                var pngFileName = System.IO.Path.Combine(_tempDir, groupName + "_ForAxisXZ_add.png");
                var ddsFileName = System.IO.Path.Combine(dir, groupName + "_ForAxisXZ_add.dds");
                if (SaveAddTexture(pngFileName, TextureAmbientOcclusionXZ, TextureEmissivenessXZ, TexturePaintabilityXZ))
                {
                    PngToDDS(pngFileName, ddsFileName);
                }
            }
            // _ForAxisXZ_cm.dds
            if (TextureRgbXZ != null && TextureMetalnessXZ != null)
            {
                var pngFileName = System.IO.Path.Combine(_tempDir, groupName + "_ForAxisXZ_cm.png");
                var ddsFileName = System.IO.Path.Combine(dir, groupName + "_ForAxisXZ_cm.dds");
                if (SaveCmTexture(pngFileName, TextureRgbXZ, TextureMetalnessXZ))
                {
                    PngToDDS(pngFileName, ddsFileName);
                }
            }
            // _ForAxisXZ_ng.dds
            if (TextureNormalXZ != null && TextureGlossXZ != null)
            {
                var pngFileName = System.IO.Path.Combine(_tempDir, groupName + "_ForAxisXZ_ng.png");
                var ddsFileName = System.IO.Path.Combine(dir, groupName + "_ForAxisXZ_ng.dds");
                if (SaveNgTexture(pngFileName, TextureNormalXZ, TextureGlossXZ))
                {
                    PngToDDS(pngFileName, ddsFileName);
                }
            }
            #endregion

            #region Y
            // _ForAxisY_add.dds
            // Additive
            if (TextureAmbientOcclusionY != null &&
                TextureEmissivenessY != null &&
                TexturePaintabilityY != null)
            {
                var pngFileName = System.IO.Path.Combine(_tempDir, groupName + "_ForAxisY_add.png");
                var ddsFileName = System.IO.Path.Combine(dir, groupName + "_ForAxisY_add.dds");
                if (SaveAddTexture(pngFileName, TextureAmbientOcclusionY, TextureEmissivenessY, TexturePaintabilityY))
                {
                    PngToDDS(pngFileName, ddsFileName);
                }
            }
            // _ForAxisY_cm.dds
            if (TextureRgbY != null && TextureMetalnessY != null)
            {
                var pngFileName = System.IO.Path.Combine(_tempDir, groupName + "_ForAxisY_cm.png");
                var ddsFileName = System.IO.Path.Combine(dir, groupName + "_ForAxisY_cm.dds");
                if (SaveCmTexture(pngFileName, TextureRgbY, TextureMetalnessY))
                {
                    PngToDDS(pngFileName, ddsFileName);
                }
            }
            // _ForAxisY_ng.dds
            if (TextureNormalY != null && TextureGlossY != null)
            {
                var pngFileName = System.IO.Path.Combine(_tempDir, groupName + "_ForAxisY_ng.png");
                var ddsFileName = System.IO.Path.Combine(dir, groupName + "_ForAxisY_ng.dds");
                if (SaveNgTexture(pngFileName, TextureNormalY, TextureGlossY))
                {
                    PngToDDS(pngFileName, ddsFileName);
                }
            }
            #endregion

        });

        bool PngToDDS(string pngFileName, string ddsFileName)
        {
            try
            {
                var localDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var texconvPath = System.IO.Path.Combine(localDir, "texconv.exe");
                if (!System.IO.File.Exists(texconvPath))
                {
                    MessageBox.Show("texconv.exe not found in the application directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = texconvPath;
                process.StartInfo.Arguments = $"\"{pngFileName}\" -y -ft dds BC7_UNORM_SRGB -o \"{System.IO.Path.GetDirectoryName(ddsFileName)}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
                return File.Exists(ddsFileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert PNG to DDS: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        bool SaveNgTexture(string fileName,
            WriteableBitmap normal, WriteableBitmap gloss)
        {
            if (normal == null || gloss == null)
            {
                MessageBox.Show("Not all textures are available for saving NG texture.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            SixLabors.ImageSharp.Image<Rgba32> image = new SixLabors.ImageSharp.Image<Rgba32>(
                normal.PixelWidth, normal.PixelHeight);

            normal.Lock();
            gloss.Lock();
            try
            {
                image.ProcessPixelRows(accessor =>
                {
                    unsafe
                    {
                        byte* nStart = (byte*)normal.BackBuffer;
                        byte* gStart = (byte*)gloss.BackBuffer;
                        int stride = normal.BackBufferStride;

                        for (int y = 0; y < image.Height; y++)
                        {
                            byte* nLine = nStart + y * stride;
                            byte* gLine = gStart + y * stride;

                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                            for (int x = 0; x < image.Width; x++)
                            {
                                // RED, GREEN, BLUE Channels: Normal Map
                                // ALPHA Channel: Gloss
                                pixelRow[x] = new Rgba32(
                                    nLine[x * 4 + 2], // R
                                    nLine[x * 4 + 1], // G
                                    nLine[x * 4], // B
                                    gLine[x * 4 + 1]); // A: Gloss from green value
                            }
                        }
                    }
                });
                image.SaveAsPng(fileName, new PngEncoder
                {
                    ColorType = PngColorType.RgbWithAlpha
                }); // Save the image to the specified file
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                normal.Unlock();
                gloss.Unlock();
            }
        }

        bool SaveCmTexture(string fileName,
            WriteableBitmap rgb, WriteableBitmap metal)
        {

            if (rgb == null || metal == null)
            {
                MessageBox.Show("Not all textures are available for saving NG texture.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            SixLabors.ImageSharp.Image<Rgba32> image = new SixLabors.ImageSharp.Image<Rgba32>(
                rgb.PixelWidth, rgb.PixelHeight);

            rgb.Lock();
            metal.Lock();
            try
            {
                image.ProcessPixelRows(accessor =>
                {
                    unsafe
                    {
                        byte* rgbStart = (byte*)rgb.BackBuffer;
                        byte* mStart = (byte*)metal.BackBuffer;
                        int stride = rgb.BackBufferStride;

                        for (int y = 0; y < image.Height; y++)
                        {
                            byte* rgbLine = rgbStart + y * stride;
                            byte* mLine = mStart + y * stride;

                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                            for (int x = 0; x < image.Width; x++)
                            {
                                // RED, GREEN, BLUE Channels: Material Color
                                // ALPHA Channel: Metalness
                                pixelRow[x] = new Rgba32(
                                    rgbLine[x * 4 + 2], // R
                                    rgbLine[x * 4 + 1], // G
                                    rgbLine[x * 4], // B
                                    mLine[x * 4 + 1]); // A: Gloss from green value
                            }
                        }
                    }
                });
                image.SaveAsPng(fileName, new PngEncoder
                {
                    ColorType = PngColorType.RgbWithAlpha
                }); // Save the image to the specified file
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                rgb.Unlock();
                metal.Unlock();
            }
        }

        bool SaveAddTexture(
            string fileName,
            WriteableBitmap ambientOcclusion, WriteableBitmap emissiveness, WriteableBitmap paintability)
        {
            if (ambientOcclusion == null || emissiveness == null || paintability == null)
            {
                MessageBox.Show("Not all textures are available for saving ADD texture.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            SixLabors.ImageSharp.Image<Rgba32> image = new SixLabors.ImageSharp.Image<Rgba32>(
                ambientOcclusion.PixelWidth, ambientOcclusion.PixelHeight);

            ambientOcclusion.Lock();
            emissiveness.Lock();
            paintability.Lock();
            try
            {
                image.ProcessPixelRows(accessor =>
                {
                    unsafe
                    {
                        byte* ocStart = (byte*)ambientOcclusion.BackBuffer;
                        byte* emStart = (byte*)emissiveness.BackBuffer;
                        byte* paStart = (byte*)paintability.BackBuffer;
                        int stride = ambientOcclusion.BackBufferStride;

                        for (int y = 0; y < image.Height; y++)
                        {
                            byte* ocLine = ocStart + y * stride;
                            byte* emLine = emStart + y * stride;
                            byte* paLine = paStart + y * stride;

                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                            for (int x = 0; x < image.Width; x++)
                            {
                                // RED Channel: Ambient Occlusion
                                // GREEN Channel: Emissiveness
                                // BLUE Channel: Unused
                                // ALPHA Channel: Paintability
                                pixelRow[x] = new Rgba32(
                                    ocLine[x * 4], // R: Ambient Occlusion
                                    emLine[x * 4 + 1], // G: Emissiveness
                                    0, // B: Unused
                                    paLine[x * 4 + 1]); // A: Paintability from green value
                            }
                        }
                    }
                });
                image.SaveAsPng(fileName, new PngEncoder
                {
                    ColorType = PngColorType.RgbWithAlpha
                }); // Save the image to the specified file
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                ambientOcclusion.Unlock();
                emissiveness.Unlock();
                paintability.Unlock();
            }
        }
    }
}

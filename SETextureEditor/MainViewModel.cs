using Microsoft.Win32;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace SETextureEditor
{
    public class MainViewModel : PropChangeNotifier
    {
        string _lastFileName;

        string _tempDir;
        readonly string[] _fileNamePatterns = new string[]
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

        BitmapSource _textureRgb;
        public BitmapSource TextureRgb
        {
            get => _textureRgb;
            set
            {
                if (SetProp(ref _textureRgb, value))
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
                    AutoscaleAction?.Invoke();
                }
            }
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
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DDS Files (*.dds)|*.dds|All Files (*.*)|*.*",
                Title = "Open Texture File"
            };
            if (openFileDialog.ShowDialog() != true)
                return;

            // Try to match group of files
            string groupName = null;
            foreach (var pattern in _fileNamePatterns)
            {
                var fileName = openFileDialog.FileName;
                if (fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    // Found a matching file
                    groupName = fileName.Substring(0, fileName.Length - pattern.Length);
                    break;
                }
            }

            var loader = new TextureLoader();
            if (!loader.LoadTexture(openFileDialog.FileName, _tempDir))
            {
                return;
            }

            //TextureVm = new TextureViewModel
            //{
            //    Texture = loader.Texture,
            //};
            _lastFileName = openFileDialog.FileName;

        });
    }
}

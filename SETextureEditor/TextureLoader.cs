using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SETextureEditor
{
    class TextureLoader
    {
        Image<Rgba32> _image;
        public WriteableBitmap Texture;
        public WriteableBitmap TextureR;
        public WriteableBitmap TextureG;
        public WriteableBitmap TextureB;
        public WriteableBitmap TextureA;

        public bool LoadTexture(string filePath, string tempDir)
        {
            try
            {
                // Step 1 convert using texconv
                var localDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var texconvPath = System.IO.Path.Combine(localDir, "texconv.exe");
                if (!System.IO.File.Exists(texconvPath))
                {
                    MessageBox.Show("texconv.exe not found in the application directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = texconvPath;
                process.StartInfo.Arguments = $"\"{filePath}\" -y -ft png -o \"{tempDir}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                var targetFileName = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileNameWithoutExtension(filePath) + ".png");
                var image = SixLabors.ImageSharp.Image.Load(targetFileName);
                _image = image as Image<Rgba32>;
                if (_image == null)
                {
                    _image = image.CloneAs<Rgba32>();
                    image.Dispose();
                }
                var bmp = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);
                bmp.Lock();
                try
                {
                    _image.ProcessPixelRows(accessor =>
                    {
                        var backBuffer = bmp.BackBuffer;

                        for (var y = 0; y < _image.Height; y++)
                        {
                            Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                            for (var x = 0; x < _image.Width; x++)
                            {
                                var backBufferPos = backBuffer + (y * _image.Width + x) * 4;
                                var val = pixelRow[x];
                                var color = /*val.A << 24 |*/ val.R << 16 | val.G << 8 | val.B;
                                System.Runtime.InteropServices.Marshal.WriteInt32(backBufferPos, color);
                            }
                        }
                    });
                    bmp.AddDirtyRect(new Int32Rect(0, 0, _image.Width, _image.Height));
                }
                finally
                {
                    bmp.Unlock();
                }
                Texture = bmp;
                //Clipboard.SetImage(bmp);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load texture from {filePath}.\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}

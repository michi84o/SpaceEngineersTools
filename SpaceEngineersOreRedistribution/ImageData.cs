using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using SixLabors.ImageSharp.Formats.Png;
using System.Threading;
using SpaceEngineersToolsShared;

namespace SpaceEngineersOreRedistribution
{
    class ImageData : IDisposable
    {
        public CubeMapFace Face { get; }

        // One 24 bit RGB image with 2048x2048 is around 12MB.
        public byte[,] R { get; private set; }
        public byte[,] G { get; private set; }
        public byte[,] B { get; private set; }
        // Additional 4+4 MB for height map and gradients (compressed, original is ushort)
        public byte[,] H { get; private set; }
        public byte[,] HGrad { get; private set; }


        public static ImageData Create(string directory, CubeMapFace face, CancellationToken token)
        {
            string materialMapFile = Path.Combine(directory, face.ToString().ToLowerInvariant()+"_mat.png");
            string heightMapFile = Path.Combine(directory, face.ToString().ToLowerInvariant() + ".png");

            if (!File.Exists(materialMapFile)) return null;

            bool skipHeightMap = false;
            if (!File.Exists(heightMapFile)) skipHeightMap = true;

            var img1 = SixLabors.ImageSharp.Image.Load(materialMapFile);

            SixLabors.ImageSharp.Image img2 = null;
            if (!skipHeightMap)
                img2 = SixLabors.ImageSharp.Image.Load(heightMapFile);

            Image<Rgb24> matMap;
            Image<L16> hMap;

            if (img1 is Image<Rgb24>) matMap = (Image<Rgb24>)img1;
            else
            {
                matMap = img1.CloneAs<Rgb24>();
                img1.Dispose();
            }
            if (!skipHeightMap && img2 is Image<L16>) hMap = (Image<L16>)img2;
            else
            {
                hMap = new SixLabors.ImageSharp.Image<L16>(2048, 2048); // img1.CloneAs<L16>();
                img2?.Dispose();
            }

            if (matMap.Width != 2048 || hMap.Width != 2048)
            {
                matMap.Dispose();
                hMap.Dispose();
                MessageBox.Show("Sorry! Only images with 2048 pixels are supported!");
                return null;
            }

            if (token.IsCancellationRequested) return null;
            var retval = new ImageData(face);
            Parallel.For(0, 2048, x =>
            {
                Parallel.For(0, 2048, y =>
                {
                    if (token.IsCancellationRequested) return;
                    var pixVal = matMap[x, y];
                    retval.G[x, y] = pixVal.G;
                    retval.R[x, y] = pixVal.R;
                    retval.B[x, y] = pixVal.B;

                    var hPixVal = hMap[x, y];
                    retval.H[x, y] = (byte)(255.0 * hPixVal.PackedValue / 65535.0 + .5);
                });
            });
            // Gradients
            Parallel.For(1, 2047, x =>
            {
                Parallel.For(1, 2047, y =>
                {
                    if (token.IsCancellationRequested) return;
                    // Old Code: Just calc diff
                    //List<int> neighbors = new()
                    //{
                    //    hMap[x - 1, y].PackedValue,
                    //    hMap[x + 1, y].PackedValue,
                    //    hMap[x, y - 1].PackedValue,
                    //    hMap[x, y + 1].PackedValue
                    //};
                    //var currentPixel = hMap[x, y].PackedValue;
                    //int maxGradient = 0;
                    //foreach (var n in neighbors)
                    //{
                    //    var gradient = Math.Abs(currentPixel - n);
                    //    if (gradient > maxGradient) maxGradient = gradient;
                    //}
                    //maxGradient *= 20;
                    //if (maxGradient > 65535) maxGradient = 65535;
                    //retval.HGrad[x, y] = (byte)(255.0 * maxGradient / 65535.0 + 0.5);

                    // New Code: Calc degrees TODO: Not working
                    double dh = hMap[x - 1, y].PackedValue - hMap[x, y].PackedValue;
                    double dv = hMap[x, y - 1].PackedValue - hMap[x, y].PackedValue;

                    var pixelWidthMeter = 100000.0 / (2048 * 4); // 100km planet;
                    // 1 pixel = 12m ?
                    var verticalPixelWidthMeter = 3*655.35 / 65535; // 3cm?

                    var normal = My3dHelper.CalcNormal(
                        new System.Windows.Media.Media3D.Point3D(
                            -1 * pixelWidthMeter,
                            0,
                            hMap[x - 1, y].PackedValue * verticalPixelWidthMeter),
                        new System.Windows.Media.Media3D.Point3D(
                            0 ,
                            -1 * pixelWidthMeter,
                            hMap[x, y - 1].PackedValue * verticalPixelWidthMeter),
                        new System.Windows.Media.Media3D.Point3D(
                            x,
                            y,
                            hMap[x, y].PackedValue * verticalPixelWidthMeter));

                    if (normal.Z < 0)
                        normal *= -1;

                    var angle = Math.Atan(normal.Z / Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y));
                    double slopeAngleDeg = angle * 180 / Math.PI;

                    retval.HGrad[x, y] = (byte)(255 - slopeAngleDeg * 255.0 / 90); // Scale to 8-bit

                });
            });
            matMap.Dispose();
            hMap.Dispose();
            return retval;
        }

        public ImageData(CubeMapFace face)
        {
            Face = face;
            G = new byte[2048, 2048];
            R = new byte[2048, 2048];
            B = new byte[2048, 2048];
            H = new byte[2048, 2048];
            HGrad = new byte[2048, 2048];
        }

        public void Dispose()
        {
            // Might buffer images later
            R = B = G = H = HGrad = null;
        }

        public BitmapImage CreateBitmapImage(bool heightMap, bool ore, bool complexMaterials, int? selectedComplexMaterial, List<OreMapping> oreMappings, bool biomes, int? selectedBiome)
        {
            using MemoryStream memory = new MemoryStream();
            var image = new Image<Rgb24>(2048, 2048);
            Parallel.For(0, 2048, x =>
            {
                Parallel.For(0, 2048, y =>
                {
                    byte r, g, b;
                    // Gray background
                    byte[,] background = heightMap ? H : HGrad;
                    r = background[x, y];
                    g = background[x, y];
                    b = background[x, y];
                    // Biomes
                    if (biomes)
                    {
                        if (selectedBiome != null && G[x, y] == selectedBiome.Value)
                        {
                            r = b = 255;
                            g = 0;
                        }
                        else
                        {
                            double blend = 0.2;
                            var newVal = (int)((blend * G[x, y]) + (1 - blend) * g + .5);
                            if (newVal > 255) newVal = 255;
                            g = (byte)newVal;
                            var rb = (int)((1 - blend) * g + 0.5);
                            if (rb > 255) rb = 255;
                            r = b = (byte)rb;
                        }
                    }
                    // Lakes
                    if (complexMaterials && selectedComplexMaterial != null)
                    {
                        if (R[x, y] == selectedComplexMaterial.Value)
                        {
                            double blend = 0.4;
                            var newRed = (int)((blend * 82) + (1 - blend) * r + .5);
                            if (newRed > 255) newRed = 255;
                            r = (byte)newRed; g = 0; b = 0;
                        }
                    }
                    // Ore
                    if (ore && B[x,y] != 255)
                    {
                        if (oreMappings == null)
                        {
                            g = 255; r = 0; b = 0;
                        }
                        else
                        {
                            var mapping = oreMappings.FirstOrDefault(o => o.Value == B[x,y]);
                            if (mapping != null)
                            {
                                r = mapping.MapRgbValue.R;
                                g = mapping.MapRgbValue.G;
                                b = mapping.MapRgbValue.B;
                            }
                            else
                            {
                                r = 0; g = 0; b = 64;
                            }
                        }
                    }

                    image[x, y] = new Rgb24(r,g,b);
                    //image[x, y] = new Rgb24(R[x, y], G[x, y], B[x, y]);
                });
            });
            image.SaveAsBmp(memory);
            memory.Position = 0;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        public bool SaveMaterialMap(string filename)
        {
            var image = new Image<Rgb24>(2048, 2048);
            Parallel.For(0, 2048, x =>
            {
                Parallel.For(0, 2048, y =>
                {
                    image[x, y] = new Rgb24(R[x, y], G[x, y], B[x, y]);
                });
            });
            try
            {
                image.SaveAsPng(filename);
                return true;
            }
            catch { return false; }
        }

    }
}

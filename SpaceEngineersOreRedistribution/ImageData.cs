using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

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

        public List<(int, int, byte)> OrePixels { get; } = new();

        public static ImageData Create(string directory, CubeMapFace face, int tileWidth, CancellationToken token)
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
                hMap = new SixLabors.ImageSharp.Image<L16>(tileWidth, tileWidth); // img1.CloneAs<L16>();
                img2?.Dispose();
            }

            if (matMap.Width != tileWidth)
            {
                if (face == CubeMapFace.Back && matMap.Width > tileWidth)
                    MessageBox.Show("Loaded material maps larger than selected tile width!\r\nPossible loss of data due to downscaling!","Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                matMap.Mutate(k => k.Resize(tileWidth, tileWidth, KnownResamplers.NearestNeighbor));
            }
            if (hMap.Width != tileWidth)
            {
                hMap.Mutate(k => k.Resize(tileWidth, tileWidth, KnownResamplers.Triangle));
            }

            if (token.IsCancellationRequested) return null;
            var retval = new ImageData(face, tileWidth);
            Task t1 = Task.Run(() =>
            {
                matMap.ProcessPixelRows(row =>
                {
                    for (var y = 0; y < tileWidth; ++y)
                    {
                        if (token.IsCancellationRequested) return;
                        Span<Rgb24> pixelMRow = row.GetRowSpan(y);
                        for (var x = 0; x < matMap.Width; ++x)
                        {
                            var pixVal = pixelMRow[x];
                            retval.G[x, y] = pixVal.G;
                            retval.R[x, y] = pixVal.R;
                            retval.B[x, y] = pixVal.B;
                            if (pixVal.B != 255) // If not empty pixel
                            {
                                retval.OrePixels.Add((x, y, pixVal.B)); // Store ore pixel with value
                            }
                        }
                    }
                });
            });

            /*
             On a 100km planet, the height map difference between 0 and 65535 is 6.51km.
             On a 20km planet, the height map difference between 0 and 65535 is 1.3km.
             Max Diff Formula in meters: 65535 * radius(km) / 1000
             In other words, height diff per pixel in meter is planet diameter in meters divided by one million.
            */
            int numOfPixels = 6 * (tileWidth * tileWidth);
            int planetRadius = 100000; // 100km planet radius
            double averageAreaPerPixel = 4 * Math.PI * planetRadius * planetRadius * 1.0 / numOfPixels; // in m^2
            double averageWidthPerPixel = Math.Sqrt(averageAreaPerPixel); // in m
            double averageHeightPerPixel = planetRadius / 1000000d; // 10 centimeter

            Task t2 = Task.Run(() =>
            {
                hMap.ProcessPixelRows(hrow =>
                {
                    for (var y = 0; y < tileWidth; y++)
                    {
                        if (token.IsCancellationRequested) return;
                        Span<L16> pixelHRow = hrow.GetRowSpan(y);
                        Span<L16> pixelHRow2 = default;
                        if (y > 0)
                            pixelHRow2 = hrow.GetRowSpan(y - 1); ;
                        for (var x = 0; x < matMap.Width; ++x)
                        {
                            var curentVal = pixelHRow[x].PackedValue;
                            retval.H[x, y] = (byte)(255.0 * curentVal / 65535.0 + .5);

                            if (y > 0 && x > 0)
                            {
                                var valAbove = pixelHRow2[x].PackedValue;
                                var valLeft = pixelHRow[x - 1].PackedValue;
                                var normal = My3dHelper.CalcNormal(
                                    new System.Windows.Media.Media3D.Point3D(
                                        -1 * averageWidthPerPixel,
                                        0,
                                        valLeft * averageHeightPerPixel),
                                    new System.Windows.Media.Media3D.Point3D(
                                        0,
                                        -1 * averageWidthPerPixel,
                                        valAbove * averageHeightPerPixel),
                                    new System.Windows.Media.Media3D.Point3D(
                                        x,
                                        y,
                                        curentVal * averageHeightPerPixel));

                                if (normal.Z < 0)
                                    normal *= -1;

                                var angle = Math.Atan(normal.Z / Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y));
                                double slopeAngleDeg = angle * 180 / Math.PI;
                                retval.HGrad[x, y] = (byte)(255 - slopeAngleDeg * 255.0 / 90); // Scale to 8-bit
                            }
                        }
                    }
                });
            });
            Task.WaitAll(t1, t2);
            matMap.Dispose();
            hMap.Dispose();
            return retval;
        }

        public int TileWidth { get; }
        public ImageData(CubeMapFace face, int tileWidth)
        {
            Face = face;
            G = new byte[tileWidth, tileWidth];
            R = new byte[tileWidth, tileWidth];
            B = new byte[tileWidth, tileWidth];
            H = new byte[tileWidth, tileWidth];
            HGrad = new byte[tileWidth, tileWidth];
            TileWidth = tileWidth;
        }

        public void Dispose()
        {
            // Might buffer images later
            R = B = G = H = HGrad = null;
        }

        public BitmapImage CreateBitmapImage(bool heightMap, bool complexMaterials, int? selectedComplexMaterial, bool biomes, int? selectedBiome)
        {
            using MemoryStream memory = new MemoryStream();
            var image = new Image<Rgb24>(TileWidth, TileWidth);
            Parallel.For(0, TileWidth, x =>
            {
                for (var y = 0; y < TileWidth; y++)
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
                    image[x, y] = new Rgb24(r,g,b); // TODO: Use Span<Rgb24> for performance
                }
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
            var image = new Image<Rgb24>(TileWidth, TileWidth);
            image.ProcessPixelRows(row =>
            {
                    for (var y = 0; y < TileWidth; ++y)
                    {
                        Span<Rgb24> pixelRow = row.GetRowSpan(y);
                        for (var x = 0; x < TileWidth; ++x)
                        {
                            pixelRow[x] = new Rgb24(R[x, y], G[x, y], B[x, y]);
                        }
                }
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

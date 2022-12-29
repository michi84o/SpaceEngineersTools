using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SpaceEngineersOreRedistribution
{
    class ImageData : IDisposable
    {
        public string FileName { get; set; }

        public Bitmap GetBitmap()
        {
            using var image = System.Drawing.Image.FromFile(FileName);
            return new System.Drawing.Bitmap(image);
        }

        public Bitmap GetHeightMap()
        {
            var file = FileName.Replace("_mat", "");
            if (!File.Exists(file)) return null;
            using var image = System.Drawing.Image.FromFile(file);
            return new System.Drawing.Bitmap(image);
        }

        public Bitmap CalculateGradients()
        {
            var bmap = GetHeightMap();
            if (bmap == null) return null;

            var oreMap = GetBitmap();
            var lOreMap = new LockBitmap(oreMap);
            lOreMap.LockBits();

            var gMap = new Bitmap(2048,2048);
            var lgMap = new LockBitmap(gMap);
            lgMap.LockBits();

            var lbmap = new LockBitmap(bmap);
            lbmap.LockBits();

            Parallel.For(1, lbmap.Width-1, x =>
            {
                Parallel.For(1, lbmap.Height-1, y =>
                {
                    // Calc gradient in all directions and use max value
                    int currentPixel = lbmap.GetPixel(x, y).G;

                    List<int> neighbors = new();
                    neighbors.Add(lbmap.GetPixel(x-1, y).G);
                    neighbors.Add(lbmap.GetPixel(x+1, y).G);
                    neighbors.Add(lbmap.GetPixel(x, y-1).G);
                    neighbors.Add(lbmap.GetPixel(x, y+1).G);
                    neighbors.Add(lbmap.GetPixel(x-1, y-1).G);
                    neighbors.Add(lbmap.GetPixel(x+1, y+1).G);
                    neighbors.Add(lbmap.GetPixel(x+1, y-1).G);
                    neighbors.Add(lbmap.GetPixel(x-1, y+1).G);
                    double maxGradient = 0;
                    foreach (var n in neighbors)
                    {
                        var gradient = Math.Abs(currentPixel - n);
                        if (gradient > maxGradient) maxGradient = gradient;
                    }

                    int ig = (int)(maxGradient*20); // Don't know max value of all gradients for stretching. Vanilla Earthlike: 24, but most are 12 or less
                    if (ig > 255) ig = 255;
                    int r = lOreMap.GetPixel(x,y).R;
                    if (r != 82) r = ig;
                    lgMap.SetPixel(x, y, Color.FromArgb(r,ig,ig));
                });
            });

            lbmap.UnlockBits();
            lgMap.UnlockBits();
            lOreMap.UnlockBits();

            bmap.Dispose();
            oreMap.Dispose();
            return gMap;
        }

        public Bitmap ShowOre(Bitmap background, List<OreMapping> oreMappings)
        {
            var bmap = GetBitmap();

            // Height map used to replace black background

            LockBitmap lhmap = null;
            if (background != null)
            {
                lhmap = new LockBitmap(background);
                lhmap.LockBits();
            }

            var lockBmap = new LockBitmap(bmap);
            lockBmap.LockBits();

            // Each mapping has a different value.

            Parallel.For(0, lockBmap.Width, x =>
            {
                Parallel.For(0, lockBmap.Height, y =>
                {
                    var px = lockBmap.GetPixel(x, y);
                    var bg = lhmap != null ? lhmap.GetPixel(x, y) : Color.FromArgb(0, 0, 0);

                    // Lakes have value 82 in red channel

                    if (oreMappings == null)
                    {
                        if (px.B != 255) { px = Color.FromArgb(0, 255, 0); }
                        else if (px.R == 82) px = Color.FromArgb(64, 0, 0); // Lake
                        else px = bg;
                    }
                    else
                    {
                        var mapping = oreMappings.FirstOrDefault(x => x.Value == px.B);
                        if (mapping != null)
                        {
                            px = Color.FromArgb(
                                mapping.MapRgbValue.R,
                                mapping.MapRgbValue.G,
                                mapping.MapRgbValue.B);
                        }
                        else if (px.B < 255)
                        {
                            px = Color.FromArgb(0, 0, 64);
                        }
                        else
                        {
                            if (px.R == 82) px = Color.FromArgb(64, 0, 0); // Lake
                            else px = bg;
                        }
                    }
                    lockBmap.SetPixel(x, y, px);
                });
            });

            lockBmap.UnlockBits();
            return bmap;
        }

        public void Dispose()
        {
            // Might buffer images later
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
}

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

        public Bitmap FilterForOre(List<OreMapping> oreMappings)
        {
            var bmap = GetBitmap();

            // Height map used to replace black background
            var hmap = GetHeightMap();
            LockBitmap lhmap = null;
            if (hmap != null)
            {
                lhmap = new LockBitmap(hmap);
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
                        if (oreMappings.Any(x => x.Value == px.B))
                        {
                            px = Color.FromArgb(0, 255, 0);
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

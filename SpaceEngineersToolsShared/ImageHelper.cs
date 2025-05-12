using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersToolsShared
{
    public static class ImageHelper
    {
        public static void ExtractPixels(Image<L16> image, double[,] values)
        {
            if (image == null) return;
            image.ProcessPixelRows(rows =>
            {
                for (int y = 0; y < rows.Height; y++)
                {
                    Span<L16> pixelRow = rows.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        values[x, y] = pixelRow[x].PackedValue / 65535d;
                    }
                }
            });
        }

        public static void InsertPixels(Image<L16> image, double[,] values)
        {
            image.ProcessPixelRows(rows =>
            {
                for (int y = 0; y < rows.Height; y++)
                {
                    Span<L16> pixelRow = rows.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        var val = values[x, y] * 65535;
                        if (val < 0) val = 0;
                        else if (val > 65535) val = 65535;
                        ushort value = (ushort)(val);
                        pixelRow[x].PackedValue = value;
                    }
                }
            });
        }
    }
}

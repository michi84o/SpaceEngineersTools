using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ComplexMaterialViewer
{
    public static class ColorExtensions
    {
        public static Color FromHSV(double hue, double saturation, double value)
        {
            // Stellen Sie sicher, dass die Eingabewerte im gültigen Bereich liegen.
            if (hue < 0 || hue > 360 || saturation < 0 || saturation > 1 || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException("Die HSV-Werte müssen im Bereich von H[0-360], S[0-1], V[0-1] liegen.");
            }

            double r = 0, g = 0, b = 0;

            if (Math.Abs(saturation) < 0.00001) // Graustufen
            {
                r = g = b = value;
            }
            else
            {
                int i;
                double f, p, q, t;

                hue /= 60; // Hue in 6 Sektoren unterteilen
                i = (int)Math.Floor(hue);
                f = hue - i; // Bruchteiliger Teil von hue
                p = value * (1 - saturation);
                q = value * (1 - saturation * f);
                t = value * (1 - saturation * (1 - f));

                switch (i)
                {
                    case 0:
                        r = value;
                        g = t;
                        b = p;
                        break;
                    case 1:
                        r = q;
                        g = value;
                        b = p;
                        break;
                    case 2:
                        r = p;
                        g = value;
                        b = t;
                        break;
                    case 3:
                        r = p;
                        g = q;
                        b = value;
                        break;
                    case 4:
                        r = t;
                        g = p;
                        b = value;
                        break;
                    default: // case 5:
                        r = value;
                        g = p;
                        b = q;
                        break;
                }
            }

            // Konvertiere die RGB-Werte von 0-1 in 0-255 und erstelle die Color-Struktur.
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
    }
}

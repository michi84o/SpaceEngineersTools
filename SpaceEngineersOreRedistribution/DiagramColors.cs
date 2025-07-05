using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SpaceEngineersOreRedistribution
{
    public static class DiagramColors
    {
        static Color[] _colors;

        // 35 colors available
        public static Color[] Colors
        {
            get
            {
                if (_colors == null)
                {
                    _colors = new Color[]
                    {
                        Color.FromRgb(000,200,000),
                        Color.FromRgb(000,128,255),
                        Color.FromRgb(190,000,000),
                        Color.FromRgb(220,128,000),
                        Color.FromRgb(157,216,102),
                        Color.FromRgb(128,000,255),
                        Color.FromRgb(000,200,200),
                        Color.FromRgb(220,000,220),
                        Color.FromRgb(200,200,000),
                        Color.FromRgb(102,118,216),
                        Color.FromRgb(190,112,112),
                        Color.FromRgb(000,255,096),
                        Color.FromRgb(000,255,255),
                        Color.FromRgb(255,096,096),
                        Color.FromRgb(255,170,096),
                        Color.FromRgb(195,090,255),
                        Color.FromRgb(220,220,050),
                        Color.FromRgb(000,128,000),
                        Color.FromRgb(100,100,000),
                        Color.FromRgb(000,100,100),
                        Color.FromRgb(100,000,100),
                        Color.FromRgb(000,056,120),
                        Color.FromRgb(132,060,060),
                        Color.FromRgb(114,010,010),
                        Color.FromRgb(129,140,050),
                        Color.FromRgb(210,096,018),
                        Color.FromRgb(170,120,090),
                        Color.FromRgb(090,127,172),
                        Color.FromRgb(128,128,128),
                        Color.FromRgb(080,080,080),
                        Color.FromRgb(190,190,190),
                        Color.FromRgb(010,010,010),
                        Color.FromRgb(000,255,000),
                        Color.FromRgb(000,000,255),
                        Color.FromRgb(255,000,000),
                    };
                }
                return _colors;
            }
        }

        public static Brush GetBrush(int index)
        {
            var colors = Colors;
            index = index % colors.Length;
            return new SolidColorBrush(Colors[index]);
        }
    }
}

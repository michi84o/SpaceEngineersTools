using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpaceEngineersOreRedistribution
{
    // <Ore Value="200" Type="Iron_02" Start="3" Depth="7" TargetColor="#616c83" ColorInfluence="15"/>
    public class OreMapping : PropChangeNotifier
    {
        int _tier;
        // Needed for clearer understanding of table. UI only.
        public int Tier { get => _tier; set => SetProp(ref _tier, value); }

        int _value;
        public int Value { get => _value; set => SetProp(ref _value, value); }
        string _type;
        public string Type { get => _type; set => SetProp(ref _type, value); }
        int _start;
        public int Start { get => _start; set => SetProp(ref _start, value); }
        int _depth;
        public int Depth { get => _depth; set => SetProp(ref _depth, value); }
        string _targetColor;
        public string TargetColor { get => _targetColor; set => SetProp(ref _targetColor, value); }
        string _colorInfluence;
        public string ColorInfluence { get => _colorInfluence; set => SetProp(ref _colorInfluence, value); }

        RgbValue _mapRgbValue;
        public RgbValue MapRgbValue
        {
            get => _mapRgbValue;
            set
            {
                if (!SetProp(ref _mapRgbValue, value)) return;
                MapBrush = new(System.Windows.Media.Color.FromRgb(value.R, value.G, value.B));
                OnPropertyChanged(nameof(MapBrush));
            }
        }

        // Used for display in local UI
        public System.Windows.Media.SolidColorBrush MapBrush
        {
            get;
            protected set;
        } = new(System.Windows.Media.Color.FromRgb(0, 255, 0));

        public XElement ToXElement()
        {
            return new XElement("Ore",
                new XAttribute("Type", Type),
                new XAttribute("Value", Value),
                new XAttribute("Start", Start),
                new XAttribute("Depth", Depth),
                new XAttribute("TargetColor", TargetColor),
                new XAttribute("ColorInfluence", ColorInfluence));
        }
        public static OreMapping FromXElement(XElement x)
        {
            var elem = new OreMapping();
            elem.Type = x.Attribute("Type").Value;
            elem.Value = int.Parse(x.Attribute("Value").Value);
            elem.Start = int.Parse(x.Attribute("Start").Value);
            elem.Depth = int.Parse(x.Attribute("Depth").Value);
            elem.TargetColor = x.Attribute("TargetColor").Value;
            if (elem.TargetColor != null && !elem.TargetColor.StartsWith("#")) elem.TargetColor = "#" + elem.TargetColor;
            elem.ColorInfluence = x.Attribute("ColorInfluence").Value;
            return elem;
        }
    }

    public class RgbValue
    {
        public byte R;
        public byte G;
        public byte B;
        public RgbValue(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
}

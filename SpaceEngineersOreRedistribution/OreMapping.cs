using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    // <Ore Value="200" Type="Iron_02" Start="3" Depth="7" TargetColor="#616c83" ColorInfluence="15"/>
    public class OreMapping : PropChangeNotifier
    {
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

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    public class OreDistributionStatViewModel : PropChangeNotifier
    {
        string _oreType;
        public string OreType
        {
            get => _oreType;
            set => SetProp(ref _oreType, value);
        }

        int _percentage;
        public int Percentage
        {
            get => _percentage;
            set => SetProp(ref _percentage, value);
        }

        int _scaledPercentage;
        public int ScaledPercentage
        {
            get => _scaledPercentage;
            set => SetProp(ref _scaledPercentage, value);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    public class OreDistributionStatViewModel : PropChangeNotifier
    {
        public OreDistributionStatViewModel(
            string oreType,
            double percentageArea, double percentageVolume,
            int totalArea, int totalVolume)
        {
            _oreType = oreType;
            _percentageArea = percentageArea;
            _percentageVolume = percentageVolume;
            _totalArea = totalArea;
            _totalVolume = totalVolume;
            ShowByArea = true;
        }

        double _percentageArea;
        double _percentageVolume;
        double _scaledPercentageArea;
        double _scaledPercentageVolume;
        int _totalArea;
        int _totalVolume;

        public double ScaledPercentageArea
        {
            get => _scaledPercentageArea;
            set => SetProp(ref _scaledPercentageArea, value);
        }

        public double ScaledPercentageVolume
        {
            get => _scaledPercentageVolume;
            set => SetProp(ref _scaledPercentageVolume, value);
        }

        public double PercentageArea
        {
            get => _percentageArea;
            set => SetProp(ref _percentageArea, value);
        }

        public double PercentageVolume
        {
            get => _percentageVolume;
            set => SetProp(ref _percentageVolume, value);
        }

        double _total;
        public double Total
        {
            get => _total;
            set => SetProp(ref _total, value);
        }

        bool _showByArea;
        public bool ShowByArea
        {
            get => _showByArea;
            set
            {
                SetProp(ref _showByArea, value);
                // Always update
                Percentage = value ? _percentageArea : _percentageVolume;
                ScaledPercentage = value ? _scaledPercentageArea : _scaledPercentageVolume;
                Total = value ? _totalArea : _totalVolume;
            }
        }

        string _oreType;
        public string OreType
        {
            get => _oreType;
            set => SetProp(ref _oreType, value);
        }

        double _percentage;
        public double Percentage
        {
            get => _percentage;
            set => SetProp(ref _percentage, value);
        }

        double _scaledPercentage;
        public double ScaledPercentage
        {
            get => _scaledPercentage;
            set => SetProp(ref _scaledPercentage, value);
        }

    }
}

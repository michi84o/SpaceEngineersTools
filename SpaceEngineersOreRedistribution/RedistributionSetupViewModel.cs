using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SpaceEngineersOreRedistribution
{
    internal class RedistributionSetupViewModel : PropChangeNotifier
    {
        public ObservableCollection<OreInfo> OreInfos { get; } = new();
        OreInfo _selectedInfo;
        public OreInfo SelectedInfo
        {
            get => _selectedInfo;
            set => SetProp(ref _selectedInfo, value);
        }

        public ICommand AddOreCommand => new RelayCommand(o =>
        {
            var info = new OreInfo();
            OreInfos.Add(info);
            SelectedInfo = info;
        });

        public ICommand RemoveOreCommand => new RelayCommand(o =>
        {
            OreInfos.Remove(SelectedInfo);
            SelectedInfo = null;
        }, o=> SelectedInfo != null);

        int _stdDev = 10;
        public int StdDev
        {
            get => _stdDev;
            set
            {
                if (SetProp(ref _stdDev, value))
                {
                    if (value < 1) StdDev = 1;
                    if (value > 50) StdDev = 50;
                }
        }    }

        public class OreInfo : PropChangeNotifier
        {
            string _name;
            public string Name
            {
                get => _name;
                set => SetProp(ref _name, value);
            }

            int _spawnWeight = 1;
            public int SpawnWeight
            {
                get => _spawnWeight;
                set => SetProp(ref _spawnWeight, value);
            }

            int _typicalSize = 50;
            public int TypicalSize
            {
                get => _typicalSize;
                set
                {
                    if (SetProp(ref _typicalSize, value))
                    {
                        if (value > 100) _typicalSize = 100;
                        if (value < -1) _typicalSize = -1;
                    }
                }
            }

            int _typicalDepth = 10;
            public int TypicalDepth
            {
                get => _typicalDepth;
                set
                {
                    if (SetProp(ref _typicalDepth, value))
                    {
                        if (value > 100) TypicalDepth = 100;
                        if (value < -1) TypicalDepth = -1;
                    }
                }
            }
        }
    }

}

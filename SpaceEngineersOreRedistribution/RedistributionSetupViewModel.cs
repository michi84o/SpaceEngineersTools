using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

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
            OnPropertyChanged(nameof(ValuesCount));
        });

        public ICommand RemoveOreCommand => new RelayCommand(o =>
        {
            OreInfos.Remove(SelectedInfo);
            SelectedInfo = null;
        }, o => SelectedInfo != null);

        public ICommand ExportCommand => new RelayCommand(o =>
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "XML Files|*.xml",
                FileName = "OreInfos.xml"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var root = new XElement("OreInfos");
                foreach (var oreInfo in OreInfos)
                {
                    root.Add(oreInfo.ToXElement());
                }

                var doc = new XDocument(root);
                doc.Save(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }, o => OreInfos.Count > 0);

        public ICommand ImportCommand => new RelayCommand(o =>
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "XML Files|*.xml",
                FileName = "OreInfos.xml"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var doc = XDocument.Load(dlg.FileName);
                var root = doc.Root;
                if (root?.Name != "OreInfos") throw new Exception("Invalid file!");
                OreInfos.Clear();
                foreach (var node in root.Elements("OreInfo"))
                {
                    OreInfos.Add(OreInfo.FromXElement(node));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

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
            }
        }

        // In original file each ore deposit is about 28 pixel away from the next deposit
        // in a checkboard pattern.
        // Lets randomize this a bit:
        // Square of 2*28 pixels width and length with 1 ore means:
        // 1 ore in 3136 pixels
        int _oreSpawnRate = 2000;
        public int OreSpawnRate
        {
            get => _oreSpawnRate;
            set => SetProp(ref _oreSpawnRate, value);
        }

        public Action ConfirmAction;
        public ICommand ConfirmCommand => new RelayCommand(o =>
        {
            ConfirmAction?.Invoke();
        },
        // TODO: Value should be buffered
        o=>ValuesCount <= 254);

        public int ValuesCount
        {
            get
            {
                var cnt = 0;
                var ores = OreInfos.Select(o => o.Name).Distinct().ToList();
                foreach (var ore in ores)
                {
                    cnt += 9;
                    if (OreInfos.Any(x => x.Name == ore && x.VeryDeepOre))
                        ++cnt;
                }
                return cnt;
            }
        }

        public RedistributionSetupViewModel()
        {
            OreInfos.CollectionChanged += OreInfos_CollectionChanged;
        }

        private void OreInfos_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    ((OreInfo)item).PropertyChanged += OreItem_PropertyChanged;
                }
            }
            // Minor memory leak prevention
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    ((OreInfo)item).PropertyChanged -= OreItem_PropertyChanged;
                }
            }
            OnPropertyChanged(nameof(ValuesCount));
        }

        private void OreItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OreInfo.VeryDeepOre))
                OnPropertyChanged(nameof(ValuesCount));
        }


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

            int _typicalSize = 15;
            public int TypicalSize
            {
                get => _typicalSize;
                set
                {
                    if (SetProp(ref _typicalSize, value))
                    {
                        if (value > 50) _typicalSize = 50;
                        else if (value < 0) _typicalSize = 0;
                    }
                }
            }

            int _typicalDepth = -1;
            public int TypicalDepth
            {
                get => _typicalDepth;
                set
                {
                    if (SetProp(ref _typicalDepth, value))
                    {
                        if (value > 8)
                        {
                            if (VeryDeepOre) TypicalDepth = 9;
                            else TypicalDepth = 8;
                        }
                        else if (value < -1) TypicalDepth = -1;
                    }
                }
            }

            bool _veryDeepOre = true;
            public bool VeryDeepOre
            {
                get => _veryDeepOre;
                set
                {
                    if (SetProp(ref _veryDeepOre, value))
                    {
                        if (!value && TypicalDepth == 10)
                            TypicalDepth = 9;
                    }
                }
            }

            public static OreInfo FromXElement(XElement node)
            {
                var info = new OreInfo();
                info.Name = node.Element("Name")?.Value ?? "";
                int tmp;
                if (int.TryParse(node.Element("SpawnWeight")?.Value, out tmp)) info.SpawnWeight = tmp;
                if (int.TryParse(node.Element("TypicalSize")?.Value, out tmp)) info.TypicalSize = tmp;
                if (int.TryParse(node.Element("TypicalDepth")?.Value, out tmp)) info.TypicalDepth = tmp;
                if (bool.TryParse(node.Element("VeryDeepOre")?.Value, out var bb)) info.VeryDeepOre = bb;
                return info;
            }

            public XElement ToXElement()
            {
                return new XElement(
                    "OreInfo",
                    new XElement("Name", Name),
                    new XElement("SpawnWeight", SpawnWeight),
                    new XElement("TypicalSize", TypicalSize),
                    new XElement("TypicalDepth", TypicalDepth),
                    new XElement("VeryDeepOre", VeryDeepOre));
            }
        }

        public int WeightSum;
        public void FinalizeList()
        {
            // Weight Sum
            var weights = OreInfos.Select(o => o.SpawnWeight).ToList();
            int weightSum = 0;
            foreach (var weight in weights) weightSum += weight;
            WeightSum = weightSum;
        }
        public OreInfo PickRandomOreWeighted(Random rnd)
        {
            if (WeightSum == 0) return null;
            var pick = rnd.Next(WeightSum) + 1;

            int currentPos = 0;
            foreach (var ore in OreInfos)
            {
                currentPos += ore.SpawnWeight;
                if (currentPos >= pick) return ore;
            }
            return null;
        }

    }

}

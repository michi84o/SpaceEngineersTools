using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using SpaceEngineersToolsShared;

namespace SpaceEngineersOreRedistribution
{
    // TODO Allow to edit ore mappings for each ore type so tiers can be adjusted

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
            info.AddDefaultMapping();
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
                root.Add(new XAttribute("StdDev", StdDev));
                root.Add(new XAttribute("StdDevDepth", StdDevDepth));
                root.Add(new XAttribute("OreSpawnRate", OreSpawnRate));
                root.Add(new XAttribute("Seed", Seed));
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
                if (int.TryParse(root.Attribute("StdDev")?.Value, out var stdDev)) StdDev = stdDev;
                if (int.TryParse(root.Attribute("StdDevDepth")?.Value, out var stdDevDepth)) StdDevDepth = stdDevDepth;
                if (int.TryParse(root.Attribute("OreSpawnRate")?.Value, out var oresp)) OreSpawnRate = oresp;
                if (int.TryParse(root.Attribute("Seed")?.Value, out var seed)) Seed= seed;
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
                    if (value < 0) StdDev = 0;
                    if (value > 50) StdDev = 50;
                }
            }
        }

        int _stdDevDepth = 4;
        public int StdDevDepth
        {
            get => _stdDevDepth;
            set
            {
                if (SetProp(ref _stdDevDepth, value))
                {
                    if (value < 1) StdDevDepth = 1;
                    if (value > 10) StdDevDepth = 10;
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

        int _seed;
        public int Seed
        {
            get => _seed;
            set => SetProp(ref _seed, value);
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
            get { return FinalizeList(); }
        }

        public class OreTierItem
        {
            public int Start;
            public int Depth;
        }
        public class OreTierListContainer
        {
            public string Name { get; set; }
            public List<OreTierItem> Items { get; } = new List<OreTierItem>();

            public XElement ToXelement()
            {
                var elem = new XElement("OreTierPreset", new XAttribute("Name", Name));
                foreach (var item in Items)
                {
                    elem.Add(
                        new XElement("OreTierItem",
                            new XAttribute("Start", item.Start),
                            new XAttribute("Depth", item.Depth)));
                }
                return elem;
            }
        }

        OreTierListContainer _selectedOreMappingPreset;
        public OreTierListContainer SelectedOreMappingPreset
        {
            get => _selectedOreMappingPreset;
            set => SetProp(ref _selectedOreMappingPreset, value);
        }
        public ObservableCollection<OreTierListContainer> OreMappingPresets { get; } = new();

        public RedistributionSetupViewModel()
        {
            OreInfos.CollectionChanged += OreInfos_CollectionChanged;
            LoadPresets();
            SelectedOreMappingPreset = OreMappingPresets.FirstOrDefault();
        }

        void LoadPresets()
        {
            try
            {
                OreMappingPresets.Clear();
                SelectedOreMappingPreset = null;
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (Directory.Exists(dir))
                {
                    var path = Path.Combine(dir, "OreTierPresets.xml");
                    if (File.Exists(path))
                    {
                        var doc = XDocument.Load(path);
                        if (doc.Root?.Name == "OreTierPresets")
                        {
                            try
                            {
                                foreach (var elem in doc.Root.Elements("OreTierPreset"))
                                {
                                    OreTierListContainer container = new();
                                    container.Name = elem.Attribute("Name")?.Value ?? "Undefined";
                                    foreach (var item in elem.Elements("OreTierItem"))
                                    {
                                        var tierItem = new OreTierItem
                                        {
                                            Start = int.Parse(item?.Attribute("Start")?.Value ?? "1"),
                                            Depth = int.Parse(item?.Attribute("Depth")?.Value ?? "1")
                                        };
                                        container.Items.Add(tierItem);
                                    }
                                    OreMappingPresets.Add(container);
                                }
                            }
                            catch { /* silent */ }
                        }
                    }
                }
                SelectedOreMappingPreset = OreMappingPresets.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ICommand LoadPresetCommand => new RelayCommand(o =>
        {
            var items = SelectedOreMappingPreset.Items;
            for (int i = 0; i < items.Count && i < SelectedInfo.OreMappings.Count; ++i)
            {
                SelectedInfo.OreMappings[i].Start = items[i].Start;
                SelectedInfo.OreMappings[i].Depth = items[i].Depth;
            }

            // Causes redraw of diagram
            var info = SelectedInfo;
            SelectedInfo = null;
            SelectedInfo = info;

        }, o => SelectedOreMappingPreset != null && SelectedInfo != null);

        public Window Parent;
        public ICommand SavePresetCommand => new RelayCommand(o =>
        {
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(dir, "OreTierPresets.xml");
                XDocument doc;
                try
                {
                    doc = XDocument.Load(path);
                    if (doc.Root.Name != "OreTierPresets") throw new Exception();
                }
                catch
                {
                    var elem = new XElement("OreTierPresets");
                    doc = new XDocument(elem);
                }

                TextInputPopup popup = new TextInputPopup();
                popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                popup.Owner = Parent;
                var res = popup.ShowDialog();
                if (res != true) return;

                OreTierListContainer container = new();
                foreach (var item in SelectedInfo.OreMappings)
                {
                    container.Items.Add(new OreTierItem { Start = item.Start, Depth = item.Depth });
                }
                container.Name = popup.MyText.Text;
                doc.Root.Add(container.ToXelement());
                doc.Save(path);

                OreMappingPresets.Add(container);
                SelectedOreMappingPreset = container;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }, o => SelectedInfo != null);

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
            CalcRatios();
            OnPropertyChanged(nameof(ValuesCount));
        }

        private void OreItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ValuesCount));
            CalcRatios();
        }

        void CalcRatios()
        {
            // Calc rations
            double sum = 0;
            foreach (var item in OreInfos)
            {
                sum += item.TypicalSize * item.SpawnWeight;
            }
            foreach (var item in OreInfos)
            {
                item.ExpectedRatio = (item.TypicalSize * item.SpawnWeight) / sum;
            }
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
                        if (value > 500) _typicalSize = 500;
                        else if (value < 0) _typicalSize = 0;
                    }
                }
            }

            double _expectedRatio;
            public double ExpectedRatio
            {
                get => _expectedRatio;
                set => SetProp(ref _expectedRatio, value);
            }

            int _typicalDepthMin = -1;
            public int TypicalDepthMin
            {
                get => _typicalDepthMin;
                set
                {
                    if (SetProp(ref _typicalDepthMin, value))
                    {
                        if (value > 8)
                        {
                            if (VeryDeepOre) TypicalDepthMin = 9;
                            else TypicalDepthMin = 8;
                        }
                        else if (value < -1) TypicalDepthMin = -1;
                        if (value != -1 && value > TypicalDepthMax && TypicalDepthMax != -1) TypicalDepthMin = TypicalDepthMax;
                    }
                }
            }

            int _preferredDepth = -1;
            public int PreferredDepth
            {
                get => _preferredDepth;
                set => SetProp(ref _preferredDepth, value);
            }

            int _typicalDepthMax = -1;
            public int TypicalDepthMax
            {
                get => _typicalDepthMax;
                set
                {
                    if (SetProp(ref _typicalDepthMax, value))
                    {
                        if (value > 8)
                        {
                            if (VeryDeepOre) TypicalDepthMax = 9;
                            else TypicalDepthMax = 8;
                        }
                        else if (value < -1) TypicalDepthMax = -1;
                        if (value != -1 && value < TypicalDepthMin) TypicalDepthMax = TypicalDepthMin;
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
                        if (!value)
                        {
                            if (TypicalDepthMax == 9)
                                TypicalDepthMax = 8;
                            if (TypicalDepthMin == 9)
                                TypicalDepthMin = 8;
                        }
                    }
                }
            }

            public ObservableCollection<OreMapping> OreMappings { get; } = new();

            public void AddDefaultMapping()
            {
                OreMappings.Clear();
                List<OreMapping> mList = new();
                // Tier 1: Flat surface ore. Depth: 3m - 15m
                OreMappings.Add(new OreMapping { Tier = 1, Value = 0, Type = Name, Start = 3, Depth = 3, ColorInfluence = "15", TargetColor = "#616c83" });
                OreMappings.Add(new OreMapping { Tier = 1, Value = 1, Type = Name, Start = 5, Depth = 5, ColorInfluence = "15", TargetColor = "#616c83" });
                OreMappings.Add(new OreMapping { Tier = 1, Value = 2, Type = Name, Start = 8, Depth = 7, ColorInfluence = "15", TargetColor = "#616c83" });
                // Tier 2: Medium deep veins. Depth: 40m - 102m
                OreMappings.Add(new OreMapping { Tier = 2, Value = 3, Type = Name, Start = 40, Depth = 12, ColorInfluence = "15", TargetColor = "#616c83" });
                OreMappings.Add(new OreMapping { Tier = 2, Value = 4, Type = Name, Start = 50, Depth = 22, ColorInfluence = "15", TargetColor = "#616c83" });
                OreMappings.Add(new OreMapping { Tier = 2, Value = 5, Type = Name, Start = 70, Depth = 32, ColorInfluence = "15", TargetColor = "#616c83" });
                // Tier 3: Very deep veins. Depth: 140m - 372m
                OreMappings.Add(new OreMapping { Tier = 3, Value = 6, Type = Name, Start = 140, Depth = 62, ColorInfluence = "15", TargetColor = "#616c83" });
                OreMappings.Add(new OreMapping { Tier = 3, Value = 7, Type = Name, Start = 200, Depth = 72, ColorInfluence = "15", TargetColor = "#616c83" });
                OreMappings.Add(new OreMapping { Tier = 3, Value = 8, Type = Name, Start = 270, Depth = 102, ColorInfluence = "15", TargetColor = "#616c83" });
                // Bonus: Very deep ore. Depth: 370m - 450m
                OreMappings.Add(new OreMapping { Tier = 4, Value = 9, Type = Name, Start = 370, Depth = 80, ColorInfluence = "15", TargetColor = "#616c83" });
            }

            public static OreInfo FromXElement(XElement node)
            {
                var info = new OreInfo();
                info.Name = node.Element("Name")?.Value ?? "";
                int tmp;
                if (int.TryParse(node.Element("SpawnWeight")?.Value, out tmp)) info.SpawnWeight = tmp;
                if (int.TryParse(node.Element("TypicalSize")?.Value, out tmp)) info.TypicalSize = tmp;
                if (int.TryParse(node.Element("TypicalDepthMin")?.Value, out tmp)) info.TypicalDepthMin = tmp;
                if (int.TryParse(node.Element("TypicalDepthMax")?.Value, out tmp)) info.TypicalDepthMax = tmp;
                if (int.TryParse(node.Element("PreferredDepth")?.Value, out tmp)) info.PreferredDepth = tmp;
                if (bool.TryParse(node.Element("VeryDeepOre")?.Value, out var bb)) info.VeryDeepOre = bb;

                var mappings = node.Element("Mappings");
                if (mappings != null)
                {
                    info.OreMappings.Clear();
                    foreach (var item in mappings.Elements())
                    {
                        if (item.Name == "Ore")
                        {
                            info.OreMappings.Add(OreMapping.FromXElement(item));
                        }
                    }
                }

                // Fallback if mapping is incomplete
                if (info.OreMappings.Count != 10) info.AddDefaultMapping();

                info.OreMappings[0].Tier = info.OreMappings[1].Tier = info.OreMappings[2].Tier = 1;
                info.OreMappings[3].Tier = info.OreMappings[4].Tier = info.OreMappings[5].Tier = 2;
                info.OreMappings[6].Tier = info.OreMappings[7].Tier = info.OreMappings[8].Tier = 3;
                info.OreMappings[9].Tier = 4;

                return info;
            }

            public XElement ToXElement()
            {
                var elem = new XElement(
                    "OreInfo",
                    new XElement("Name", Name),
                    new XElement("SpawnWeight", SpawnWeight),
                    new XElement("TypicalSize", TypicalSize),
                    new XElement("TypicalDepthMin", TypicalDepthMin),
                    new XElement("TypicalDepthMax", TypicalDepthMax),
                    new XElement("PreferredDepth", PreferredDepth),
                    new XElement("VeryDeepOre", VeryDeepOre));

                if (OreMappings.Count > 0)
                {
                    var mappings = new XElement("Mappings");
                    foreach (var mapping in OreMappings)
                    {
                        mapping.Type = Name;
                        mappings.Add(mapping.ToXElement());
                    }
                    elem.Add(mappings);
                }
                return elem;
            }
        }

        public int WeightSum;
        public int FinalizeList()
        {
            // Weight Sum
            var weights = OreInfos.Select(o => o.SpawnWeight).ToList();
            int weightSum = 0;
            foreach (var weight in weights) weightSum += weight;
            WeightSum = weightSum;

            int value = 1;
            // Update ore mapping values
            foreach (var info in OreInfos)
            {
                foreach (var mapping in info.OreMappings)
                {
                    mapping.Value = 0; // enabled
                }

                // If deep ore deselected, ignore entry
                if (!info.VeryDeepOre) info.OreMappings[9].Value = -1; // disabled
                if (info.TypicalDepthMin > 0)
                {
                    for (int i=0; i<info.TypicalDepthMin; ++i)
                        info.OreMappings[i].Value = -1;
                }
                if (info.TypicalDepthMax > 0)
                {
                    for (int i = 9; i > info.TypicalDepthMax; --i)
                        info.OreMappings[i].Value = -1;
                }

                foreach (var mapping in info.OreMappings)
                {
                    if (mapping.Value == 0)
                        mapping.Value = value++;
                }
            }
            return value - 1;
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

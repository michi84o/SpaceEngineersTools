using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.PixelFormats;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

// The RED channel is the voxel material placement map. -> 82=Lake 43=Desert
// The GREEN channel is the foliage placement map (including voxel stones) -> Biome value, Material based on rules
// The BLUE channel is the ore placement map

namespace SpaceEngineersOreRedistribution
{
    internal class MainWindowViewModel : PropChangeNotifier
    {
        public ObservableCollection<PlanetDefinition> PlanetDefinitions { get; } = new();

        public ObservableCollection<OreDistributionStatViewModel> OreTypes { get; } = new();

        CancellationTokenSource _imageUpdateTcs;
        bool _updatingImages = false;

        Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set => SetProp(ref _progressBarVisibility, value);
        }

        bool _showDistByArea = true;
        public bool ShowOreDistByArea
        {
            get => _showDistByArea;
            set
            {
                if (SetProp(ref _showDistByArea, value))
                {
                    ShowDistByVolume = !value;
                    foreach (var item in OreTypes)
                        item.ShowByArea = value;
                    var list = OreTypes.ToList();
                    list.Sort((a, b) => b.Percentage.CompareTo(a.Percentage));
                    //if (list.Count > 0)
                    //{
                    //    var maxP = list.Max(x => x.Percentage);
                    //    foreach (var x in list)
                    //        x.ScaledPercentage = (int)(0.5 + x.Percentage * 100 / maxP);
                    //}
                    OreTypes.Clear();
                    foreach (var item in list)
                    {
                        OreTypes.Add(item);
                    }
                }
            }
        }

        bool _showDistByVolume;
        public bool ShowDistByVolume
        {
            get => _showDistByVolume;
            set
            {
                if (SetProp(ref _showDistByVolume, value))
                {
                    ShowOreDistByArea = !value;
                }
            }
        }

        PlanetDefinition _selectedPlanetDefinition;
        public PlanetDefinition SelectedPlanetDefinition
        {
            get => _selectedPlanetDefinition;
            set
            {
                if (!SetProp(ref _selectedPlanetDefinition, value)) return;

                if (_imageUpdateTcs != null)
                {
                    _imageUpdateTcs.Cancel();
                    _imageUpdateTcs.Dispose();
                    _imageUpdateTcs = null;
                }

                ClearImages();
                OreTypes.Clear();
                EnvironmentItems.Clear();
                ComplexMaterials.Clear();
                if (value == null)
                {
                    return;
                }
                //HashSet<string> types = new();
                //foreach (var item in value.OreMappings)
                //{
                //    types.Add(item.Type);
                //}
                //foreach (var type in types)
                //    OreTypes.Add(type);
                foreach (var item in value.ComplexMaterials)
                {
                    ComplexMaterials.Add(item);
                }
                foreach (var item in value.EnvironmentItems)
                {
                    EnvironmentItems.Add(item);
                }

                _imageUpdateTcs = new();
                var token = _imageUpdateTcs.Token;
                _updatingImages = true;


                Task.Run(() =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressBarVisibility = Visibility.Visible;
                        });
                        Parallel.ForEach(Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>(), f =>
                        {
                            if (token.IsCancellationRequested) return;
                            _images[f] = GetInfo(f, token);
                        });
                        if (token.IsCancellationRequested) return;

                        // Update ore
                        Dictionary<string, int> oreArea = new(); // Counts ore area
                        Dictionary<string, int> oreVolume = new(); // Counts ore volume
                        Parallel.ForEach(_images.Values, img =>
                        {
                            if (img == null) return;
                            for (int x = 0; x < 2048; ++x)
                                for (int y = 0; y < 2048; ++y)
                                {
                                    var blue = img.B[x, y];
                                    var mapping = value.OreMappings.FirstOrDefault(x => x.Value == blue);
                                    if (mapping != null)
                                    {
                                        lock (oreArea)
                                        {
                                            if (!oreArea.ContainsKey(mapping.Type))
                                            {
                                                oreArea[mapping.Type] = 1;
                                                oreVolume[mapping.Type] = mapping.Depth;
                                            }
                                            else
                                            {
                                                oreArea[mapping.Type] += 1;
                                                oreVolume[mapping.Type] += mapping.Depth;
                                            }
                                        }
                                    }
                                }
                        });
                        int oreSumArea = oreArea.Values.Sum();
                        int oreSumVolume = oreVolume.Values.Sum();
                        List<OreDistributionStatViewModel> list = new();
                        foreach (var key in oreArea.Keys)
                        {
                            list.Add(new OreDistributionStatViewModel(
                                key,
                                oreArea[key] * 100.0 / oreSumArea,
                                oreVolume[key] * 100.0 / oreSumVolume, oreArea[key], oreVolume[key])
                            { ShowByArea = ShowOreDistByArea });
                        }
                        list.Sort((a, b) => b.Percentage.CompareTo(a.Percentage));
                        if (list.Count > 0)
                        {
                            var maxPArea = list.Max(x => x.PercentageArea);
                            var maxPVolume = list.Max(x => x.PercentageVolume);
                            foreach (var x in list)
                            {
                                x.ScaledPercentageArea = (int)(0.5 + x.PercentageArea * 100 / maxPArea);
                                x.ScaledPercentageVolume = (int)(0.5 + x.PercentageVolume * 100 / maxPVolume);
                            }
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var item in list)
                            {
                                item.ShowByArea = ShowOreDistByArea; // Update scaled percentages
                                OreTypes.Add(item);
                            }
                        });

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            _updatingImages = false;
                            UpdateImages();
                        });
                    }
                    finally
                    {
                        ProgressBarVisibility = Visibility.Collapsed;
                    }
                });


            }
        }

        List<System.Windows.Media.Color> _colors = new List<System.Windows.Media.Color>
        {
            System.Windows.Media.Colors.LawnGreen,
            System.Windows.Media.Colors.DeepSkyBlue,
            System.Windows.Media.Colors.Red,
            System.Windows.Media.Colors.Orange,
            System.Windows.Media.Colors.BlueViolet,
            System.Windows.Media.Colors.Magenta,
            System.Windows.Media.Colors.Green,
            System.Windows.Media.Colors.Gold,
            System.Windows.Media.Colors.MediumPurple,
            System.Windows.Media.Colors.Blue,
            System.Windows.Media.Colors.Firebrick,
            System.Windows.Media.Colors.Goldenrod,
            System.Windows.Media.Colors.MediumAquamarine,
            System.Windows.Media.Colors.MediumVioletRed,
            System.Windows.Media.Colors.OliveDrab,
        };
        System.Windows.Media.SolidColorBrush _defaultOreBrush = new(System.Windows.Media.Color.FromRgb(0, 0xff, 0));
        OreDistributionStatViewModel _selectedOreType;
        public OreDistributionStatViewModel SelectedOreType
        {
            get => _selectedOreType;
            set
            {
                if (!SetProp(ref _selectedOreType, value)) return;
                OreMappings.Clear();
                if (value == null || SelectedPlanetDefinition == null)
                {
                    if (ShowOreLocations && !_updatingImages)
                        UpdateImages();
                    return;
                }

                // Reset colors so they don't appear in ore inspector
                foreach (var m in SelectedPlanetDefinition.OreMappings)
                {
                    m.MapRgbValue = new(0, 0, 64);
                }

                var mappings = SelectedPlanetDefinition.OreMappings.Where(x => x.Type == value.OreType);
                int colorIndex = 0;
                foreach (var mapping in mappings)
                {
                    if (colorIndex < _colors.Count)
                    {
                        var color = _colors[colorIndex++];
                        mapping.MapRgbValue = new(color.R, color.G, color.B);
                    }
                    else
                        mapping.MapRgbValue = new(0, 0, 255);
                    OreMappings.Add(mapping);
                }
                if (ShowOreLocations && !_updatingImages)
                    UpdateImages();
            }
        }

        public ObservableCollection<OreMapping> OreMappings { get; } = new();
        OreMapping _selectedOreMapping;
        public OreMapping SelectedOreMapping
        {
            get => _selectedOreMapping;
            set => SetProp(ref _selectedOreMapping, value);
        }
        bool _ignoreOreMappingsForRedestribution = true;
        public bool IgnoreOreMappingsForRedestribution
        {
            get => _ignoreOreMappingsForRedestribution;
            set => SetProp(ref _ignoreOreMappingsForRedestribution, value);
        }

        public ObservableCollection<ComplexMaterial> ComplexMaterials { get; } = new();
        ComplexMaterial _selectedComplexMaterial;
        int _lastComplexMaterial;
        public ComplexMaterial SelectedComplexMaterial
        {
            get => _selectedComplexMaterial;
            set
            {
                SetProp(ref _selectedComplexMaterial, value);
                var number = value?.Value ?? 0;
                if (ShowLakes && !_updatingImages && number != _lastComplexMaterial)
                    UpdateImages();
                _lastComplexMaterial = number;
            }
        }

        public ObservableCollection<EnvironmentItem> EnvironmentItems { get; } = new();
        int _lastBiome;
        EnvironmentItem _selectedEnvironmentItem;
        public EnvironmentItem SelectedEnvironmentItem
        {
            get => _selectedEnvironmentItem;
            set
            {
                SetProp(ref _selectedEnvironmentItem, value);
                var biome = value?.Biome ?? 0;
                if (ShowBiomes && !_updatingImages && biome != _lastBiome)
                    UpdateImages();
                _lastBiome = biome;
            }
        }

        bool _showOreLocations = true;
        public bool ShowOreLocations
        {
            get => _showOreLocations;
            set
            {
                if (!SetProp(ref _showOreLocations, value)) return;
                if (!_updatingImages)
                    UpdateImages();
            }
        }

        bool _showGradients;
        public bool ShowGradients
        {
            get => _showGradients;
            set
            {
                if (!SetProp(ref _showGradients, value)) return;
                if (!_updatingImages)
                    UpdateImages();
            }
        }

        bool _showLakes = true;
        public bool ShowLakes
        {
            get => _showLakes;
            set
            {
                if (!SetProp(ref _showLakes, value)) return;
                if (!_updatingImages)
                    UpdateImages();
            }
        }

        bool _showBiomes = false;
        public bool ShowBiomes
        {
            get => _showBiomes;
            set
            {
                if (!SetProp(ref _showBiomes, value)) return;
                if (!_updatingImages)
                    UpdateImages();
            }
        }

        public ICommand OpenPlanetDefinitionCommand => new RelayCommand(o =>
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "sbc Files|*.sbc";
            dlg.FileName = "PlanetGeneratorDefinitions.sbc";
            dlg.Multiselect = false;
            var res = dlg.ShowDialog();
            if (res != true) return;
            OpenPlanetDefinition(dlg.FileName);
        });

        string _lastOpenedFile;

        public void OpenPlanetDefinition(string file)
        {
            _lastOpenedFile = file;
            PlanetDefinitions.Clear();
            TileUp = null;
            TileFront = null;
            TileRight = null;
            TileBack = null;
            TileLeft = null;
            TileDown = null;

            try
            {
                PlanetDefinitions.Clear();

                XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
                if (!File.Exists(file)) return;
                var doc = XDocument.Load(file);
                var root = doc?.Root;
                if (root?.Name != "Definitions")
                {
                    MessageBox.Show("File does not contain definitions", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var definitions = root.Elements("Definition");
                foreach (var definition in definitions)
                {
                    string xsiTypeValue = definition.Attribute(xsi + "type")?.Value;
                    if (xsiTypeValue != "PlanetGeneratorDefinition") continue;
                    ReadPlanetDefinition(definition);
                }
                // Pertam & Triton use different format
                var defs = root.Elements("PlanetGeneratorDefinitions");
                foreach (var def in defs)
                {
                    var subDefs = def.Elements("PlanetGeneratorDefinition");
                    foreach (var subDef in subDefs)
                    {
                        ReadPlanetDefinition(subDef);
                    }
                }

            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        bool ReadPlanetDefinition(XElement definition)
        {
            var id = definition.Element("Id")?.Element("SubtypeId")?.Value;
            if (id == null) return false;
            var def = new PlanetDefinition();
            def.Name = id;
            PlanetDefinitions.Add(def);
            var oreMappings = definition.Element("OreMappings");
            if (oreMappings != null)
            {
                foreach (var oreMapping in oreMappings.Elements("Ore"))
                {
                    try
                    {
                        var ore = new OreMapping();
                        ore.Value = int.Parse(oreMapping.Attribute("Value")?.Value);
                        ore.Type = oreMapping.Attribute("Type")?.Value;
                        ore.Start = int.Parse(oreMapping.Attribute("Start")?.Value);
                        ore.Depth = int.Parse(oreMapping.Attribute("Depth")?.Value);
                        // optional:
                        ore.TargetColor = oreMapping.Attribute("TargetColor")?.Value;
                        if (ore.TargetColor != null && !ore.TargetColor.StartsWith("#")) ore.TargetColor = "#" + ore.TargetColor;
                        ore.ColorInfluence = oreMapping.Attribute("ColorInfluence")?.Value;
                        def.OreMappings.Add(ore);
                    }
                    catch { continue; }
                }
            }

            var complexMaterials = definition.Element("ComplexMaterials");
            if (complexMaterials != null)
            {
                foreach ( var complexMaterial in complexMaterials.Elements("MaterialGroup"))
                {
                    var name = complexMaterial.Attribute("Name")?.Value;
                    var value = complexMaterial.Attribute("Value")?.Value;
                    if (int.TryParse(value, out var iValue))
                    {
                        var cm = new ComplexMaterial { Name = name, Value = iValue };
                        def.ComplexMaterials.Add(cm);
                    }
                }
            }

            var environmentItems = definition.Element("EnvironmentItems");
            if (environmentItems != null)
            {
                foreach (var item in environmentItems.Elements("Item"))
                {
                    var env = new EnvironmentItem { Material = new() };
                    var biomes = item.Element("Biomes");
                    if (biomes != null)
                    {
                        var biome = biomes.Element("Biome");
                        if (biome != null)
                        {
                            if (int.TryParse(biome.Value, out var b)) env.Biome = b;
                        }
                    }
                    var materials = item.Element("Materials");
                    if (materials != null)
                    {
                        var material = materials.Element("Material");
                        if (material != null)
                        {
                            env.Material.Name = material.Value;
                            // TODO: Value, MaxDepth are stored in another node
                        }

                    }
                    var rule = item.Element("Rule");
                    if (rule != null)
                    {
                        double min, max;
                        RuleGetMinMax(rule.Element("Height"), out min, out max);
                        env.MinHeight = min; env.MaxHeight = max;
                        RuleGetMinMax(rule.Element("Latitude"), out min, out max);
                        env.MinLatitude = min; env.MaxLatitude = max;
                        RuleGetMinMax(rule.Element("Slope"), out min, out max);
                        env.MinSlope = min; env.MaxSlope = max;
                    }
                    def.EnvironmentItems.Add(env);
                }
            }

            return true;
        }

        void RuleGetMinMax(XElement element, out double min, out double max)
        {
            min = 0;
            max = 0;
            if (element == null) return;
            var eMin = element.Attribute("Min");
            if (eMin != null) double.TryParse(eMin.Value, out min);
            var eMax = element.Attribute("Max");
            if (eMax != null) double.TryParse(eMax.Value, out max);
        }

        BitmapImage _tileUp;
        public BitmapImage TileUp
        {
            get => _tileUp;
            set => SetProp(ref _tileUp, value);
        }

        BitmapImage _tileFront;
        public BitmapImage TileFront
        {
            get => _tileFront;
            set => SetProp(ref _tileFront, value);
        }

        BitmapImage _tileRight;
        public BitmapImage TileRight
        {
            get => _tileRight;
            set => SetProp(ref _tileRight, value);
        }
        BitmapImage _tileBack;
        public BitmapImage TileBack
        {
            get => _tileBack;
            set => SetProp(ref _tileBack, value);
        }
        BitmapImage _tileLeft;
        public BitmapImage TileLeft
        {
            get => _tileLeft;
            set => SetProp(ref _tileLeft, value);
        }
        BitmapImage _tileDown;
        public BitmapImage TileDown
        {
            get => _tileDown;
            set => SetProp(ref _tileDown, value);
        }

        int _oreInspectorSize = 20;
        public int OreInspectorSize
        {
            get => _oreInspectorSize;
            set => SetProp(ref _oreInspectorSize, value);
        }

        void ClearImages()
        {
            foreach (var info in _images.Values)
            {
                if (info != null) info.Dispose();
            }
            _images.Clear();
        }

        void UpdateImages()
        {
            TileUp = null;
            TileFront = null;
            TileRight = null;
            TileBack = null;
            TileLeft = null;
            TileDown = null;

            var keys = _images.Keys;
            foreach (var key in keys)
            {
                if (SelectedPlanetDefinition == null) return;
                if (!_images.TryGetValue(key, out var value)) return;
                if (value == null) return;

                List<OreMapping> oreMappings = null;
                if (SelectedOreType != null && OreMappings != null && OreMappings.Count > 0)
                {
                    oreMappings = OreMappings.ToList();
                }
                int? biome = null;
                if (SelectedEnvironmentItem != null) biome = SelectedEnvironmentItem.Biome;
                int? complexMaterial = null;
                if (SelectedComplexMaterial != null) complexMaterial = SelectedComplexMaterial.Value;
                var image = value.CreateBitmapImage(!ShowGradients, ShowOreLocations, ShowLakes, complexMaterial, oreMappings, ShowBiomes, biome);

                switch (key)
                {
                    case CubeMapFace.Back: TileBack = image; break;
                    case CubeMapFace.Down: TileDown = image; break;
                    case CubeMapFace.Front: TileFront = image; break;
                    case CubeMapFace.Left: TileLeft = image; break;
                    case CubeMapFace.Right: TileRight = image; break;
                    case CubeMapFace.Up: TileUp = image; break;
                }
            }
        }

        Dictionary<CubeMapFace, ImageData> _images = new();

        public ImageData GetInfo(CubeMapFace face, CancellationToken token)
        {
            var dir = Path.GetDirectoryName(_lastOpenedFile);
            var imageDir = Path.Combine(dir, "PlanetDataFiles", SelectedPlanetDefinition.Name);
            if (!Directory.Exists(imageDir)) return null;
            return ImageData.Create(imageDir, face, token);
        }

        Dictionary<CubeMapFace, ImageData> _materialMaps = new();
        Dictionary<CubeMapFace, ImageData> _heightMaps = new();

        public ICommand NoiseMapGeneratorCommand => new RelayCommand(o =>
        {
            //var ng = new NoiseGeneratorView();
            //ng.Show();
        });

        public ICommand RedistributeOreCommand => new RelayCommand(o =>
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "PNG Files|*.png";
            sfd.FileName = "specifyFolder.png";
            if (sfd.ShowDialog() != true)
                return;

            // Collect ore info
            var setup = new RedistributionSetup();
            //var existingOreDefs = SelectedPlanetDefinition.OreMappings.Select(o => o.Type).Distinct().ToList();
            foreach (var oredef in OreTypes)
            {
                var info = new RedistributionSetupViewModel.OreInfo { Name = oredef.OreType, SpawnWeight = (int)(oredef.Percentage + .5) };
                info.AddDefaultMapping();
                setup.ViewModel.OreInfos.Add(info);
            }
            if (setup.ShowDialog() != true)
                return;

            // Update ore mapping values
            var nextOreValue = setup.ViewModel.FinalizeList();

            Func<int, int> GetTier = (d) =>
            {
                if (d <= 2) return 1;
                if (d <= 5) return 2;
                return 3;
            };
            if (nextOreValue >= 256)
            {
                MessageBox.Show("Too many ore definitions in oremappings.xml. This will not work!",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var directory = Path.GetDirectoryName(sfd.FileName);

            Dictionary<string, List<OreMapping>> mappingsDictionary = new();

            // Save ore mappings as XML
            var xmlFileName = Path.Combine(directory, "oremappings.xml");
            if (File.Exists(xmlFileName))
            {
                if (MessageBox.Show("File \r\n'" + xmlFileName + "'\r\n already exists. " +
                        "Override?\r\nProcess will abort if 'No' is selected.", "File found", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;
            }
            try
            {
                var node = new XElement("OreMappings");
                foreach (var info in setup.ViewModel.OreInfos)
                {
                    var list = new List<OreMapping>();
                    list.AddRange(info.OreMappings);
                    mappingsDictionary[info.Name] = list;

                    foreach (var mapping in list)
                    {
                        if (mapping.Value > 0)
                        {
                            mapping.Type = info.Name;
                            node.Add(mapping.ToXElement());
                        }
                    }
                }
                var doc = new XDocument(node);
                doc.Save(xmlFileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            var keys = _images.Keys;
            var rnd = new Random(setup.ViewModel.Seed); // Seed 0 is better for debugging.
            var normal = new Normal(rnd);
            bool overridefiles = false;
            foreach (var key in keys)
            {
                if (!_images.TryGetValue(key, out var value)) return;

                var fileName = Path.Combine(directory, key.ToString().ToLower() + "_mat.png");
                if (!overridefiles && File.Exists(fileName))
                {
                    if (MessageBox.Show("File \r\n'" + fileName + "'\r\n already exists. " +
                        "Override this and any other PNG files?\r\nProcess will abort if 'No' is selected.", "File found", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                    overridefiles = true;
                }

                var image = new ImageData(key);

                // Clear ores: Blue = 255
                Parallel.For(0, 2048, x =>
                {
                    Parallel.For(0, 2048, y =>
                    {
                        image.R[x, y] = value.R[x, y];
                        image.G[x, y] = value.G[x, y];
                        image.B[x, y] = 255;
                    });
                });

                int spawnRate = setup.ViewModel.OreSpawnRate; // Default 3000
                if (spawnRate < 100) spawnRate = 100;
                else if (spawnRate > 2000000) spawnRate = 2000000;

                double prop = 1.0 / spawnRate;//(3136);
                double nBaseMin = 0.72; // This is just a random number I picked
                double nBaseMax = nBaseMin + prop;
                for (int x = 0; x < 2048; ++x)
                {
                    for (int y = 0; y < 2048; ++y)
                    {
                        // Check if ore spawns at this location
                        var r = rnd.NextDouble();
                        if (r < nBaseMin || r > nBaseMax) continue;

                        // Decide which ore is spawned
                        var info = setup.ViewModel.PickRandomOreWeighted(rnd);
                        var spawnSize = info.TypicalSize;
                        var depthMin = info.TypicalDepthMin;
                        var depthMax = info.TypicalDepthMax;

                        int depth = -1;
                        if (info.PreferredDepth > -1)
                        {
                            double[] depthWeights = new double[10] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
                            if (!info.VeryDeepOre) depthWeights[9] = 0;
                            if (depthMin > 0)
                            {
                                for (int i = 0; i < depthMin; ++i) depthWeights[i] = 0;
                            }
                            if (depthMax > -1)
                            {
                                for (int i = 8; i > depthMax; --i) depthWeights[i] = 0;
                            }
                            double weightSum = 0;
                            for (int i = 0; i < 10; ++i)
                            {
                                if (depthWeights[i] == 0) continue;
                                double stdDev = setup.ViewModel.StdDevDepth;
                                var divider = stdDev * Math.Sqrt(2 * Math.PI);
                                var eTerm = -0.5 * Math.Pow((i - info.PreferredDepth) / stdDev, 2);
                                depthWeights[i] = Math.Exp(eTerm) / divider; // 10 is used to roughly get a prob of 1.0 for preferred depth
                                weightSum += depthWeights[i];
                            }
                            var depthRnd = rnd.NextDouble() * weightSum;
                            weightSum = 0;
                            for (int i = 0; i < 10; ++i)
                            {
                                if (depthWeights[i] == 0) continue;
                                weightSum += weightSum += depthWeights[i];
                                if (depthRnd <= weightSum)
                                {
                                    depth = i;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (depthMin == -1 && depthMax == -1)
                            {
                                depth = rnd.Next(0, info.VeryDeepOre ? 9 : 10); // Max = 9
                            }
                            else if (depthMin == -1 && depthMax > -1)
                            {
                                depth = rnd.Next(0, depthMax + 1);
                            }
                            else if (depthMin >= -1 && depthMax == -1)
                            {
                                depth = rnd.Next(depthMin, info.VeryDeepOre ? 9 : 10);
                            }
                            else //if (depthMin >= -1 && depthMax >= -1)
                            {
                                depth = rnd.Next(depthMin, depthMax + 1);
                            }
                        }
                        if (depth == -1) continue;


                        // Randomize if not set by user
                        if (spawnSize == 0) spawnSize = rnd.Next(5, 31);
                        // Gauss randomizer
                        if (setup.ViewModel.StdDev > 0)
                        {
                            var stdDevPercentage = setup.ViewModel.StdDev / 100.0;
                            spawnSize = (int)(normal.Next(spawnSize, spawnSize * stdDevPercentage) + 0.5);
                            if (spawnSize < 1) spawnSize = 1; else if (spawnSize > 500) spawnSize = 500;
                        }

                        //int defaultTier = GetTier(depth);
                        int defaultDepthIndex = depth;
                        int maxDepthIndex = info.VeryDeepOre ? 9 : 8;
                        if (depthMax > -1 && depthMax < maxDepthIndex) maxDepthIndex = depthMax;
                        int minDepthIndex = 0;
                        if (depthMin > -1 && depthMin > minDepthIndex) minDepthIndex = depthMin;

                        // Get mapping info for pixel value
                        var mappings = mappingsDictionary[info.Name];

                        HashSet<OreToDraw> drawnOre = new();
                        drawnOre.Add(new OreToDraw { X = x, Y = y, Value = mappings[defaultDepthIndex].Value });
                        int lastDepthIndex = defaultDepthIndex;
                        int lastX = x;
                        int lastY = y;

                        // Vanilla ore scanner max range is 150m
                        // Only index 6 is detectable from surface
                        // Start there and fall down to default depth index
                        bool dive = false;
                        if (lastDepthIndex > 6 && minDepthIndex <= 6)
                        {
                            lastDepthIndex = 5; // We call ++ below to set it to 6
                            dive = true;
                        }

                        // Draw ore
                        // Strategy:
                        // - Start at X,Y
                        // - Select random neighbor and draw if not already drawn
                        // - If all neighbors are already drawn, escape in straight random direction until a non drawn field is available
                        // - While drawing
                        // - If last drawn height is not starting height give a 30% to jump back to starting height. 20% Chance of getting further away.
                        //   50% of staying at same height.
                        for (int i=1;i<spawnSize;++i) // We already added one pixel at this point, so start with 1
                        {
                            var direction = rnd.Next(4);
                            var nextOreToDraw = new OreToDraw();
                            // Check if that pixel is already painted:
                            int directionTries = 0;
                            bool foundFreePixel = false;
                            while (directionTries < 4)
                            {
                                ++direction;
                                if (direction > 3) direction = 0;
                                // direction: 0 -> left, 1 -> up, 2 -> right, 3 -> down
                                int dx, dy;

                                // 10% chance of skipping over a pixel
                                var skipX = rnd.NextDouble() < 0.1 ? 2 : 1;
                                var skipY = rnd.NextDouble() < 0.1 ? 2 : 1;

                                GetDeltas(direction, out dx, out dy);
                                nextOreToDraw.X = lastX + dx * skipX;
                                nextOreToDraw.Y = lastY + dy * skipY;

                                if (!drawnOre.Contains(nextOreToDraw))
                                {
                                    foundFreePixel = true;
                                    break;
                                }
                                ++directionTries;
                            }
                            if (!foundFreePixel)
                            {
                                int newX = lastX;
                                int newY = lastY;
                                // We're stuck. Get unstuck by going into random direction, brute force
                                int pleaseDontHang = 0;
                                int dx, dy;
                                GetDeltas(direction, out dx, out dy);
                                if (lastX < 500 && dx < 0) dx = 1;
                                if (lastY < 500 && dy < 0) dy = 1;
                                if (lastX > 1800 && dx > 0) dx = -1;
                                if (lastY > 1800 && dy > 0) dy = -1;
                                nextOreToDraw.X = lastX;
                                nextOreToDraw.Y = lastY;
                                while (pleaseDontHang++ < 1000)
                                {
                                    nextOreToDraw.X += dx;
                                    nextOreToDraw.Y += dy;
                                    if (!drawnOre.Contains(nextOreToDraw))
                                    {
                                        foundFreePixel = true;
                                        break;
                                    }
                                }
                            }
                            if (!foundFreePixel) break; // Bye bye. Don't hammer your head against a wall

                            lastX = nextOreToDraw.X;
                            lastY = nextOreToDraw.Y;
                            drawnOre.Add(nextOreToDraw);
                            var depthMod = rnd.NextDouble();

                            if (dive)
                            {
                                lastDepthIndex++;
                                if (lastDepthIndex >= defaultDepthIndex)
                                    dive = false;
                            }
                            // Give a 75% chance of staying at the same height if height is starting height
                            else if (lastDepthIndex == defaultDepthIndex)
                            {
                                if (depthMod >= 0.75)
                                {
                                    // Flip to neighbor index
                                    if (lastDepthIndex <= 0) lastDepthIndex++;
                                    else if (lastDepthIndex >= maxDepthIndex) lastDepthIndex--;
                                    else
                                    {
                                        // both directions possible
                                        // 70% chance of staying in the same tier
                                        if (rnd.NextDouble() > 0.7)
                                        {
                                            if (rnd.Next(2) == 0) lastDepthIndex--; else lastDepthIndex++;
                                        }
                                        else
                                        {
                                            var oldTier = GetTier(lastDepthIndex);
                                            var tierDown = GetTier(lastDepthIndex + 1);
                                            var tierUp = GetTier(lastDepthIndex - 1);
                                            if (tierDown != oldTier && tierUp == oldTier)
                                                lastDepthIndex--;
                                            else if (tierDown == oldTier && tierUp != oldTier)
                                                lastDepthIndex++;
                                            else
                                            {   /* Same as random above. Reaching this shouldn't be possible though */
                                                if (rnd.Next(2) == 0) lastDepthIndex--; else lastDepthIndex++;
                                            }
                                        }

                                    }
                                }
                            }
                            // Give a 30% chance to jump back to starting height and 20% chance of getting further away
                            else
                            {
                                if (depthMod <= 0.3)
                                {
                                    lastDepthIndex += Math.Sign(defaultDepthIndex - lastDepthIndex);
                                }
                                else if (depthMod <= 0.5) // 0.5-0.3 -> 20%
                                {
                                    if (lastDepthIndex < defaultDepthIndex)
                                    {
                                        lastDepthIndex--;
                                    }
                                    else
                                    {
                                        lastDepthIndex++;
                                    }
                                }
                                // else: 50% chance of staying
                            }
                            // Ignore limits while diving
                            if (!dive)
                            {
                                if (lastDepthIndex > maxDepthIndex) lastDepthIndex = maxDepthIndex;
                                else if (lastDepthIndex < minDepthIndex) lastDepthIndex = minDepthIndex;
                            }

                            // Get value for depth
                            nextOreToDraw.Value = mappings[lastDepthIndex].Value;
                        }

                        foreach (var drawn in drawnOre)
                        {
                            if (drawn.Value > 0 && drawn.Value < 256) // don't draw disabled entries which have value -1. See FinalizeList()
                                image.B[drawn.X, drawn.Y] = (byte)drawn.Value;

                        }
                    }
                }
                image.SaveMaterialMap(fileName);
            }
            MessageBox.Show("Finished!\r\nPlease copy the pngs to the 'PlanetDataFiles\\PlanetName' folder.\r\nThen open the planet definition SBC\r\nand copy the content of 'oremappings.xml' to the corresponding section.", "Ore Redistribution", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start("explorer.exe", xmlFileName);
        },
        o => SelectedPlanetDefinition != null);

        public ICommand DeselectOreTypeCommand => new RelayCommand(o =>
        {
            SelectedOreType = null;
        }, o => SelectedOreType != null);

        public ICommand DeselectEvironmentItemCommand => new RelayCommand(o =>
        {
            SelectedEnvironmentItem = null;
        }, o=> SelectedEnvironmentItem != null);

        void GetDeltas(int direction, out int dx, out int dy)
        {
            switch (direction)
            {
                case 0: // Left
                    dx = -1;
                    dy = 0;
                    return;
                case 1: // Up
                    dx = 0;
                    dy = -1;
                    return;
                case 2: // Right
                    dx = 1;
                    dy = 0;
                    return;
                case 3: // Down
                    dx = 0;
                    dy = 1;
                    return;
            }
            dx = 0;
            dy = 0;
        }

        class OreToDraw
        {
            int _x,_y;

            public int X
            {
                get => _x;
                set
                {
                    if (value < 0) _x = 0;
                    else if (value > 2047) _x = 2047;
                    else _x = value;
                }
            }
            public int Y
            {
                get => _y;
                set
                {
                    if (value < 0) _y = 0;
                    else if (value > 2047) _y = 2047;
                    else _y = value;
                }
            }
            public int Value; // Selectively ignored for hash

            public override bool Equals(object obj)
            {
                if (obj is not OreToDraw ore) return false;
                return X == ore.X && Y == ore.Y;
            }

            public override int GetHashCode()
            {
                return X.GetHashCode() ^ Y.GetHashCode();
            }
        }

        public ICommand CreateOcclusionMapsCommand => new RelayCommand(o =>
        {
            var dir = Path.GetDirectoryName(_lastOpenedFile);
            var imageDir = Path.Combine(dir, "PlanetDataFiles", SelectedPlanetDefinition.Name);
            var msgRes = MessageBox.Show("This will create the '*_add.png' files.\r\nThey don't seem to have any effect, but whatever...\r\nContinue?", "Warning", MessageBoxButton.OKCancel);
            if (msgRes != MessageBoxResult.OK) return;

            // Write files

            foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
            {
                var faceName = face.ToString().ToLower();
                dynamic material = SixLabors.ImageSharp.Image.Load(Path.Combine(imageDir, faceName + "_mat.png"));
                var addImage = new SixLabors.ImageSharp.Image<Rgba32>(2048, 2048);
                for (int x = 0; x < 2048; ++x)
                    for (int y = 0; y < 2048; ++y)
                    {
                        // Spaced that have ore must have red value of 144. All other rgb values are 0.
                        var blue = material[x, y].B;
                        if (blue == 255)
                            addImage[x, y] = new Rgba32(0, 0, 0);
                        else
                            addImage[x, y] = new Rgba32(144, 0, 0);
                    }
                addImage.SaveAsPng(Path.Combine(imageDir, faceName + "_add.png"));
            }
            MessageBox.Show("Finished");
        }, o=> SelectedPlanetDefinition != null);

        public ICommand RewriteBiomesCommand => new RelayCommand(o =>
        {
            var msgRes = MessageBox.Show("This will override the biomes of the currently selected planet.\r\nOnly recommended for EarthLike and Alien\r\nContinue?", "Warning", MessageBoxButton.OKCancel);
            if (msgRes != MessageBoxResult.OK) return;

            var dir = Path.GetDirectoryName(_lastOpenedFile);
            var imageDir = Path.Combine(dir, "PlanetDataFiles", SelectedPlanetDefinition.Name);
            if (!Directory.Exists(imageDir))
            {
                MessageBox.Show("Cannot find PlanetDataFiles folder.\r\nMake sure it exists!\r\n" + imageDir);
                return;
            }

            HashSet<int> allBiomes = new HashSet<int>();
            // Collect all required values:
            foreach (var envItem in EnvironmentItems)
            {
                allBiomes.Add(envItem.Biome);
            }

            // Analyze existing files by latitude
            // Remember all Biome values by number and quantity
            Dictionary<int, Dictionary<int, int>> biomeDic = new();
            foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
            {
                var faceName = face.ToString().ToLower();
                dynamic image = SixLabors.ImageSharp.Image.Load(Path.Combine(imageDir, faceName + "_mat.png"));
                for (int x = 0; x < 2048; ++x)
                    for (int y = 0; y < 2048; ++y)
                    {
                       int val = image[x, y].G;
                        if (!allBiomes.Contains(val))
                            val = -1; // Count how many pixels don't have biome def

                        var point = CoordinateHelper.GetNormalizedSphereCoordinates(face, x, y); // Based on 2048*2048!!
                        var latitude = CoordinateHelper.ToLongitudeLatitude(point).latitude; // -90 to 90
                        var bucket = GetLatitudeBucket(latitude);
                        if (!biomeDic.ContainsKey(bucket)) { biomeDic[bucket] = new(); }
                        if (!biomeDic[bucket].ContainsKey(val))
                            biomeDic[bucket][val] = 1;
                        else
                            biomeDic[bucket][val]++;
                    }
            }

            // EarthLike: +- 5° has only 113 + 85, ignore others
            foreach (var key in biomeDic[0].Keys.ToList())
            {
                if (key != 113 && key != 85 && key != -1) biomeDic[0].Remove(key);
            }

            // Generate new biome map
            byte[] planetSurface = new byte[2048 * 2048 * 6];

            int surfaceBiomeCount = 0;
            var random = new Random(0);
            Action<int,int> placeBiome = (biomeIndex, biomeValue) =>
            {
                lock (planetSurface)
                {
                    if (biomeValue > 0)
                    {
                        if (planetSurface[biomeIndex] == 0) ++surfaceBiomeCount;
                        planetSurface[biomeIndex] = (byte)biomeValue;
                    }
                    else
                    {
                        if (planetSurface[biomeIndex] > 0) --surfaceBiomeCount;
                        planetSurface[biomeIndex] = 0;
                    }
                }
            };

            // Strategy: Drop random seeds of biomes that grow until map is filled to 83% (same value as vanilla)

            // Part 1: place seeds on 4% of all pixels
            double seedPercentage = 0.09;
            while (surfaceBiomeCount < planetSurface.Length * seedPercentage)
            {
                var index = random.NextTS(planetSurface.Length);
                var coord = PlanetSurfaceToCoordinates(index);
                var point = CoordinateHelper.GetNormalizedSphereCoordinates(coord.face, coord.x, coord.y);
                var latitude = CoordinateHelper.ToLongitudeLatitude(point).latitude; // -90 to 90
                var bucket = GetLatitudeBucket(latitude);
                var dic = biomeDic[bucket];
                placeBiome(index, RandomBiomePick(dic, random));
            }

            // Part 2: Let the seeds grow until surface is covered
            int xx = 0;
            while (surfaceBiomeCount < planetSurface.Length * 0.97 && ++xx < 8)
            {
                // Find all current seeds
                List<(int surfaceIndex, byte value)> seeds = new();
                for (int i = 0; i < planetSurface.Length; i++)
                {
                    var surfaceValue = planetSurface[i];
                    if (surfaceValue == 0) continue;
                    seeds.Add((i, surfaceValue));
                }
                // Randonly pick seeds and let them grow
                Parallel.For(0, seeds.Count, seedIndex =>
                {
                    (int surfaceIndex, byte value) seed;
                    lock (planetSurface)
                    {
                        seed = seeds[seedIndex];
                        if (seed.value != planetSurface[seed.surfaceIndex])
                            return; // Seed got destroyed
                    }

                    var coords = PlanetSurfaceToCoordinates(seed.surfaceIndex);
                    var pt = new CubeMapPointLight { Face = coords.face, X = coords.x, Y = coords.y };
                    int[] nmap = new int[4];
                    // Get all four neigbors
                    var nUp = CubeMapPointLight.GetPointRelativeTo(pt, 0, 1);
                    var indexUp = CoordinatesToPlanetSurface(nUp.Face, nUp.X, nUp.Y);
                    nmap[0] = planetSurface[indexUp];
                    var nDown = CubeMapPointLight.GetPointRelativeTo(pt, 0, -1);
                    var indexDown = CoordinatesToPlanetSurface(nDown.Face, nDown.X, nDown.Y);
                    nmap[1] = planetSurface[indexDown];
                    var nLeft = CubeMapPointLight.GetPointRelativeTo(pt, -1, 0);
                    var indexLeft = CoordinatesToPlanetSurface(nLeft.Face, nLeft.X, nLeft.Y);
                    nmap[2] = planetSurface[indexLeft];
                    var nRight = CubeMapPointLight.GetPointRelativeTo(pt, 1, 0);
                    var indexRight = CoordinatesToPlanetSurface(nRight.Face, nRight.X, nRight.Y);
                    nmap[3] = planetSurface[indexRight];

                    Func<int, int> NeighborMapIndexToSurfaceMapIndex = (nIndex) =>
                    {
                        if (nIndex == 0) return indexUp;
                        if (nIndex == 1) return indexDown;
                        if (nIndex == 2) return indexLeft;
                        return indexRight;
                    };

                    // Rules:
                    // - Prefer to grow into unoccupied spaces
                    // - Never grow into same biome
                    List<int> emptyNeighbors = new List<int>();
                    List<int> differentNeighbors = new List<int>();
                    for (int i = 0; i < nmap.Length; ++i)
                    {
                        if (nmap[i] > 0)
                        {
                            if (nmap[i] != seed.value)
                            {
                                differentNeighbors.Add(i);
                            }
                        }
                        else emptyNeighbors.Add(i);
                    }
                    if (emptyNeighbors.Count > 0)
                    {
                        // Grow into unoccupied space
                        var growTo = emptyNeighbors[random.NextTS(emptyNeighbors.Count)];
                        placeBiome(NeighborMapIndexToSurfaceMapIndex(growTo), seed.value);
                    }
                    // THIS DID NOT WORK PROPERLY
                    // Grow into other biome if same neighbor count is larger than that other biomes neighbor count
                    //else if (differentNeighbors.Count > 0)
                    //{
                    //    int power = 4 - differentNeighbors.Count;
                    //    if (power < 2) return;

                    //    // Check how many distinct neighbors we have
                    //    Dictionary<int, int> neighborCounts = new(); // Key=Biome, Value=Count
                    //    foreach (var neighbor in differentNeighbors)
                    //    {
                    //        var neighborVal = nmap[neighbor];
                    //        if (!neighborCounts.ContainsKey(neighborVal)) neighborCounts[neighborVal] = 1;
                    //        else neighborCounts[neighborVal]++;
                    //    }
                    //    var weakNeighbors = neighborCounts.Where(o => o.Value < power).ToList();
                    //    if (weakNeighbors.Count > 0)
                    //    {
                    //        weakNeighbors.Sort((a, b) => a.Value.CompareTo(b.Value));
                    //        var weakNeighborKey = weakNeighbors.First().Key;
                    //        List<int> neighborIndexes = new();
                    //        for (int i = 0; i < 4; ++i)
                    //            if (nmap[i] == weakNeighborKey) neighborIndexes.Add(i);
                    //        var growTo = neighborIndexes[random.NextTS(neighborIndexes.Count)];
                    //        placeBiome(NeighborMapIndexToSurfaceMapIndex(growTo), seed.value);
                    //    }
                    //}
                });
            }

            // TODO: Don't leave black spots. Ore display bug. Fill them randomly
            for (int i = 0; i < planetSurface.Length; ++i)
            {
                if (planetSurface[i] != 0) continue;
                var coords = PlanetSurfaceToCoordinates(i);

                var neigbors = new CubeMapPointLight[]
                {
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, -1, 0),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, -1, 1),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, -1, -1),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, 1, 0),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, 1, 1),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, 1, -1),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, 0, 1),
                    CubeMapPointLight.GetPointRelativeTo(new() { X = coords.x, Y = coords.y, Face = coords.face }, 0, -1),
                };
                var neighborsWithValue = neigbors.Where(x =>
                {
                    var index = CoordinatesToPlanetSurface(x.Face, x.X, x.Y);
                    var value = planetSurface[index];
                    return value != 0;
                }).ToList();
                Debug.Assert(neighborsWithValue.Count > 0);
                var randomNeighbor = neighborsWithValue[random.NextTS(neighborsWithValue.Count)];
                var indexOfRnd = CoordinatesToPlanetSurface(randomNeighbor.Face, randomNeighbor.X, randomNeighbor.Y);
                var value = planetSurface[indexOfRnd];
                foreach (var x in neigbors)
                {
                    var index = CoordinatesToPlanetSurface(x.Face, x.X, x.Y);
                    if (planetSurface[index] == 0)
                    {
                        planetSurface[index] = value;
                    }
                }
                planetSurface[i] = value;
            }

            // Write files

            foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
            {
                var faceName = face.ToString().ToLower();
                var image = new SixLabors.ImageSharp.Image<Rgba32>(2048,2048);
                dynamic source = SixLabors.ImageSharp.Image.Load(Path.Combine(imageDir, faceName + "_mat.png"));
                for (int x = 0; x < 2048; ++x)
                    for (int y = 0; y < 2048; ++y)
                    {
                        var surfaceVal = planetSurface[CoordinatesToPlanetSurface(face, x, y)];
                        image[x, y] = new Rgba32(surfaceVal /*source[x,y].R*/, surfaceVal, surfaceVal /*source[x,y].B*/);
                    }
                image.SaveAsPng(Path.Combine(imageDir, faceName + "_mat2.png"));
            }

            MessageBox.Show("Finished. Files were saved as '*_mat2.png' to preserve the original files.");
        }, o => SelectedPlanetDefinition != null);

        // Use a biome dictionary for a latitude.
        // Choose a random biome from that latitude.
        // Take relative quantities of different biomes into account
        int RandomBiomePick(Dictionary<int,int> dic, Random rnd)
        {
            // Sum up all biomes
            var sum = 0;
            var kvps = dic.ToList();
            foreach (var kvp in kvps)
            {
                sum += kvp.Value;
            }
            // Pick random number
            var pick = rnd.NextTS(sum);

            // Use counts like an index in a list
            // This should preserve the relative amounts of different biomes
            sum = 0;
            for (int i=0;i< kvps.Count; i++)
            {
                sum += kvps[i].Value;
                if (pick < sum) return kvps[i].Key;
            }
            // Fallback. Should not be hit.
            Debug.Assert(false);
            return kvps[0].Key;
        }

        (CubeMapFace face, int x, int y) PlanetSurfaceToCoordinates(int surfaceIndex)
        {
            var div = surfaceIndex / (2048*2048);
            var start = 2048 * 2048 * div;
            var posRel = surfaceIndex % (2048*2048);
            var y = posRel / 2048;
            var x = surfaceIndex % 2048;
            return ((CubeMapFace)div, x, y);
        }

        int CoordinatesToPlanetSurface(CubeMapFace face, int x, int y)
        {
            return (int)face * 2048 * 2048 + x + y * 2048;
        }

        // Turn latitude values into dictionary keys
        // Bucket size is width of longitude area to cover for one key.
        // Keens SBC use a granularity of 5°.
        int GetLatitudeBucket(double latitude, int bucketsize = 5)
        {
            for (int i = -90; i <= 90; i += bucketsize)
                if (latitude < i) return i;
            return 90;
        }

        public ICommand PlusCommand => new RelayCommand(o =>
        {
            if (OreInspectorSize < 100) OreInspectorSize += 10;
        });

        public ICommand MinusCommand => new RelayCommand(o =>
        {
            if (OreInspectorSize > 10) OreInspectorSize -= 10;
        });

        public ICommand CalcOreAmountCommand => new RelayCommand(o =>
        {
            // TODO: Calculate sum of ore patch * depth to get total volume of ore
        });
    }
}

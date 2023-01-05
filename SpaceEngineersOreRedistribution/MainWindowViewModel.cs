using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public ObservableCollection<string> OreTypes { get; } = new();

        PlanetDefinition _selectedPlanetDefinition;
        public PlanetDefinition SelectedPlanetDefinition
        {
            get => _selectedPlanetDefinition;
            set
            {
                if (!SetProp(ref _selectedPlanetDefinition, value)) return;
                ClearImages();
                OreTypes.Clear();
                EnvironmentItems.Clear();
                if (value == null)
                {
                    UpdateImages();
                    return;
                }
                HashSet<string> types = new();
                foreach (var item in value.OreMappings)
                {
                    types.Add(item.Type);
                }
                foreach (var type in types)
                    OreTypes.Add(type);

                foreach(var item in value.EnvironmentItems)
                {
                    EnvironmentItems.Add(item);
                }
                LoadImages();
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
        string _selectedOreType;
        public string SelectedOreType
        {
            get => _selectedOreType;
            set
            {
                if (!SetProp(ref _selectedOreType, value)) return;
                OreMappings.Clear();
                if (value == null || SelectedPlanetDefinition == null)
                {
                    if (ShowOreLocations)
                        UpdateImages();
                    return;
                }
                var mappings = SelectedPlanetDefinition.OreMappings.Where(x => x.Type == value);
                int colorIndex = 0;
                foreach (var mapping in mappings)
                {
                    if (colorIndex < _colors.Count)
                    {
                        var color = _colors[colorIndex++];
                        mapping.MapRgbValue = new(color.R, color.G, color.B);
                    }
                    else
                        mapping.MapRgbValue = new(0, 255, 0);
                    OreMappings.Add(mapping);
                }
                if (ShowOreLocations)
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

        public ObservableCollection<EnvironmentItem> EnvironmentItems { get; } = new();
        EnvironmentItem _selectedEnvironmentItem;
        public EnvironmentItem SelectedEnvironmentItem
        {
            get => _selectedEnvironmentItem;
            set => SetProp(ref _selectedEnvironmentItem, value);
        }

        bool _showOreLocations = true;
        public bool ShowOreLocations
        {
            get => _showOreLocations;
            set
            {
                if (!SetProp(ref _showOreLocations, value)) return;
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
                        ore.ColorInfluence = oreMapping.Attribute("ColorInfluence")?.Value;
                        def.OreMappings.Add(ore);
                    }
                    catch { continue; }
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
                System.Drawing.Bitmap b;
                if (ShowGradients)
                {
                    b = value.CalculateGradients();
                }
                else
                {
                    b = value.GetHeightMap();
                }
                if (ShowOreLocations)
                {
                    var oreMap = value.ShowOre(b, oreMappings);
                    b.Dispose();
                    b = oreMap;
                }

                var img = ImageData.FromBitmap(b);
                b.Dispose();

                switch (key)
                {
                    case "back": TileBack = img; break;
                    case "down": TileDown = img; break;
                    case "front": TileFront = img; break;
                    case "left": TileLeft = img; break;
                    case "right": TileRight = img; break;
                    case "up": TileUp = img; break;
                }
            }
        }

        void LoadImages()
        {
            ClearImages();
            _images["back"] = GetInfo("back_mat.png");
            _images["down"] = GetInfo("down_mat.png");
            _images["front"] = GetInfo("front_mat.png");
            _images["left"] = GetInfo("left_mat.png");
            _images["right"] = GetInfo("right_mat.png");
            _images["up"] = GetInfo("up_mat.png");
            UpdateImages();
        }

        ImageData GetInfo(string pngName)
        {
            var dir = Path.GetDirectoryName(_lastOpenedFile);
            var imageDir = Path.Combine(dir, "PlanetDataFiles", SelectedPlanetDefinition.Name);
            if (!Directory.Exists(imageDir)) return null;
            var file = Path.Combine(imageDir, pngName);
            if (!File.Exists(file)) return null;
            return new ImageData() { FileName = file };
        }

        Dictionary<string, ImageData> _images = new();

        public ICommand NoiseMapGeneratorCommand => new RelayCommand(o =>
        {
            //var ng = new NoiseGeneratorView();
            //ng.Show();
        });

        public ICommand RedistributeOreCommand => new RelayCommand(o =>
        {
            MessageBox.Show("WIP, Sorry not implemented yet!");
            return;

            var sfd = new SaveFileDialog();
            sfd.Filter = "PNG Files|*.png";
            sfd.FileName = "specifyFolder.png";
            if (sfd.ShowDialog() != true)
                return;

            int nextOreValue = 1;

            // Collect ore info
            var setup = new RedistributionSetup();
            var existingOreDefs = SelectedPlanetDefinition.OreMappings.Select(o => o.Type).Distinct().ToList();
            foreach (var oredef in existingOreDefs)
            {
                setup.ViewModel.OreInfos.Add(new() { Name = oredef });
            }
            if (setup.ShowDialog() != true)
                return;

            // Build the ore mapping list.
            // See the OreMappings.png in the screenshots folder of this repository to see the design of the mapping.
            //Dictionary<>

            // WIP


            var directory = Path.GetDirectoryName(sfd.FileName);
            var keys = _images.Keys;
            var rnd = new Random(0);
            foreach (var key in keys)
            {
                if (!_images.TryGetValue(key, out var value)) return;

                var fileName = Path.Combine(directory, key + "_mat.png");
                if (File.Exists(fileName))
                {
                    if (MessageBox.Show("File \r\n'" + fileName + "'\r\n already exists. " +
                        "Override?\r\nProcess will abort if 'No' is selected.", "File found", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                }

                using var gradients = value.CalculateGradients();
                using var materialMap = value.GetBitmap();

                var g = new LockBitmap(gradients);
                var m = new LockBitmap(materialMap);
                g.LockBits();
                m.LockBits();

                // Clear ores: Blue = 255
                for (int x=0;x<2048;++x)
                {
                    for (int y = 0; y < 2048; ++y)
                    {
                        var px = m.GetPixel(x, y);
                        m.SetPixel(x,y, System.Drawing.Color.FromArgb(px.R, px.G, 255));
                    }
                }

                // In original file each ore deposit is about 28 pixel away from the next deposit
                // in a checkboard pattern.
                // Lets randomize this a bit:
                // Square of 2*28 pixels width and length with 1 ore means:
                // 1 ore in 3136 pixels
                double prop = 1.0 / (3136);
                double nBaseMin = 0.72; // This is just a random number I picked
                double nBaseMax = nBaseMin + prop;
                for (int x = 0; x < 2048; ++x)
                {
                    for (int y = 0; y < 2048; ++y)
                    {
                        // Check if ore spawns at this location
                        var r = rnd.NextDouble();
                        if (r < nBaseMin || r > nBaseMax) continue;

                        // Spawn ore:
                        // Steps:
                        // - Decide which ore to spawn
                        // - Generate a vein using multiple [Start,Depth] sets.

                       //var chosenOreType = OreTypes[rnd.Next(OreTypes.Count)];
                       var px = m.GetPixel(x, y);
                    }
                }

                g.UnlockBits();
                m.UnlockBits();

                materialMap.Save(fileName);

            }

        },
        o => SelectedPlanetDefinition != null);

    }
}

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

                foreach (var item in value.EnvironmentItems)
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
            //MessageBox.Show("WIP, Sorry not implemented yet!");
            //return;

            var sfd = new SaveFileDialog();
            sfd.Filter = "PNG Files|*.png";
            sfd.FileName = "specifyFolder.png";
            if (sfd.ShowDialog() != true)
                return;

            // Collect ore info
            var setup = new RedistributionSetup();
            var existingOreDefs = SelectedPlanetDefinition.OreMappings.Select(o => o.Type).Distinct().ToList();
            foreach (var oredef in existingOreDefs)
            {
                setup.ViewModel.OreInfos.Add(new() { Name = oredef });
            }
            if (setup.ShowDialog() != true)
                return;

            setup.ViewModel.FinalizeList();
            // Build the ore mapping list.
            // See the OreMappings.png in the screenshots folder of this repository to see the design of the mapping.
            Dictionary<string, List<OreMapping>> mappingsDictionary = new();
            var oreTypeList = setup.ViewModel.OreInfos.Select(o => o.Name).Distinct().ToList();
            int nextOreValue = 1;
            foreach (var oreType in oreTypeList)
            {
                List<OreMapping> mList = new();
                // Tier 1: Flat surface ore. Depth: 3m - 15m
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 3, Depth = 3, ColorInfluence = "15", TargetColor = "616c83" });
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 5, Depth = 5, ColorInfluence = "15", TargetColor = "616c83" });
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 8, Depth = 7, ColorInfluence = "15", TargetColor = "616c83" });
                // Tier 2: Medium deep veins. Depth: 40m - 102m
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 40, Depth = 12, ColorInfluence = "15", TargetColor = "616c83" });
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 50, Depth = 22, ColorInfluence = "15", TargetColor = "616c83" });
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 70, Depth = 32, ColorInfluence = "15", TargetColor = "616c83" });
                // Tier 3: Very deep veins. Depth: 140m - 372m
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 140, Depth = 62, ColorInfluence = "15", TargetColor = "616c83" });
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 200, Depth = 72, ColorInfluence = "15", TargetColor = "616c83" });
                mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 370, Depth = 102, ColorInfluence = "15", TargetColor = "616c83" });
                // Bonus: Very deep ore. Depth: 370m - 450m
                if (setup.ViewModel.OreInfos.Any(o => o.Name == oreType && o.VeryDeepOre))
                    mList.Add(new OreMapping { Value = nextOreValue++, Type = oreType, Start = 370, Depth = 80, ColorInfluence = "15", TargetColor = "616c83" });
                mappingsDictionary[oreType] = mList;
            }
            Func<int, int> GetTier = (d) =>
            {
                if (d <= 2) return 1;
                if (d <= 5) return 2;
                return 3;
            };
            if (nextOreValue > 255)
            {
                MessageBox.Show("Oops! Something went wrong. We have too many ore definitions. Please try again.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var directory = Path.GetDirectoryName(sfd.FileName);

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
                foreach (var kv in mappingsDictionary)
                {
                    foreach (var mapping in kv.Value)
                    {
                        node.Add(mapping.ToXElement());
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
            var rnd = new Random(0); // Seed 0 is better for debugging.
            var normal = new Normal(50, 10, 0);
            bool overridefiles = false;
            foreach (var key in keys)
            {
                if (!_images.TryGetValue(key, out var value)) return;

                var fileName = Path.Combine(directory, key + "_mat.png");
                if (!overridefiles && File.Exists(fileName))
                {
                    if (MessageBox.Show("File \r\n'" + fileName + "'\r\n already exists. " +
                        "Override this and any other PNG files?\r\nProcess will abort if 'No' is selected.", "File found", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                    overridefiles = true;
                }

                // Final touch: Create the 'add' file:
                // All ore locations are drawn with a red value of 144 onto a black image
                var addFileName = Path.Combine(directory, key + "_add.png");
                using var bmap = new System.Drawing.Bitmap(2048, 2048, PixelFormat.Format32bppArgb);
                var lbmap = new LockBitmap(bmap);
                lbmap.LockBits();

                using var gradients = value.CalculateGradients();
                using var materialMap = value.GetBitmap();

                var g = new LockBitmap(gradients);
                var m = new LockBitmap(materialMap);
                g.LockBits();
                m.LockBits();

                // Clear ores: Blue = 255
                for (int x = 0; x < 2048; ++x)
                {
                    for (int y = 0; y < 2048; ++y)
                    {
                        var px = m.GetPixel(x, y);
                        m.SetPixel(x, y, System.Drawing.Color.FromArgb(px.R, px.G, 255));
                        lbmap.SetPixel(x, y, System.Drawing.Color.FromArgb(255, 0, 0, 0));
                    }
                }
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
                        var depth = info.TypicalDepth;
                        // Randomize if not set by user
                        if (spawnSize == 0) spawnSize = rnd.Next(5, 26);
                        if (depth == -1) depth = rnd.Next(0, info.VeryDeepOre ? 9:10);
                        // Gauss randomizer
                        var stdDevPercentage = setup.ViewModel.StdDev / 100.0;
                        spawnSize = (int)(normal.Next(spawnSize, 50 * stdDevPercentage) + 0.5);
                        if (spawnSize < 1) spawnSize = 1; else if (spawnSize > 50) spawnSize = 50;
                        depth = (int)(normal.Next(depth, 10 * stdDevPercentage) + 0.5);
                        int maxDepthIndex = info.VeryDeepOre ? 9 : 8;
                        if (depth < 0) depth = 0; else if (depth > 8) depth = maxDepthIndex;

                        int defaultTier = GetTier(depth);
                        int defaultDepthIndex = depth;

                        // Get mapping info for pixel value
                        var mappings = mappingsDictionary[info.Name];

                        HashSet<OreToDraw> drawnOre = new();
                        drawnOre.Add(new OreToDraw { X = x, Y = y, Value = mappings[defaultDepthIndex].Value });
                        int lastDepthIndex = defaultDepthIndex;
                        int lastX = x;
                        int lastY = y;

                        // Draw ore
                        // Strategy:
                        // - Start at X,Y
                        // - Select random neighbor and draw if not already drawn
                        // - If all neighbors are already drawn, escape in straight random direction until a non drawn field is available
                        // - While drawing
                        // - If last drawn height is not starting height give a 30% to jump back to starting height. 20% Chance of getting further away.
                        //   50% of staying at same height.

                        for (int i=0;i<spawnSize;++i)
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
                                GetDeltas(direction, out dx, out dy);
                                nextOreToDraw.X = lastX + dx;
                                nextOreToDraw.Y = lastY + dy;

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
                            // Give a 75% chance of staying at the same height if height is starting height
                            if (lastDepthIndex == defaultDepthIndex)
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
                            if (lastDepthIndex > maxDepthIndex) lastDepthIndex = maxDepthIndex;
                            else if (lastDepthIndex < 0) lastDepthIndex = 0;
                            // Get value for depth
                            nextOreToDraw.Value = mappings[lastDepthIndex].Value;
                        }

                        foreach (var drawn in drawnOre)
                        {
                            var px = m.GetPixel(drawn.X, drawn.Y);
                            m.SetPixel(drawn.X, drawn.Y, System.Drawing.Color.FromArgb(px.R, px.G, drawn.Value));
                            lbmap.SetPixel(drawn.X, drawn.Y, System.Drawing.Color.FromArgb(255,144, 0, 0));
                        }
                    }
                }

                g.UnlockBits();
                m.UnlockBits();

                materialMap.Save(fileName);

                lbmap.UnlockBits();
                bmap.Save(addFileName);

            }
            MessageBox.Show("Finished!\r\nPlease copy the pngs to the 'PlanetDataFiles\\PlanetName' folder.\r\nThen open the planet definition SBC\r\nand copy the content of 'oremappings.xml' to the corresponding section.", "Ore Redistribution", MessageBoxButton.OK, MessageBoxImage.Information);
        },
        o => SelectedPlanetDefinition != null);

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
    }
}

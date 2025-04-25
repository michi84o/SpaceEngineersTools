using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpaceEngineersOreRedistribution;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

namespace ComplexMaterialViewer
{
    class MainWindowViewModel : PropChangeNotifier
    {
        public Action UpdateCanvasAction { get; set; }
        public List<Rule> CanvasRules { get; } = new List<Rule>();

        ObservableCollection<Definition> _definitions = new();
        public ObservableCollection<Definition> Definitions => _definitions;
        Definition _selectedDefinition;
        public Definition SelectedDefinition
        {
            get => _selectedDefinition;
            set
            {
                if (SetProp(ref _selectedDefinition, value))
                    UpdateSummary();
            }
        }

        MaterialGroup _selectedMaterialGroup;
        public MaterialGroup SelectedMaterialGroup
        {
            get => _selectedMaterialGroup;
            set
            {
                if (SetProp(ref _selectedMaterialGroup, value))
                    UpdateSummary();
            }
        }

        Rule _selectedRule;
        public Rule SelectedRule
        {
            get => _selectedRule;
            set => SetProp(ref _selectedRule, value);
        }

        int _minLatitude = 0;
        public int MinLatitude
        {
            get => _minLatitude;
            set
            {
                if (SetProp(ref _minLatitude, value))
                    UpdateSummary();
            }
        }

        int _maxLatitude = 90;
        public int MaxLatitude
        {
            get => _maxLatitude;
            set
            {
                if (SetProp(ref _maxLatitude, value))
                    UpdateSummary();
            }
        }

        double _minHeight = 0;
        public double MinHeight
        {
            get => _minHeight;
            set
            {
                if (SetProp(ref _minHeight, value))
                    UpdateCanvasAction?.Invoke();
            }
        }

        double _maxHeight = 1;
        public double MaxHeight
        {
            get => _maxHeight;
            set
            {
                if (SetProp(ref _maxHeight, value))
                    UpdateCanvasAction?.Invoke();
            }
        }

        string _summary;
        public string Summary
        {
            get => _summary;
            set => SetProp(ref _summary, value);
        }

        public ObservableCollection<Rule> Heights { get; } = new();
        Rule _selectedHeight;
        public Rule SelectedHeight
        {
            get => _selectedHeight;
            set
            {
                var backup = _selectedHeight;
                if (!SetProp(ref _selectedHeight, value)) return;
                if (backup != null) backup.IsHighlighted = false;
                if (value != null) value.IsHighlighted = true;
            }
        }

        void UpdateSummary()
        {
            SelectedHeight = null;
            CanvasRules.Clear();
            UpdateCanvasAction?.Invoke();
            Summary = "";
            Heights.Clear();
            if (MaxLatitude < MinLatitude) return;
            if (SelectedMaterialGroup == null) return;

            //       [........]
            // 1 [...|...]    |
            // 2     |      [.|.....]
            // 3  [..|........|....]
            // 4     | [..]   |

            foreach (var rule in SelectedMaterialGroup.Rules)
            {
                rule.Actions.Clear();
            }

            var rules = SelectedMaterialGroup.Rules.Where(x=>
                x.Latitude.Min <= MinLatitude && x.Latitude.Max > MinLatitude ||  // 1 + 3
                x.Latitude.Min < MaxLatitude && x.Latitude.Max >= MaxLatitude  ||  // 2 + 3
                x.Latitude.Min >= MinLatitude && x.Latitude.Max <= MaxLatitude)   // 4
                .ToList();

            foreach (var rule in rules)
            {
                XDocument doc = XDocument.Parse(rule.Xml);
                if (doc.Root != null)
                {
                    // Override required since our zones are shifted.
                    doc.Root.Element("Latitude").Attribute("Min").Value = "0";
                    doc.Root.Element("Latitude").Attribute("Max").Value = "90";
                    Summary += doc.Root.ToString() + "\r\n";
                }
            }

            rules.Sort((a, b) => a.Height.Min.CompareTo(b.Height.Min));
            foreach (var rule in rules)
            {
                Heights.Add(rule);
            }

            CanvasRules.AddRange(rules);
            UpdateCanvasAction?.Invoke();
        }

        string _lastSbc;
        public ICommand OpenSbcCommand => new RelayCommand(o =>
        {
            _lastSbc = null;
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "SBC Files|*.sbc|XML Files|*.xml|All Files|*.*";
            if (dlg.ShowDialog() != true) return;

            try
            {
                _definitions.Clear();

                XDocument doc = XDocument.Load(dlg.FileName);
                if (doc.Root?.Name != "Definitions")
                {
                    MessageBox.Show("Root node of file does not have name \"Definitions\"!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _lastSbc = dlg.FileName;

                var defs = doc.Root.Elements("Definition")?.ToList();
                if (defs == null || defs.Count == 0)
                {
                    // Triton?
                    defs = doc.Root.Element("PlanetGeneratorDefinitions")?.Elements("PlanetGeneratorDefinition")?.ToList();
                    if (defs == null || defs.Count == 0)
                    {
                        MessageBox.Show("No definitions found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                foreach ( var def in defs )
                {
                    var complex = def.Element("ComplexMaterials");
                    if (complex == null) continue;
                    var matGroups = complex.Elements("MaterialGroup").ToList();
                    if (matGroups.Count == 0) continue;
                    Definition ddef = new();
                    ddef.Name = def.Element("Id")?.Element("SubtypeId")?.Value ?? "<planet>";

                    var custom = def.Element("CustomMaterialTable");
                    if (custom != null)
                    {
                        var customItems = custom.Elements("Material");
                        foreach (var item in customItems)
                        {
                            var customMat = new CustomMaterial();
                            customMat.Material = item.Attribute("Material")?.Value ?? "";
                            if (int.TryParse(item.Attribute("Value")?.Value, out var value)) customMat.Value = value;
                            if (int.TryParse(item.Attribute("MaxDepth")?.Value, out value)) customMat.MaxDepth = value;
                            ddef.CustomMaterials.Add(customMat);
                        }
                    }

                    foreach ( var matGroup in matGroups )
                    {
                        MaterialGroup grp = new();
                        grp.Name = matGroup.Attribute("Name")?.Value ?? "";
                        ddef.ComplexMaterials.Add(grp);
                        if (int.TryParse(matGroup.Attribute("Value")?.Value, out var value)) grp.Value = value;
                        foreach (var rule in matGroup.Elements("Rule"))
                        {
                            var rrule = new Rule();
                            grp.Rules.Add(rrule);
                            rrule.Xml = rule.ToString();
                            var layers = rule.Element("Layers")?.Elements("Layer");
                            if (layers != null)
                                foreach (var layer in layers)
                                {
                                    var llayer = new Layer();
                                    rrule.Layers.Add(llayer);
                                    llayer.Material = layer.Attribute("Material")?.Value ?? "";
                                    if (int.TryParse(layer.Attribute("Depth")?.Value, out var depth)) llayer.Depth = depth;
                                }
                            rrule.Height = MinMax.FromXElement(rule.Element("Height"));
                            rrule.Latitude = MinMax.FromXElement(rule.Element("Latitude"));
                            rrule.Slope = MinMax.FromXElement(rule.Element("Slope"));
                        }
                    }
                    _definitions.Add(ddef);
                }
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        bool IsRedValueAvailabe(int value)
        {
            if (SelectedDefinition == null) return false;
            if (SelectedDefinition.CustomMaterials.Any(x => x.Value == value)) return false;
            if (SelectedDefinition.ComplexMaterials.Any(x => x.Value == value)) return false;
            return true;
        }

        double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProp(ref _progress, value);
        }

        bool _isBusy;
        bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProp(ref _isBusy, value))
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        ProgressBarVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                    }));
                }
            }
        }
        Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set => SetProp(ref _progressBarVisibility, value);
        }

        public ICommand GenerateClimateZonesCommand => new RelayCommand(o =>
        {
            Progress = 0;
            IsBusy = true;
            Task.Run(() => {
                try
                {
                    // Analyze rules from DefaultSetUp
                    var defaultSetup = SelectedDefinition.ComplexMaterials.FirstOrDefault(x => x.Name == "DefaultSetUp");
                    if (defaultSetup == null) return;

                    HashSet<double> latitudes = new();

                    foreach (var rule in defaultSetup.Rules)
                    {
                        latitudes.Add(rule.Latitude.Min);
                        latitudes.Add(rule.Latitude.Max);
                    }
                    List<double> sortedLatitudes = latitudes.OrderBy(x => x).ToList();
                    int numOfZones = sortedLatitudes.Count() - 1;
                    if (numOfZones < 2)
                    {
                        MessageBox.Show("1 or less climate zones found. Aborting...");
                        return;
                    }

                    // Define one climate zone per increment of sorted latitudes
                    // Caution !!! There is a CustomMaterialTable section which override red material rules!
                    List<int> reds = new List<int>();
                    // create list of available red values
                    // Alternate colors so they are easier to see
                    int bottomRed = 50;
                    int topRed = 200;
                    for (int i = 0; i < numOfZones; ++i)
                    {
                        if (i % 2 > 0)
                        {
                            while (!IsRedValueAvailabe(bottomRed) && bottomRed < 200)
                            {
                                ++bottomRed;
                            }
                            if (bottomRed == 200)
                            {
                                MessageBox.Show("Not enough available red values!");
                                return;
                            }
                            reds.Add(bottomRed++);
                        }
                        else
                        {
                            while (!IsRedValueAvailabe(topRed) && topRed > 50)
                            {
                                --topRed;
                            }
                            if (topRed == 50)
                            {
                                MessageBox.Show("Not enough available red values!");
                                return;
                            }
                            reds.Add(topRed--);
                        }
                    }

                    if (!Directory.Exists(".\\GeneratedClimateZones"))
                    {
                        Directory.CreateDirectory(".\\GeneratedClimateZones");
                    }

                    // Create rulesets
                    XElement documentRoot = new XElement("ComplexMaterials", new XComment("Copy these MaterialGroup definitions into the 'ComplexMaterials' node of the Planet Definition"));
                    for (int i = 0; i < sortedLatitudes.Count - 1; ++i)
                    {
                        double latMin = sortedLatitudes[i];
                        double latMax = sortedLatitudes[i + 1];
                        var ruleset = defaultSetup.Rules.Where(x =>
                            x.Latitude.Min <= latMin && x.Latitude.Max > latMin ||
                            x.Latitude.Min < latMax && x.Latitude.Max >= latMax ||
                            x.Latitude.Min >= latMin && x.Latitude.Max <= latMax)
                            .ToList();
                        var matGroup = new XElement("MaterialGroup", new XAttribute("Name", "ClimateZone_" + latMin + "_" + latMax + "_" + reds[i]), new XAttribute("Value", reds[i]));
                        foreach (var rule in ruleset)
                        {
                            //var nMin = rule.Latitude.Min;
                            //var nMax = rule.Latitude.Max;
                            //if (nMin < latMin) nMin = latMin;
                            //if (nMax > latMax) nMax = latMax;
                            matGroup.Add(rule.ToXElement(0, 90)); // 0..90 overide required since our zones are shifted in latitude
                        }
                        documentRoot.Add(matGroup);
                    }
                    XDocument ruleSetDocument = new XDocument(documentRoot);
                    ruleSetDocument.Save(".\\GeneratedClimateZones\\rules.xml");

                    Progress += 5;

                    var cubeMapFaces = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToArray();

                    Dictionary<CubeMapFace,Image<L16>> heightMaps = new();
                    foreach (var face in cubeMapFaces)
                    {
                        var heightMapFilePath =
                                Path.Combine(
                                    Path.GetDirectoryName(_lastSbc) ?? ".",
                                    "PlanetDataFiles",
                                    SelectedDefinition.Name,
                                    face.ToString().ToLower() + ".png");
                        var heightMap = SixLabors.ImageSharp.Image.Load<L16>(heightMapFilePath);
                        heightMaps[face] = heightMap;
                    }

                    var rnd = new Random(0);
                    foreach (var face in cubeMapFaces)
                    {
                        // Each iteration should increase progress by 15 -> 6*15 = 90
                        try
                        {
                            var heightMap = heightMaps[face];

                            var image = new SixLabors.ImageSharp.Image<Rgb24>(2048, 2048);
                            for (int x = 0; x < 2048; ++x)
                            {
                                Progress += 10 / 2048d;
                                for (int y = 0; y < 2048; ++y)
                                {
                                    // Determine latitude of pixel
                                    var point = CoordinateHelper.GetNormalizedSphereCoordinates(face, x, y);
                                    var lolat = CoordinateHelper.ToLongitudeLatitude(point);
                                    var latAbs = Math.Abs(lolat.latitude);
                                    // Latitude 0 is equator, +- 90 is pole

                                    int latIndex = 0;
                                    bool foundLat = false;
                                    for (int i = 0; i < sortedLatitudes.Count - 1; ++i)
                                    {
                                        var curLat = sortedLatitudes[i];
                                        var nextLat = sortedLatitudes[i + 1];

                                        if (curLat <= latAbs && nextLat >= latAbs)
                                        {
                                            latIndex = i;
                                            foundLat = true;
                                            break;
                                        }
                                    }
                                    if (!foundLat) throw new Exception("Cound not determine latitude ranges. Please use different definitions.");

                                    var latMin = sortedLatitudes[latIndex];
                                    var latMax = sortedLatitudes[latIndex + 1];
                                    byte myRed = (byte)reds[latIndex];
                                    var middle = (latMin + latMax) / 2;
                                    var width = latMax - latMin;
                                    if (width < 1) // region is too small
                                    {
                                        image[x, y] = new Rgb24(myRed, 0, 0);
                                    }
                                    else
                                    {
                                        // Middle area stays the same
                                        var distanceFromMiddle = Math.Abs(latAbs - middle);
                                        //if (distanceFromMiddle < (width / 4)) //<------ Middle width is 1/2 of area. Side areas are 1/4 each
                                        if (distanceFromMiddle < (width / 8)) // <----- Middle width is 2/8 of area. Side areas are 3/8 each
                                        {
                                            image[x, y] = new Rgb24(myRed, 0, 0);
                                        }
                                        else // We are outside of the middle area
                                        {
                                            // Must determine if we are below middle or above middle
                                            var distanceToStart = Math.Abs(latAbs - latMin);
                                            var distanceToEnd = Math.Abs(latAbs - latMax);
                                            double distancePercentage;
                                            double percentage;
                                            byte neighborRed;

                                            double heightValue = GetRelativeHeight(heightMaps, face, x, y, 40);

                                            // Debug
                                            //heightValue = 0.5;

                                            if (distanceToStart < distanceToEnd)
                                            {
                                                // We are below middle and closer to start
                                                // Calculate gradient probablity based on distance
                                                // At edge the propability is 50%, at the middle it is 100%
                                                //distancePercentage = distanceToStart / (width / 4); // <----- Middle width is 1/2 of area. Side areas are 1/4 each
                                                distancePercentage = distanceToStart / (width * 3 / 8d);   // <----- Middle width is 2/8 of area. Side areas are 3/8 each
                                                percentage = 0.5 * distancePercentage + .5;
                                                neighborRed = (byte)reds[latIndex > 0 ? latIndex - 1 : 0];
                                                //neighborIsColder = false;

                                                percentage = ModifyPercentage(distancePercentage, -1 * heightValue + 1);
                                            }
                                            else
                                            {
                                                // We are above middle and closer to end
                                                //distancePercentage = distanceToEnd / (width / 4);
                                                distancePercentage = distanceToEnd / (width * 3 / 8d);
                                                percentage = 0.5 * distancePercentage + .5;
                                                neighborRed = (byte)reds[latIndex + 1 < reds.Count ? latIndex + 1 : latIndex];
                                                //neighborIsColder = true;

                                                percentage = ModifyPercentage(distancePercentage, heightValue);
                                            }

                                            var dd = rnd.NextDouble();
                                            var targetRed = dd < percentage ? myRed : neighborRed;
                                            image[x, y] = new Rgb24(targetRed, 0, 0);
                                            //image[x, y] = new Rgb24((byte)(255 * percentage), 0, 0);
                                        }
                                    }
                                }
                            }

                            // Apply median filter to reduce pixel clouds
                            var imageFiltered = new SixLabors.ImageSharp.Image<Rgb24>(2048, 2048);
                            int filterRadius = 3;
                            int filterRadiusSquared = filterRadius * filterRadius;
                            for (int x = 0; x < 2048; ++x)
                            {
                                Progress += 5 / 2048d;
                                for (int y = 0; y < 2048; ++y)
                                {
                                    List<byte> pixels = new();
                                    var xStart = Math.Max(0, x - filterRadius);
                                    var yStart = Math.Max(0, y - filterRadius);
                                    var xEnd = Math.Min(image.Width - 1, x + filterRadius);
                                    var yEnd = Math.Min(image.Height - 1, y + filterRadius);
                                    for (int xx = xStart; xx <= xEnd; ++xx)
                                    {
                                        for (int yy = yStart; yy <= yEnd; ++yy)
                                        {
                                            // Check of current pixel within a circle
                                            if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= filterRadiusSquared)
                                            {
                                                pixels.Add(image[xx, yy].R);
                                            }
                                        }
                                    }
                                    if (pixels.Any())
                                    {
                                        pixels.Sort();
                                        imageFiltered[x, y] = new Rgb24(pixels[pixels.Count / 2], 0, 0);
                                    }
                                }
                            }
                            image.Dispose();
                            imageFiltered.SaveAsPng(".\\GeneratedClimateZones\\" + face.ToString().ToLower() + "_zones.png");
                            imageFiltered.Dispose();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    var choice = MessageBox.Show("Finished!\r\nPlease copy the content from the 'rules.xml'\r\nto the corresponding 'ComplexMaterials' node in your planet SBC.\r\nDo you want me to include the new zones into existing material maps?\r\nValue 82 will not be changed to conserve lakes.", "", MessageBoxButton.YesNo);
                    if (choice != MessageBoxResult.Yes) return;
                    choice = MessageBox.Show("Please use the next dialog to select one file in the target folder where the material maps should be loaded from", "", MessageBoxButton.OKCancel);
                    if (choice != MessageBoxResult.OK) return;
                    var dlg = new OpenFileDialog();
                    dlg.FileName = "back_mat.png";
                    dlg.Filter = "PNG|*.png";
                    if (dlg.ShowDialog() != true) return;
                    var folder = Path.GetDirectoryName(dlg.FileName);

                    // Last 5% progress
                    try
                    {
                        foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
                        {
                            var fileName = Path.Combine(folder, face + "_mat.png");
                            if (!File.Exists(fileName))
                            {
                                MessageBox.Show("File not found! Skipping file:\r\n" + fileName);
                                Progress += 5 / 6d;
                                continue;
                            }
                            dynamic matFile = SixLabors.ImageSharp.Image.Load(fileName);
                            var zoneFile = SixLabors.ImageSharp.Image.Load<Rgb24>(".\\GeneratedClimateZones\\" + face.ToString().ToLower() + "_zones.png");
                            var targetMatFile = new Image<Rgb24>(2048, 2048);
                            Parallel.For(0, 2048, x =>
                            {
                                for (int y = 0; y < 2048; ++y)
                                {
                                    var pixel = matFile[x, y];
                                    targetMatFile[x, y] = new Rgb24(pixel.R == 82 ? (byte)pixel.R : zoneFile[x, y].R, pixel.G, pixel.B);
                                }
                            });
                            matFile.Dispose();
                            zoneFile.Dispose();
                            targetMatFile.SaveAsPng(".\\GeneratedClimateZones\\" + face.ToString().ToLower() + "_mat.png");
                            targetMatFile.Dispose();
                            Progress += 5 / 6d;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                finally { IsBusy = false; }
            });
        }, o =>
        SelectedDefinition != null && SelectedDefinition.ComplexMaterials?.Exists(x=>x.Name == "DefaultSetUp") == true &&
        !string.IsNullOrEmpty(_lastSbc) && !IsBusy);

        double GetRelativeHeight(Dictionary<CubeMapFace, Image<L16>> heightMaps, CubeMapFace currentFace, int x, int y, int radius)
        {
            double heightValue = heightMaps[currentFace][x, y].PackedValue;
            // Use localized height map scaling
            double heightMapMin = heightValue;
            double heightMapMax = heightValue;

            var xStart = x - radius;
            var yStart = y - radius;
            var xEnd = x + radius;
            var yEnd = y + radius;
            var radiusSquared = radius * radius;

            CubeMapPointLight origin = new CubeMapPointLight { X = x, Y = y, Face = currentFace };

            Parallel.For(xStart, xEnd + 1, xx =>
            {
                for (int yy = yStart; yy <= yEnd; ++yy)
                {
                    // Check of current pixel within a circle
                    if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= radiusSquared)
                    {
                        var pt = CubeMapPointLight.GetPointRelativeTo(origin, xx - x, yy - y);
                        var val = heightMaps[pt.Face][pt.X, pt.Y].PackedValue;
                        // Might run into concurrency issues here...
                        // Can probably be ignored.
                        if (heightMapMin > val) heightMapMin = val;
                        if (heightMapMax < val) heightMapMax = val;
                    }


                }
            });
            // Scale 0...1
            heightMapMax = heightMapMax / 65535;
            heightMapMin = heightMapMin / 65535;
            heightValue = heightValue / 65535;

            // Scale with min max
            return (heightValue - heightMapMin) / (heightMapMax - heightMapMin);
        }

        double ModifyPercentage(double distancePercentage, double height)
        {
            // Using 0..100 range here:
            distancePercentage *= 100;

            // Do some math magic here.
            // The idea is to start at 100% when we are at the edge of the middle area and 0% if we reach the middle area of our neighbor.
            // By default we have 50% propability at the border between climate zones.
            // This is shifted to 75% if the height is 0 and 25% if the height is 1 on the height map.
            // The resulting 2nd degree polynomial is symmentric if you mirror x and y at the edge of the climate zones.
            //double coeffA = 0.005 * height - 0.0025;
            //double coeffB = 0.5;
            //double coeffC = -50 * height + 75;
            //return (coeffA * (distancePercentage*distancePercentage) + coeffB * distancePercentage + coeffC)/100;

            // Version above was barely noticable. Using more extreme values:
            double coeffA = 0.00006876 * (height*height) - 0.00006876 * height + 0.00001719;
            double coeffB = 0.008 * height - 0.004;
            double coeffC = -0.6875 * (height*height) + 0.6875 * height + 0.328125;
            double coeffD = -80 * height + 90;

            return
                (coeffA * (distancePercentage * distancePercentage * distancePercentage) +
                coeffB * (distancePercentage * distancePercentage) +
                coeffC * distancePercentage +
                coeffD)/100;
        }
    }

    class Definition
    {
        public string Name { get; set; }
        public List<MaterialGroup> ComplexMaterials { get; } = new();
        public List<CustomMaterial> CustomMaterials { get; } = new();
    }

    class CustomMaterial
    {
        public string Material { get; set; }
        public int Value { get; set; }
        public int MaxDepth { get; set; }

        public override string ToString()
        {
            return Value + " | " + Material;
        }
    }

    class MaterialGroup
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public List<Rule> Rules { get; } = new();
    }

    class Rule
    {
        public List<Layer> Layers { get; } = new();
        public MinMax Height { get; set; }
        public MinMax Latitude { get; set; }
        public MinMax Slope { get; set; }
        public string Xml { get; set; }

        bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted == value) return;
                _isHighlighted = value;
                foreach (var action in Actions)
                {
                    action.Invoke(this);
                }
            }
        }
        // Not using events to prevent memory leaks on reused rules
        public List<Action<Rule>> Actions { get; } = new();

        public override string ToString()
        {
            string str = "";
            if (Layers.Count > 0)
            {
                str += Layers[0].Material + " ";
            }
            str += "L[" +
                Latitude.Min.ToString(CultureInfo.InvariantCulture) + "," +
                Latitude.Max.ToString(CultureInfo.InvariantCulture) +
                "] H[" +
                Height.Min.ToString(CultureInfo.InvariantCulture) + "," +
                Height.Max.ToString(CultureInfo.InvariantCulture) +
                "] S[" +
                Slope.Min.ToString(CultureInfo.InvariantCulture) + "," +
                Slope.Max.ToString(CultureInfo.InvariantCulture) + "]";
            return str;
        }

        // We could also just parse the Xml string property.
        // It contains a copy of the original XML this object was parsed from.
        public XElement ToXElement(double? latitudeMinOverride = null, double? latitudeMaxOverride = null)
        {
            var xElem = new XElement("Rule");
            var layers = new XElement("Layers");
            foreach (var layer in Layers)
            {
                layers.Add(layer.ToXElement());
            }
            xElem.Add(layers);
            if (Height != null)
            {
                var hElem = new XElement("Height", new XAttribute("Min", Height.Min), new XAttribute("Max", Height.Max));
                xElem.Add(hElem);
            }
            if (Latitude != null)
            {
                var hElem = new XElement("Latitude", new XAttribute("Min", latitudeMinOverride ?? Latitude.Min), new XAttribute("Max", latitudeMaxOverride ?? Latitude.Max));
                xElem.Add(hElem);
            }
            if (Slope != null)
            {
                var hElem = new XElement("Slope", new XAttribute("Min", Slope.Min), new XAttribute("Max", Slope.Max));
                xElem.Add(hElem);
            }
            return xElem;
        }
    }

    class Layer
    {
        public string Material { get; set; }
        public int Depth { get; set; }

        public XElement ToXElement()
        {
            return new XElement("Layer", new XAttribute("Material", Material), new XAttribute("Depth", Depth));
        }
    }
    class MinMax
    {
        public double Min { get; set; }
        public double Max { get; set; }

        public static MinMax FromXElement(XElement? xElement)
        {
            MinMax mm = new();
            if (xElement != null)
            {
                if (double.TryParse(xElement.Attribute("Min")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min)) mm.Min = min;
                if (double.TryParse(xElement.Attribute("Max")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var max)) mm.Max = max;
            }
            return mm;
        }
    }

    public enum CubeMapFace
    {
        Front,
        Back,
        Left,
        Right,
        Up,
        Down
    }
    public static class CoordinateHelper
    {
        public static Point3D GetNormalizedSphereCoordinates(CubeMapFace face, int x, int y)
        {
            Point3D origin = new Point3D();
            double cubeWidth = 2048;

            // It makes sense if you draw a picture. See below
            double offset = (cubeWidth - 1) / 2.0;
            // w=5: [0][1][2][3][4] -> middle = 2,   [0] on 3d axis = -2
            //             | middle (0 in xyz)
            // w=4   [0][1]|[2][3]  -> middle = 1.5, [0] on 3d axis = -1.5

            // offset at 2048 -> 1023.5

            switch (face)
            {
                case CubeMapFace.Front: // Y-
                    origin.X = x - offset;
                    origin.Y = -offset;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Back: // Y+
                    origin.X = offset - x;
                    origin.Y = offset;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Left: // X-
                    origin.X = -offset;
                    origin.Y = offset - x;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Right: // X+
                    origin.X = offset;
                    origin.Y = x - offset;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Up: // Z+
                    origin.X = x - offset;
                    origin.Y = offset - y;
                    origin.Z = offset;
                    break;
                case CubeMapFace.Down: // Z-
                    origin.X = offset - x;
                    origin.Y = offset - y;
                    origin.Z = -offset;
                    break;
            }
            var r = Math.Sqrt(origin.X * origin.X + origin.Y * origin.Y + origin.Z * origin.Z);

            return new Point3D(origin.X / r, origin.Y / r, origin.Z / r);
        }

        // google Bard probably stole this from somewhere.
        // It forgot the -90 for latitude despite me telling it to output -90 to +90. Fail!
        public static (double longitude, double latitude) ToLongitudeLatitude(this Point3D point)
        {
            double longitude = Math.Atan2(point.Y, point.X) * 180 / Math.PI;
            if (longitude < 0)
            {
                longitude += 360;
            }

            double latitude = Math.Acos(point.Z / 1) * 180 / Math.PI;
            if (latitude < 0)
            {
                latitude += 180;
            }

            return (longitude, latitude - 90);
        }
    }


}

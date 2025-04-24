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

        string _summary;
        public string Summary
        {
            get => _summary;
            set => SetProp(ref _summary, value);
        }

        public ObservableCollection<Rule> Heights { get; } = new();

        void UpdateSummary()
        {
            Summary = "";
            Heights.Clear();
            if (MaxLatitude < MinLatitude) return;
            if (SelectedMaterialGroup == null) return;

            //       [........]
            // 1 [...|...]    |
            // 2     |      [.|.....]
            // 3  [..|........|....]
            // 4     | [..]   |

            var rules = SelectedMaterialGroup.Rules.Where(x=>
                x.Latitude.Min <= MinLatitude && x.Latitude.Max > MinLatitude ||  // 1 + 3
                x.Latitude.Min < MaxLatitude && x.Latitude.Max > MaxLatitude  ||  // 2 + 3
                x.Latitude.Min >= MinLatitude && x.Latitude.Max <= MaxLatitude)   // 4
                .ToList();

            foreach ( var rule in rules )
            {
                XDocument doc = XDocument.Parse(rule.Xml);
                if (doc.Root != null)
                {
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

        public ICommand GenerateClimateZonesCommand => new RelayCommand(o =>
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
            int numOfZones = sortedLatitudes.Count()-1;
            if (numOfZones < 2)
            {
                MessageBox.Show("1 or less climate zones found. Aborting...");
                return;
            }

            List<int> reds = new List<int>();
            // create list of available red values
            // Alternate colors so they are easier to see
            int bottomRed = 50;
            int topRed = 200;
            for (int i=0; i<numOfZones;++i)
            {
                if (i%2 > 0)
                {
                    while(!IsRedValueAvailabe(bottomRed) && bottomRed < 200)
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

            // Define one climate zone per increment of sorted latitudes
            // Caution !!! There is a CustomMaterialTable section which override red material rules!

            var rnd = new Random(0);
            foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>())
            {
                // Debug
                if (face != CubeMapFace.Back) continue;
                try
                {
                    var heightMapFilePath =
                        Path.Combine(
                            Path.GetDirectoryName(_lastSbc) ?? ".",
                            "PlanetDataFiles",
                            SelectedDefinition.Name,
                            face.ToString().ToLower() + ".png");
                    var heigthMap = SixLabors.ImageSharp.Image.Load<L16>(heightMapFilePath);
                    double heightMapMin = heigthMap[0, 0].PackedValue;
                    double heightMapMax = heightMapMin;
                    for (int x = 0; x < 2048; ++x)
                        for (int y = 0; y < 2048; ++y)
                        {
                            var val = heigthMap[x, y].PackedValue;
                            if (heightMapMin > val) heightMapMin = val;
                            if (heightMapMax < val) heightMapMax = val;
                        }
                    // Scale 0...1
                    heightMapMax = heightMapMax / 65535.0;
                    heightMapMin = heightMapMin / 65535.0;

                    var image = new SixLabors.ImageSharp.Image<Rgb24>(2048, 2048);
                    for (int x = 0; x < 2048; ++x)
                    {
                        //if (x > 100) break;
                        for (int y = 0; y < 2048; ++y)
                        {
                            // Step 1: Determine latitude of pixel
                            var point = CoordinateHelper.GetNormalizedSphereCoordinates(face, x, y);
                            var lolat = CoordinateHelper.ToLongitudeLatitude(point);
                            var latAbs = Math.Abs(lolat.latitude);

                            // Latitude 0 is equator, +- 90 is pole

                            // Strategy:
                            // Area between two latitude lines is covered by its corresponding rules
                            // At the edge we assign 75% propability of beeing the same rules as middle
                            // and 25% propability of beeing neighboring ruleset.

                            // Additional refinement:
                            // Probability can change based on the height of a pixel
                            // Higher pixels have more propability for colder climate zone.
                            // Latitudes with higher numbers are considered colder.

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

                            var ruleset = defaultSetup.Rules.Where(x =>
                                x.Latitude.Min <= latMin && x.Latitude.Max > latMin ||  // 1 + 3
                                x.Latitude.Min < latMax && x.Latitude.Max > latMax ||   // 2 + 3
                                x.Latitude.Min >= latMin && x.Latitude.Max <= latMax)   // 4
                                .ToList();

                            // TODO: Write ruleset to file

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
                                if (distanceFromMiddle < (width / 4))
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
                                    var heightValue = heigthMap[x, y].PackedValue / 65535.0;
                                    // Scale with min max
                                    heightValue = (heightValue - heightMapMin) / (heightMapMax-heightMapMin);

                                    // Debug
                                    //heightValue = 0;

                                    if (distanceToStart < distanceToEnd)
                                    {
                                        // We are below middle and closer to start
                                        // Calculate gradient probablity based on distance
                                        // At edge the propability is 50%, at the middle it is 100%
                                        distancePercentage = distanceToStart / (width / 4);
                                        percentage = 0.5 * distancePercentage + .5;
                                        neighborRed = (byte)reds[latIndex > 0 ? latIndex - 1 : 0];
                                        //neighborIsColder = false;

                                        percentage = ModifyPercentage(distancePercentage, -1*heightValue + 1);
                                    }
                                    else
                                    {
                                        // We are above middle and closer to end
                                        distancePercentage = distanceToEnd / (width / 4);
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
                    image.SaveAsPng(face.ToString().ToLower() + "_zones.png");
                    image.Dispose();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            MessageBox.Show("Finished");

        }, o =>
        SelectedDefinition != null && SelectedDefinition.ComplexMaterials?.Exists(x=>x.Name == "DefaultSetUp") == true &&
        !string.IsNullOrEmpty(_lastSbc));

        double ModifyPercentage(double distancePercentage, double height)
        {
            // Using 0..100 range here:
            distancePercentage *= 100;

            // so some math magic here.
            // The idea is to start at 100% when we are at the edge of the middle area and 0% if we reach the middle area of our neighbor.
            // By default we have 50% propability at the border between climate zones.
            // This is shifted to 75% if the height is 0 and 25% if the height is 1 on the height map.
            // The resulting 2nd degree polynomial is symmentric if you mirror x and y at the edge of the climate zones.
            double coeffA = 0.005 * height - 0.0025;
            double coeffB = 0.5;
            double coeffC = -50 * height + 75;
            return (coeffA * (distancePercentage*distancePercentage) + coeffB * distancePercentage + coeffC)/100;
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
    }

    class Layer
    {
        public string Material { get; set; }
        public int Depth { get; set; }
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

using Microsoft.Win32;
using SpaceEngineersOreRedistribution;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

        public ICommand OpenSbcCommand => new RelayCommand(o =>
        {
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
    }

    class Definition
    {
        public string Name { get; set; }
        public List<MaterialGroup> ComplexMaterials { get; } = new();
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
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace SpaceEngineersOreRedistribution
{
    class OreOverviewViewModel : PropChangeNotifier
    {
        public List<string> CalculationBases { get; } = new List<string>
        {
            "Spawn Weight",
            "Expected Ratio"
        };
        string _selectedCalculationBase = "Spawn Weight";
        public string SelectedCalculationBase
        {
            get => _selectedCalculationBase;
            set
            {
               if (SetProp(ref _selectedCalculationBase, value))
                   Refresh();
            }
        }

        public RedistributionSetupViewModel SetupVm { get; }

        public BindingList<PieChartDataItem> ItemsRaw { get; } = new BindingList<PieChartDataItem>();
        public BindingList<PieChartDataItem> ItemsBase { get; } = new BindingList<PieChartDataItem>();

        Dictionary<string, OreItem> _oreItems = new Dictionary<string, OreItem>();

        public OreOverviewViewModel(RedistributionSetupViewModel setupVm)
        {
            setupVm.RatiosCalculated += (s, e) => Refresh();
            try
            {
                SetupVm = setupVm;
                var path = Assembly.GetExecutingAssembly().Location;
                var dir = System.IO.Path.GetDirectoryName(path);
                var fileName = System.IO.Path.Combine(dir, "ingotmap.xml");
                if (System.IO.File.Exists(fileName))
                {
                    XDocument doc = XDocument.Load(fileName);
                    foreach (var node in doc.Root.Elements("ore"))
                    {
                        //<ore name = "Iron_02">
                        //  <ingot name = "Iron" amount = "0.7" />
                        //</ ore >
                        var oreName = node.Attribute("name")?.Value;
                        if (string.IsNullOrEmpty(oreName))
                            continue;
                        var oreItem = new OreItem { Name = oreName };
                        foreach (var ingotNode in node.Elements("ingot"))
                        {
                            var ingotName = ingotNode.Attribute("name")?.Value;
                            if (string.IsNullOrEmpty(ingotName))
                                continue;
                            if (!double.TryParse(ingotNode.Attribute("amount")?.Value, out double amount))
                                amount = 0;
                            var ingotItem = new IngotItem { Name = ingotName, Amount = amount };
                            oreItem.Ingots.Add(ingotItem);
                        }
                        // Duplicates will be overwritten
                        _oreItems[oreName] = oreItem;
                    }
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading ingot map: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public RelayCommand RefreshCommand => new RelayCommand(o=>Refresh());

        public void Refresh()
        {
            ItemsRaw.Clear();
            ItemsBase.Clear();
            if (SetupVm == null)
                return;
            int i = 0;
            int j = 0;
            foreach (var ore in SetupVm.OreInfos)
            {
                if (string.IsNullOrEmpty(ore.Name)) continue;

                float value = (float)(SelectedCalculationBase == "Spawn Weight" ? ore.SpawnWeight : ore.ExpectedRatio);

                var item = new OreOverviewItem
                {
                    Name = ore.Name,
                    Value = value,
                    FillBrush = DiagramColors.GetBrush(i++)
                };
                ItemsRaw.Add(item);

                if (_oreItems.ContainsKey(ore.Name))
                {
                    var oreItem = _oreItems[ore.Name];
                    foreach (var ingot in oreItem.Ingots)
                    {
                        var baseItem = ItemsBase.FirstOrDefault(x => ((OreOverviewItem)x).Name == ingot.Name);
                        if (baseItem == null)
                        {
                            baseItem = new OreOverviewItem
                            {
                                Name = ingot.Name,
                                Value = value,
                                FillBrush = DiagramColors.GetBrush(j++)
                            };
                            ItemsBase.Add(baseItem);
                        }
                        else
                        {
                            baseItem.Value += value;
                        }
                    }
                }

            }
            UpdatePercentage(ItemsRaw);
            UpdatePercentage(ItemsBase);
        }

        void UpdatePercentage(BindingList<PieChartDataItem> list)
        {
            var sum = list.Sum(x => ((OreOverviewItem)x).Value);
            foreach (var item in list)
            {
                var ore = (OreOverviewItem)item;
                ore.Percentage = sum > 0 ? $"{(ore.Value / sum * 100):F2}%" : "0%";
            }
            var copy = list.ToList();
            copy.Sort((x, y) => ((OreOverviewItem)y).Value.CompareTo(((OreOverviewItem)x).Value));
            list.Clear();
            foreach (var item in copy)
            {
                list.Add(item);
            }
        }
    }

    public class OreItem : PropChangeNotifier
    {
        string _name;
        public string Name
        {
            get => _name;
            set => SetProp(ref _name, value);
        }
        public ObservableCollection<IngotItem> Ingots { get; } = new();
    }

    public class IngotItem : PropChangeNotifier
    {
        string _name;
        public string Name
        {
            get => _name;
            set => SetProp(ref _name, value);
        }

        double _amount;
        public double Amount
        {
            get => _amount;
            set => SetProp(ref _amount, value);
        }
    }

    public class OreOverviewItem : PieChartDataItem
    {
        string _name;
        public string Name
        {
            get => _name;
            set => SetProp(ref _name, value);
        }

        string _percentage;
        public string Percentage
        {
            get => _percentage;
            set => SetProp(ref _percentage, value);
        }

    }
}

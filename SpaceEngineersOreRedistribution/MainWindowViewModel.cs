﻿using Microsoft.Win32;
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
                if (value == null)
                {
                    UpdateImage();
                    return;
                }
                HashSet<string> types = new();
                foreach (var item in value.OreMappings)
                {
                    types.Add(item.Type);
                }
                foreach (var type in types)
                    OreTypes.Add(type);
                LoadImages();
            }
        }
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
                    UpdateImage();
                    return;
                }
                var mappings = SelectedPlanetDefinition.OreMappings.Where(x => x.Type == value);
                foreach (var mapping in mappings)
                {
                    OreMappings.Add(mapping);
                }
                UpdateImage();
            }
        }
        public ObservableCollection<OreMapping> OreMappings { get; } = new();

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

        string _currentImage = "front";
        public string CurrentImage
        {
            get => _currentImage;
            set
            {
                if (!SetProp(ref _currentImage, value)) return;
                UpdateImage();
            }
        }

        public void OpenPlanetDefinition(string file)
        {
            _lastOpenedFile = file;
            PlanetDefinitions.Clear();
            Image = null;
            try
            {
                XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
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
                    var id = definition.Element("Id")?.Element("SubtypeId")?.Value;
                    if (id == null) continue;
                    var def = new PlanetDefinition();
                    def.Name = id;
                    PlanetDefinitions.Add(def);
                    var oreMappings = definition.Element("OreMappings");
                    if (oreMappings == null) continue;
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
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        BitmapImage _image;
        public BitmapImage Image
        {
            get => _image;
            set => SetProp(ref _image, value);
        }

        void ClearImages()
        {
            foreach (var info in _images.Values)
            {
                if (info != null) info.Dispose();
            }
            _images.Clear();
        }

        void UpdateImage()
        {
            Image = null;
            if (SelectedPlanetDefinition == null) return;
            if (!_images.TryGetValue(_currentImage, out var value)) return;
            if (value == null) return;

            List<OreMapping> oreMappings = null;

            if (SelectedOreType != null && OreMappings != null && OreMappings.Count > 0)
            {
                oreMappings = OreMappings.ToList();
            }
            using var b = value.FilterForOre(oreMappings);


            Image = ImageData.FromBitmap(b);
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
            UpdateImage();
        }

        ImageData GetInfo(string pngName)
        {
            var dir = Path.GetDirectoryName(_lastOpenedFile);
            var imageDir = Path.Combine(dir, SelectedPlanetDefinition.Name);
            if (!Directory.Exists(imageDir)) return null;
            var file = Path.Combine(imageDir, pngName);
            if (!File.Exists(file)) return null;
            return new ImageData() { FileName = file };
        }

        Dictionary<string, ImageData> _images = new();

        public ICommand ImageCommand => new RelayCommand(o =>
        {
            if (o is string s)
                CurrentImage = s;
        });

    }
}

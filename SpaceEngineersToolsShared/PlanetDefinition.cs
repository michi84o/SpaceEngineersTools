using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersToolsShared
{
    public class PlanetDefinition
    {
        public string Name { get; set; }
        public ObservableCollection<OreMapping> OreMappings { get; } = new();
        public ObservableCollection<MaterialType> MaterialTypes { get; } = new();
        public ObservableCollection<EnvironmentItem> EnvironmentItems { get; } = new();
        public ObservableCollection<ComplexMaterial> ComplexMaterials { get; } = new();

    }
}

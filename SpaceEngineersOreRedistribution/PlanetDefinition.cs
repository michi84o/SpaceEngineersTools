using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    public class PlanetDefinition
    {
        public string Name { get; set; }
        public ObservableCollection<OreMapping> OreMappings { get; } = new();
    }
}

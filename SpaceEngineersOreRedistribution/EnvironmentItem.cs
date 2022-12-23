using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    public class EnvironmentItem
    {
        public int Biome { get; set; }
        public MaterialType Material { get; set; }
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public double MinLatitude { get; set; }
        public double MaxLatitude { get; set; }
        public double MinSlope { get; set; }
        public double MaxSlope { get; set; }

        public override string ToString()
        {
            return Biome + " | " + (Material?.Name ?? "<?>");
        }
    }
}


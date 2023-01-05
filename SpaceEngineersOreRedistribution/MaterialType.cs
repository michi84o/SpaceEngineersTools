using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    // Only used for the Environment Item List display.
    public class MaterialType
    {
        public string Name { get; set; }
        /// <summary>Value for RED channel.</summary>
        public int Value { get; set; }
        public int MaxDepth { get; set; }

        public override string ToString()
        {
            return Value + " | " + (Name ?? "<?>");
        }
    }
}

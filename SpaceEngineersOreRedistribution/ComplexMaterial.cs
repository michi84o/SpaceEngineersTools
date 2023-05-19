using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using System.Windows.Media.Media3D;

namespace SpaceEngineersOreRedistribution
{
    public class ComplexMaterial
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public override string ToString()
        {
            return Value + " | " + (Name ?? "<?>");
        }
    }
}

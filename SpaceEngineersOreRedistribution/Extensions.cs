using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    public static class RandomExtensions
    {
        public static int NextTS(this Random random, int max)
        {
            lock (random)
            {
                return random.Next(max);
            }
        }
    }
}

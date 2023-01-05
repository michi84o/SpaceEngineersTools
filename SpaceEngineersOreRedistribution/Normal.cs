using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersOreRedistribution
{
    // Credits: https://stackoverflow.com/questions/218060/random-gaussian-variables
    internal class Normal
    {
        public double Mean { get; }
        public double StdDev { get; }

        Random _rnd;
        public Normal(double mean, double stdDev, int seed)
        {
            _rnd = new Random(seed);
            Mean = mean;
            StdDev = stdDev;
        }

        public double Next()
        {
            return Next(Mean, StdDev);
        }

        public double Next(double mean, double stdDev)
        {
            double u1 = 1.0 - _rnd.NextDouble(); //uniform(0,1] random doubles
            double u2 = 1.0 - _rnd.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
            return mean + stdDev * randStdNormal; //random normal(mean,stdDev^2)
        }
    }
}

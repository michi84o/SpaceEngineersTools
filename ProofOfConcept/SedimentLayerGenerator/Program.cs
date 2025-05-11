using System;
using System.Text;

namespace SedimentLayerGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var layers = SedimentGenerator.GenerateSedimentLayers(new Random(0), 65535, 0.002, 0.9, 2000, 0.01);
            StringBuilder str = new();
            foreach (var layer in layers)
            {
                str.AppendLine(layer.ToString());
            }
            System.IO.File.WriteAllText("layers.csv", str.ToString());
        }
    }
}
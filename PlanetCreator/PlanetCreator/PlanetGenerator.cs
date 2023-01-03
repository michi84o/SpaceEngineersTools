using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetCreator
{
    internal class PlanetGenerator
    {
        const int _tileWidth = 2048;
        const int _tileHeight = 2048;

        public PlanetGenerator()
        {
            Init();
        }
        void Init()
        {
            TileUp = new ushort[_tileWidth, _tileHeight];
            TileDown = new ushort[_tileWidth, _tileHeight];
            TileFront = new ushort[_tileWidth, _tileHeight];
            TileRight = new ushort[_tileWidth, _tileHeight];
            TileLeft = new ushort[_tileWidth, _tileHeight];
            TileBack = new ushort[_tileWidth, _tileHeight];
        }

        /* Cube Map: Folds into a cube
        [UP]
        [FRONT][RIGHT][BACK][LEFT]
                      [DOWN]
        */

        public ushort[,] TileUp { get; private set; }
        public ushort[,] TileDown { get; private set; }
        public ushort[,] TileFront { get; private set; }
        public ushort[,] TileRight { get; private set; }
        public ushort[,] TileLeft { get; private set; }
        public ushort[,] TileBack { get; private set; }

        public void GeneratePlanet()
        {
            // Layers of diferent noise frequencies
            List<NoiseMaker> list = new()
            {
                new(0, 1.0 / 200, 1),
                new(1, 1.0 / 100, .6),
                new(2, 1.0 / 50, .2),
            };

            // Planet features:
            // A: Flat planes on equator.
            // B: Mountains around 45°
            // C: Flat poles

            // WIP, TODO
            // - Weighten noise based on location
            // - Make tiles seamles
            // - Apply some erosion to noise
            // - Add lakes

        }
    }

    class NoiseMaker
    {
        OpenSimplexNoise _noise;
        public double ResolutionScale;
        public double Weight;
        public double GetValue(double x, double y)
        {
            return _noise.Evaluate(x * ResolutionScale, y * ResolutionScale) * Weight;
        }
        public NoiseMaker(int seed, double resolutionScale, double weight)
        {
            _noise = new OpenSimplexNoise(seed);
            ResolutionScale = resolutionScale;
            Weight = weight;
        }
    }
}

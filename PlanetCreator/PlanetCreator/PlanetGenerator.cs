﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace PlanetCreator
{
    internal class PlanetGenerator
    {
        const int _tileWidth = 2048;

        public PlanetGenerator()
        {
            Init();
        }
        void Init()
        {
            TileUp = new double[_tileWidth, _tileWidth];
            TileDown = new double[_tileWidth, _tileWidth];
            TileFront = new double[_tileWidth, _tileWidth];
            TileRight = new double[_tileWidth, _tileWidth];
            TileLeft = new double[_tileWidth, _tileWidth];
            TileBack = new double[_tileWidth, _tileWidth];

            _faces = new()
            {
                { CubeMapFace.Up, TileUp },
                { CubeMapFace.Down, TileDown },
                { CubeMapFace.Front, TileFront },
                { CubeMapFace.Right, TileRight },
                { CubeMapFace.Left, TileLeft },
                { CubeMapFace.Back, TileBack },
            };
        }

        /* Cube Map: Folds into a cube
        [UP]
        [FRONT][RIGHT][BACK][LEFT]
                      [DOWN]
        */

        Dictionary<CubeMapFace, double[,]> _faces;
        public double[,] TileUp { get; private set; }
        public double[,] TileDown { get; private set; }
        public double[,] TileFront { get; private set; }
        public double[,] TileRight { get; private set; }
        public double[,] TileLeft { get; private set; }
        public double[,] TileBack { get; private set; }

        public void GeneratePlanet()
        {
            // Layers of diferent noise frequencies
            List<NoiseMaker> list = new()
            {
                //new(0, 1.0, 1), // Static noise for checking textures
                new(0, 1.0 / 200, 1),
                new(1, 1.0 / 100, .6),
                new(2, 1.0 / 50, .2),
            };
            double sphereRadius = 1000;
            double max = 0;
            double min = 0;

            // Apply noise
            foreach (var kv in _faces)
            {
                var face = kv.Key;
                var tile = kv.Value;
                Parallel.For(0, _tileWidth, x =>
                {
                    Parallel.For(0, _tileWidth, y =>
                    {
                        var point = GetNormalizedSphereCoordinates(face, x, y);
                        double value = 0;
                        foreach (var nm in list)
                        {
                            value += nm.GetValue3D(point.X * sphereRadius, point.Y * sphereRadius, point.Z * sphereRadius);
                        }
                        if (value < min) min = value;
                        if (value > max) max = value;
                        tile[x, y] = value;
                    });

                });
            }

            // Normalize noise to 0....1
            double offset = -1 * min;
            double stretch = Math.Abs(max - min);

            foreach (var kv in _faces)
            {
                var face = kv.Key;
                var tile = kv.Value;
                Parallel.For(0, _tileWidth, x =>
                {
                    Parallel.For(0, _tileWidth, y =>
                    {
                        double value = tile[x, y];
                        value += offset;
                        value /= stretch;
                        tile[x, y] = value;
                    });
                });
                TileToImage(tile, face);
            }

            // WIP, TODO
            // Planet features:
            // A: Flat planes on equator.
            // B: Mountains around 45°
            // C: Flat poles
            // - Weighten noise based on location
            // - Apply some erosion to generate canyons
            // - Add lakes
        }

        // tiles must be normalized to 0...1 !!!
        void TileToImage(double[,] tile, CubeMapFace face)
        {
            var image = new Image<L16>(_tileWidth, _tileWidth);
            Parallel.For(0, _tileWidth, x =>
            {
                Parallel.For(0, _tileWidth, y =>
                {
                    // Using full spectrum is too exteme
                    // Smooth v
                    var v = 0.6 * tile[x, y] + 0.3; // Values between 0.3 and 0.9
                    ushort value = (ushort)(v * 65535);
                    image[x, y] = new L16(value);
                });
            });
            string filename = face.ToString().ToLower() + ".png";
            image.SaveAsPng(filename);
        }

        Point3D GetNormalizedSphereCoordinates(CubeMapFace face, int x, int y)
        {
            Point3D origin = new Point3D();
            double cubeWidth = 2048;

            // It makes sense if you draw a picture. See below
            double offset = (cubeWidth - 1) / 2.0;
            // w=5: [0][1][2][3][4] -> middle = 2,   [0] on 3d axis = -2
            //             | middle (0 in xyz)
            // w=4   [0][1]|[2][3]  -> middle = 1.5, [0] on 3d axis = -1.5

            // offset at 2048 -> 1023.5
            // Problem: Edges between 2 tiles will have duplicate pixels
            // What did NOT work so far:
            // - Moving faces closer or further away from sphere by using an offset for 1 axis

            switch (face)
            {
                case CubeMapFace.Front: // Y-
                    origin.X = x - offset;
                    origin.Y = -offset;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Back: // Y+
                    origin.X = offset - x;
                    origin.Y = offset;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Left: // X-
                    origin.X = -offset;
                    origin.Y = offset -x;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Right: // X+
                    origin.X = offset;
                    origin.Y = x - offset;
                    origin.Z = offset - y;
                    break;
                case CubeMapFace.Up: // Z+
                    origin.X = x - offset;
                    origin.Y = offset - y;
                    origin.Z = offset;
                    break;
                case CubeMapFace.Down: // Z-
                    origin.X = offset - x;
                    origin.Y = offset - y;
                    origin.Z = -offset;
                    break;
            }
            var r = Math.Sqrt(origin.X * origin.X + origin.Y * origin.Y + origin.Z * origin.Z);

            return new Point3D(origin.X / r, origin.Y / r, origin.Z / r);
        }

    }

    enum CubeMapFace
    {
        Front,
        Back,
        Left,
        Right,
        Up,
        Down
    }

    class NoiseMaker
    {
        OpenSimplexNoise _noise;
        public double ResolutionScale;
        public double Weight;
        public double GetValue2D(double x, double y)
        {
            return _noise.Evaluate(x * ResolutionScale, y * ResolutionScale) * Weight;
        }
        public double GetValue3D(double x, double y, double z)
        {
            return _noise.Evaluate(x * ResolutionScale, y * ResolutionScale, z * ResolutionScale) * Weight;
        }
        public NoiseMaker(int seed, double resolutionScale, double weight)
        {
            _noise = new OpenSimplexNoise(seed);
            ResolutionScale = resolutionScale;
            Weight = weight;
        }
    }
}

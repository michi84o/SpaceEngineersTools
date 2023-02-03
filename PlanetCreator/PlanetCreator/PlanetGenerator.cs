using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
                new(2, 1.0 / 25, .1),
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
                        if (value < min) min = value; // Non-Thread safe access! TODO?
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
            }

            // Apply erosion
            // Place a water droplet on each pixel. Let droplet move 'down' (determined by gradient).
            // When droplet moves, it will take 1/65535 of height with it. When it reaches the lowest point, add 1/65536 to terrain height.
            bool erode = true;
            if (erode)
            {
                //double erMin = 0;
                //double erMax = 0;
                var rnd = new Random(0);
                var ff = _faces[CubeMapFace.Up];

                HashSet<int> xxx = new();

                // ii: Number of erosion iterations
                for (int ii = 0; ii < 5; ++ii)
                {
                    Debug.WriteLine("ii: " + ii);
                    // Lets work on every pixel in parallel but make sure we have some distance:
                    // First run   0 -> 16 -> 32 -> ... -> 2032
                    // Second run  1 -> 17 -> 33 -> ... -> 2033
                    // Last run   15 -> 31 -> 47 -> ... -> 2047
                    int skipWidth = 16;

                    long cnt = 0;

                    for (int skipX = 0; skipX < skipWidth; ++skipX)
                    {
                        Parallel.For(0, _tileWidth / skipWidth, new ParallelOptions { MaxDegreeOfParallelism = 8 }, xx =>
                        {
                            for (int skipY = 0; skipY < skipWidth; ++skipY)
                            {
                                Parallel.For(0, _tileWidth / skipWidth, new ParallelOptions { MaxDegreeOfParallelism = 8 }, yy =>
                                {
                                    Interlocked.Increment(ref cnt);

                                    var x = xx * skipWidth + skipX;
                                    var y = yy * skipWidth + skipY;

                                    if (x > 512 || y > 512) return;

                                    Erode(ff, x, y, 0);
                                });
                            }
                        });

                    }
                }

                //// Normalize noise to 0....1
                //offset = -1 * erMin;
                //stretch = Math.Abs(erMax - erMin);
                //Parallel.For(0, _tileWidth, x =>
                //{
                //    Parallel.For(0, _tileWidth, y =>
                //    {
                //        double value = ff2[x, y];
                //        value += offset;
                //        value /= stretch;
                //        ff[x, y] = value;
                //    });
                //});
            }

            // WIP, TODO
            // Planet features:
            // A: Flat planes on equator.
            // B: Mountains around 45°
            // C: Flat poles
            // - Weighten noise based on location
            // - Apply some erosion to generate canyons (rain by using gradients + east-west wind)
            // - Add lakes

            // Create pictures
            foreach (var kv in _faces)
            {
                var face = kv.Key;
                var tile = kv.Value;
                TileToImage(tile, face);
            }
            MessageBox.Show("Done");
        }

        void Erode(double[,] ff, int x, int y, int iteration)
        {
            var xp = x;
            var yp = y;
            // =========================================================================================
            // Surrounding gradients: (using simplified math here, substraction instead of division)
            List<NeighborGradient> neighbors = new();
            if (xp > 0)
            {
                neighbors.Add(new NeighborGradient { X = xp - 1, Y = yp }); // Left
                if (yp > 0) neighbors.Add(new NeighborGradient { X = xp - 1, Y = yp - 1 }); // Top Left
                if (yp < 2047) neighbors.Add(new NeighborGradient { X = xp - 1, Y = yp + 1 }); // Bottom Left
            }
            if (yp > 0) neighbors.Add(new NeighborGradient { X = xp, Y = yp - 1 }); // Top
            if (yp < 2047) neighbors.Add(new NeighborGradient { X = xp, Y = yp + 1 }); // Bottom
            if (xp < 2047)
            {
                neighbors.Add(new NeighborGradient { X = xp + 1, Y = yp }); // Right
                if (yp > 0) neighbors.Add(new NeighborGradient { X = xp + 1, Y = yp - 1 }); // Top Right
                if (yp < 2047) neighbors.Add(new NeighborGradient { X = xp + 1, Y = yp + 1 }); // Bottom Right
            }
            // =========================================================================================
            // Loop all neighbors and calculate gradient
            double maxGradient = 0;
            foreach (var neighbor in neighbors)
            {
                neighbor.Gradient = ff[neighbor.X, neighbor.Y] - ff[xp, yp];
                if (neighbor.Gradient < maxGradient)
                {
                    maxGradient = neighbor.Gradient;
                }
            }
            // Remove all neighbors that are higher
            neighbors.RemoveAll(o => o.Gradient >= 0);
            // =========================================================================================
            if (neighbors.Count == 0)
                return;
            maxGradient = Math.Abs(maxGradient); // Number will always be 0 or less
            // Apply erosion
            // Take some material away and wash it onto neighbors
            // Amount of material washed away is 1/65535 or maxGradient, whatever is less
            var matValue = (150.0 / 65535) / iteration;
            if (maxGradient < matValue) matValue = maxGradient;

            var decrement = matValue / 16;

            // Give part of material to random neighbors.
            // Stop if neighbor reached same height
            List<NeighborGradient> lowerNeighbors = new List<NeighborGradient>(neighbors);
            Random rnd = new Random();
            while (matValue > 0 && lowerNeighbors.Count > 0)
            {
                var val = ff[xp, yp] - decrement;
                var index = rnd.Next(lowerNeighbors.Count);
                var n = lowerNeighbors[index];
                var nVal = ff[n.X, n.Y] + decrement*0.8; // Some material gets lost
                if (nVal >= val)
                {
                    nVal = val = (nVal + val) / 2;
                    lowerNeighbors.RemoveAt(index);
                }
                if (val < 0) val = 0;
                if (nVal < 0) nVal = 0;

                ff[xp, yp] = val;
                ff[n.X, n.Y] = nVal;
                matValue -= decrement;
            }

            // Don't iterate when all neighbors are equalized
            if (lowerNeighbors.Count == 0) return;

            // Abort on too many iterations
            if (iteration >= 16) return;

            // All lower neighbors:
            foreach (var neighbor in lowerNeighbors)
            {
                Erode(ff, neighbor.X, neighbor.Y, iteration + lowerNeighbors.Count);
            }

            //++iteration;
            // Lowest neigbor only:
            // Find lowest neighbot and let it continue
            //NeighborGradient lowest = lowerNeighbors[0];

            //if (lowerNeighbors.Count > 1)
            //{
            //    for (int i=1;i<lowerNeighbors.Count; i++)
            //    {
            //        var n = lowerNeighbors[i];
            //        if (ff[n.X,n.Y] < ff[lowest.X, lowest.Y])
            //            lowest = n;
            //    }
            //}
            //Erode(ff, lowest.X, lowest.Y, iteration);
        }

        class NeighborGradient
        {
            public int X;
            public int Y;
            public double Gradient;
        }
        //class XY
        //{
        //    public double X;
        //    public double Y;
        //}

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

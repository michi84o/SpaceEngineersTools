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

        double[,] _debugTile = new double[2048, 2048];

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
                var rnd = new Random(0);
                var facesValues = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToArray();

                Parallel.For(0, 10000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, pit =>
                {
                    Erode(rnd);
                });


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
            //foreach (var kv in _faces)
            //{
            //    var face = kv.Key;
            //    var tile = kv.Value;
            //    TileToImage(tile, face);
            //}

            TileToImage(_faces[CubeMapFace.Up], CubeMapFace.Up);
            TileToImage(_debugTile, CubeMapFace.Down);

            MessageBox.Show("Done");
        }

        void Erode(Random rnd)
        {
            CubeMapPoint point;
            double volume = 1f;
            double sediment = 0;
            double minCapacity = 0.1;
            double waterLossFactor = 0.99;
            double erodeFactor = 0.3;
            int maxDropletLifetime = 30;
            CubeMapFace face;

            lock (rnd)
            {
                /*
                 point = new CubeMapPoint(
                     _faces,
                     rnd.Next(0, _tileWidth),
                     rnd.Next(0, _tileWidth),
                     facesValues[rnd.Next(0, facesValues.Length)]);
                */
                // DEBUG:
                point = new CubeMapPoint(
                     _faces,
                     rnd.Next(0, 512),
                     rnd.Next(0, 512),
                     CubeMapFace.Up);
            }

            PointD lastGradient = new PointD();
            for (int i = 0; i < maxDropletLifetime; ++i)
            {
                int velocityDirX, velocityDirY;
                CubeMapPoint.CalculateDirection(point.VelocityX, point.VelocityY, out velocityDirX, out velocityDirY);

                _debugTile[point.PosX, point.PosY] = volume;

                var nextPoint = CubeMapPoint.GetPointRelativeTo(point, velocityDirX, velocityDirY, _faces);
                var oldPoint = point.Clone();
                point.PosX = nextPoint.PosX;
                point.PosY = nextPoint.PosY;
                point.Face = nextPoint.Face;

                if (i > 0 && velocityDirX == 0 && velocityDirY == 0)
                {
                    return;
                }

                // =======================================================================
                // STEP 1: Apply erosion
                // =======================================================================
                var heightDiff = point.Value - oldPoint.Value;

                // Capacity for sediment
                double capacity = Math.Max(lastGradient.Length * volume * oldPoint.VelocityLength, minCapacity);

                if (sediment > capacity || heightDiff > 0)
                {
                    // Apply fill
                    double sedimentDelta = 0;
                    // Fill up if uphill, else deposit what is too much
                    if (heightDiff > 0)
                    {
                        sedimentDelta = -1* Math.Min(heightDiff, sediment);
                        oldPoint.Value += sediment; // TODO: Could cause sudden jumps
                    }
                    else
                    {
                        // Try to distribute the sediment around point to even out terrrain
                        sedimentDelta = capacity - sediment; // this number is <0
                        List<CubeMapPoint> cell = new List<CubeMapPoint>();
                        cell.Add(oldPoint);
                        for (int x = -1; x <= 1; ++x)
                        {
                            for (int y = -1; y <= 1; ++y)
                            {
                                if (x == 0 && y == 0) continue;
                                cell.Add(CubeMapPoint.GetPointRelativeTo(oldPoint, x, y, _faces));
                            }
                        }
                        // Get average heigt
                        var avgHeight = 0.0;
                        foreach (var p in cell)
                            avgHeight += p.Value;
                        avgHeight /= 9;
                        // Fill up anything below average height
                        var lowerPoints = cell.Where(p => p.Value < avgHeight).ToList();
                        lowerPoints.Sort((a,b)=> a.Value.CompareTo(b.Value));
                        var sedimentLeft = -sedimentDelta;
                        foreach (var p in lowerPoints)
                        {
                            var sedimentToUse = Math.Min(sedimentLeft, avgHeight - p.Value);
                            sedimentLeft -= sedimentToUse;
                            p.Value += sedimentToUse;
                            if (sedimentLeft <= 0) break;
                        }

                    }
                    sediment += sedimentDelta;
                }
                else
                {
                    // No erosion on flat terrain (heightDiff = 0)
                    double sedimentDelta = -1 * Math.Min((capacity - sediment) * erodeFactor, -heightDiff);
                    // Apply erosion:
                    // - Current pixel gets 50%
                    // - Give rest other neightbors
                    //   - Horizontal and vertical neigbors get more than diagonal ones
                    //     - Imagine a circle of radius 1 around current point and the surface of neighbors it covers
                    if (sedimentDelta != 0)
                    {
                        double horVert = 0.17857 / 2; // /2 because 50%
                        double diag = 0.071428 / 2;
                        // If you multiply each number above by 4 and add them, this should result in 0.5
                        for (int x = -1; x <= 1; ++x)
                        {
                            for (int y = -1; y <= 1; ++y)
                            {
                                if (x == 0 && y == 0)
                                {
                                    oldPoint.Value += sedimentDelta;
                                }
                                var pxy = CubeMapPoint.GetPointRelativeTo(oldPoint, x, y, _faces);
                                if (x != 0 && y != 0)
                                    pxy.Value += diag * sedimentDelta;
                                else
                                    pxy.Value += horVert * sedimentDelta;
                            }
                        }
                        sediment += -sedimentDelta;
                    }
                }
                volume *= waterLossFactor;
                // =======================================================================
                // STEP 2: Add acceleration added by gradient and move point
                // =======================================================================
                lastGradient = point.GetGradient();
                int gradientDirX, gradientDirY;
                CubeMapPoint.CalculateDirection(lastGradient.X, lastGradient.Y, out gradientDirX, out gradientDirY);
                var gPoint = CubeMapPoint.GetPointRelativeTo(point, gradientDirX, gradientDirY, _faces);
                double gDist = 1;
                if (gradientDirX != 0 && gradientDirY != 0) gDist = 1.414;

                var gg = point.Value - gPoint.Value;
                // Calculate force applied by gradient: F = sin(alpha)*m*g
                // g = gravity, m = mass. Let's set both to 1
                // For simplification use this triangle:
                //     /|
                //    / | gg
                //   /a)|
                //  -----
                //   dDist (1 or 1.414)
                // alpha = atan(G)
                // F = sin(atan(G)) = G/(SQRT(d^2+G^2))
                // F = mass * acceleration
                // Since mass = 1:
                var acceleration = gg / (Math.Sqrt(gDist * gDist + gg * gg));

                point.VelocityX += gg * (lastGradient.X / lastGradient.Length);
                point.VelocityY += gg * (lastGradient.Y / lastGradient.Length);

                // Add some friction: (would normally be based on speed and other factors)
                //point.VelocityX *= 0.95;
                //point.VelocityY *= 0.95;
            }
        }

        class CubeMapPoint
        {
            public CubeMapFace Face { get; set; } = CubeMapFace.Up;

            public double Value
            {
                get => _faces[Face][PosX, PosY];
                set => _faces[Face][PosX, PosY] = value;
            }

            public int PosX { get; set; }
            public int PosY { get; set; }

            public PointD GetGradient()
            {
                CalcGradient(out var gx, out var gy);
                return new PointD { X = gx, Y = gy };
            }

            public double VelocityX;
            public double VelocityY;
            public double VelocityLength => Math.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY);

            Dictionary<CubeMapFace, double[,]> _faces;

            public CubeMapPoint(Dictionary<CubeMapFace, double[,]> faces, int posX = 0, int posY = 0, CubeMapFace face = CubeMapFace.Up)
            {
                _faces = faces;
                Face = face;
                PosX = posX;
                PosY = posY;
            }

            public CubeMapPoint Clone()
            {
                return new CubeMapPoint(_faces, PosX, PosY, Face)
                {
                    VelocityX = VelocityX,
                    VelocityY = VelocityY
                };
            }

            void CalcGradient(out double gx, out double gy)
            {
                // Gradient is steepness of terrain. Consider this matrix:
                // | G00 G10 G20 |
                // | G01 G11 G21 |
                // | G02 G12 G22 |
                //
                // Our current position is G11.
                // The gradient to each neighbor is: Gxx-G11/distance
                // The points G10,G01,G21 and G12 have a distance of 1
                // The other points are diagonal and have a distance of Sqrt(2) = 1.414 (Pythagoras)
                //
                // Each gradient causes a force that drags the water droplet towards it
                // We can just sum the gradients up indepentently for each axis to get the final gradient vector

                var g00 = (GetPointRelativeTo(this, -1, -1, _faces).Value - Value) / 1.414;
                var g10 = GetPointRelativeTo(this, 0, -1, _faces).Value - Value;
                var g20 = (GetPointRelativeTo(this, 1, -1, _faces).Value - Value) / 1.414;
                var g01 = GetPointRelativeTo(this, -1, 0, _faces).Value - Value;
                var g21 = GetPointRelativeTo(this, 1, 0, _faces).Value - Value;
                var g02 = (GetPointRelativeTo(this, -1, 1, _faces).Value - Value) / 1.414;
                var g12 = GetPointRelativeTo(this, 0, 1, _faces).Value - Value;
                var g22 = (GetPointRelativeTo(this, 1, 1, _faces).Value - Value) / 1.414;

                gx = (g00 + g01 + g02) - (g20 + g21 + g22);
                gy = (g00 + g10 + g20) - (g02 + g12 + g22);
            }

            public static void CalculateDirection(double vx, double vy, out int dx, out int dy)
            {
                dx = 0;
                dy = 0;

                if (vy == 0 && vx == 0) return;

                var angle = Math.Atan2(vy, vx) * 180 / Math.PI; ;
                if (angle >= -22.5 && angle < 22.5)
                {
                    dx = 1;
                }
                else if (angle >= 22.5 && angle < 67.5)
                {
                    dx = 1;
                    dy = 1;
                }
                else if (angle >= 67.5 && angle < 112.5)
                {
                    dy = 1;
                }
                else if (angle >= 112.5 && angle < 157.5)
                {
                    dx = -1;
                    dy = 1;
                }
                else if (angle >= 157.5 || angle < -157.5)
                {
                    dx = -1;
                }
                else if (angle >= -157.5 && angle < -112.5)
                {
                    dx = -1;
                    dy = -1;
                }
                else if (angle >= -112.5 && angle < -67.5)
                {
                    dy = -1;
                }
                else
                {
                    dx = 1;
                    dy = -1;
                }
            }

            /// <summary>
            /// Do not use dx or dy values greater than 2048!!!
            /// </summary>
            /// <param name="origin"></param>
            /// <param name="dx"></param>
            /// <param name="dy"></param>
            /// <returns></returns>
            public static CubeMapPoint GetPointRelativeTo(CubeMapPoint origin, int dx, int dy, Dictionary<CubeMapFace, double[,]> faces)
            {
                if (dx == 0 && dy == 0)
                    return origin;

                int backup;
                // Move in X direction first:
                var currentFace = origin.Face;
                var currentX = origin.PosX + dx;
                var currentY = origin.PosY;
                if (currentX < 0) // West
                {
                    switch (origin.Face)
                    {
                        case CubeMapFace.Up:
                            // West of 'Up' is 'Left', rotated clockwise by 90°
                            // x/y flipped!
                            backup = currentX;
                            currentX = origin.PosY;
                            currentY = (-1 * backup) - 1;
                            return GetPointRelativeTo(
                                new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Left)
                                {
                                    VelocityX = origin.VelocityY,
                                    VelocityY = -1 * origin.VelocityX
                                }, dy, 0, faces);
                        case CubeMapFace.Down:
                            // West of 'Down' is 'Right', rotated counterclockwise by 90°
                            // x/y flipped!
                            currentY = 2048 + currentX;
                            currentX = 2047 - origin.PosY;
                            return GetPointRelativeTo(
                                new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Right)
                                {
                                    VelocityX = -1 * origin.VelocityY,
                                    VelocityY = origin.VelocityX
                                }, -dy, 0, faces);
                        case CubeMapFace.Left:
                            // West of 'Left' is 'Back'
                            currentX += 2048;
                            currentFace = CubeMapFace.Back;
                            break;
                        case CubeMapFace.Right:
                            // West of 'Right' is 'Front'
                            currentX += 2048;
                            currentFace = CubeMapFace.Front;
                            break;
                        case CubeMapFace.Front:
                            // West of 'Right' is 'Left'
                            currentX += 2048;
                            currentFace = CubeMapFace.Left;
                            break;
                        case CubeMapFace.Back:
                            // West of 'Back' is 'Right'
                            currentX += 2048;
                            currentFace = CubeMapFace.Right;
                            break;
                    }
                }
                else if (currentX > 2047) // East
                {
                    switch (origin.Face)
                    {
                        case CubeMapFace.Up:
                            // East of 'Up' is 'Right' rotated counterclockwise by 90°
                            // x/y flipped! dy & velocityXY must be converted!
                            currentY = currentX - 2048;
                            currentX = 2047 - origin.PosY;
                            return GetPointRelativeTo(
                                new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Right)
                                {
                                    VelocityX = -1 * origin.VelocityY,
                                    VelocityY = origin.VelocityX,
                                }, -dy, 0, faces);
                        case CubeMapFace.Down:
                            // East of 'Down' is 'Left', rotated clockwise by 90°
                            // x/y flipped! dy & velocityXY must be converted!
                            currentY = (2047 + 2048) - currentX; // range: 2047..->..0 bottom to top
                            currentX = origin.PosY;
                            currentFace = CubeMapFace.Left;
                            return GetPointRelativeTo(
                                new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Left)
                                {
                                    VelocityX = origin.VelocityY,
                                    VelocityY = -1 * origin.VelocityX,
                                }, dy, 0, faces);
                        case CubeMapFace.Left:
                            // East of 'Left' is 'Front'
                            currentX = currentX - 2048;
                            currentFace = CubeMapFace.Front;
                            break;
                        case CubeMapFace.Right:
                            // East of 'Right' is 'Back'
                            currentX = currentX - 2048;
                            currentFace = CubeMapFace.Back;
                            break;
                        case CubeMapFace.Front:
                            // East of 'Front' is 'Right'
                            currentX = currentX - 2048;
                            currentFace = CubeMapFace.Right;
                            break;
                        case CubeMapFace.Back:
                            // East of 'Back' is 'Left'
                            currentX = currentX - 2048;
                            currentFace = CubeMapFace.Back;
                            break;
                    }
                }

                // Now move in Y direction:
                currentY = currentY + dy;
                var currentVelocityX = origin.VelocityX;
                var currentVelocityY = origin.VelocityY;
                if (currentY < 0) // North
                {
                    switch (origin.Face)
                    {
                        case CubeMapFace.Up:
                            // North of 'Up' is 'Back' rotated by 180°
                            currentX = 2047 - currentX;
                            currentY = (-1 * currentY) - 1;
                            currentVelocityX = -1 * origin.VelocityX;
                            currentVelocityY = -1 * origin.VelocityY;
                            currentFace = CubeMapFace.Back;
                            break;
                        case CubeMapFace.Down:
                            // North of 'Down' is 'Back'
                            currentY = 2048 + currentY;
                            currentFace = CubeMapFace.Back;
                            break;
                        case CubeMapFace.Left:
                            // North of 'Left' is 'Up' rotated counterclockwise by 90°
                            backup = currentX;
                            currentX = (-1 * currentY) - 1;
                            currentY = backup;
                            currentVelocityX = -1 * origin.VelocityY;
                            currentVelocityY = origin.VelocityX;
                            currentFace = CubeMapFace.Up;
                            break;
                        case CubeMapFace.Right:
                            // North of 'Right' is 'Up' rotated clockwise by 90!
                            backup = currentX;
                            currentX = currentY + 2048;
                            currentY = 2047 - backup;
                            currentVelocityX = origin.VelocityY;
                            currentVelocityY = -1 * origin.VelocityX;
                            currentFace = CubeMapFace.Up;
                            break;
                        case CubeMapFace.Front:
                            // North of 'Front' is 'Up'
                            currentY = currentY + 2048;
                            currentFace = CubeMapFace.Up;
                            break;
                        case CubeMapFace.Back:
                            // North of 'Back is 'Up' rotated by 180°
                            backup = currentX;
                            currentX = 2047 - currentX;
                            currentY = (-1 * currentY) - 1;
                            currentVelocityX = -1 * origin.VelocityX;
                            currentVelocityY = -1 * origin.VelocityY;
                            currentFace = CubeMapFace.Up;
                            break;
                    }
                }
                else if (currentY > 2047) // South
                {
                    switch (origin.Face)
                    {
                        case CubeMapFace.Up:
                            // South of 'Up' is 'Front'
                            currentY = 2048 - currentY;
                            currentFace = CubeMapFace.Front;
                            break;
                        case CubeMapFace.Down:
                            // South of 'Down' is 'Front' rotated by 180°
                            currentX = 2047 - currentX;
                            currentY = (2047 + 2048) - currentY;
                            currentVelocityX = currentVelocityX * -1;
                            currentVelocityY = currentVelocityY * -1;
                            currentFace = CubeMapFace.Front;
                            break;
                        case CubeMapFace.Left:
                            // South of 'Left' is 'Down' rotated counterclockwise by 90°
                            backup = currentX;
                            currentX = (2047 + 2048) - currentY;
                            currentY = backup;
                            currentVelocityX = -1 * origin.VelocityY;
                            currentVelocityY = origin.VelocityX;
                            currentFace = CubeMapFace.Down;
                            break;
                        case CubeMapFace.Right:
                            // South of 'Right' is 'Down' rotated clockwise by 90°
                            backup = currentX;
                            currentX = currentY - 2048;
                            currentY = 2047 - backup;
                            currentVelocityX = origin.VelocityY;
                            currentVelocityY = -1 * origin.VelocityX;
                            currentFace = CubeMapFace.Down;
                            break;
                        case CubeMapFace.Front:
                            // South of 'Right' is 'Down' rotated by 180°
                            backup = currentX;
                            currentX = 2047 - currentX;
                            currentY = (2047 + 2048) - currentY;
                            currentVelocityX = -1 * origin.VelocityX;
                            currentVelocityY = -1 * origin.VelocityY;
                            currentFace = CubeMapFace.Down;
                            break;
                        case CubeMapFace.Back:
                            // South of 'Back' is 'Down'
                            currentY = currentY - 2048;
                            break;
                    }
                }
                return new CubeMapPoint(faces, currentX, currentY, currentFace)
                {
                    VelocityX = currentVelocityX,
                    VelocityY = currentVelocityY,
                };
            }

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
                    origin.Y = offset - x;
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

    class PointD
    {
        public double X;
        public double Y;
        public double Length => Math.Sqrt(X* X + Y* Y);
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

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
    class ProgressEventArgs : EventArgs
    {
        public int Progress { get; }
        public ProgressEventArgs(int progress)
        {
            Progress = progress;
        }
    }

    internal class PlanetGenerator
    {
        const int _tileWidth = 2048;

        public event EventHandler<ProgressEventArgs> ProgressChanged;

        public bool DebugMode
        {
            get => _debug;
            set => _debug = value;
        }

        public PlanetGenerator()
        {
            Init();
        }
        void Init()
        {
            _faces = new()
            {
                { CubeMapFace.Up, new double[_tileWidth, _tileWidth] },
                { CubeMapFace.Down, new double[_tileWidth, _tileWidth] },
                { CubeMapFace.Front, new double[_tileWidth, _tileWidth] },
                { CubeMapFace.Right, new double[_tileWidth, _tileWidth] },
                { CubeMapFace.Left, new double[_tileWidth, _tileWidth] },
                { CubeMapFace.Back, new double[_tileWidth, _tileWidth] },
            };
            _lakes = new()
            {
                { CubeMapFace.Up, new byte[_tileWidth, _tileWidth] },
                { CubeMapFace.Down, new byte[_tileWidth, _tileWidth] },
                { CubeMapFace.Front, new byte[_tileWidth, _tileWidth] },
                { CubeMapFace.Right, new byte[_tileWidth, _tileWidth] },
                { CubeMapFace.Left, new byte[_tileWidth, _tileWidth] },
                { CubeMapFace.Back, new byte[_tileWidth, _tileWidth] },
            };
        }

        /* Cube Map: Folds into a cube
        [UP]
        [FRONT][RIGHT][BACK][LEFT]
                      [DOWN]
        */

        Dictionary<CubeMapFace, double[,]> _faces;
        Dictionary<CubeMapFace, byte[,]> _lakes;
        double _lakeDepth = 30.0 / 65535;

        double[,] _debugTileR = new double[2048, 2048];
        double[,] _debugTileG = new double[2048, 2048];
        double[,] _debugTileB = new double[2048, 2048];
        bool _debug = false;
        CubeMapFace _debugFace = CubeMapFace.Back;

        public int Seed { get; set; } = 0;
        public int NoiseScale { get; set; } = 100;
        public int Octaves { get; set; } = 4;
        public int ErosionIterations { get; set; } = 1000000;

        public void GeneratePlanet(CancellationToken token)
        {
            // Layers of diferent noise frequencies
            List<NoiseMaker> list = new();
            //{
            //    new(Seed + 0, 1.0 / NoiseScale, 1),
            //    new(Seed + 1, 2.0 / NoiseScale, .5),
            //    new(Seed + 2, 4.0 / NoiseScale, 0.25),
            //    new(Seed + 3, 8.0 / NoiseScale, 0.125),
            //};
            var octave = 1;
            for (int i = 0; i < Octaves; ++i)
            {
                list.Add(new(Seed + 0, 1.0 * octave / NoiseScale, 1.0 / (octave)));
                octave *= 2;
            }
            double sphereRadius = 1000;

            // Apply noise
            foreach (var kv in _faces)
            {
                if (token.IsCancellationRequested) return;
                var face = kv.Key;
                var tile = kv.Value;
                if (_debug && face != _debugFace) continue;
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
                        tile[x, y] = value;
                    });

                });
            }

            if (token.IsCancellationRequested) return;
            if (_debug)
            {
                Parallel.For(0, _tileWidth, x =>
                {
                    Parallel.For(0, _tileWidth, y =>
                    {
                        _debugTileR[x, y] = 0.5;
                        _debugTileG[x, y] = 0.5;
                        _debugTileB[x, y] = 0.5;
                    });
                });
            }

            if (token.IsCancellationRequested) return;
            // Normalize noise to 0....1
            if (!_debug)
                Normalize(_faces.Values.ToList());
            else
                Normalize(new List<double[,]> { _faces[_debugFace] });

            // TODO: Tweak these:
            // Apply an S-Curve for flatter plains and mountain tops
            foreach (var kv in _faces)
            {
                if (token.IsCancellationRequested) return;
                var face = kv.Key;
                var tile = kv.Value;
                if (_debug && face != _debugFace) continue;
                Parallel.For(0, _tileWidth, x =>
                {
                    Parallel.For(0, _tileWidth, y =>
                    {
                        double value = tile[x, y];
                        value = 0.5 * Math.Sin(Math.PI * (value - 0.5)) + 0.5;
                        // Use this line to modify further:
                        value = Math.Pow(value, 2.5); // Lower=More mountains, Higher: more flat plains
                        if (value < 0) value = 0;
                        if (value > 1) value = 1;
                        tile[x, y] = value;
                    });
                });
            }

            // Generate lakes:
            foreach (var kv in _faces)
            {
                if (token.IsCancellationRequested) return;
                var face = kv.Key;
                var tile = kv.Value;
                if (_debug && face != _debugFace) continue;
                Parallel.For(0, _tileWidth, x =>
                {
                    Parallel.For(0, _tileWidth, y =>
                    {
                        double value = tile[x, y];
                        if (value <= _lakeDepth)
                        {
                            _lakes[face][x, y] = 82; // 82 Red Channel = Lake
                        }
                    });
                });
            }

            ProgressChanged?.Invoke(this, new ProgressEventArgs(5));

            // Apply erosion
            // Place a water droplet on each pixel. Let droplet move 'down' (determined by gradient).
            // When droplet moves, it will take 1/65535 of height with it. When it reaches the lowest point, add 1/65536 to terrain height.
            if (EnableErosion)
            {
                int iterations = ErosionIterations;
                if (_debug)
                {
                    // 1 instead of 6 faces -> 1/6
                    // 1/16 of area of normal face
                    iterations /= (6*16);
                    if (iterations == 0) iterations = 1;
                }
                var rnd = new Random(Seed);
                int progress = 0;
                Task.Run(async () =>
                {
                    while (progress < iterations - 1)
                    {
                        if (token.IsCancellationRequested) return;
                        await Task.Delay(1000);
                        ProgressChanged?.Invoke(this, new ProgressEventArgs(
                           (int)(5 + progress * 90.0 / iterations)));
                    }
                });
                var processors = Environment.ProcessorCount;
                if (processors < 4) processors = 4;
                try
                {
                    Parallel.For(0, iterations, new ParallelOptions { MaxDegreeOfParallelism = processors * 2, CancellationToken = token }, pit =>
                    {
                        Erode(rnd, token);
                        Interlocked.Increment(ref progress);
                    });
                }
                catch { return; } // Cancelled

                // Refresh lakes:
                foreach (var kv in _faces)
                {
                    if (token.IsCancellationRequested) return;
                    var face = kv.Key;
                    var tile = kv.Value;
                    if (_debug && face != _debugFace) continue;
                    Parallel.For(0, _tileWidth, x =>
                    {
                        Parallel.For(0, _tileWidth, y =>
                        {
                            double value = tile[x, y];
                            if (value <= _lakeDepth)
                            {
                                _lakes[face][x, y] = 82; // 82 Red Channel = Lake
                            }
                        });
                    });
                }

                //// Normalize noise to 0....1
                //if (!_debug)
                //    Normalize(_faces.Values.ToList());
                //else
                //    Normalize(new List<double[,]> { _faces[_debugFace] });
            }

            // WIP, TODO
            // Planet features:
            // A: Flat planes on equator.
            // B: Mountains around 45°
            // C: Flat poles
            // - Weighten noise based on location
            // - Apply some erosion to generate canyons (rain by using gradients + east-west wind)
            // - Add lakes
            if (!_debug)
            {
                // Create pictures
                foreach (var kv in _faces)
                {
                    if (token.IsCancellationRequested) return;
                    var face = kv.Key;
                    var tile = kv.Value;
                    TileToImage(tile, face);

                    WriteMaterialMap(_lakes[face], face);
                }
            }
            else
            {
                if (token.IsCancellationRequested) return;
                WriteMaterialMap(_lakes[_debugFace], _debugFace);
                TileToImage(_faces[_debugFace], _debugFace);
                var image = new Image<Rgb48>(_tileWidth, _tileWidth);
                Normalize(new List<double[,]> { _debugTileR });
                Normalize(new List<double[,]> { _debugTileG });
                Normalize(new List<double[,]> { _debugTileB });
                Parallel.For(0, _tileWidth, x =>
                {
                    Parallel.For(0, _tileWidth, y =>
                    {
                        image[x, y] = new Rgb48((ushort)(_debugTileR[x, y] * 65535), (ushort)(_debugTileG[x, y] * 65535), (ushort)(_debugTileB[x, y] * 65535));
                    });
                });
                image.SaveAsPng("debug.png");
            }

            ProgressChanged?.Invoke(this, new ProgressEventArgs(100));
        }

        public bool EnableErosion { get; set; } = true;
        public int ErosionMaxDropletLifeTime { get; set; } = 100;
        public double ErosionInteria { get; set; } = 0.01;
        public double ErosionSedimentCapacityFactor { get; set; } = 30;
        public double ErosionDepositSpeed { get; set; } = 0.1;
        public double ErosionErodeSpeed { get; set; } = 0.3;
        public double ErosionDepositBrush { get; set; } = 3;
        public double ErosionErodeBrush { get; set; } = 3;
        public double Gravity { get; set; } = 10;
        public double EvaporateSpeed { get; set; } = 0.01;
        void Erode(Random rnd, CancellationToken token)
        {
            // Code based on Sebastian Lague
            // https://www.youtube.com/watch?v=eaXk97ujbPQ
            // https://github.com/SebLague/Hydraulic-Erosion
            // which is based on a paper by Hans Theobald Beyer
            // https://www.firespark.de/resources/downloads/implementation%20of%20a%20methode%20for%20hydraulic%20erosion.pdf
            // which is based on a blog entry Alexey Volynskov
            // http://ranmantaru.com/blog/2011/10/08/water-erosion-on-heightmap-terrain/

            int maxDropletLifetime = ErosionMaxDropletLifeTime;
            double inertia = ErosionInteria;
            double speed = 1;
            double water = 1;
            double sediment = 0;
            double sedimentCapacityFactor = ErosionSedimentCapacityFactor;
            double minSedimentCapacity = 0.5/65535; // Half a pixel value
            double depositSpeed = ErosionDepositSpeed;
            double erodeSpeed = ErosionErodeSpeed;
            double erodeBrush = ErosionErodeBrush;
            double depositBrush = ErosionDepositBrush;
            double evaporateSpeed = EvaporateSpeed;
            double gravity = Gravity;

            // Make sure we have no water left at the end:
            // Modify this exponential curve to become 0 at the end:
            // water *= (1 - evaporateSpeed);
            double waterX = water;
            for (int i = 0; i < maxDropletLifetime-1; ++i)
            {
                waterX *= (1 - evaporateSpeed);
            }
            double evaporatePart = waterX;
            // => waterModified = water - evaporatePart*iteration

            CubeMapFace face = CubeMapFace.Up;
            PointD pos = new();
            PointD dir = new();
            var facesValues = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToArray();

            lock (rnd)
            {
                if (!_debug)
                {
                    pos.X = rnd.NextDouble() * 2047;
                    pos.Y = rnd.NextDouble() * 2047;
                    face = facesValues[rnd.Next(0, facesValues.Length)];
                }
                else
                {
                    //pos.X = 333;
                    //pos.Y = 458;
                    pos.X = rnd.NextDouble() * 512 + 512;
                    pos.Y = rnd.NextDouble() * 512 + 512;
                    //pos.X = rnd.NextDouble() * 2047;
                    //pos.Y = rnd.NextDouble() * 2047;
                    face = _debugFace;
                }
            }

            #region Code based on Sebastian Lague
            for (int i = 0; i < maxDropletLifetime; ++i)
            {
                if (token.IsCancellationRequested) return;

                double waterModified = water - (evaporatePart * i / (maxDropletLifetime-1)); // Will be 0 at last iteration

                // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
                PointD cellOffset = pos.IntegerOffset(); // 0 <= offset < 1

                // Calculate droplet's height and direction of flow with bilinear interpolation of surrounding heights
                HeightAndGradient heightAndGradient = CalculateHeightAndGradient(pos, face);

                // Update the droplet's direction and position (move position 1 unit regardless of speed)
                dir = (dir * inertia - heightAndGradient.Gradient * (1 - inertia));

                // Normalize direction
                dir = dir.Normalize();
                CubeMapFace faceOld = face;
                PointD posOld = pos;
                pos += dir;

                // Stop simulating droplet if it's not moving
                if (dir.X == 0 && dir.Y == 0) break;

                var posI = pos.ToIntegerPoint();
                var posOldI = posOld.ToIntegerPoint();

                if (posI.X >= _tileWidth || posI.X < 0 || posI.Y >= _tileWidth || posI.Y < 0)
                {
                    // We left the tile! Check in which tile we are now:
                    var newPosPoint = CubeMapPoint.GetPointRelativeTo(
                        new CubeMapPoint(_faces, posOldI.X, posOldI.Y, face) { VelocityX = dir.X, VelocityY = dir.Y, OffsetX = cellOffset.X, OffsetY = cellOffset.Y },
                            posI.X - posOldI.X,
                            posI.Y - posOldI.Y,
                            _faces);
                    // Update face, position, speed and offset:
                    //Debug.WriteLine("Face {0};{1}", face, newPosPoint.Face);
                    if (newPosPoint.Face != face)
                    {
                        if (newPosPoint.PosX >= _tileWidth || newPosPoint.PosX < 0 || newPosPoint.PosY >= _tileWidth || newPosPoint.PosY < 0)
                        {
                            Debugger.Break();
                        }
                        face = newPosPoint.Face;
                        pos.X = newPosPoint.PosX + newPosPoint.OffsetX;
                        pos.Y = newPosPoint.PosY + newPosPoint.OffsetY;
                        //Debug.WriteLine("Pos {0};{1}", pos.X, pos.Y);
                        dir.X = newPosPoint.VelocityX;
                        dir.Y = newPosPoint.VelocityY;
                        cellOffset.X = newPosPoint.OffsetX;
                        cellOffset.Y = newPosPoint.OffsetY;
                    }
                }

                posI = pos.ToIntegerPoint();
                if (_lakes[face][posI.X, posI.Y] == 82)
                {
                    // Lakes swallow everything
                    break;
                }

                // Find the droplet's new height and calculate the deltaHeight
                double newHeight = CalculateHeightAndGradient(pos, face).Height;
                double deltaHeight = newHeight - heightAndGradient.Height;

                // Calculate the droplet's sediment capacity (higher when moving fast down a slope and contains lots of water)
                double sedimentCapacity = Math.Max(-deltaHeight * speed * waterModified * sedimentCapacityFactor, minSedimentCapacity);


                if (_debug)
                {
                    _debugTileG[posOldI.X, posOldI.Y] = waterModified;
                    //var sedimentFill = sediment / sedimentCapacity;
                    //if (sedimentFill > 1) sedimentFill = 1;
                    //_debugTileG[posOldI.X, posOldI.Y + 1] = sedimentFill;
                }


                // If carrying more sediment than capacity, or if flowing uphill:
                var oldPoint = new CubeMapPoint(_faces, posOldI.X, posOldI.Y, faceOld);
                if (sediment > sedimentCapacity || deltaHeight > 0)
                {
                    // If moving uphill (deltaHeight > 0) try fill up to the current height, otherwise deposit a fraction of the excess sediment
                    double amountToDeposit = (deltaHeight > 0) ? Math.Min(deltaHeight, sediment) : (sediment - sedimentCapacity) * depositSpeed;

                    ApplyBrush(depositBrush, oldPoint, posOld, amountToDeposit);
                    sediment -= amountToDeposit;
                }
                else
                {
                    // Erode a fraction of the droplet's current carry capacity.
                    // Clamp the erosion to the change in height so that it doesn't dig a hole in the terrain behind the droplet
                    double amountToErode = Math.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);

                    ApplyBrush(erodeBrush, oldPoint, posOld, -amountToErode);
                    sediment += amountToErode;
                }
                // Update delta
                deltaHeight = newHeight - CalculateHeightAndGradient(posOld, faceOld).Height;

                // Update droplet's speed and water content
                double gravityDelta = (-deltaHeight) * gravity;
                double speedSquared = speed*speed;
                // Prevent speed from becomming double.NaN
                // None of the authors this code was based on noticed this flaw
                if (gravityDelta < 0 && -gravityDelta > speedSquared)
                {
                    speed = Math.Sqrt(-gravityDelta);
                    dir.X = -dir.X;
                    dir.Y = -dir.Y;
                }
                else
                    speed = Math.Sqrt(speedSquared + gravityDelta);
                water *= (1 - evaporateSpeed);

            }

            #endregion
        }

        public bool GenerateLakes { get; set; } = true;
        public ushort LakeDepth { get; set; } = 5;

        void FillWithWater()
        {

        }
        void ApplyBrush(double brushRadius, CubeMapPoint mapLocation, PointD exactLocation, double materialAmount)
        {
            List<CubeMapPoint> brushPoints = new();
            List<double> brushWeights = new();
            int brushDelta = (int)(brushRadius + 0.5);
            double brushWeightSum = 0;
            // Cycle through all points within radius
            for (int dx = -brushDelta; dx <= brushDelta; ++dx)
            {
                for (int dy = -brushDelta; dy <= brushDelta; ++dy)
                {
                    var pt = CubeMapPoint.GetPointRelativeTo(mapLocation, dx, dy, _faces);
                    double brushWeight = 0;
                    // Split grid into 16 chunks, divide by 4 on each axis. Vector goes to center of subgrid
                    for (double subDx = -0.375; subDx <= 0.3751; subDx += 0.25)
                    {
                        for (double subDy = -0.375; subDy <= 0.3751; subDy += 0.25)
                        {
                            PointD vector = new PointD
                            {
                                X = pt.PosX + subDx - exactLocation.X,
                                Y = pt.PosY + subDy - exactLocation.Y,
                            };
                            var distance = vector.Length;
                            if (distance <= brushRadius)
                            {
                                double newWeight = 1 - distance / brushRadius;
                                brushWeight += newWeight;
                                brushWeightSum += newWeight;
                            }
                        }
                    }
                    brushPoints.Add(pt);
                    brushWeights.Add(brushWeight);
                    if (materialAmount > 0 && _lakes[pt.Face][pt.PosX, pt.PosY] == 82)
                    {
                        // Don't deposit stuff near lake!
                        return;
                    }
                }
            }
            for (int ii = 0; ii < brushWeights.Count; ++ii)
            {
                double brushpart = materialAmount * brushWeights[ii] / brushWeightSum;
                brushPoints[ii].Value += brushpart;

                if (_debug)
                {
                    if (brushpart > 0)
                        _debugTileB[brushPoints[ii].PosX, brushPoints[ii].PosY] += brushpart;
                    else
                        _debugTileR[brushPoints[ii].PosX, brushPoints[ii].PosY] += -brushpart;
                }

                if (brushPoints[ii].Value > 1) brushPoints[ii].Value = 1;
                else if (brushPoints[ii].Value < 0) brushPoints[ii].Value = 0;
            }
        }

        void Normalize(List<double[,]> images)
        {
            double max = images[0][0, 0];
            double min = images[0][0, 0];
            Parallel.For(0, _tileWidth, x =>
            {
                Parallel.For(0, _tileWidth, y =>
                {
                    foreach (var image in images)
                    {
                        var value = image[x, y];
                        if (value > max)
                        {
                            lock (images)
                            {
                                if (value > max) max = value;
                            }
                        }
                        if (value < min)
                        {
                            lock (images)
                            {
                                if (value < min) min = value;
                            }
                        }
                    }
                });
            });
            double offset = -1 * min;
            double stretch = Math.Abs(max - min);
            Parallel.For(0, _tileWidth, x =>
            {
                Parallel.For(0, _tileWidth, y =>
                {
                    foreach (var image in images)
                    {
                        var value = image[x, y];
                        value += offset;
                        value /= stretch;
                        image[x, y] = value;
                    }
                });
            });
        }

        HeightAndGradient CalculateHeightAndGradient(PointD pos, CubeMapFace face)
        {
            int coordX = (int)pos.X;
            int coordY = (int)pos.Y;
            // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
            double x = pos.X - coordX;
            double y = pos.Y - coordY;

            var point = new CubeMapPoint(_faces, coordX, coordY, face);

            // Calculate heights of the four nodes of the droplet's cell
            double heightNW = CubeMapPoint.GetPointRelativeTo(point,-1,-1,_faces).Value;
            double heightNE = CubeMapPoint.GetPointRelativeTo(point, 1, 0, _faces).Value;
            double heightSW = CubeMapPoint.GetPointRelativeTo(point, -1, 1, _faces).Value;
            double heightSE = CubeMapPoint.GetPointRelativeTo(point, 1, 1, _faces).Value;

            // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
            double gradientX = (heightNE - heightNW) * (1 - y) + (heightSE - heightSW) * y;
            double gradientY = (heightSW - heightNW) * (1 - x) + (heightSE - heightNE) * x;

            // Calculate height with bilinear interpolation of the heights of the nodes of the cell
            double height = heightNW * (1 - x) * (1 - y) + heightNE * x * (1 - y) + heightSW * (1 - x) * y + heightSE * x * y;

            return new HeightAndGradient() { Height = height, Gradient = { X = gradientX, Y = gradientY } };
        }

        struct HeightAndGradient
        {
            public double Height;
            public PointD Gradient;
        }

        class CubeMapPoint
        {
            public CubeMapFace Face { get; set; } = CubeMapFace.Up;

            public double Value
            {
                get => _faces[Face][PosX, PosY];
                set => _faces[Face][PosX, PosY] = value;
            }

            // 0 <= offset < 1
            public double OffsetX { get; set; }
            public double OffsetY { get; set; }


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
                double backupD;
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
                                    VelocityY = -1 * origin.VelocityX,
                                    OffsetX = origin.OffsetY,
                                    OffsetY = 1 - origin.OffsetX
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
                                    VelocityY = origin.VelocityX,
                                    OffsetX = 1 - origin.OffsetY,
                                    OffsetY = origin.OffsetX,
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
                                    OffsetX = 1 - origin.OffsetY,
                                    OffsetY = origin.OffsetX
                                }, -dy, 0, faces);
                        case CubeMapFace.Down:
                            // East of 'Down' is 'Left', rotated clockwise by 90°
                            // x/y flipped! dy & velocityXY must be converted!
                            currentY = (2047 + 2048) - currentX; // range: 2047..->..0 bottom to top
                            currentX = origin.PosY;
                            return GetPointRelativeTo(
                                new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Left)
                                {
                                    VelocityX = origin.VelocityY,
                                    VelocityY = -1 * origin.VelocityX,
                                    OffsetX = origin.OffsetY,
                                    OffsetY = 1 - origin.OffsetX
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
                            currentFace = CubeMapFace.Left;
                            break;
                    }
                }

                // Now move in Y direction:
                currentY = currentY + dy;
                var currentVelocityX = origin.VelocityX;
                var currentVelocityY = origin.VelocityY;
                var currentOffsetX = origin.OffsetX;
                var currentOffsetY = origin.OffsetY;
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
                            currentOffsetX = 1 - origin.OffsetX;
                            currentOffsetY = 1 - origin.OffsetY;
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
                            currentOffsetX = 1 - origin.OffsetY;
                            currentOffsetY = origin.OffsetX;
                            currentFace = CubeMapFace.Up;
                            break;
                        case CubeMapFace.Right:
                            // North of 'Right' is 'Up' rotated clockwise by 90!
                            backup = currentX;
                            currentX = currentY + 2048;
                            currentY = 2047 - backup;
                            currentVelocityX = origin.VelocityY;
                            currentVelocityY = -1 * origin.VelocityX;
                            currentOffsetX = origin.OffsetY;
                            currentOffsetY = 1 - origin.OffsetX;
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
                            currentOffsetX = 1 - origin.OffsetX;
                            currentOffsetY = 1 - origin.OffsetY;
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
                            currentY = currentY - 2048;
                            currentFace = CubeMapFace.Front;
                            break;
                        case CubeMapFace.Down:
                            // South of 'Down' is 'Front' rotated by 180°
                            currentX = 2047 - currentX;
                            currentY = (2047 + 2048) - currentY;
                            currentVelocityX = currentVelocityX * -1;
                            currentVelocityY = currentVelocityY * -1;
                            currentOffsetX = 1 - origin.OffsetX;
                            currentOffsetY = 1 - origin.OffsetY;
                            currentFace = CubeMapFace.Front;
                            break;
                        case CubeMapFace.Left:
                            // South of 'Left' is 'Down' rotated counterclockwise by 90°
                            backup = currentX;
                            currentX = (2047 + 2048) - currentY;
                            currentY = backup;
                            currentVelocityX = -1 * origin.VelocityY;
                            currentVelocityY = origin.VelocityX;
                            currentOffsetX = 1 - origin.OffsetY;
                            currentOffsetY = origin.OffsetX;
                            currentFace = CubeMapFace.Down;
                            break;
                        case CubeMapFace.Right:
                            // South of 'Right' is 'Down' rotated clockwise by 90°
                            backup = currentX;
                            currentX = currentY - 2048;
                            currentY = 2047 - backup;
                            currentVelocityX = origin.VelocityY;
                            currentVelocityY = -1 * origin.VelocityX;
                            currentOffsetX = origin.OffsetY;
                            currentOffsetY = 1 - origin.OffsetX;
                            currentFace = CubeMapFace.Down;
                            break;
                        case CubeMapFace.Front:
                            // South of 'Front' is 'Down' rotated by 180°
                            backup = currentX;
                            currentX = 2047 - currentX;
                            currentY = (2047 + 2048) - currentY;
                            currentVelocityX = -1 * origin.VelocityX;
                            currentVelocityY = -1 * origin.VelocityY;
                            currentOffsetX = 1 - origin.OffsetX;
                            currentOffsetY = 1 - origin.OffsetY;
                            currentFace = CubeMapFace.Down;
                            break;
                        case CubeMapFace.Back:
                            // South of 'Back' is 'Down'
                            currentY = currentY - 2048;
                            currentFace = CubeMapFace.Down;
                            break;
                    }
                }
                return new CubeMapPoint(faces, currentX, currentY, currentFace)
                {
                    VelocityX = currentVelocityX,
                    VelocityY = currentVelocityY,
                    OffsetX = currentOffsetX,
                    OffsetY = currentOffsetY,
                };
            }

        }

        // tiles must be normalized to 0...1 !!!
        void TileToImage(double[,] tile, CubeMapFace face)
        {
            TileToImage(tile, face.ToString().ToLower() + ".png");
        }
        void TileToImage(double[,] tile, string fileName)
        {
            // Using full spectrum is too exteme. Flatten area:
            double minVal = 0.3;
            double maxVal = 0.9;
            double lakeLevel = (maxVal - minVal) * _lakeDepth + minVal;

            var image = new Image<L16>(_tileWidth, _tileWidth);
            Parallel.For(0, _tileWidth, x =>
            {
                Parallel.For(0, _tileWidth, y =>
                {
                    var v = (maxVal-minVal) * tile[x, y] + minVal;
                    if (v > 1) v = 1;
                    else if (v < 0) v = 0;
                    if (v < lakeLevel) v = lakeLevel;
                    ushort value = (ushort)(v * 65535);
                    image[x, y] = new L16(value);
                });
            });
            image.SaveAsPng(fileName);
        }

        void WriteMaterialMap(byte[,] red, CubeMapFace face)
        {
            WriteMaterialMap(red, face.ToString().ToLower() + "_mat.png");
        }

        void WriteMaterialMap(byte[,] red, string fileName)
        {
            var image = new Image<Rgb24>(_tileWidth, _tileWidth);
            Parallel.For(0, _tileWidth, x =>
            {
                Parallel.For(0, _tileWidth, y =>
                {
                    image[x, y] = new Rgb24(red[x,y], 0,0);
                });
            });
            image.SaveAsPng(fileName);
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

    struct PointD
    {
        public double X;
        public double Y;

        public static PointD operator +(PointD p1, PointD p2) => new PointD { X = p1.X + p2.X, Y = p1.Y + p2.Y };
        public static PointD operator +(PointD p1) => p1;
        public static PointD operator -(PointD p1, PointD p2) => new PointD { X = p1.X - p2.X, Y = p1.Y - p2.Y };
        public static PointD operator -(PointD p1) => new PointD { X = -p1.X, Y = -p1.Y };
        public static PointD operator /(PointD p1, double d) => new PointD { X = p1.X / d, Y = p1.Y / d };
        public static PointD operator /(PointD p1, PointD p2) => new PointD { X = p1.X / p2.X, Y = p1.Y / p2.Y };
        public static PointD operator *(PointD p1, double d) => new PointD { X = p1.X * d, Y = p1.Y * d };
        public static PointD operator *(PointD p1, PointD p2) => new PointD { X = p1.X * p2.X, Y = p1.Y * p2.Y };

        public double Length => Math.Sqrt(X* X + Y* Y);

        public PointD Normalize()
        {
            var len = Length;
            if (len == 0) return this;
            return this / len;
        }

        // !!! (int)(-0.1) = 0 !!! But we want -1 !!!
        public PointI ToIntegerPoint() => new PointI() { X = X < 0? (int)(X-1) : (int)X, Y = Y < 0 ? (int)(Y - 1) : (int)Y };
        public PointD IntegerOffset() => new PointD() { X = X - (int)X, Y = Y - (int)Y };
    }

    struct PointI
    {
        public int X;
        public int Y;
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

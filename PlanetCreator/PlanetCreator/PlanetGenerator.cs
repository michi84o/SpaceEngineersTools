using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input.Manipulations;
using System.Windows.Media.Animation;
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
        public bool ExtendedDebugMode
        {
            get; set;
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
            //_quadrants = new()
            //{
            //    { CubeMapFace.Up, new List<object> { new object(), new object(), new object(), new object(), } },
            //    { CubeMapFace.Down, new List<object> { new object(), new object(), new object(), new object(), } },
            //    { CubeMapFace.Front, new List<object> { new object(), new object(), new object(), new object(), } },
            //    { CubeMapFace.Right, new List<object> { new object(), new object(), new object(), new object(), } },
            //    { CubeMapFace.Left, new List<object> { new object(), new object(), new object(), new object(), } },
            //    { CubeMapFace.Back, new List<object> { new object(), new object(), new object(), new object(), } }
            //};
        }

        /* Cube Map: Folds into a cube
        [UP]
        [FRONT][RIGHT][BACK][LEFT]
                      [DOWN]
        */

        Dictionary<CubeMapFace, double[,]> _faces;
        Dictionary<CubeMapFace, byte[,]> _lakes;
        //Dictionary<CubeMapFace, List<object>> _quadrants; // Use for multitasking water fill
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

        void UpdateProgress(int progress)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(progress));
        }

        public ParallelOptions POptions(CancellationToken token)
        {
            var processors = Environment.ProcessorCount;
            if (processors < 4) processors = 4;
            return new ParallelOptions { MaxDegreeOfParallelism = processors * 2, CancellationToken = token };
        }


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
                Parallel.For(0, _tileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < _tileWidth; ++y)
                    {
                        var point = GetNormalizedSphereCoordinates(face, x, y);
                        double value = 0;
                        foreach (var nm in list)
                        {
                            value += nm.GetValue3D(point.X * sphereRadius, point.Y * sphereRadius, point.Z * sphereRadius);
                        }
                        tile[x, y] = value;
                    }
                });
            }

            // Fix glitches at border of cubemaps
            EdgeFixer.MakeSeemless(_faces, token);

            if (token.IsCancellationRequested) return;
            if (_debug)
            {
                Parallel.For(0, _tileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < _tileWidth; ++y)
                    {
                        _debugTileR[x, y] = 0.5;
                        _debugTileG[x, y] = 0.5;
                        _debugTileB[x, y] = 0.5;
                    };
                });
            }

            if (token.IsCancellationRequested) return;
            // Normalize noise to 0....1
            if (!_debug)
                Normalize(_faces.Values.ToList(), token);
            else
                Normalize(new List<double[,]> { _faces[_debugFace] }, token);

            // TODO: Tweak these:
            // Apply an S-Curve for flatter plains and mountain tops
            foreach (var kv in _faces)
            {
                if (token.IsCancellationRequested) return;
                var face = kv.Key;
                var tile = kv.Value;
                if (_debug && face != _debugFace) continue;
                Parallel.For(0, _tileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < _tileWidth; ++y)
                    {
                        double value = tile[x, y];
                        value = 0.5 * Math.Sin(Math.PI * (value - 0.5)) + 0.5;
                        // Use this line to modify further:
                        value = Math.Pow(value, 2.5); // Lower=More mountains, Higher: more flat plains
                        if (value < 0) value = 0;
                        if (value > 1) value = 1;
                        tile[x, y] = value;
                    };
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
                    iterations /= (6 * (ExtendedDebugMode ? 1 : 16));
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
                        UpdateProgress((int)(5 + progress * 45.0 / iterations));
                    }
                });
                if (token.IsCancellationRequested) return;
                try
                {
                    Parallel.For(0, iterations, POptions(token), pit =>
                    {
                        Erode(rnd, token);
                        Interlocked.Increment(ref progress);
                    });
                }
                catch { return; } // Cancelled

            }

            UpdateProgress(50);

            // Normalize noise to 0....1
            if (!_debug)
                Normalize(_faces.Values.ToList(), token);
            else
                Normalize(new List<double[,]> { _faces[_debugFace] }, token);

            if (GenerateLakes)
            {
                MakeLakes(token);
            }

            // Fix glitches at border of cubemaps, again since erosion does not care about duplicate edge pixels
            EdgeFixer.MakeSeemless(_faces, token);

            ProgressChanged?.Invoke(this, new ProgressEventArgs(95));

            // WIP, TODO
            // Planet features:
            // A: Flat planes close to equator.
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

                    if (GenerateLakes)
                        WriteMaterialMap(_lakes[face], face);
                }
            }
            else
            {
                if (token.IsCancellationRequested) return;
                if (GenerateLakes)
                    WriteMaterialMap(_lakes[_debugFace], _debugFace);
                TileToImage(_faces[_debugFace], _debugFace);
                var image = new Image<Rgb48>(_tileWidth, _tileWidth);
                Normalize(new List<double[,]> { _debugTileR }, token);
                Normalize(new List<double[,]> { _debugTileG }, token);
                Normalize(new List<double[,]> { _debugTileB }, token);
                Parallel.For(0, _tileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < _tileWidth; ++y)
                    {
                        image[x, y] = new Rgb48((ushort)(_debugTileR[x, y] * 65535), (ushort)(_debugTileG[x, y] * 65535), (ushort)(_debugTileB[x, y] * 65535));
                    }
                });
                image.SaveAsPng("debug.png");
            }

            ProgressChanged?.Invoke(this, new ProgressEventArgs(100));
        }

        public bool EnableErosion { get; set; } = true;
        public int ErosionMaxDropletLifeTime { get; set; } = 100;
        public double ErosionInteria { get; set; } = 0.01;
        public double ErosionSedimentCapacityFactor { get; set; } = 25;
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
            double minSedimentCapacity = 0.5 / 65535; // Half a pixel value
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
            for (int i = 0; i < maxDropletLifetime - 1; ++i)
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
                    if (!ExtendedDebugMode)
                    {
                        pos.X = rnd.NextDouble() * 512 + 512;
                        pos.Y = rnd.NextDouble() * 512 + 512;
                    }
                    else
                    {
                        pos.X = rnd.NextDouble() * 2047;
                        pos.Y = rnd.NextDouble() * 2047;
                    }
                    face = _debugFace;
                }
            }

            #region Code based on Sebastian Lague
            for (int i = 0; i < maxDropletLifetime; ++i)
            {
                if (token.IsCancellationRequested) return;

                double waterModified = water - (evaporatePart * i / (maxDropletLifetime - 1)); // Will be 0 at last iteration

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
                    var newPosPoint = (new CubeMapPoint(_faces, posOldI.X, posOldI.Y, face) { VelocityX = dir.X, VelocityY = dir.Y, OffsetX = cellOffset.X, OffsetY = cellOffset.Y })
                        .GetPointRelative(
                            posI.X - posOldI.X,
                            posI.Y - posOldI.Y);
                    // Update face, position, speed and offset:
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

                // Find the droplet's new height and calculate the deltaHeight
                double newHeight = CalculateHeightAndGradient(pos, face).Height;
                double deltaHeight = newHeight - heightAndGradient.Height;

                // Calculate the droplet's sediment capacity (higher when moving fast down a slope and contains lots of water)
                double sedimentCapacity = Math.Max(-deltaHeight * speed * waterModified * sedimentCapacityFactor, minSedimentCapacity);


                if (_debug)
                {
                    _debugTileG[posOldI.X, posOldI.Y] = waterModified;
                    //DebugOverlay.DebugDrawPixel(_debugFace, posOldI.X, posOldI.Y, 1, (byte)(255*waterModified), 0,0);
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
                double speedSquared = speed * speed;
                // Prevent speed from becomming double.NaN
                // None of the authors this code was based on noticed this flaw
                if (gravityDelta < 0 && -gravityDelta > speedSquared)
                {
                    speed = Math.Sqrt(-gravityDelta);
                    dir.X = -dir.X;
                    dir.Y = -dir.Y;
                }
                else // The square doesn't even make sense, but whatever
                    speed = Math.Sqrt(speedSquared + gravityDelta);
                water *= (1 - evaporateSpeed);

            }

            #endregion
        }

        public bool GenerateLakes { get; set; } = true;
        public ushort LakesPerTile { get; set; } = 40;
        public double LakeVolumeMultiplier { get; set; } = 1.0;

        public IDebugOverlay DebugOverlay;

        void CheckFillLake(CubeMapPoint point, double stopHeight, double maxVolume, int maxFilledPixels, CancellationToken token, out int filledPixelCount, out HashSet<CubeMapPointLight> filledPoints , out double filledVolume)
        {
            filledPixelCount = 0;
            filledPoints = new();
            filledVolume = 0;
            if (_debug && point.Face != _debugFace) return;
            if (point.Value >= stopHeight) return;
            var pointLight = new CubeMapPointLight { Face = point.Face, X = point.PosX, Y = point.PosY };
            //var faceValues = _faces.Keys.ToList();
            //if (_debug) faceValues = new List<CubeMapFace> { _debugFace };
            //foreach ( var faceValue in faceValues ) filledPixels[faceValue] = new bool[_tileWidth, _tileWidth];
            Queue<CubeMapPointLight> queue = new Queue<CubeMapPointLight>();

            // Use this to prevent thousands of duplicates beeing added to the queue
            HashSet<CubeMapPointLight> queuedPoints = new();

            queue.Enqueue(pointLight);
            while (queue.Count > 0)
            {
                if (token.IsCancellationRequested) return;
                var pt = queue.Dequeue();
                if (_debug)
                {
                    if (pt.Face != _debugFace ||
                        (pt.X < 1 || pt.X > _tileWidth - 2 ||
                         pt.Y < 1 || pt.Y > _tileWidth - 2))
                        continue;
                }
                if (!filledPoints.Contains(pt))
                {
                    if (_debug && pt.Face != _debugFace) Debugger.Break();

                    ++filledPixelCount;
                    filledPoints.Add(pt);
                    filledVolume += (stopHeight - CubeMapPointLight.GetValue(pt, _faces));
                    if (filledVolume > maxVolume || filledPixelCount > maxFilledPixels) return;
                }

                var np = CubeMapPointLight.GetPointRelativeTo(pt,-1, 0);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, _faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
                np = CubeMapPointLight.GetPointRelativeTo(pt, 0, -1);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, _faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
                np = CubeMapPointLight.GetPointRelativeTo(pt, 1, 0);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, _faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
                np = CubeMapPointLight.GetPointRelativeTo(pt, 0, 1);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, _faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
            }
        }

        void MakeLakes(CancellationToken token)
        {

            // Strategy: Drop water on tiles and see where they end up. No inertia.
            int pixelsPerTile = 262144;
            int totalTilePixels = _tileWidth * _tileWidth;
            int pixelGap = totalTilePixels / pixelsPerTile;
            int pixelsPerRow = _tileWidth / pixelGap;
            int lostPixels = _tileWidth - (pixelsPerRow * pixelGap);
            int offset = lostPixels / 2;

            Dictionary<CubeMapFace, double[,]> waterSpots = new Dictionary<CubeMapFace, double[,]>();
            Dictionary<CubeMapFace, double[,]> waterSpots2 = new Dictionary<CubeMapFace, double[,]>();
            var faceValues = _faces.Keys.ToList();
            if (_debug) { faceValues = new List<CubeMapFace> { CubeMapFace.Back }; }
            foreach (var face in faceValues)
            {
                waterSpots[face] = new double[_tileWidth, _tileWidth];
                waterSpots2[face] = new double[_tileWidth, _tileWidth];
            }
            // TODO: Stacking Parallel.For() might not be efficient
            Parallel.For(0, faceValues.Count, POptions(token), faceIndex =>
            {
                var face = faceValues[faceIndex];
                Parallel.For(0, 1 + _tileWidth / pixelGap, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, xred =>
                {
                    for (int yred = 0; yred < 1 + _tileWidth / pixelGap; ++yred)
                    {
                        int x = offset + xred * pixelGap;
                        int y = offset + yred * pixelGap;
                        if (x >= _tileWidth || y >= _tileWidth) continue;

                        var currentFace = face;
                        PointD pos = new PointD { X = x, Y = y };
                        PointD posOld = pos;
                        CubeMapPoint oldPoint = null;
                        HeightAndGradient heightAndGradient = CalculateHeightAndGradient(pos, currentFace);
                        int iteration = 0;
                        bool floored = false;
                        while (iteration++ < _tileWidth / 2 && !token.IsCancellationRequested) // Limit movement to half map
                        {
                            var dir = -heightAndGradient.Gradient;
                            dir = dir.Normalize();
                            PointD cellOffset = pos.IntegerOffset();

                            HeightAndGradient lastHeight = heightAndGradient;
                            posOld = pos;
                            var posOldI = posOld.ToIntegerPoint();
                            oldPoint = new CubeMapPoint(_faces, posOldI.X, posOldI.Y, currentFace) { VelocityX = dir.X, VelocityY = dir.Y, OffsetX = cellOffset.X, OffsetY = cellOffset.Y };

                            // Update position
                            pos += dir;

                            #region Out of bounds handling
                            var posI = pos.ToIntegerPoint();
                            if (posI.X >= _tileWidth || posI.X < 0 || posI.Y >= _tileWidth || posI.Y < 0)
                            {
                                // We left the tile! Check in which tile we are now:
                                var newPosPoint = oldPoint.GetPointRelative(
                                    posI.X - posOldI.X,
                                    posI.Y - posOldI.Y);
                                // Update face, position, speed and offset:
                                if (newPosPoint.Face != currentFace)
                                {
                                    currentFace = newPosPoint.Face;
                                    pos.X = newPosPoint.PosX + newPosPoint.OffsetX;
                                    pos.Y = newPosPoint.PosY + newPosPoint.OffsetY;

                                }
                            }
                            #endregion

                            if (_debug)
                            {
                                var ipos = pos.ToIntegerPoint();
                                _debugTileR[ipos.X, ipos.Y] += 2;
                                if (_debugTileR[ipos.X, ipos.Y] > 50) _debugTileR[ipos.X, ipos.Y] = 50;

                                // Bad performance!!!
                                //DebugOverlay.DebugDrawPixel(_debugFace, posOldI.X, posOldI.Y, 128, 255, 0, 0);

                            }
                            heightAndGradient = CalculateHeightAndGradient(pos, currentFace);
                            if (heightAndGradient.Height >= lastHeight.Height)
                            {
                                floored = true;
                                break;
                            }

                        } // while
                        if (!floored || oldPoint == null)
                        {
                            continue;
                        }
                        if (_debug && oldPoint.Face != _debugFace)
                        {
                            continue;
                        }
                        lock (waterSpots[oldPoint.Face])
                        {
                            waterSpots[oldPoint.Face][oldPoint.PosX, oldPoint.PosY] += 1;
                            _debugTileG[oldPoint.PosX, oldPoint.PosY] = 255;
                        }
                    }
                });
            });

            UpdateProgress(60);

            // Apply a folding matrix to determine maxima
            const int matrixSize = 9;
            for (int faceIndex = 0;  faceIndex < faceValues.Count; ++faceIndex)
            {
                if (token.IsCancellationRequested) return;
                var face = faceValues[faceIndex];
                Parallel.For(0, _tileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < _tileWidth; ++y)
                    {
                        if (token.IsCancellationRequested) return;
                        var point = new CubeMapPointLight { Face = face, X = x, Y = y };
                        double sum = 0;
                        for (int x2 = x - matrixSize / 2; x2 <= x + matrixSize / 2; ++x2)
                        {
                            for (int y2 = y - matrixSize / 2; y2 <= y + matrixSize / 2; ++y2)
                            {
                                var dp = CubeMapPointLight.GetPointRelativeTo(point,x2 - x, y2 - y);
                                if ((!_debug || dp.Face == face))
                                {
                                    var value = CubeMapPointLight.GetValue(dp, waterSpots);
                                    if (value > 0)
                                    {
                                        PointD d;
                                        d.X = x - x2;
                                        d.Y = y - y2;
                                        var distance = d.Length;
                                        sum += value * (1 / (distance + 0.5));
                                    }
                                }
                            }
                        }
                        waterSpots2[face][x, y] = sum;
                        _debugTileB[x, y] = sum;
                    }
                });
            }
            UpdateProgress(70);
            Dictionary<CubeMapFace, List<HighScore>> highscores = new Dictionary<CubeMapFace, List<HighScore>>();
            foreach (var face in faceValues)
            {
                highscores[face] = new List<HighScore>();
            }
            // We end up having lots of blue squares in the debug image and waterSpots2
            // Reduce squares to actual points
            // Brute Force Strategy:
            // - Check all points and put them in a managed high score list
            // - Avoid multiple scores within same area by checking distance
            const double safetyDistance = matrixSize * 1.41 + 5; // Diagonal length of 9x9 box + 5 pixel
            Dictionary<CubeMapFace, double> maxScore = new();
            foreach (var face in faceValues)
                maxScore[face] = 0;
            Parallel.For(0, faceValues.Count, POptions(token), faceIndex =>
            {
                var face = faceValues[faceIndex];
                var highscoreList = highscores[face];
                var waterSpotList = waterSpots2[face];
                double highScoreMin = 0;
                int highScoreCount = 0;
                //double highScoreMax = 0;
                Parallel.For(0, _tileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < _tileWidth; ++y)
                    {
                        var value = waterSpotList[x, y];
                        if (value == 0) continue;
                        if (value > highScoreMin || highScoreCount < (LakesPerTile + 10)) // Allow 10 backups so we can compensate skipping small lakes later
                        {
                            lock (highscoreList)
                            {
                                if (value > highScoreMin || highScoreCount < (LakesPerTile + 10))
                                {
                                    var score = new HighScore(value, x, y);
                                    var neighbors = highscoreList.Where(s => s.Position.IsWithinRadius(score.Position, safetyDistance)).ToList();
                                    if (neighbors.Any(s => s.Score > value)) continue;
                                    foreach (var neighbor in neighbors)
                                        highscoreList.Remove(neighbor);
                                    highscoreList.Add(score);

                                    highscoreList.Sort((a, b) => b.Score.CompareTo(a.Score));
                                    while (highscoreList.Count > LakesPerTile)
                                        highscoreList.RemoveAt(highscoreList.Count - 1);

                                    if (highscoreList[0].Score > maxScore[face])
                                        maxScore[face] = highscoreList[0].Score;
                                    highScoreMin = highscoreList[highscoreList.Count - 1].Score;
                                    highScoreCount = highscoreList.Count;
                                }
                            }
                        }
                    }
                });
            });
            UpdateProgress(80);
            if (_debug)
            {
                foreach (var score in highscores[_debugFace])
                {
                    var x = score.Position.X;
                    var y = score.Position.Y;
                    for (int xx = x - 2; xx < x + 2; xx++)
                    {
                        for (int yy = y - 2; yy < y + 2; yy++)
                        {
                            if (xx > 0 && yy > 0 && xx < _tileWidth && yy < _tileWidth)
                                _debugTileR[xx, yy] = 55;
                        }
                    }
                    // Somewhere between 70 and 30
                    //Debug.WriteLine("SCORE: s=" + (int)score.Score + ", p=" + score.Position.X + ";" + score.Position.Y);
                }
            }

            // Last step: Fill up the lakes
            // Warning! If lake depth is too high it will overflow and fill nearly the whole map
            // We need to determine the maximum sane size of a lake without overflowing
            Parallel.For(0, faceValues.Count, POptions(token), faceIndex =>
            {
                var face = faceValues[faceIndex];
                var highscoreList = highscores[face];
                int drawnLakeCount = 0;
                foreach (var score in highscoreList)
                {
                    if (drawnLakeCount >= LakesPerTile) break;
                    // We want each lake to be up to 100.000 pixels with some tolerance.
                    // Lake depth around 5 increments (1/65535) => volume = 1.5
                    // Max scores around 70:
                    var volume = score.Score * (1.5 / maxScore[face]) * LakeVolumeMultiplier;
                    if (volume < 0.8) volume = 0.8;
                    int maxFilledPixels = 120000;
                    int minFilledPixels = 25;

                    var point = new CubeMapPoint(_faces, score.Position.X, score.Position.Y, face);
                    var height = point.Value;
                    var stopHeight = height + 1.0 / 65535;

                    double lastStopHeight = stopHeight;
                    CheckFillLake(point, stopHeight, volume, maxFilledPixels, token, out var lastFilledPixelCount, out var lastFilledPoints, out var lastFilledVolume);
                    if (lastFilledPixelCount == 0)
                    {
                        Debug.WriteLine("Skipped lake at {0},{1}", point.PosX, point.PosY);
                        continue; // Not possible to fill stuff here
                    }
                    if (lastFilledVolume > volume || lastFilledPixelCount > maxFilledPixels)
                    {
                        Debug.WriteLine("Skipped lake at {0},{1}. Out of bounds.", point.PosX, point.PosY);
                        continue; // Probably non-fillable flat area
                    }

                    // Go with large increments of 5
                    while (true)
                    {
                        if (token.IsCancellationRequested) return;
                        CheckFillLake(point, stopHeight, volume, maxFilledPixels, token, out var filledPixelCount, out var filledPoints, out var filledVolume);
                        // Sanity check:
                        if (filledVolume > volume && filledPixelCount < minFilledPixels)
                            volume *= 1.5; // Increase volume by 50% if not enough pixels filled

                        if (filledPixelCount > 0 && filledVolume < volume && filledPixelCount < maxFilledPixels)
                        {
                            lastStopHeight = stopHeight;
                            lastFilledPixelCount = filledPixelCount;
                            lastFilledPoints = filledPoints;
                            lastFilledVolume = filledVolume;
                            stopHeight += 5.0/65535;
                            continue;
                        }
                        break;
                    }
                    // Repeat last but with 1 step increments
                    stopHeight = lastStopHeight + 1.0/65535;
                    while (true)
                    {
                        if (token.IsCancellationRequested) return;
                        CheckFillLake(point, stopHeight, volume, maxFilledPixels, token, out var filledPixelCount, out var filledPixels, out var filledVolume);
                        if (filledPixelCount > 0 && filledVolume < volume && filledPixelCount < maxFilledPixels)
                        {
                            lastStopHeight = stopHeight;
                            lastFilledPixelCount = filledPixelCount;
                            lastFilledPoints = filledPixels;
                            lastFilledVolume = filledVolume;
                            stopHeight += 1.0 / 65535;
                            continue;
                        }
                        break;
                    }
                    //Debug.WriteLine("Used increments: " + (int)((stopHeight - height) * 65535));
                    // Apply the lake
                    if (lastFilledPixelCount >= minFilledPixels) // Don't draw mini lakes
                    {
                        lock (_faces)
                        {
                            foreach (var pt in lastFilledPoints)
                            {
                                DebugOverlay.DebugDrawPixel(pt.Face, pt.X, pt.Y, 128, 255, 0, 0);
                                _faces[pt.Face][pt.X, pt.Y] = lastStopHeight;
                                _lakes[pt.Face][pt.X, pt.Y] = 82;
                            }
                            ++drawnLakeCount;
                        }
                    }
                }
            });
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
                    var pt = mapLocation.GetPointRelative(dx, dy);
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

        void Normalize(List<double[,]> images, CancellationToken token)
        {
            double max = images[0][0, 0];
            double min = images[0][0, 0];
            Parallel.For(0, _tileWidth, POptions(token), x =>
            {
                for (int y = 0; y < _tileWidth; ++y)
                {
                    foreach (var image in images)
                    {
                        if (token.IsCancellationRequested) return;
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
                }
            });
            if (token.IsCancellationRequested) return;
            double offset = -1 * min;
            double stretch = Math.Abs(max - min);
            Parallel.For(0, _tileWidth, POptions(token), x =>
            {
                for (int y = 0; y < _tileWidth; ++y)
                {
                    if (token.IsCancellationRequested) return;
                    foreach (var image in images)
                    {
                        var value = image[x, y];
                        value += offset;
                        value /= stretch;
                        image[x, y] = value;
                    }
                };
            });
        }

        HeightAndGradient CalculateHeightAndGradient(PointD pos, CubeMapFace face)
        {
            int coordX = (int)pos.X;
            int coordY = (int)pos.Y;
            // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
            // Remarks x and y are not coordinates, but offsets!
            double x = pos.X - coordX;
            double y = pos.Y - coordY;

            var point = new CubeMapPoint(_faces, coordX, coordY, face);

            // Calculate heights of the four nodes of the droplet's cell
            // Remarks: The pos point is within the NW pixel, so the sky directions might be misleading here.
            double heightNW = point.Value;
            double heightNE = point.GetPointRelative(1, 0).Value;
            double heightSW = point.GetPointRelative(0, 1).Value;
            double heightSE = point.GetPointRelative(1, 1).Value;

            // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
            double gradientX = (heightNE - heightNW) * (1 - y) + (heightSE - heightSW) * y;
            double gradientY = (heightSW - heightNW) * (1 - x) + (heightSE - heightNE) * x;

            // Calculate height with bilinear interpolation of the heights of the nodes of the cell
            double height = heightNW * (1 - x) * (1 - y) + heightNE * x * (1 - y) + heightSW * (1 - x) * y + heightSE * x * y;

            return new HeightAndGradient() { Height = height, Gradient = { X = gradientX, Y = gradientY } };
        }

        class HighScore
        {
            public double Score;
            public PointI Position;
            public HighScore(double score, int x, int y)
            {
                Score = score;
                Position.X = x;
                Position.Y = y;
            }
        }

        struct HeightAndGradient
        {
            public double Height;
            public PointD Gradient;
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
            WriteMaterialMap(red, face.ToString().ToLower() + "_lakes.png");
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

    public enum CubeMapFace
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
        public double DistanceTo(PointI p)
        {
            var dx = p.X - X;
            var dy = p.Y - Y;
            return Math.Sqrt(1.0* dx * dx + dy * dy);
        }
        public bool IsWithinRadius(PointI p, double radius)
        {
            var dx = p.X - X;
            var dy = p.Y - Y;
            if (dx < - radius || dx > radius || dy < -radius || dy > radius) return false;
            return Math.Sqrt(1.0 * dx * dx + dy * dy) <= radius;
        }
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

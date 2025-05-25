using MathNet.Numerics.Random;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Input.Manipulations;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Xaml.Schema;

namespace PlanetCreator
{
    public class ProgressEventArgs : EventArgs
    {
        public int Progress { get; }
        public ProgressEventArgs(int progress)
        {
            Progress = progress;
        }
    }

    public class PlanetGenerator
    {
        public event EventHandler<ProgressEventArgs> ProgressChanged;
        void OnProgress(int progress)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(progress));
        }

        public int TileWidth { get; set; } = 2048;
        public bool DebugMode { get; set; }
        public bool LimitedDebugMode { get; set; }
        public CubeMapFace PreviewFace { get; set; } = CubeMapFace.Back;
        public string WorkingDirectory { get; set; }

        /* Cube Map: Folds into a cube
        [UP]
        [FRONT][RIGHT][BACK][LEFT]
                      [DOWN]
        */

        public ParallelOptions POptions(CancellationToken token)
        {
            var processors = Environment.ProcessorCount;
            if (processors < 4) processors = 4;
            return new ParallelOptions { MaxDegreeOfParallelism = processors * 2, CancellationToken = token };
        }

        void GetPixelRange(out int cStart, out int cEnd)
        {
            cStart = 0;
            cEnd = TileWidth;
            if (LimitedDebugMode)
            {
                cStart = TileWidth / 4;
                cEnd = cStart * 2;
            }
        }

        Dictionary<CubeMapFace, double[,]> GetFaces()
        {
            Dictionary<CubeMapFace, double[,]> faces;

            if (DebugMode)
            {
                faces = new() { { PreviewFace, new double[TileWidth, TileWidth] } };
            }
            else faces = new()
            {
                { CubeMapFace.Up, new double[TileWidth, TileWidth] },
                { CubeMapFace.Down, new double[TileWidth, TileWidth] },
                { CubeMapFace.Front, new double[TileWidth, TileWidth] },
                { CubeMapFace.Right, new double[TileWidth, TileWidth] },
                { CubeMapFace.Left, new double[TileWidth, TileWidth] },
                { CubeMapFace.Back, new double[TileWidth, TileWidth] },
            };

            return faces;
        }

        void SaveFaces(Dictionary<CubeMapFace, double[,]> faces, CancellationToken token)
        {
            GetPixelRange(out var cStart, out var cEnd);
            // Save output
            Parallel.ForEach(faces, kv =>
            {
                if (token.IsCancellationRequested) return;
                var image = new Image<L16>(TileWidth, TileWidth);
                ImageHelper.InsertPixels(image, kv.Value);
                try
                {
                    image.SaveAsPng(Path.Combine(WorkingDirectory, (kv.Key + ".png").ToLower()));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        Dictionary<CubeMapFace, double[,]> LoadHeightMaps(CancellationToken token)
        {
            var faces = GetFaces();
            Parallel.ForEach(faces, kv =>
            {
                if (token.IsCancellationRequested) return;
                var sourceFile = Path.Combine(WorkingDirectory, (kv.Key + ".png")).ToLower();
                if (File.Exists(sourceFile))
                {
                    try
                    {
                        var img = Image.Load(sourceFile) as Image<L16>;
                        if (img.Width != TileWidth)
                            img.Mutate(k => k.Resize(TileWidth, TileWidth, KnownResamplers.NearestNeighbor));
                        ImageHelper.ExtractPixels(img, kv.Value);
                    }
                    catch { return; }
                }
            });
            return faces;
        }

        public void GenerateSimplexHeightMaps(int seed, int octaves, int noiseScale, CancellationToken token)
        {
            var faces = GetFaces();
            GetPixelRange(out var cStart, out var cEnd);

            OnProgress(1);

            // Layers of diferent noise frequencies
            List<NoiseMaker> list = new();
            var octave = 1;
            for (int i = 0; i < octaves; ++i)
            {
                list.Add(new(seed + 0, 1.0 * octave / noiseScale, 1.0 / (octave)));
                octave *= 2;
            }
            double sphereRadius = 1000;

            // Apply noise
            int progress = 0;
            foreach (var kv in faces)
            {
                ++progress;
                if (token.IsCancellationRequested) return;
                var face = kv.Key;
                var tile = kv.Value;
                Parallel.For(cStart, cEnd, POptions(token), x =>
                {
                    for (int y = cStart; y < cEnd; ++y)
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
                OnProgress((int)(progress*90.0/faces.Count));
            }
            Normalize(faces.Values.ToList(), token);
            if (!DebugMode)
                EdgeFixer.MakeSeamless(faces, token);
            OnProgress(95);
            Normalize(faces.Values.ToList(), token);
            OnProgress(99);
            SaveFaces(faces, token);
            OnProgress(100);
        }

        public void GenerateWorleyHeightMaps(int seed, int cells, bool invert, CancellationToken token)
        {
            var faces = GetFaces();

            double sphereRadius = 1000;
            SphericalWorleyNoise noise = new(seed, sphereRadius, cells);

            OnProgress(1);

            int cStart = 0;
            int cEnd = TileWidth;
            if (LimitedDebugMode)
            {
                cStart = TileWidth/4;
                cEnd = cStart*2;
            }

            // Apply noise
            int progress = 0;
            foreach (var kv in faces)
            {
                ++progress;
                if (token.IsCancellationRequested) return;
                var face = kv.Key;
                var tile = kv.Value;
                try
                {
                    Parallel.For(cStart, cEnd, POptions(token), x =>
                    {
                        for (int y = cStart; y < cEnd; ++y)
                        {
                            if (token.IsCancellationRequested) return;
                            var point = GetNormalizedSphereCoordinates(face, x, y);
                            double value = noise.GetValue3D(point.X * sphereRadius, point.Y * sphereRadius, point.Z * sphereRadius);
                            tile[x, y] = value;
                        }
                    });
                }
                catch { return; }
                OnProgress((int)(progress * 90.0 / faces.Count));
            }
            Normalize(faces.Values.ToList(), token, invert:invert);
            if (!DebugMode)
                EdgeFixer.MakeSeamless(faces, token);
            Normalize(faces.Values.ToList(), token);
            OnProgress(99);
            SaveFaces(faces, token);
            OnProgress(100);
        }

        public void AddWorleyFreckles(int seed, int frecklesPerTile, int cells, int segmentCount, int segmentRadius, CancellationToken token)
        {
            if (frecklesPerTile == 0) return;
            var faces = LoadHeightMaps(token);

            OnProgress(1);
            if (token.IsCancellationRequested) return;

            double worleyBlendStart = 0.5;
            double worleyLayerMaxHeight = .12; // .1 to flat, .15 too high

            var rnd = new Random(seed);
            int sphereRadius = 1000;
            var worley = new SphericalWorleyNoise(seed, sphereRadius, cells);
            int progress = 1;
            int progressIncrement = (int)(0.5 + 89 / (faces.Count * frecklesPerTile));
            try
            {
                Parallel.ForEach(faces, face =>
                {
                    if (token.IsCancellationRequested) return;
                    var worleyFreckleFile = Path.Combine(
                        WorkingDirectory,
                        (face.Key + "_worley.png").ToLower());
                    Image<Rgb24> image = null;
                    if (File.Exists(worleyFreckleFile))
                        image = Image.Load(worleyFreckleFile) as Image<Rgb24>;
                    if (image == null)
                        image = new Image<Rgb24>(TileWidth, TileWidth);
                    else if (image.Width != TileWidth)
                    {
                        image.Mutate(k => k.Resize(TileWidth, TileWidth, KnownResamplers.NearestNeighbor));
                    }
                    for (int i = 0; i < frecklesPerTile; ++i)
                    {
                        if (token.IsCancellationRequested) return;
                        if (i > 0)
                        {
                            Interlocked.Add(ref progress, progressIncrement);
                            OnProgress(progress);
                        }

                        double worleyMin = double.NaN;
                        double worleyMax = double.NaN;
                        var worleySubLayer = new double[TileWidth, TileWidth];
                        var worleyBlendLayer = new double[TileWidth, TileWidth];
                        List<int[]> patchPoints = new List<int[]>();
                        // Get Start points. Put them in the middle so we don't have to deal with edge cases
                        var x = (int)(rnd.NextInt64((int)(TileWidth * .8)) + TileWidth * .1);
                        var y = (int)(rnd.NextInt64((int)(TileWidth * .8)) + TileWidth * .1);
                        patchPoints.Add(new int[] { x, y });
                        // Extend area from start point
                        int numSteps = (int)rnd.NextInt64(1, segmentCount);
                        for (int j = 0; j < numSteps; ++j)
                        {
                            var dx = (int)rnd.NextInt64(segmentRadius * 2) - segmentRadius;
                            var dy = (int)rnd.NextInt64(segmentRadius * 2) - segmentRadius;
                            patchPoints.Add(new int[]
                            {
                                patchPoints[patchPoints.Count - 1][0] + dx,
                                patchPoints[patchPoints.Count - 1][1] + dy
                            });
                        }
                        // Draw worley patches
                        // -------------------
                        double maxDistance = (segmentRadius + 1); // Outside the radius
                                                                  // We compare without calculating the square root, so calculate the square
                        maxDistance *= maxDistance;
                        // We have x and y parts, so we need to of these.
                        maxDistance *= 2;

                        // For each patch point...
                        foreach (var patchPoint in patchPoints)
                        {
                            // ... get the surrounding points within the radius (using square for simplicity).
                            for (x = patchPoint[0] - segmentRadius; x < patchPoint[0] + segmentRadius; ++x)
                            {
                                for (y = patchPoint[1] - segmentRadius; y < patchPoint[1] + segmentRadius; ++y)
                                {
                                    if (token.IsCancellationRequested) return;
                                    // For each surrounding pount,
                                    // get the minimum distance to any of the patch points.

                                    // Skip if already calculated
                                    if (x > (TileWidth - 1) || y > (TileWidth - 1) || x < 0 || y < 0 || worleySubLayer[x, y] > 0) continue;

                                    var minDistance = maxDistance;

                                    foreach (var patchPoint2 in patchPoints)
                                    {
                                        // Calculate the squared distance of (x,y) to each patch point
                                        var dx = x - patchPoint2[0];
                                        dx *= dx;
                                        var dy = y - patchPoint2[1];
                                        dy *= dy;
                                        minDistance = Math.Min(minDistance, dx + dy);
                                    }

                                    var distance = Math.Sqrt(minDistance);
                                    if (distance >= segmentRadius) continue;

                                    var pt = GetNormalizedSphereCoordinates(face.Key, x, y);
                                    var value = worley.GetValue3D(pt.X * sphereRadius, pt.Y * sphereRadius, pt.Z * sphereRadius);

                                    if (double.IsNaN(worleyMax) || worleyMax < value)
                                        worleyMax = value;
                                    if (double.IsNaN(worleyMin) || worleyMin > value)
                                        worleyMin = value;
                                    if (distance > (segmentRadius * worleyBlendStart))
                                    {
                                        // Apply linear blend at the edge of the area
                                        worleyBlendLayer[x, y] = 1 - (
                                            (distance - segmentRadius * worleyBlendStart) /
                                            (segmentRadius - segmentRadius * worleyBlendStart));
                                    }
                                    else worleyBlendLayer[x, y] = 1;
                                    worleySubLayer[x, y] = value;
                                }
                            }
                        }
                        // Invert and normalize the worley pattern
                        var offset = -1 * worleyMin;
                        var stretch = Math.Abs(worleyMax - worleyMin);
                        for (x = 0; x < TileWidth; ++x)
                        {
                            for (y = 0; y < TileWidth; ++y)
                            {
                                var value = worleySubLayer[x, y];
                                if (value == 0) continue;
                                // Normalize
                                value += offset;
                                value /= stretch;
                                // Invert
                                value = 1 - value; // TODO: Randomly decide if a formation gets inverted?
                                                   // Flatten peaks
                                                   // Limits values from 0.0 to 0.52
                                value = -2.56410256 * value * value * value * value + 4.03651904 * value * value * value - 1.05011655 * value * value + 0.10637141 * value;
                                // Limit and blend
                                worleySubLayer[x, y] = value * worleyLayerMaxHeight * worleyBlendLayer[x, y];
                            }
                        }
                        if (token.IsCancellationRequested) return;
                        image.ProcessPixelRows(rows =>
                        {
                            for (int y = 0; y < rows.Height; y++)
                            {
                                Span<Rgb24> pixelRow = rows.GetRowSpan(y);
                                for (int x = 0; x < pixelRow.Length; x++)
                                {
                                    face.Value[x, y] += worleySubLayer[x, y];
                                    if (worleySubLayer[x, y] > 0)
                                        image[x, y] = new Rgb24(76, 0, 0);
                                }
                            }
                        });
                    }
                    // Save worley areas in file so modder can define them as their own climate zone
                    image.SaveAsPng(worleyFreckleFile);
                    image.Dispose();
                });
            }
            catch { return; }

            OnProgress(90);
            Normalize(faces.Values.ToList(), token);
            OnProgress(99);
            SaveFaces(faces, token);
            OnProgress(100);
        }

        public void FlattenHistogram(int minStretch, int maxStretch, CancellationToken token)
        {
            // Strech using a SINUS curve.
            var faces = LoadHeightMaps(token);
            OnProgress(1);

            int cStart = 0;
            int cEnd = TileWidth;
            if (LimitedDebugMode)
            {
                cStart = TileWidth / 4;
                cEnd = cStart * 2;
            }

            try
            {
                Parallel.ForEach(faces, face =>
                {
                    if (token.IsCancellationRequested) return;
                    Parallel.For(cStart, cEnd, x =>
                    {
                        for (int y = cStart; y < cEnd; ++y)
                        {
                            if (token.IsCancellationRequested) return;
                            var val = face.Value[x, y];
                            // +-0.2 aligns the Sinus so that the gradient is 1 at value 0.5.
                            var valStretched = (1 - .2 * 2) * 0.5 * Math.Sin(Math.PI * (val - 0.5)) + 0.5;
                            // Apply the percentage
                            // 100% means that 0s are shifted tp 0.2 and 1s are lowered to .8.
                            // The value range will be sequeezed to 0.2 <-> 0.8
                            // Lower percentage shifts the values more towards their original.

                            // We have the original value "v".
                            // We have the stretched valie "s".
                            // We define a factor "f", so that our target value "t" equals:
                            // t = (v+s*f)/(1+f)
                            // If f==1 we get the average between original and stretched.
                            // f==1 would be percentage p=50%.
                            // Problem: Our intput is not "f" but the percentage "p".
                            // This required a complicated Excel spreadsheet with multiple segments:
                            // It looks like some exponential function at lower percentages but flattens out hard at higher percentages
                            Func<double, double> getFactor = (p) =>
                            {
                                if (p >= 50)
                                {
                                    return 5.38458104E-08 * p * p * p * p - 1.98322019E-05 * p * p * p + 2.87748371E-03 * p * p - 2.05511035E-01 * p + 6.22399659E+00;
                                }
                                else if (p >= 16.66667)
                                {
                                    return 4.40567976E-06 * p * p * p * p - 7.01736952E-04 * p * p * p + 4.31525352E-02 * p * p - 1.27854469E+00 * p + 1.72278030E+01;
                                }
                                else if (p >= 1)
                                {
                                    return 1.01896976E+02 * Math.Pow(p, -1.06095389E+00);
                                }
                                return 100;
                            };
                            if (val < .5)
                            {
                                if (minStretch == 100)
                                    val = valStretched;
                                else
                                {
                                    var factor = getFactor(minStretch);
                                    val = (val * factor + valStretched) / (1 + factor);
                                }
                            }
                            else
                            {
                                if (maxStretch == 100)
                                    val = valStretched;
                                else
                                {
                                    var factor = getFactor(maxStretch);
                                    val = (val * factor + valStretched) / (1 + factor);
                                }
                            }
                            face.Value[x, y] = val;
                        }
                    });
                });
            }
            catch { return; }
            OnProgress(90);
            SaveFaces(faces, token);
            OnProgress(100);
        }

        public void Invert(CancellationToken token)
        {
            var faces = LoadHeightMaps(token);
            OnProgress(1);
            Parallel.ForEach(faces, face =>
            {
                if (token.IsCancellationRequested) return;
                Parallel.For(0, TileWidth, x =>
                {
                    if (token.IsCancellationRequested) return;
                    for (int y = 0; y < TileWidth; ++y)
                    {
                        face.Value[x, y] = 1 - face.Value[x, y];
                    }
                });
            });
            OnProgress(90);
            SaveFaces(faces, token);
            OnProgress(100);
        }
        public void ExponentialStretch(double stretchAmount, int equatorWidthPercentage, CancellationToken token)
        {
            var faces = LoadHeightMaps(token);
            OnProgress(1);
            try
            {
                Parallel.ForEach(faces, face =>
                {
                    if (token.IsCancellationRequested) return;
                    Parallel.For(0, TileWidth, x =>
                    {
                        if (token.IsCancellationRequested) return;
                        for (int y = 0; y < TileWidth; ++y)
                        {
                            var val = face.Value[x, y];

                            double stretch;
                            double equatorWidth = TileWidth * equatorWidthPercentage / 100d;
                            if (equatorWidthPercentage > 1)
                            {
                                // Let's generate a Gauss curve that peaks at 2.5 around the equator
                                // and goes back to around 1.0 to allign with pole tiles.
                                double yd = y;
                                if (face.Key == CubeMapFace.Up || face.Key == CubeMapFace.Down)
                                {
                                    yd = 0; // Apply same value as on equator tile edges for seamless transition.
                                }
                                stretch = stretchAmount * Math.Exp(-0.5 * Math.Pow((yd - (TileWidth - 1) / 2) / equatorWidth, 2)) + 1;
                            }
                            else
                            {
                                // All that complicated stuff inside Math.Exp() above becomes 1 when we are at the equator
                                stretch = stretchAmount + 1;
                            }
                            val = Math.Pow(val, stretch);
                            if (val < 0) val = 0;
                            if (val > 1) val = 1;
                            face.Value[x, y] = val;
                        }
                    });
                });
            }
            catch { return; }
            OnProgress(90);
            SaveFaces(faces, token);
            OnProgress(100);
        }

        public void StrechHistogram(double min, double max, CancellationToken token)
        {
            var faces = LoadHeightMaps(token);
            OnProgress(1);
            Normalize(faces.Values.ToList(), token, min, max);
            OnProgress(90);
            SaveFaces(faces, token);
            OnProgress(100);
        }

        // TODO: handle preview mode and limited preview mode
        public void GenerateSedimentLayers(int seed, int sedimentTypeCount, int sedimentPlateCount, CancellationToken token)
        {
            OnProgress(0);
            // Sediment Layer System
            // Sediment layers can have heights in the range
            // of a few centimeters up to tens of meters or more
            // Our height map covers 65535 layers at most.
            // If we pre-generate rock layer infos that would amount
            // to 274,873,712,640 layer infos per tile.
            // If we only used a ushort value for defining the hardnes of the layer,
            // we would have to store 512GB of data per tile !!!
            // We can either reduce the data or procedurally generate the layer infos.
            // If we split each tile into 64 polygons, each containing the same layers,
            // we need only 8MB of data. One area would be 256x256 pixels on average.

            // Store the index of a sediment layer definition
            // There are 64 different layer definitions this index refers to.
            Dictionary<CubeMapFace, byte[,]> sedimentLayerIndexes = new();
            List<ushort[]> sedimentLayers = new();
            Random rnd = new Random(seed);
            for (int i = 0; i < sedimentTypeCount; ++i)
            {
                if (i == 0) // Lets make index 0 special by beeing the default value
                {
                    sedimentLayers.Add(Enumerable.Repeat<ushort>(32768, 65536).ToArray());
                    continue;
                }
                ushort[] layers = SedimentGenerator.GenerateSedimentLayers(rnd, 65536, 0.002, 0.9, 2000, 0.01);
                sedimentLayers.Add(layers);
            }

            if (token.IsCancellationRequested) return;
            OnProgress(1);

            // Initialize sedimentLayerIndexes.
            int unsetCount = 0;
            var faces = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToList();
            if (DebugMode) faces = new List<CubeMapFace> { PreviewFace };
            foreach(var face in  faces)
            {
                sedimentLayerIndexes[face] = new byte[TileWidth, TileWidth];
                for (ushort x = 0; x < TileWidth; ++x)
                    for (ushort y = 0; y < TileWidth; ++y)
                    {
                        ++unsetCount;
                        sedimentLayerIndexes[face][x, y] = 0;
                    }
            };

            if (token.IsCancellationRequested) return;
            OnProgress(2);

            // Place sedimentPlateCount random seeds
            HashSet<(CubeMapFace, int, int)> placedSeeds = new();
            int cnt = 0;

            var faceValues = sedimentLayerIndexes.Keys.ToList();

            while (placedSeeds.Count < sedimentPlateCount)
            {
                (CubeMapFace, int, int) tuple = new()
                {
                    Item1 = faceValues[faces.Count == 1 ? 0 : (int)rnd.NextInt64(0, faces.Count)],
                    Item2 = (int)rnd.NextInt64(0, TileWidth),
                    Item3 = (int)rnd.NextInt64(0, TileWidth)
                };
                if (placedSeeds.Contains(tuple)) continue;
                placedSeeds.Add(tuple);
                ++cnt;
            }
            foreach (var tuple in placedSeeds)
            {
                var seedValue = (byte)(rnd.NextInt64(sedimentTypeCount - 1) + 1); // Don't use 0
                sedimentLayerIndexes[tuple.Item1][tuple.Item2, tuple.Item3] = seedValue;
            }
            unsetCount -= sedimentPlateCount;

            if (token.IsCancellationRequested) return;
            OnProgress(3);

            // Let the seeds randomly grow
            var initialUnsetCount = unsetCount;
            int concurrency = 256;

            long iteration = 0;
            int lastProgress = 3;
            while (unsetCount > 0 && placedSeeds.Count > 0)
            {
                // Don't screw ourselves with parallel stuff when nearly no pixels are left
                if (unsetCount * 4 < concurrency)
                    concurrency = 1;

                HashSet<int> nextIndexes = new();
                int deadLockDetect = 0;
                while (nextIndexes.Count < concurrency && nextIndexes.Count < placedSeeds.Count)
                {
                    var index = (int)rnd.NextInt64(placedSeeds.Count);
                    if (!nextIndexes.Add(index) && ++deadLockDetect > 10000)
                    {
                        MessageBox.Show("Random Number Generator is deadlocked! Please try again with another seed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                var indexes = nextIndexes.ToArray();
                ConcurrentBag<(CubeMapFace, int, int)> pointsToRemove = new();
                ConcurrentBag<(CubeMapFace, int, int, byte)> pointsToSet = new();
                var list = placedSeeds.ToList();
                try
                {
                    Parallel.For(0, indexes.Length, i =>
                    {
                        if (token.IsCancellationRequested) return;
                        // Pick random item from HashSet
                        var index = indexes[i];

                        var tuple = list[index];
                        // This code avoids converting the hashset into a list.
                        // But is it waaaaaay slower
                        //int currentIndex = 0;
                        //(CubeMapFace, int, int) tuple = default;
                        //foreach (var element in placedSeeds)
                        //{
                        //    if (currentIndex == index)
                        //    {
                        //        tuple = element;
                        //        break;
                        //    }
                        //    ++currentIndex;
                        //}

                        var point = new CubeMapPointLight(tuple.Item1, tuple.Item2, tuple.Item3);

                        var neighbors = point.GetNeighbors();
                        if (neighbors.Count == 0)
                        {
                            pointsToRemove.Add(tuple);
                            return;
                        }
                        neighbors.RemoveAll(neighbor => DebugMode && neighbor.Face != PreviewFace || sedimentLayerIndexes[neighbor.Face][neighbor.X, neighbor.Y] != 0);
                        if (neighbors.Count == 0)
                        {
                            pointsToRemove.Add(tuple);
                            return;
                        }
                        CubeMapPointLight neighbor;
                        if (neighbors.Count == 1)
                        {
                            neighbor = neighbors[0];
                            pointsToRemove.Add(tuple);
                        }
                        else
                        {
                            lock (rnd) // Not locking caused rnd instance to get corrupted. It only returned 0s after a while.
                            {
                                neighbor = neighbors[(int)rnd.NextInt64(neighbors.Count)];
                            }
                        }
                        pointsToSet.Add((
                            neighbor.Face,
                            neighbor.X,
                            neighbor.Y,
                            sedimentLayerIndexes[point.Face][point.X, point.Y]));
                    });
                }
                catch { return; }

                var pointsToSetList = pointsToSet.ToList();
                var pointsToRemoveList = pointsToRemove.ToList();

                foreach (var point in pointsToSetList)
                {
                    if (token.IsCancellationRequested) return;
                    sedimentLayerIndexes[point.Item1][point.Item2, point.Item3] = point.Item4;
                    placedSeeds.Add((point.Item1, point.Item2, point.Item3));
                    --unsetCount;
                }
                foreach (var point in pointsToRemoveList)
                {
                    if (token.IsCancellationRequested) return;
                    placedSeeds.Remove(point);
                }

                if (token.IsCancellationRequested) return;
                if (++iteration % 1000 == 0)
                {
                    int progress = (int)(.5 + 3 + 92 * (1 - unsetCount * 1.0 / initialUnsetCount));
                    if (progress != lastProgress)
                    {
                        OnProgress((int)progress);
                        lastProgress = progress;
                    }
                }
            }

            if (token.IsCancellationRequested) return;
            OnProgress(95);
            try
            {
                Parallel.ForEach(faces, face =>
                {
                    var image = new Image<Rgb24>(TileWidth, TileWidth);
                    Parallel.For(0, TileWidth, POptions(token), x =>
                    {
                        if (token.IsCancellationRequested) return;
                        for (int y = 0; y < TileWidth; ++y)
                        {
                            var value = (byte)sedimentLayerIndexes[face][x, y];
                            image[x, y] = new Rgb24(value, value, value); // TODO Slow AF, use ProcessPixelRows method!
                        }
                    });
                    image.SaveAsPng(Path.Combine(WorkingDirectory, (face + "_sediments.png").ToLower()));
                    image.Dispose();
                });
            }
            catch { return; }

            OnProgress(98);

            using (var fs = new FileStream(Path.Combine(WorkingDirectory, "sediments.txt"), FileMode.Create))
            using (var sw = new StreamWriter(fs))
            {
                sw.WriteLine("!!! DO NOT EDIT THIS FILE !!!");
                foreach (var layer in sedimentLayers)
                {
                    foreach(var num in layer)
                    {
                        sw.Write(num.ToString(CultureInfo.InvariantCulture) + ";");
                    }
                    sw.WriteLine();
                }
                sw.Flush();
                fs.Flush();
            }

            OnProgress(100);
        }

        HydraulicErosion _hyd;
        bool _enableErosionDebugOverlay;
        public void InitErosion(bool useSediments, CancellationToken token, bool enableErosionDebugOverlay = false)
        {
            _enableErosionDebugOverlay = enableErosionDebugOverlay;
            var faces = LoadHeightMaps(token);
            Dictionary<CubeMapFace, byte[,]> sedimentLayerIndexes = null;
            List<ushort[]> sedimentLayers = null;

            if (useSediments)
            {
                sedimentLayerIndexes = new();
                try
                {
                    foreach (var face in faces)
                    {
                        sedimentLayerIndexes[face.Key] = new byte[TileWidth, TileWidth];
                    }
                    Parallel.ForEach(faces, face =>
                    {
                        var fileName = Path.Combine(WorkingDirectory, (face.Key + "_sediments.png").ToLower());
                        if (File.Exists(fileName))
                        {
                            var myArray = sedimentLayerIndexes[face.Key];
                            var image = Image.Load(fileName) as Image<Rgb24>;
                            if (image != null)
                            {
                                Parallel.For(0, TileWidth, POptions(token), x =>
                                {
                                    for (int y = 0; y < TileWidth; ++y)
                                    {
                                        var value = image[x, y].R; // TODO Slow AF, use ProcessPixelRows method!
                                        myArray[x, y] = value;                                    ;
                                    }
                                });
                                image.Dispose();
                            }
                        }
                    });

                    sedimentLayers = new();
                    using (var fs = new FileStream(Path.Combine(WorkingDirectory, "sediments.txt"), FileMode.Open))
                    using (var sw = new StreamReader(fs))
                    {
                        string line = sw.ReadLine(); // "!!! DO NOT EDIT THIS FILE !!!"
                        while ((line = sw.ReadLine()) != null)
                        {
                            var spl = line.Split(';');
                            var array = new ushort[65536];
                            if (spl.Length < 65536)
                            {
                                useSediments = false;
                                break;
                            }
                            ushort min = 65535;
                            ushort max = 0;
                            for (int i = 0; i < array.Length; ++i)
                            {
                                array[i] = ushort.Parse(spl[i]);
                                min = array[i] < min ? array[i] : min;
                                max = array[i] > max ? array[i] : max;
                            }
                            if (min < max)
                            {
                                // Scale layers
                                for (int i = 0; i < array.Length; ++i)
                                {
                                    array[i] = (ushort)(((double)(array[i] - min) / (max - min)) * 65535.0);
                                }
                            }
                            sedimentLayers.Add(array);
                        }
                    }
                }
                catch
                {
                    useSediments = false;
                }
            }

            if (!useSediments)
            {
                sedimentLayerIndexes = null;
                sedimentLayers = null;
            }

            _hyd = new HydraulicErosion(
            new Random() /* keep seed random */ , faces, DebugMode, LimitedDebugMode, PreviewFace,
            sedimentLayerIndexes, sedimentLayers,enableErosionDebugOverlay);
        }
        public void Erode(CancellationToken token)
        {
            _hyd.Erode(token);
        }
        public void FinishErode(CancellationToken token)
        {
            if (!DebugMode)
                EdgeFixer.MakeSeamless(_hyd.Faces, token);
            SaveFaces(_hyd.Faces, token);

            if (!_enableErosionDebugOverlay || _hyd.DebugOverlayRed == null) return;

            // Normalize debug overlays
            Normalize(_hyd.DebugOverlayRed.Values.ToList(), token, 0, 255);
            Normalize(_hyd.DebugOverlayGreen.Values.ToList(), token, 0, 255);
            Normalize(_hyd.DebugOverlayBlue.Values.ToList(), token, 0, 255);

            Dictionary<CubeMapFace, byte[,]> red = new();
            Dictionary<CubeMapFace, byte[,]> green = new();
            Dictionary<CubeMapFace, byte[,]> blue = new();

            foreach (var kv in _hyd.DebugOverlayRed)
            {
                red[kv.Key] = new byte[TileWidth, TileWidth];
                green[kv.Key] = new byte[TileWidth, TileWidth];
                blue[kv.Key] = new byte[TileWidth, TileWidth];
                Parallel.For(0, TileWidth, POptions(token), x =>
                {
                    for (int y = 0; y < TileWidth; ++y)
                    {
                        red[kv.Key][x, y] = (byte)kv.Value[x, y];
                        green[kv.Key][x, y] = (byte)_hyd.DebugOverlayGreen[kv.Key][x, y];
                        blue[kv.Key][x, y] = (byte)_hyd.DebugOverlayBlue[kv.Key][x, y];
                    }
                });
            }

            SaveOverlays(red, green, blue);
        }

        public IDebugOverlay DebugOverlay;

        void CheckFillLake(CubeMapPoint point, double stopHeight, int maxFilledPixels, Dictionary<CubeMapFace, double[,]> faces,CancellationToken token, out int filledPixelCount, out HashSet<CubeMapPointLight> filledPoints)
        {
            filledPixelCount = 0;
            filledPoints = new();
            if (point.Value >= stopHeight) return;
            if (token.IsCancellationRequested) return;
            var pointLight = new CubeMapPointLight { Face = point.Face, X = point.PosX, Y = point.PosY, TileWidth = (ushort)TileWidth };
            Queue<CubeMapPointLight> queue = new Queue<CubeMapPointLight>();
            // Use this to prevent thousands of duplicates beeing added to the queue
            HashSet<CubeMapPointLight> queuedPoints = new();

            queue.Enqueue(pointLight);
            while (queue.Count > 0)
            {
                if (token.IsCancellationRequested) return;
                var pt = queue.Dequeue();
                if (DebugMode)
                {
                    if (pt.Face != PreviewFace ||
                        (pt.X < 1 || pt.X > TileWidth - 2 ||
                         pt.Y < 1 || pt.Y > TileWidth - 2))
                        continue;
                }
                if (!filledPoints.Contains(pt))
                {
                    if (DebugMode && pt.Face != PreviewFace) Debugger.Break();

                    ++filledPixelCount;
                    filledPoints.Add(pt);
                    // Volume of one pixel is pillar heigt of of water between height map pixel and stop height
                    //filledVolume += (stopHeight - CubeMapPointLight.GetValue(pt, faces));
                    if (filledPixelCount > maxFilledPixels) return;
                }

                var np = CubeMapPointLight.GetPointRelativeTo(pt,-1, 0);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
                np = CubeMapPointLight.GetPointRelativeTo(pt, 0, -1);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
                np = CubeMapPointLight.GetPointRelativeTo(pt, 1, 0);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
                np = CubeMapPointLight.GetPointRelativeTo(pt, 0, 1);
                if (!queuedPoints.Contains(np) && !filledPoints.Contains(np) && CubeMapPointLight.GetValue(np, faces) < stopHeight)
                {
                    queue.Enqueue(np);
                    queuedPoints.Add(np);
                }
            }
        }

        Dictionary<CubeMapFace, Image<Rgb24>> LoadMaterialMaps(List<CubeMapFace> faceValues, string materialSource, bool addMode)
        {
            var lakes = new Dictionary<CubeMapFace, Image<Rgb24>>();
            try
            {
                Parallel.ForEach(faceValues, face =>
                {
                    Image<Rgb24> image = null;
                    if (materialSource != null)
                    {
                        var path = Path.Combine(materialSource, face.ToString().ToLower() + "_mat.png");
                        if (File.Exists(path))
                        {
                            try
                            {
                                var img = Image.Load(path);
                                image = img as Image<Rgb24>;
                                if (image == null)
                                {
                                    image = img.CloneAs<Rgb24>();
                                    img.Dispose();
                                }
                                if (image.Width != TileWidth)
                                {
                                    image.Mutate(k => k.Resize(TileWidth, TileWidth, KnownResamplers.NearestNeighbor));
                                }
                                if (!addMode)
                                {
                                    // Delete red channel
                                    image.ProcessPixelRows(rows =>
                                    {
                                        for (int y = 0; y < rows.Height; y++)
                                        {
                                            Span<Rgb24> pixelRow = rows.GetRowSpan(y);
                                            for (int x = 0; x < pixelRow.Length; x++)
                                            {
                                                pixelRow[x].R = 0;
                                            }
                                        }
                                    });
                                }
                            }
                            catch { return; }
                        }
                    }
                    lock (lakes)
                        lakes[face] = image ?? new Image<Rgb24>(TileWidth, TileWidth);
                });
            }
            catch { }
            return lakes;
        }

        // Currently only used for lakes
        void SaveMaterialMaps(Dictionary<CubeMapFace, Image<Rgb24>> maps, byte redOverlayValue)
        {
            foreach (var kv in maps)
            {
                kv.Value.SaveAsPng(Path.Combine(WorkingDirectory, (kv.Key + "_mat.png").ToLower()));
                Image<Rgba32> overlayImage;
                overlayImage = kv.Value.CloneAs<Rgba32>();
                overlayImage.ProcessPixelRows(rows =>
                {
                    for (int y = 0; y < rows.Height; y++)
                    {
                        Span<Rgba32> pixelRow = rows.GetRowSpan(y);
                        for (int x = 0; x < pixelRow.Length; x++)
                        {
                            // Overlay only contains lake values
                            if (pixelRow[x].R != redOverlayValue)
                            {
                                pixelRow[x].R = 0;
                                pixelRow[x].A = 0;
                            }
                            pixelRow[x].G = 0;
                            pixelRow[x].B = 0;
                        }
                    }
                });
                overlayImage.SaveAsPng(Path.Combine(WorkingDirectory, (kv.Key + "_overlay.png").ToLower()), new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
            }
        }

        void SaveOverlays(Dictionary<CubeMapFace, byte[,]> red = null, Dictionary<CubeMapFace, byte[,]> green = null, Dictionary<CubeMapFace, byte[,]> blue = null, bool keepExistingIfNull = true, byte alpha = 150)
        {
            HashSet<CubeMapFace> faces = new();
            if (red != null) faces.UnionWith(red.Keys);
            if (green != null) faces.UnionWith(green.Keys);
            if (blue != null) faces.UnionWith(blue.Keys);

            try
            {
                foreach (var face in faces)
                {
                    var fileName = Path.Combine(WorkingDirectory, (face + "_overlay.png").ToLower());
                    Image<Rgba32> image;
                    if (keepExistingIfNull && (red == null || green == null || blue == null) && File.Exists(fileName))
                    {
                        var img = Image.Load(fileName);
                        if (img is Image<Rgba32> img2)
                            image = img2;
                        else
                            image = img.CloneAs<Rgba32>();
                    }
                    else
                        image = new Image<Rgba32>(TileWidth, TileWidth);

                    byte[,] redChannel = red?[face];
                    byte[,] greenChannel = green?[face];
                    byte[,] blueChannel = blue?[face];

                    image.ProcessPixelRows(rows =>
                    {
                        for (int y = 0; y < rows.Height; y++)
                        {
                            Span<Rgba32> pixelRow = rows.GetRowSpan(y);
                            for (int x = 0; x < pixelRow.Length; x++)
                            {
                                bool transparent = true;
                                if (redChannel != null)
                                {
                                    pixelRow[x].R = redChannel[x, y];
                                    if (redChannel[x, y] != 0) transparent = false;
                                }
                                if (greenChannel != null)
                                {
                                    pixelRow[x].G = greenChannel[x, y];
                                    if (greenChannel[x, y] != 0) transparent = false;
                                }
                                if (blueChannel != null)
                                {
                                    pixelRow[x].B = blueChannel[x, y];
                                    if (blueChannel[x, y] != 0) transparent = false;
                                }
                                pixelRow[x].A = transparent ? (byte)0 : alpha;
                            }
                        }
                    });
                    image.SaveAsPng(Path.Combine(WorkingDirectory, (face + "_overlay.png").ToLower()), new PngEncoder { ColorType = PngColorType.RgbWithAlpha });
                }
            }
            catch { return; }
        }

        string AskForMaterialSource(string materialSource)
        {
            var matSourceExists = Directory.Exists(materialSource);
            // Check if local files are present
            var files = Directory.GetFiles(WorkingDirectory, "*_mat.png");
            if (files.Length > 0)
            {
                if (!matSourceExists)
                    return WorkingDirectory;
                // Mat source exists
                var res = MessageBox.Show("Use material maps from working directory or the material source you specified?\r\nPress 'Yes' for working directory or 'No' for material source.", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                    return WorkingDirectory;
                else
                    return materialSource;
            }
            // No local files
            if (!Directory.Exists(materialSource))
            {
                materialSource = null;
                var dlgRes = MessageBox.Show("You did not specifiy a valid directory as a material map source.\r\n" +
                    "The generated lakes can automatically be added to an existing material map.\r\n" +
                    "If you don't specify a directory, the output files will have black blue and green channels.\r\n" +
                    "The output files will be written to the current working directory. If the source files are somewhere else, they will not be overriden.\r\n\r\nSpecify a source folder now?",
                    "No Material Map Source specified", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (dlgRes == MessageBoxResult.Yes)
                {
                    var dlg = new OpenFileDialog() { Filter = "PNG Files|*.png", FileName = "back_mat.png" };
                    if (dlg.ShowDialog() == true)
                    {
                        materialSource = Path.GetDirectoryName(dlg.FileName);
                    }
                }
            }
            return WorkingDirectory;
        }

        public void AddLakes(ushort lakesPerTile, double lakeAreaMultiplier, double lakeStampDepth, string materialSource, byte redValue, bool addMode, CancellationToken token)
        {
            var faces = LoadHeightMaps(token);

            OnProgress(1);

            materialSource = AskForMaterialSource(materialSource);

            // Strategy: Drop water on tiles and see where they end up. No inertia.
            int dropletsPerTile = (TileWidth/4)*(TileWidth/4); // Every 4th pixel
            int totalTilePixels = TileWidth * TileWidth;
            int pixelGap = totalTilePixels / dropletsPerTile;
            int pixelsPerRow = TileWidth / pixelGap;
            int lostPixels = TileWidth - (pixelsPerRow * pixelGap);
            int offset = lostPixels / 2;

            // Used for simulated end points of water
            Dictionary<CubeMapFace, double[,]> waterSpots = new Dictionary<CubeMapFace, double[,]>();
            // Used for sum of water
            Dictionary<CubeMapFace, double[,]> waterSpots2 = new Dictionary<CubeMapFace, double[,]>();
            var faceValues = faces.Keys.ToList();
            foreach (var face in faceValues)
            {
                waterSpots[face] = new double[TileWidth, TileWidth];
                waterSpots2[face] = new double[TileWidth, TileWidth];
            }

            int cStart = 0;
            int cEnd = TileWidth;
            if (LimitedDebugMode)
            {
                cStart = TileWidth / 4;
                cEnd = cStart * 2;
            }

            try
            {
                Parallel.For(0, faceValues.Count, POptions(token), faceIndex =>
                {
                    if (token.IsCancellationRequested) return;
                    var face = faceValues[faceIndex];
                    Parallel.For(0, 1 + TileWidth / pixelGap, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, xred =>
                    {
                        if (token.IsCancellationRequested) return;
                        for (int yred = 0; yred < 1 + TileWidth / pixelGap; ++yred)
                        {
                            int x = offset + xred * pixelGap;
                            int y = offset + yred * pixelGap;
                            if (x >= TileWidth || y >= TileWidth) continue;

                            if (x < cStart || x > cEnd) continue;
                            if (y < cStart || y > cEnd) continue;

                            var currentFace = face;
                            PointD pos = new PointD { X = x, Y = y };
                            PointD posOld = pos;
                            CubeMapPoint oldPoint = null;
                            HeightAndGradient heightAndGradient = HydraulicErosion.CalculateHeightAndGradient(pos, currentFace, faces);
                            int iteration = 0;
                            bool floored = false;
                            while (iteration++ < TileWidth / 2 && !token.IsCancellationRequested) // Limit movement to half map
                            {
                                var dir = -heightAndGradient.Gradient;
                                dir = dir.Normalize();
                                PointD cellOffset = pos.IntegerOffset();

                                HeightAndGradient lastHeight = heightAndGradient;
                                posOld = pos;
                                var posOldI = posOld.ToIntegerPoint();
                                oldPoint = new CubeMapPoint(faces, posOldI.X, posOldI.Y, currentFace) { VelocityX = dir.X, VelocityY = dir.Y, OffsetX = cellOffset.X, OffsetY = cellOffset.Y };

                                // Update position
                                pos += dir;

                                #region Out of bounds handling
                                var posI = pos.ToIntegerPoint();
                                if (posI.X >= TileWidth || posI.X < 0 || posI.Y >= TileWidth || posI.Y < 0)
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

                                heightAndGradient = HydraulicErosion.CalculateHeightAndGradient(pos, currentFace, faces);
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
                            if (DebugMode && oldPoint.Face != PreviewFace)
                            {
                                continue;
                            }
                            lock (waterSpots[oldPoint.Face])
                            {
                                waterSpots[oldPoint.Face][oldPoint.PosX, oldPoint.PosY] += 1;
                            }
                        }
                    });
                });
            }
            catch { return; }

            OnProgress(3);

            // Apply a folding matrix to determine maxima
            const int matrixSize = 9;
            for (int faceIndex = 0;  faceIndex < faceValues.Count; ++faceIndex)
            {
                if (token.IsCancellationRequested) return;
                var face = faceValues[faceIndex];
                try
                {
                    Parallel.For(cStart, cEnd, POptions(token), x =>
                    {
                        for (int y = cStart; y < cEnd; ++y)
                        {
                            if (token.IsCancellationRequested) return;
                            var point = new CubeMapPointLight { Face = face, X = x, Y = y, TileWidth = (ushort)TileWidth };
                            double sum = 0;
                            for (int x2 = x - matrixSize / 2; x2 <= x + matrixSize / 2; ++x2)
                            {
                                for (int y2 = y - matrixSize / 2; y2 <= y + matrixSize / 2; ++y2)
                                {
                                    var dp = CubeMapPointLight.GetPointRelativeTo(point, x2 - x, y2 - y);
                                    if (dp.Face == face)
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
                            //DebugOverlay.DebugCollectPixel(face, x, y, 128, 0, 255, 0);
                        }
                    });
                }
                catch { return; }
            }

            OnProgress(5);
            if (token.IsCancellationRequested) return;
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

            if (token.IsCancellationRequested) return;
            try
            {
                Parallel.For(0, faceValues.Count, POptions(token), faceIndex =>
                {
                    var face = faceValues[faceIndex];
                    var highscoreList = highscores[face];
                    var waterSpotList = waterSpots2[face];
                    double highScoreMin = 0;
                    int highScoreCount = 0;
                    //double highScoreMax = 0;
                    Parallel.For(cStart, cEnd, POptions(token), x =>
                    {
                        for (int y = cStart; y < cEnd; ++y)
                        {
                            var value = waterSpotList[x, y];
                            if (value == 0) continue;
                            if (value > highScoreMin || highScoreCount < (lakesPerTile + 10)) // Allow 10 backups so we can compensate skipping small lakes later
                            {
                                lock (highscoreList)
                                {
                                    if (value > highScoreMin || highScoreCount < (lakesPerTile + 10))
                                    {
                                        var score = new HighScore(value, x, y);
                                        var neighbors = highscoreList.Where(s => s.Position.IsWithinRadius(score.Position, safetyDistance)).ToList();
                                        if (neighbors.Any(s => s.Score > value)) continue;
                                        foreach (var neighbor in neighbors)
                                            highscoreList.Remove(neighbor);
                                        highscoreList.Add(score);

                                        highscoreList.Sort((a, b) => b.Score.CompareTo(a.Score));
                                        while (highscoreList.Count > lakesPerTile)
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
            }
            catch { return; }

            OnProgress(7);
            if (token.IsCancellationRequested) return;

            // Last step: Fill up the lakes
            // Warning! If lake depth is too high it will overflow and fill nearly the whole map
            // We need to determine the maximum sane size of a lake without overflowing
            Dictionary<CubeMapFace, Image<Rgb24>> lakes = LoadMaterialMaps(faceValues, materialSource, addMode);

            int totalProgressIncrements = 0;
            int processedIncrements = 0;
            for (int i = 0; i< faceValues.Count; ++i)
            {
                var face = faceValues[i];
                var highscoreList = highscores[face];
                totalProgressIncrements += highscoreList.Count;
            }

            try
            {
                Parallel.For(0, faceValues.Count, POptions(token), faceIndex =>
                {
                    var face = faceValues[faceIndex];
                    var highscoreList = highscores[face];
                    int drawnLakeCount = 0;

                    double averageScore = 0;
                    foreach (var score in highscoreList)
                    {
                        averageScore += score.Score;
                    }
                    averageScore /= highscoreList.Count;
                    // Average lake size is around 3000 pixels
                    // avg * avgFactor = 3000
                    var avgFactor = 3000 / averageScore;

                    int minFilledPixels = 50;
                    foreach (var score in highscoreList)
                    {
                        Interlocked.Increment(ref processedIncrements);
                        OnProgress(7 + (int)(processedIncrements * 98.0 / totalProgressIncrements));

                        if (drawnLakeCount >= lakesPerTile) break;
                        var maxFilledPixels = (int)(score.Score * avgFactor * lakeAreaMultiplier);
                        if (maxFilledPixels < 50) maxFilledPixels = 50;
                        if (maxFilledPixels > (int)(0.05 * TileWidth * TileWidth)) // 5% of tile max. 200.000 pixels on 2048x2048 maps.
                        {
                            maxFilledPixels = (int)(0.05 * TileWidth * TileWidth);
                        }

                        var point = new CubeMapPoint(faces, score.Position.X, score.Position.Y, face);
                        var height = point.Value;
                        var stopHeight = height + 1.0 / 65535;

                        double lastStopHeight = stopHeight;
                        CheckFillLake(point, stopHeight, maxFilledPixels, faces, token, out var lastFilledPixelCount, out var lastFilledPoints);
                        if (lastFilledPixelCount == 0)
                        {
                            Debug.WriteLine("Skipped lake at {0},{1}", point.PosX, point.PosY);
                            continue; // Not possible to fill stuff here
                        }
                        if (lastFilledPixelCount > maxFilledPixels)
                        {
                            Debug.WriteLine("Skipped lake at {0},{1}. Out of bounds.", point.PosX, point.PosY);
                            continue; // Probably non-fillable flat area
                        }

                        // Go with large increments of 5
                        while (true)
                        {
                            if (token.IsCancellationRequested) return;
                            CheckFillLake(point, stopHeight, maxFilledPixels, faces, token, out var filledPixelCount, out var filledPoints);

                            if (filledPixelCount > 0 && filledPixelCount < maxFilledPixels)
                            {
                                lastStopHeight = stopHeight;
                                lastFilledPixelCount = filledPixelCount;
                                lastFilledPoints = filledPoints;
                                stopHeight += 5.0 / 65535;
                                continue;
                            }
                            break;
                        }
                        // Restart from last good result but with 1 step increments
                        stopHeight = lastStopHeight + 1.0 / 65535;
                        while (true)
                        {
                            if (token.IsCancellationRequested) return;
                            CheckFillLake(point, stopHeight, maxFilledPixels, faces, token, out var filledPixelCount, out var filledPixels);
                            if (filledPixelCount > 0 && filledPixelCount < maxFilledPixels)
                            {
                                lastStopHeight = stopHeight;
                                lastFilledPixelCount = filledPixelCount;
                                lastFilledPoints = filledPixels;
                                stopHeight += 1.0 / 65535;
                                continue;
                            }
                            break;
                        }
                        // Apply the lake
                        if (lastFilledPixelCount >= minFilledPixels) // Don't draw mini lakes
                        {
                            lock (faces)
                            {
                                // Stamp out the lake bed for more visible transition
                                double stampedStopHeight = Math.Max(lastStopHeight - (lakeStampDepth / 65535d), 0);
                                foreach (var pt in lastFilledPoints)
                                {
                                    // Draw final lake pixels
                                    faces[pt.Face][pt.X, pt.Y] = stampedStopHeight;
                                    var val = lakes[pt.Face][pt.X, pt.Y];
                                    lakes[pt.Face][pt.X, pt.Y] = new Rgb24(redValue, val.G, val.B);
                                }
                                ++drawnLakeCount;
                            }
                        }
                    }
                }); // ParallelFor
            }
            catch { return; }
            OnProgress(99);
            // Stamp height maps
            SaveFaces(faces, token);
            // Write material maps
            SaveMaterialMaps(lakes, redValue);
            OnProgress(100);
        }

        Dictionary<CubeMapFace, double[,]> _preloadedHeightMaps = null;
        public void PreLoadHeightMaps(CancellationToken token)
        {
            _preloadedHeightMaps = LoadHeightMaps(token);
        }
        public void ClearPreLoadedHeightMaps() => _preloadedHeightMaps = null;
        public bool CheckAddSingleLake(
            CubeMapFace face, int x, int y, int maxFilledPixels, CancellationToken token, out HashSet<CubeMapPointLight> filledPixels, out double lakeHeight)
        {
            // Preload and keep, so we don't have to load them every time
            if (_preloadedHeightMaps == null)
                PreLoadHeightMaps(token);

            filledPixels = null;
            lakeHeight = -1;
            if (token.IsCancellationRequested) return false;

            if (token.IsCancellationRequested) return false;

            // Start dropping water onto the point and see how much the lake will will

            var point = new CubeMapPoint(_preloadedHeightMaps, x, y, face);
            var height = point.Value;
            lakeHeight = height + 1.0 / 65535;
            int filledPixelCount;

            CheckFillLake(
                point, lakeHeight, maxFilledPixels,
                _preloadedHeightMaps, token, out filledPixelCount, out filledPixels);

            return filledPixelCount < maxFilledPixels;
        }

        public void AddSingleLake(HashSet<CubeMapPointLight> filledPixels, double lakeHeight, double lakeStampDepth, string materialSource, byte redValue, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            var faces = LoadHeightMaps(token);

            materialSource = AskForMaterialSource(materialSource);

            // Split by face:
            Dictionary<CubeMapFace, List<CubeMapPointLight>> pixelsToDraw = new();
            foreach (var face in Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToList())
            {
                pixelsToDraw[face] = new();
            }
            if (token.IsCancellationRequested) return;
            foreach (var pixel in filledPixels)
            {
                pixelsToDraw[pixel.Face].Add(pixel);
            }
            if (token.IsCancellationRequested) return;
            double stampedStopHeight = Math.Max(lakeHeight - (lakeStampDepth / 65535d), 0);
            var kvs = pixelsToDraw.Where(o => o.Value.Count > 0 && faces.ContainsKey(o.Key)).ToList();

            var lakes = LoadMaterialMaps(kvs.Select(k => k.Key).ToList(), materialSource, true);

            if (token.IsCancellationRequested) return;
            try
            {
                Parallel.ForEach(kvs, kv =>
                {
                    var face = kv.Key;
                    var heightMap = faces[kv.Key];
                    foreach (var pixel in kv.Value)
                    {
                        heightMap[pixel.X, pixel.Y] = stampedStopHeight;
                        var materialMapPixel = lakes[face][pixel.X, pixel.Y];
                        lakes[face][pixel.X, pixel.Y] = new Rgb24(redValue, materialMapPixel.G, materialMapPixel.B);
                    }
                });
            }
            catch { return; }
            if (token.IsCancellationRequested) return;
            SaveFaces(faces, token);
            SaveMaterialMaps(lakes, redValue);
        }

        void Normalize(List<double[,]> images, CancellationToken token, double minHeight = 0, double maxHeight = 1.0, bool invert = false)
        {
            int cStart = 0;
            int cEnd = TileWidth;
            if (LimitedDebugMode)
            {
                cStart = TileWidth / 4;
                cEnd = cStart * 2;
            }

            double max = images[0][cStart, cStart];
            double min = images[0][cStart, cStart];
            try
            {
                Parallel.For(cStart, cEnd, POptions(token), x =>
                {
                    for (int y = cStart; y < cEnd; ++y)
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
                Parallel.For(cStart, cEnd, POptions(token), x =>
                {
                    for (int y = cStart; y < cEnd; ++y)
                    {
                        if (token.IsCancellationRequested) return;
                        foreach (var image in images)
                        {
                            var value = image[x, y];
                            // Normalize 0...1
                            value += offset;
                            value /= stretch;
                            // Stretch min...max
                            value *= (maxHeight - minHeight);
                            value += minHeight;
                            image[x, y] = invert ? (maxHeight - value) : value;
                        }
                    }
                    ;
                });
            }
            catch { return; }
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

        Point3D GetNormalizedSphereCoordinates(CubeMapFace face, int x, int y)
        {
            Point3D origin = new Point3D();
            double cubeWidth = TileWidth;

            // It makes sense if you draw a picture. See below
            double offset = (cubeWidth - 1) / 2.0;
            // w=5: [0][1][2][3][4] -> middle = 2,   [0] on 3d axis = -2
            //             | middle (0 in xyz)
            // w=4   [0][1]|[2][3]  -> middle = 1.5, [0] on 3d axis = -1.5

            // offset at 2048 -> 1023.5
            // "Problem": Edges between 2 tiles will have duplicate pixels
            // ==> Update: Duplicated pixels are expected by the game. NOT a problem
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

    // Was originally used to create sediment layers
    // The approach used now is much more realistic
    public class UShortNormalDistributedRandom
    {
        private readonly Random _random;
        // Target Values
        const ushort targetMin = 0;
        const ushort targetMax = 65535;
        const double targetMean = (targetMin + targetMax) / 2.0;

        public UShortNormalDistributedRandom(int seed)
        {
            _random = new Random(seed);
        }

        double NextNormalDistributed(double mean, double stdDev)
        {
            // Box-Muller-Algo with Standard-Random
            double u1 = 1.0 - _random.NextDouble(); // [0.0, 1.0) -> (0.0, 1.0]
            double u2 = 1.0 - _random.NextDouble();
            double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + stdDev * z0;
        }

        public ushort GetNextValue()
        {
            // Assume 0 and 65535 have a 5% chance of spawning
            double approximateStdDev = (targetMax - targetMin) / 4.0;

            int value;
            do
            {
                value = (int)Math.Round(NextNormalDistributed(targetMean, approximateStdDev));
            } while (value < targetMin || value > targetMax);

            return (ushort)value;
        }
    }

}

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetCreator
{
    static class EdgeFixer
    {
        public static int TileWidth = 2048;

        class PointWrapper
        {
            public Func<double> GetValue;
            public Action<double> SetValue;
            public PointWrapper(Func<double> getValue, Action<double> setValue)
            {
                GetValue = getValue;
                SetValue = setValue;
            }
        }

        static void ApplyPolyRegression(List<PointWrapper> edgePoints)
        {
            // The caveat about the cubemap edges: Adjacent pixels of two cubemaps must have the same value!
            // Our procedurally generated map does not have these duplicates.
            // In order to create duplicates we need to adjust the regression
            // Before we have these x coordinates:
            // 0 1 2 3 4 5 6 7 8 9
            // Coordinate #5 must be duplicated from 4. It is basically beeing skipped
            // Resulting x values:
            // 0 1 2 3 4 4 5 6 7 8
            //       * * * *  <- following for() skips these
            int nextIndex = 0;

            int version = 2;

            if (version == 1)
            {
                PointD[] points = new PointD[6];
                for (int i = 0; i < 10; ++i)
                {
                    if (i >= 3 && i <= 6) continue;
                    var x = i;
                    if (x > 6) --x; // <- Pixel skip for duplicating
                    points[nextIndex++] = new PointD { X = i, Y = edgePoints[i].GetValue() };
                }

                PolynomialRegression(points, out var regFunc);
                // 0 1 2 3 4 5 6 7 8 9
                //       | | | |        <- these are skipped and reconstructed
                edgePoints[3].SetValue(regFunc(3));
                edgePoints[4].SetValue(regFunc(4));
                edgePoints[5].SetValue(regFunc(4));
                edgePoints[6].SetValue(regFunc(5));

                // Blend calculated points with original points
                // Remember X Mapping
                // 0 1 2 3 4 5 6 7 8 9
                // 0 1 2 3 4 4 5 6 7 8
                // | | |         | | | <- these are beeing set here:
                double[] factors = new double[] { 1.0 / 6.0, 1.0 / 3.0, 2.0 / 3.0 };
                // 0;9 : 16%
                edgePoints[0].SetValue((regFunc(0) * factors[0]) + (edgePoints[0].GetValue() * (1 - factors[0])));
                edgePoints[9].SetValue((regFunc(8) * factors[0]) + (edgePoints[9].GetValue() * (1 - factors[0])));
                // 1;8 : 33%
                edgePoints[1].SetValue((regFunc(1) * factors[1]) + (edgePoints[1].GetValue() * (1 - factors[1])));
                edgePoints[8].SetValue((regFunc(7) * factors[1]) + (edgePoints[8].GetValue() * (1 - factors[1])));
                // 2;7 : 66%
                edgePoints[2].SetValue((regFunc(2) * factors[2]) + (edgePoints[2].GetValue() * (1 - factors[2])));
                edgePoints[7].SetValue((regFunc(6) * factors[2]) + (edgePoints[7].GetValue() * (1 - factors[2])));
            }
            else if (version == 2)
            {
                PointD[] points = new PointD[8];
                // Version 2: We only skip the edge points

                // Before we have these x coordinates:
                // 0 1 2 3 4 5 6 7 8 9
                // Resulting x values:
                // 0 1 2 3 4 4 5 6 7 8
                //         * *    <- following for() skips these
                for (int i = 0; i < 10; ++i)
                {
                    if (i == 4 || i == 5) continue;
                    var x = i;
                    if (x > 6) --x; // <- Pixel skip for duplicating
                    points[nextIndex++] = new PointD { X = i, Y = edgePoints[i].GetValue() };
                }

                PolynomialRegression(points, out var regFunc);
                // 0 1 2 3 4 5 6 7 8 9
                //         | |          <- these are skipped and reconstructed
                edgePoints[4].SetValue(regFunc(4));
                edgePoints[5].SetValue(regFunc(4));

                // Blend calculated points with original points
                // Remember X Mapping
                // 0 1 2 3 4 5 6 7 8 9
                // 0 1 2 3 4 4 5 6 7 8

                double[] factors = new double[] { 0.12, 0.25, 0.5, 0.8, 0.8 };
                // 0;9 : 10%
                edgePoints[0].SetValue((regFunc(0) * factors[0]) + (edgePoints[0].GetValue() * (1 - factors[0])));
                edgePoints[9].SetValue((regFunc(8) * factors[0]) + (edgePoints[9].GetValue() * (1 - factors[0])));
                // 1;8 : 30%
                edgePoints[1].SetValue((regFunc(1) * factors[1]) + (edgePoints[1].GetValue() * (1 - factors[1])));
                edgePoints[8].SetValue((regFunc(7) * factors[1]) + (edgePoints[8].GetValue() * (1 - factors[1])));
                // 2;7 : 60%
                edgePoints[2].SetValue((regFunc(2) * factors[2]) + (edgePoints[2].GetValue() * (1 - factors[2])));
                edgePoints[7].SetValue((regFunc(6) * factors[2]) + (edgePoints[7].GetValue() * (1 - factors[2])));
                // 3,6 : 80 %
                edgePoints[3].SetValue((regFunc(3) * factors[3]) + (edgePoints[3].GetValue() * (1 - factors[3])));
                edgePoints[6].SetValue((regFunc(5) * factors[3]) + (edgePoints[6].GetValue() * (1 - factors[3])));
                //// 4,5 : 80 %
                //double avg = (edgePoints[4].GetValue() + edgePoints[5].GetValue()) / 2;
                //edgePoints[4].SetValue((regFunc(4) * factors[4]) + (avg * (1 - factors[4])));
                //edgePoints[5].SetValue((regFunc(4) * factors[4]) + (avg * (1 - factors[4])));
            }

        }
        //static object LL = new object();

        // BAD! Creates sudden junps on steep terrain
        static void ApplyBlend(List<PointWrapper> edgePoints)
        {
            double[] copy = new double[10];
            for (int i = 0; i < 10; ++i)
                copy[i] = edgePoints[i].GetValue();

            for (int i = 2; i <= 7; ++i)
                edgePoints[i].SetValue(
                    (copy[i] * 0.5 +
                    copy[i-1] * 0.25 + copy[i+1] * 0.25 +
                    copy[i-2]*0.125 + copy[i+2]*0.125)/1.25);
        }

        static void ApplyEdgeFix(List<PointWrapper> edgePoints)
        {
            ApplyPolyRegression(edgePoints);
            //ApplyBlend(edgePoints);
        }

        public static void MakeSeemless(Dictionary<CubeMapFace, double[,]> faces, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            // Fix cubemap edges if there are sudden jumps
            List<Task> tasks = new List<Task>();

            // IMPORTANT: Edge pixels must always be duplicated. This means the cubemaps always overlay by one pixel

            // Strategy: polynomial regression:
            // Glitched terrain looks like this:
            //       /
            //      /
            //    --  <- this should not be here
            //   /
            //  /
            //x:123456
            // To fix this, we ignore n points at the edges
            // Take other points further away from edge and calculate polynomial regression,
            // then calculate ignore points from it

            // Front <-> Up
            // Down <-> Back
            // Direction: Bottom to top:
            CubeMapFace[][] verticals = new CubeMapFace[][]
            {
                new[] { CubeMapFace.Front, CubeMapFace.Up },
                new[] { CubeMapFace.Down, CubeMapFace.Back },
            };
            foreach (var pair in verticals)
            {
                var face1 = pair[0];
                var face2 = pair[1];
                tasks.Add(Task.Run(() =>
                {
                    for (int x = 0; x < TileWidth; ++x)
                    {
                        if (token.IsCancellationRequested) return;
                        List<PointWrapper> edgePoints = new List<PointWrapper>
                        {
                            new PointWrapper(()=>faces[face1][x, 4], d => faces[face1][x, 4] = d),
                            new PointWrapper(()=>faces[face1][x, 3], d => faces[face1][x, 3] = d),
                            new PointWrapper(()=>faces[face1][x, 2], d => faces[face1][x, 2] = d),
                            new PointWrapper(()=>faces[face1][x, 1], d => faces[face1][x, 1] = d),
                            new PointWrapper(()=>faces[face1][x, 0], d => faces[face1][x, 0] = d),           // 4
                            new PointWrapper(()=>faces[face2][x, 2047], d => faces[face2][x, 2047] = d),     // 5
                            new PointWrapper(()=>faces[face2][x, 2046], d => faces[face2][x, 2046] = d),
                            new PointWrapper(()=>faces[face2][x, 2045], d => faces[face2][x, 2045] = d),
                            new PointWrapper(()=>faces[face2][x, 2044], d => faces[face2][x, 2044] = d),
                            new PointWrapper(()=>faces[face2][x, 2043], d => faces[face2][x, 2043] = d),
                        };
                        //if (x == 1880) Debugger.Break();
                        ApplyEdgeFix(edgePoints);
                    }
                }));
            }

            //Task.WhenAll(tasks).GetAwaiter().GetResult();
            //return;

            // Equator:
            CubeMapFace[][] equator = new CubeMapFace[][]
            {
                new[] { CubeMapFace.Front, CubeMapFace.Right },
                new[] { CubeMapFace.Right, CubeMapFace.Back },
                new[] { CubeMapFace.Back, CubeMapFace.Left },
                new[] { CubeMapFace.Left, CubeMapFace.Front },
            };
            foreach (var pair in equator)
            {
                var face1 = pair[0];
                var face2 = pair[1];
                tasks.Add(Task.Run(() =>
                {
                    for (int y = 0; y < TileWidth; ++y)
                    {
                        if (token.IsCancellationRequested) return;
                        List<PointWrapper> edgePoints = new List<PointWrapper>
                        {
                            new PointWrapper(()=>faces[face1][2043, y], d => faces[face1][2043, y] = d),
                            new PointWrapper(()=>faces[face1][2044, y], d => faces[face1][2044, y] = d),
                            new PointWrapper(()=>faces[face1][2045, y], d => faces[face1][2045, y] = d),
                            new PointWrapper(()=>faces[face1][2046, y], d => faces[face1][2046, y] = d),
                            new PointWrapper(()=>faces[face1][2047, y], d => faces[face1][2047, y] = d),
                            new PointWrapper(()=>faces[face2][0, y], d => faces[face2][0, y] = d),
                            new PointWrapper(()=>faces[face2][1, y], d => faces[face2][1, y] = d),
                            new PointWrapper(()=>faces[face2][2, y], d => faces[face2][2, y] = d),
                            new PointWrapper(()=>faces[face2][3, y], d => faces[face2][3, y] = d),
                            new PointWrapper(()=>faces[face2][4, y], d => faces[face2][4, y] = d),
                        };
                        ApplyEdgeFix(edgePoints);
                    }
                }));
            }

            // Front <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int x = 0; x < TileWidth; ++x)
                {
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, 2043], d => faces[CubeMapFace.Front][x, 2043] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, 2044], d => faces[CubeMapFace.Front][x, 2044] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, 2045], d => faces[CubeMapFace.Front][x, 2045] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, 2046], d => faces[CubeMapFace.Front][x, 2046] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, 2047], d => faces[CubeMapFace.Front][x, 2047] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2047 - x, 2047], d => faces[CubeMapFace.Down][2047 - x, 2047] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2047 - x, 2046], d => faces[CubeMapFace.Down][2047 - x, 2046] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2047 - x, 2045], d => faces[CubeMapFace.Down][2047 - x, 2045] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2047 - x, 2044], d => faces[CubeMapFace.Down][2047 - x, 2044] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2047 - x, 2043], d => faces[CubeMapFace.Down][2047 - x, 2043] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Right <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2043], d => faces[CubeMapFace.Right][z, 2043] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2044], d => faces[CubeMapFace.Right][z, 2044] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2045], d => faces[CubeMapFace.Right][z, 2045] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2046], d => faces[CubeMapFace.Right][z, 2046] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2047], d => faces[CubeMapFace.Right][z, 2047] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][0, 2047-z], d => faces[CubeMapFace.Down][0, 2047-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][1, 2047-z], d => faces[CubeMapFace.Down][1, 2047-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2, 2047-z], d => faces[CubeMapFace.Down][2, 2047-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][3, 2047-z], d => faces[CubeMapFace.Down][3, 2047-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][4, 2047-z], d => faces[CubeMapFace.Down][4, 2047-z] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Left <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 2043], d => faces[CubeMapFace.Left][z, 2043] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 2044], d => faces[CubeMapFace.Left][z, 2044] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 2045], d => faces[CubeMapFace.Left][z, 2045] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 2046], d => faces[CubeMapFace.Left][z, 2046] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 2047], d => faces[CubeMapFace.Left][z, 2047] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2047, z], d => faces[CubeMapFace.Down][2047, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2046, z], d => faces[CubeMapFace.Down][2046, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2045, z], d => faces[CubeMapFace.Down][2045, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2044, z], d => faces[CubeMapFace.Down][2044, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2043, z], d => faces[CubeMapFace.Down][2043, z] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Right <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 4], d => faces[CubeMapFace.Right][z, 4] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 3], d => faces[CubeMapFace.Right][z, 3] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2], d => faces[CubeMapFace.Right][z, 2] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 1], d => faces[CubeMapFace.Right][z, 1] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 0], d => faces[CubeMapFace.Right][z, 0] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2047, 2047-z] , d => faces[CubeMapFace.Up][2047, 2047-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2046, 2047-z] , d => faces[CubeMapFace.Up][2046, 2047-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2045, 2047-z] , d => faces[CubeMapFace.Up][2045, 2047-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2044, 2047-z] , d => faces[CubeMapFace.Up][2044, 2047-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2043, 2047-z] , d => faces[CubeMapFace.Up][2043, 2047-z]  = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Back <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 4], d => faces[CubeMapFace.Back][z, 4] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 3], d => faces[CubeMapFace.Back][z, 3] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 2], d => faces[CubeMapFace.Back][z, 2] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 1], d => faces[CubeMapFace.Back][z, 1] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 0], d => faces[CubeMapFace.Back][z, 0] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2047-z, 0] , d => faces[CubeMapFace.Up][2047-z, 0]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2047-z, 1] , d => faces[CubeMapFace.Up][2047-z, 1]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2047-z, 2] , d => faces[CubeMapFace.Up][2047-z, 2]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2047-z, 3] , d => faces[CubeMapFace.Up][2047-z, 3]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2047-z, 4] , d => faces[CubeMapFace.Up][2047-z, 4]  = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Left <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 4], d => faces[CubeMapFace.Left][z, 4] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 3], d => faces[CubeMapFace.Left][z, 3] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 2], d => faces[CubeMapFace.Left][z, 2] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 1], d => faces[CubeMapFace.Left][z, 1] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, 0], d => faces[CubeMapFace.Left][z, 0] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][0, z], d => faces[CubeMapFace.Up][0, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][1, z], d => faces[CubeMapFace.Up][1, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][2, z], d => faces[CubeMapFace.Up][2, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][3, z], d => faces[CubeMapFace.Up][3, z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][4, z], d => faces[CubeMapFace.Up][4, z] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            Task.WhenAll(tasks).GetAwaiter().GetResult();

            // TODO: Maybe apply low pass filter matrix on edge pixels at the end:
            //
            //     0    .125     0
            //    .125   .5    0.125
            //     0    .125     0
            //
        }

        // Special thanks to ChatGPT for these regression codes.
        static void LinearRegression(PointD[] points, out double slope, out double yIntercept)
        {
            // Calculate the mean of x and y
            double x_mean = 0;
            double y_mean = 0;
            for (int i = 0; i < points.Length; i++)
            {
                x_mean += points[i].X;
                y_mean += points[i].Y;
            }
            x_mean /= points.Length;
            y_mean /= points.Length;

            // Calculate the slope and y-intercept of the regression line
            double numerator = 0;
            double denominator = 0;
            for (int i = 0; i < points.Length; i++)
            {
                numerator += (points[i].X - x_mean) * (points[i].Y - y_mean);
                denominator += (points[i].X - x_mean) * (points[i].X - x_mean);
            }
            slope = numerator / denominator;
            yIntercept = y_mean - slope * x_mean;
        }

        static void PolynomialRegression(PointD[] points, out Func<double,double> regFunc)
        {
            // Debug.WriteLine("PolyReg");
            double[] xValues = new double[points.Length];
            double[] yValues = new double[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                xValues[i] = points[i].X;
                yValues[i] = points[i].Y;
                //Debug.WriteLine(points[i].Y);
            }

            // Create the design matrix with columns for x, x^2, x^3, and x^4
            var designMatrix = Matrix<double>.Build.DenseOfRowArrays(
                xValues.Select(x => new double[] { 1.0, x, x * x, x * x * x, x * x * x * x }).ToArray());

            // Fit the model using least squares regression
            var regression = MultipleRegression.NormalEquations(designMatrix, Vector<double>.Build.Dense(yValues));

            // Get the coefficients of the fitted polynomial
            var coeff = regression.ToArray();

            //Debug.WriteLine("Coeff:");
            //foreach (var d in coeff) { Debug.WriteLine(d); }

            regFunc = xx =>
            {
                return coeff[0] + coeff[1] * xx + coeff[2] * Math.Pow(xx, 2) + coeff[3] * Math.Pow(xx, 3) + coeff[4] * Math.Pow(xx, 4);
            };
        }
    }
}

using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
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
            PointD[] points = new PointD[6];
            int nextIndex = 0;
            for (int i = 0; i < 10; ++i)
            {
                if (i >= 3 && i <= 6) continue;
                points[nextIndex++] = new PointD { X = i, Y = edgePoints[i].GetValue() };
            }
            PolynomialRegression(points, out var regFunc);
            // 0 1 2 3 4 5 6 7 8 9
            //       | | | |        <- these are skipped and reconstructed
            for (int i = 4; i <= 6; ++i)
                edgePoints[i].SetValue(regFunc(i));
        }

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
            //ApplyBlend(edgePoints);
            //ApplyBlend(edgePoints);
            //ApplyBlend(edgePoints);
        }

        public static void MakeSeemless(Dictionary<CubeMapFace, double[,]> faces, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            // Fix cubemap edges if there are sudden jumps
            List<Task> tasks = new List<Task>();

            // Strategy: polynomial regression:
            // Glitched terrain looks like this:
            //       /
            //      /
            //    --  <- this should not be here
            //   /
            //  /
            //x:123456
            // To fix this, we ignore points at location 3+4
            // Take 4 other points (1,2,5,6), calculate polynomial regression,
            // then calculate points 3+4 from it.

            // Since this does not fix everything, we also apply some blending afterwards:
            // Blend pixels:
            // a b c ][ d e f
            // Schema:  c = 0.5c + 0.25b + 0.25d
            // Apply for b,c,d,e

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
                        ApplyEdgeFix(edgePoints);
                    }
                }));
            }

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

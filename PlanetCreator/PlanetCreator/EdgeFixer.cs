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

        public static void MakeSeemless(Dictionary<CubeMapFace, double[,]> faces, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            // Fix cubemap edges if there are sudden jumps
            List<Task> tasks = new List<Task>();

            // Strategy: linear regression:
            // Glitched terrain looks like this:
            //       /
            //      /
            //    --  <- this should not be here
            //   /
            //  /
            //x:123456
            // To fix this, we ignore points at location 3+4
            // Take 4 other points (1,2,5,6), calculate linear regression,
            // then calculate points 3+4 from it.

            // Idea: If fix is not enough maybe use average of regression and points 2+5

            // Front <-> Up
            // Down <-> Back
            // Direction: Bottom to top:
            CubeMapFace[][] verticals = new CubeMapFace[][]
            {
                new[] { CubeMapFace.Front, CubeMapFace.Up },
                //new[] { CubeMapFace.Down, CubeMapFace.Back },
            };
            Func<double, double> regFunc;
            foreach (var pair in verticals)
            {
                var face1 = pair[0];
                var face2 = pair[1];
                tasks.Add(Task.Run(() =>
                {
                    // WIP
                    // Problems:
                    // - There are still jumps in terrain, especially at the edges of the cubemap where the distortions are the greatest
                    // - Problems with pitched roof shapes when terrain maxing out at the edge between cubemaps
                    //   - In that case we need another regression formula. Linear is not working.

                    // TODO: Weird shapes probably comming from parallel access. Need to add locks.

                    for (int x = 0; x < TileWidth; ++x)
                    {
                        if (token.IsCancellationRequested) return;
                        PointD[] points = new PointD[6];
                        points[0] = new PointD { X = 0, Y = faces[face1][x, 3] };
                        points[1] = new PointD { X = 1, Y = faces[face1][x, 2] };
                        points[2] = new PointD { X = 2, Y = faces[face1][x, 1] };
                        //                      skip 3                   x, 0
                        //                      skip 4                   x, 2047
                        points[3] = new PointD { X = 5, Y = faces[face2][x, 2046] };
                        points[4] = new PointD { X = 6, Y = faces[face2][x, 2045] };
                        points[5] = new PointD { X = 7, Y = faces[face2][x, 2044] };
                        //Debug.WriteLine("");
                        //Debug.WriteLine("3: " + faces[face1][x, 0]);
                        //Debug.WriteLine("4: " + faces[face2][x, 2047]);
                        PolynomialRegression(points, out regFunc);
                        faces[face1][x, 0] = regFunc(3);
                        faces[face2][x, 2047] = regFunc(4);
                        //Debug.WriteLine("3: " + faces[face1][x, 0]);
                        //Debug.WriteLine("4: " + faces[face2][x, 2047]);
                    }
                }));
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult();
            return;

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
                        PointD[] points = new PointD[6];
                        points[0] = new PointD { X = 0, Y = faces[face1][2044, y] };
                        points[1] = new PointD { X = 1, Y = faces[face1][2045, y] };
                        points[2] = new PointD { X = 2, Y = faces[face1][2046, y] };
                        //                      skip 3                   2047, y
                        //                      skip 4                   0, y
                        points[3] = new PointD { X = 5, Y = faces[face2][1, y] };
                        points[4] = new PointD { X = 6, Y = faces[face2][2, y] };
                        points[5] = new PointD { X = 7, Y = faces[face2][3, y] };
                        PolynomialRegression(points, out regFunc);
                        faces[face1][2047, y] = regFunc(3);
                        faces[face2][0, y] = regFunc(4);
                    }
                }));
            }

            // Front <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int x = 0; x < TileWidth; ++x)
                {
                    if (token.IsCancellationRequested) return;
                    PointD[] points = new PointD[4];
                    points[0] = new PointD { X = 1, Y = faces[CubeMapFace.Front][x, 2045] };
                    points[1] = new PointD { X = 2, Y = faces[CubeMapFace.Front][x, 2046] };
                    //                      skip 3                               x, 2047
                    //                      skip 4                              2047 - x, 2047
                    points[2] = new PointD { X = 5, Y = faces[CubeMapFace.Down][2047 - x, 2046] };
                    points[3] = new PointD { X = 6, Y = faces[CubeMapFace.Down][2047 - x, 2045] };
                    LinearRegression(points, out var slope, out var yIntercept);
                    faces[CubeMapFace.Front][x, 2047] = 3 * slope + yIntercept;
                    faces[CubeMapFace.Down][2047 - x, 2047] = 4 * slope + yIntercept;
                }
            }));

            // Right <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    PointD[] points = new PointD[4];
                    points[0] = new PointD { X = 1, Y = faces[CubeMapFace.Right][z, 2045] };
                    points[1] = new PointD { X = 2, Y = faces[CubeMapFace.Right][z, 2046] };
                    //                      skip 3                               z, 2047
                    //                      skip 4                              0, 2047-z
                    points[2] = new PointD { X = 5, Y = faces[CubeMapFace.Down][1, 2047-z] };
                    points[3] = new PointD { X = 6, Y = faces[CubeMapFace.Down][2, 2047-z] };
                    LinearRegression(points, out var slope, out var yIntercept);
                    faces[CubeMapFace.Right][z, 2047] = 3 * slope + yIntercept;
                    faces[CubeMapFace.Down][0, 2047-z] = 4 * slope + yIntercept;
                }
            }));

            // Left <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    PointD[] points = new PointD[4];
                    points[0] = new PointD { X = 1, Y = faces[CubeMapFace.Left][z, 2045] };
                    points[1] = new PointD { X = 2, Y = faces[CubeMapFace.Left][z, 2046] };
                    //                      skip 3                              z,  2047
                    //                      skip 4                              2047, z
                    points[2] = new PointD { X = 5, Y = faces[CubeMapFace.Down][2046, z] };
                    points[3] = new PointD { X = 6, Y = faces[CubeMapFace.Down][2045, z] };
                    LinearRegression(points, out var slope, out var yIntercept);
                    faces[CubeMapFace.Left][z, 2047] = 3 * slope + yIntercept;
                    faces[CubeMapFace.Down][2047, z] = 4 * slope + yIntercept;
                }
            }));

            // Right <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    PointD[] points = new PointD[4];
                    points[0] = new PointD { X = 1, Y = faces[CubeMapFace.Right][z, 2] };
                    points[1] = new PointD { X = 2, Y = faces[CubeMapFace.Right][z, 1] };
                    //                      skip 3                               z, 0
                    //                      skip 4                            2047, 2047-z
                    points[2] = new PointD { X = 5, Y = faces[CubeMapFace.Up][2046, 2047-z] };
                    points[3] = new PointD { X = 6, Y = faces[CubeMapFace.Up][2045, 2047-z] };
                    LinearRegression(points, out var slope, out var yIntercept);
                    faces[CubeMapFace.Right][z, 0] = 3 * slope + yIntercept;
                    faces[CubeMapFace.Up][2047, 2047-z] = 4 * slope + yIntercept;
                }
            }));

            // Back <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    PointD[] points = new PointD[4];
                    points[0] = new PointD { X = 1, Y = faces[CubeMapFace.Back][z, 2] };
                    points[1] = new PointD { X = 2, Y = faces[CubeMapFace.Back][z, 1] };
                    //                      skip 3                              z, 0
                    //                      skip 4                            2047-z, 0
                    points[2] = new PointD { X = 5, Y = faces[CubeMapFace.Up][2047-z, 1] };
                    points[3] = new PointD { X = 6, Y = faces[CubeMapFace.Up][2047-z, 2] };
                    LinearRegression(points, out var slope, out var yIntercept);
                    faces[CubeMapFace.Back][z, 0] = 3 * slope + yIntercept;
                    faces[CubeMapFace.Up][2047-z, 0] = 4 * slope + yIntercept;
                }
            }));

            // Left <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < TileWidth; ++z)
                {
                    if (token.IsCancellationRequested) return;
                    PointD[] points = new PointD[4];
                    points[0] = new PointD { X = 1, Y = faces[CubeMapFace.Left][z, 2] };
                    points[1] = new PointD { X = 2, Y = faces[CubeMapFace.Left][z, 1] };
                    //                      skip 3                              z, 0
                    //                      skip 4                            0, z
                    points[2] = new PointD { X = 5, Y = faces[CubeMapFace.Up][1, z] };
                    points[3] = new PointD { X = 6, Y = faces[CubeMapFace.Up][2, z] };
                    LinearRegression(points, out var slope, out var yIntercept);
                    faces[CubeMapFace.Left][z, 0] = 3 * slope + yIntercept;
                    faces[CubeMapFace.Up][0, z] = 4 * slope + yIntercept;
                }
            }));

            Task.WhenAll(tasks).GetAwaiter().GetResult();
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

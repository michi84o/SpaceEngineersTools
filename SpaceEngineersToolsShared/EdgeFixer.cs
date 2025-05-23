﻿using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpaceEngineersToolsShared
{
    public static class EdgeFixer
    {
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

        /// <summary>
        /// Fix the edges of the height map to be seamless.
        /// Don't set the 'cornersOnly' or 'erodeCorners' parameters for normal use cases.
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="token"></param>
        /// <param name="cornersOnly">If true, only the 5 pixel rows or columns near the corners are fixed. Also if true, no hydraulic erosion is applied unless specified with 'erodeCorners'.</param>
        /// <param name="erodeCorners">
        /// If true, hydraulic erosion is applied to the corners. This is already done automatically if 'cornersOnly' is false.
        /// If 'cornersOnly' is false and 'erodeCorners' is true, corners will be eroded twice!
        /// </param>
        public static void MakeSeamless(Dictionary<CubeMapFace, double[,]> faces, CancellationToken token, bool cornersOnly = false, bool erodeCorners = false)
        {
            if (token.IsCancellationRequested) return;

            int tileWidth = faces.Values.First().GetLength(0); // Assume tile width and height are the same.

            FixCubeTriplets(faces);

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
                    for (int x = 0; x < tileWidth; ++x)
                    {
                        if (cornersOnly && x == 5) x = tileWidth - 6;
                        if (token.IsCancellationRequested) return;
                        List<PointWrapper> edgePoints = new List<PointWrapper>
                        {
                            new PointWrapper(()=>faces[face1][x, 4], d => faces[face1][x, 4] = d),
                            new PointWrapper(()=>faces[face1][x, 3], d => faces[face1][x, 3] = d),
                            new PointWrapper(()=>faces[face1][x, 2], d => faces[face1][x, 2] = d),
                            new PointWrapper(()=>faces[face1][x, 1], d => faces[face1][x, 1] = d),
                            new PointWrapper(()=>faces[face1][x, 0], d => faces[face1][x, 0] = d),           // 4
                            new PointWrapper(()=>faces[face2][x, (tileWidth-1)], d => faces[face2][x, (tileWidth-1)] = d),     // 5
                            new PointWrapper(()=>faces[face2][x, (tileWidth-2)], d => faces[face2][x, (tileWidth-2)] = d),
                            new PointWrapper(()=>faces[face2][x, (tileWidth-3)], d => faces[face2][x, (tileWidth-3)] = d),
                            new PointWrapper(()=>faces[face2][x, (tileWidth-4)], d => faces[face2][x, (tileWidth-4)] = d),
                            new PointWrapper(()=>faces[face2][x, (tileWidth-5)], d => faces[face2][x, (tileWidth-5)] = d),
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
                    for (int y = 0; y < tileWidth; ++y)
                    {
                        if (cornersOnly && y == 5) y = tileWidth - 6;
                        if (token.IsCancellationRequested) return;
                        List<PointWrapper> edgePoints = new List<PointWrapper>
                        {
                            new PointWrapper(()=>faces[face1][(tileWidth-5), y], d => faces[face1][(tileWidth-5), y] = d),
                            new PointWrapper(()=>faces[face1][(tileWidth-4), y], d => faces[face1][(tileWidth-4), y] = d),
                            new PointWrapper(()=>faces[face1][(tileWidth-3), y], d => faces[face1][(tileWidth-3), y] = d),
                            new PointWrapper(()=>faces[face1][(tileWidth-2), y], d => faces[face1][(tileWidth-2), y] = d),
                            new PointWrapper(()=>faces[face1][(tileWidth-1), y], d => faces[face1][(tileWidth-1), y] = d),
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
                for (int x = 0; x < tileWidth; ++x)
                {
                    if (cornersOnly && x == 5) x = tileWidth - 6;
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, (tileWidth-5)], d => faces[CubeMapFace.Front][x, (tileWidth-5)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, (tileWidth-4)], d => faces[CubeMapFace.Front][x, (tileWidth-4)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, (tileWidth-3)], d => faces[CubeMapFace.Front][x, (tileWidth-3)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, (tileWidth-2)], d => faces[CubeMapFace.Front][x, (tileWidth-2)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Front][x, (tileWidth-1)], d => faces[CubeMapFace.Front][x, (tileWidth-1)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-1)], d => faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-1)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-2)], d => faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-2)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-3)], d => faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-3)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-4)], d => faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-4)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-5)], d => faces[CubeMapFace.Down][(tileWidth-1) - x, (tileWidth-5)] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Right <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < tileWidth; ++z)
                {
                    if (cornersOnly && z == 5) z = tileWidth - 6;
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, (tileWidth-5)], d => faces[CubeMapFace.Right][z, (tileWidth-5)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, (tileWidth-4)], d => faces[CubeMapFace.Right][z, (tileWidth-4)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, (tileWidth-3)], d => faces[CubeMapFace.Right][z, (tileWidth-3)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, (tileWidth-2)], d => faces[CubeMapFace.Right][z, (tileWidth-2)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, (tileWidth-1)], d => faces[CubeMapFace.Right][z, (tileWidth-1)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][0, (tileWidth-1)-z], d => faces[CubeMapFace.Down][0, (tileWidth-1)-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][1, (tileWidth-1)-z], d => faces[CubeMapFace.Down][1, (tileWidth-1)-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][2, (tileWidth-1)-z], d => faces[CubeMapFace.Down][2, (tileWidth-1)-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][3, (tileWidth-1)-z], d => faces[CubeMapFace.Down][3, (tileWidth-1)-z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][4, (tileWidth-1)-z], d => faces[CubeMapFace.Down][4, (tileWidth-1)-z] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Left <-> Down
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < tileWidth; ++z)
                {
                    if (cornersOnly && z == 5) z = tileWidth - 6;
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, (tileWidth-5)], d => faces[CubeMapFace.Left][z, (tileWidth-5)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, (tileWidth-4)], d => faces[CubeMapFace.Left][z, (tileWidth-4)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, (tileWidth-3)], d => faces[CubeMapFace.Left][z, (tileWidth-3)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, (tileWidth-2)], d => faces[CubeMapFace.Left][z, (tileWidth-2)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Left][z, (tileWidth-1)], d => faces[CubeMapFace.Left][z, (tileWidth-1)] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-1), z], d => faces[CubeMapFace.Down][(tileWidth-1), z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-2), z], d => faces[CubeMapFace.Down][(tileWidth-2), z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-3), z], d => faces[CubeMapFace.Down][(tileWidth-3), z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-4), z], d => faces[CubeMapFace.Down][(tileWidth-4), z] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Down][(tileWidth-5), z], d => faces[CubeMapFace.Down][(tileWidth-5), z] = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Right <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < tileWidth; ++z)
                {
                    if (cornersOnly && z == 5) z = tileWidth - 6;
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 4], d => faces[CubeMapFace.Right][z, 4] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 3], d => faces[CubeMapFace.Right][z, 3] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 2], d => faces[CubeMapFace.Right][z, 2] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 1], d => faces[CubeMapFace.Right][z, 1] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Right][z, 0], d => faces[CubeMapFace.Right][z, 0] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-1), (tileWidth-1)-z] , d => faces[CubeMapFace.Up][(tileWidth-1), (tileWidth-1)-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-2), (tileWidth-1)-z] , d => faces[CubeMapFace.Up][(tileWidth-2), (tileWidth-1)-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-3), (tileWidth-1)-z] , d => faces[CubeMapFace.Up][(tileWidth-3), (tileWidth-1)-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-4), (tileWidth-1)-z] , d => faces[CubeMapFace.Up][(tileWidth-4), (tileWidth-1)-z]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-5), (tileWidth-1)-z] , d => faces[CubeMapFace.Up][(tileWidth-5), (tileWidth-1)-z]  = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Back <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < tileWidth; ++z)
                {
                    if (cornersOnly && z == 5) z = tileWidth - 6;
                    if (token.IsCancellationRequested) return;
                    List<PointWrapper> edgePoints = new List<PointWrapper>
                    {
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 4], d => faces[CubeMapFace.Back][z, 4] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 3], d => faces[CubeMapFace.Back][z, 3] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 2], d => faces[CubeMapFace.Back][z, 2] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 1], d => faces[CubeMapFace.Back][z, 1] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Back][z, 0], d => faces[CubeMapFace.Back][z, 0] = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-1)-z, 0] , d => faces[CubeMapFace.Up][(tileWidth-1)-z, 0]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-1)-z, 1] , d => faces[CubeMapFace.Up][(tileWidth-1)-z, 1]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-1)-z, 2] , d => faces[CubeMapFace.Up][(tileWidth-1)-z, 2]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-1)-z, 3] , d => faces[CubeMapFace.Up][(tileWidth-1)-z, 3]  = d),
                        new PointWrapper(()=>faces[CubeMapFace.Up][(tileWidth-1)-z, 4] , d => faces[CubeMapFace.Up][(tileWidth-1)-z, 4]  = d),
                    };
                    ApplyEdgeFix(edgePoints);
                }
            }));

            // Left <-> Up
            tasks.Add(Task.Run(() =>
            {
                for (int z = 0; z < tileWidth; ++z)
                {
                    if (cornersOnly && z == 5) z = tileWidth - 6;
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

            FixCubeTriplets(faces);

            // Make triplet points smoother
            if (!cornersOnly)
            {
                //MakeSeamless(faces, token, true);
                MakeSeamless(faces, token, true, true);
            }

            if (erodeCorners)
            {
                var hyd = new HydraulicErosion(new Random(0), faces);
                hyd.ErosionMaxDropletLifeTime = 30;
                hyd.ErosionSedimentCapacityFactor = 30;
                var faceVals = Enum.GetValues(typeof(CubeMapFace)).Cast<CubeMapFace>().ToArray();
                List<PointD> points = new List<PointD>
                {
                    new PointD { X = 0.5, Y= 0.5 },
                    new PointD { X = 1.5, Y= 0.5 },
                    new PointD { X = 0.5, Y= 1.5 },
                };
                foreach (var face in faceVals)
                {
                    foreach (var point in points)
                    {
                        hyd.Erode(token, point, face);
                        hyd.Erode(token, new PointD() { X = (tileWidth-1)-point.X, Y = (tileWidth-1)-point.Y }, face);
                    }
                }
                FixCubeTriplets(faces);
            }

            // TODO: Maybe apply low pass filter matrix on edge pixels at the end:
            //
            //     0    .125     0
            //    .125   .5    0.125
            //     0    .125     0
            //
        }

        /// <summary>
        /// Make sure the edge pixels where 3 maps meet have the same value.
        /// </summary>
        /// <param name="faces"></param>
        public static void FixCubeTriplets(Dictionary<CubeMapFace, double[,]> faces)
        {
            int tileWidth = faces.Values.First().GetLength(0); // Assume tile width and height are the same.

            List<List<PointWrapper>> triplets = new List<List<PointWrapper>>();
            // There are 8 points in total:
            // - Front-Up-Left
            var pw1 = new PointWrapper(() => faces[CubeMapFace.Front][0, 0], d => faces[CubeMapFace.Front][0, 0] = d);
            var pw2 = new PointWrapper(() => faces[CubeMapFace.Up][0, (tileWidth-1)], d => faces[CubeMapFace.Up][0, (tileWidth-1)] = d);
            var pw3 = new PointWrapper(() => faces[CubeMapFace.Left][(tileWidth-1), 0], d => faces[CubeMapFace.Left][(tileWidth-1), 0] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });
            // - Front-Up-Right
            pw1 = new PointWrapper(() => faces[CubeMapFace.Front][(tileWidth-1), 0], d => faces[CubeMapFace.Front][(tileWidth-1), 0] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Up][(tileWidth-1), (tileWidth-1)], d => faces[CubeMapFace.Up][(tileWidth-1), (tileWidth-1)] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Right][0, 0], d => faces[CubeMapFace.Right][0, 0] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });
            // - Front-Down-Left
            pw1 = new PointWrapper(() => faces[CubeMapFace.Front][0, (tileWidth-1)], d => faces[CubeMapFace.Front][0, (tileWidth-1)] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Down][(tileWidth-1), (tileWidth-1)], d => faces[CubeMapFace.Down][(tileWidth-1), (tileWidth-1)] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Left][(tileWidth-1), (tileWidth-1)], d => faces[CubeMapFace.Left][(tileWidth-1), (tileWidth-1)] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });
            // - Front-Down-Right
            pw1 = new PointWrapper(() => faces[CubeMapFace.Front][(tileWidth-1), (tileWidth-1)], d => faces[CubeMapFace.Front][(tileWidth-1), (tileWidth-1)] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Down] [0, (tileWidth-1)], d => faces[CubeMapFace.Down][0, (tileWidth-1)] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Right][0, (tileWidth-1)], d => faces[CubeMapFace.Right][0, (tileWidth-1)] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });

            // - Back-Up-Right
            pw1 = new PointWrapper(() => faces[CubeMapFace.Back][0, 0], d => faces[CubeMapFace.Back][0, 0] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Up][(tileWidth-1), 0], d => faces[CubeMapFace.Up][(tileWidth-1), 0] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Right][(tileWidth-1), 0], d => faces[CubeMapFace.Right][(tileWidth-1), 0] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });
            // - Back-Up-Left
            pw1 = new PointWrapper(() => faces[CubeMapFace.Back][(tileWidth-1), 0], d => faces[CubeMapFace.Back][(tileWidth-1), 0] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Up][0, 0], d => faces[CubeMapFace.Up][0, 0] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Left][0, 0], d => faces[CubeMapFace.Left][0, 0] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });
            // - Back-Down-Right
            pw1 = new PointWrapper(() => faces[CubeMapFace.Back][0, (tileWidth-1)], d => faces[CubeMapFace.Back][0, (tileWidth-1)] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Down][0, 0], d => faces[CubeMapFace.Down][0, 0] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Right][(tileWidth-1), (tileWidth-1)], d => faces[CubeMapFace.Right][(tileWidth-1), (tileWidth-1)] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });
            // - Back-Down-Left
            pw1 = new PointWrapper(() => faces[CubeMapFace.Back][(tileWidth-1), (tileWidth-1)], d => faces[CubeMapFace.Back][(tileWidth-1), (tileWidth-1)] = d);
            pw2 = new PointWrapper(() => faces[CubeMapFace.Down][(tileWidth-1), 0], d => faces[CubeMapFace.Down][(tileWidth-1), 0] = d);
            pw3 = new PointWrapper(() => faces[CubeMapFace.Left][0, (tileWidth-1)], d => faces[CubeMapFace.Left][0, (tileWidth-1)] = d);
            triplets.Add(new List<PointWrapper> { pw1, pw2, pw3 });

            foreach (var triplet in triplets)
            {
                var sum = triplet[0].GetValue() + triplet[1].GetValue() + triplet[2].GetValue();
                var avg = sum / 3;
                triplet[0].SetValue(avg);
                triplet[1].SetValue(avg);
                triplet[2].SetValue(avg);
            }
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

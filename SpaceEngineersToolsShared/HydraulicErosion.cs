using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpaceEngineersToolsShared
{
    public class HydraulicErosion
    {
        int _erosionMaxDropletLifeTime = 75;
        public int ErosionMaxDropletLifeTime
        {
            get => _erosionMaxDropletLifeTime;
            set
            {
                _erosionMaxDropletLifeTime = value;
                UpdateEvaporatePart();
            }
        }

        public double ErosionInteria { get; set; } = 0.005;
        public double ErosionSedimentCapacityFactor { get; set; } = 10;
        public double ErosionDepositSpeed { get; set; } = 0.015;
        public double ErosionErodeSpeed { get; set; } = 0.005;
        public double ErosionDepositBrush { get; set; } = 3;
        public double ErosionErodeBrush { get; set; } = 3;
        public double Gravity { get; set; } = 10;

        double _evaporateSpeed = 0.0025;
        public double EvaporateSpeed
        {
            get => _evaporateSpeed;
            set
            {
                _evaporateSpeed = value;
                UpdateEvaporatePart();
            }
        }

        public double BrushPointiness { get; set; } = 0.25;

        /// <summary>
        /// Sediment System is optional. Keep null to disable.
        /// </summary>
        Dictionary<CubeMapFace, byte[,]> _sedimentLayerIndexes { get; set; }
        /// <summary>
        /// Sediment System is optional. Keep null to disable.
        /// </summary>
        public List<ushort[]> _sedimentLayers { get; set; }
        bool _sedimentsEnabled;

        Dictionary<CubeMapFace, double[,]> _depositedSedimentMap = new();

        void UpdateEvaporatePart()
        {
            EvaporatePart = Math.Pow(1 - EvaporateSpeed, ErosionMaxDropletLifeTime - 1);
        }
        double EvaporatePart { get; set; }

        Random _rnd;
        Dictionary<CubeMapFace, double[,]> _faces;
        /// <summary>Only read, don't change!</summary>
        public Dictionary<CubeMapFace, double[,]> Faces => _faces;
        bool _debugMode;
        bool _limitedDebugMode;
        CubeMapFace? _debugFace;
        int _tileWidth;
        CubeMapFace[] _faceValues;
        public HydraulicErosion(Random rnd, Dictionary<CubeMapFace, double[,]> faces,
            bool debugMode = false, bool limitedDebugMode = false, CubeMapFace? debugFace = null,
            Dictionary<CubeMapFace, byte[,]> sedimentLayerIndexes = null,
            List<ushort[]> sedimentLayers = null)
        {
            UpdateEvaporatePart();
            _rnd = rnd;
            _faces = faces;
            _tileWidth = _faces.First().Value.GetLength(0);
            _debugMode = debugMode;
            _limitedDebugMode = limitedDebugMode;
            _faceValues = faces.Keys.ToArray();
            _debugFace = debugFace;

            _sedimentsEnabled =  sedimentLayerIndexes != null && sedimentLayers != null;
            if (_sedimentsEnabled)
            {
                _depositedSedimentMap = new();
                foreach (var f in faces)
                {
                    _depositedSedimentMap[f.Key] = new double[_tileWidth, _tileWidth];
                }
                _sedimentLayerIndexes = sedimentLayerIndexes;
                _sedimentLayers = sedimentLayers;
            }
        }

        //public double[,] DebugTileWaterFlow { get; set; }
        //public double[,] DebugTileErode { get; set; }
        //public double[,] DebugTileDeposit { get; set; }

        public void Erode(CancellationToken token,
            PointD? startPoint = null,
            CubeMapFace? startPointFace = null)
        {
            // Code based on Sebastian Lague
            // https://www.youtube.com/watch?v=eaXk97ujbPQ
            // https://github.com/SebLague/Hydraulic-Erosion
            // which is based on a paper by Hans Theobald Beyer
            // https://www.firespark.de/resources/downloads/implementation%20of%20a%20methode%20for%20hydraulic%20erosion.pdf
            // which is based on a blog entry by Alexey Volynskov
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
            //double waterX = water;
            //for (int i = 0; i < maxDropletLifetime - 1; ++i)
            //{
            //    waterX *= (1 - evaporateSpeed);
            //}
            //double evaporatePart = waterX;
            // => waterModified = water - evaporatePart*iteration
            // Optimized for-loop, because Math!
            double evaporatePart = EvaporatePart; //water * Math.Pow(1 - evaporateSpeed, maxDropletLifetime - 1);

            CubeMapFace face = CubeMapFace.Up;
            PointD pos = new();
            PointD dir = new();


            lock (_rnd)
            {
                if (!_debugMode)
                {
                    pos.X = _rnd.NextDouble() * 2047;
                    pos.Y = _rnd.NextDouble() * 2047;
                    face = _faceValues[_rnd.Next(0, _faceValues.Length)];
                }
                else
                {
                    if (_limitedDebugMode)
                    {
                        pos.X = _rnd.NextDouble() * 512 + 512;
                        pos.Y = _rnd.NextDouble() * 512 + 512;
                    }
                    else
                    {
                        pos.X = _rnd.NextDouble() * 2047;
                        pos.Y = _rnd.NextDouble() * 2047;
                    }
                    face = _debugFace ?? CubeMapFace.Back;
                }
            }
            if (startPoint != null)
            {
                pos.X = startPoint.Value.X;
                pos.Y = startPoint.Value.Y;
            }
            if (startPointFace != null)
            {
                face = startPointFace.Value;
            }

            // Code based on Sebastian Lague
            for (int i = 0; i < maxDropletLifetime; ++i)
            {
                if (token.IsCancellationRequested) return;

                double waterModified = water - (evaporatePart * i / (maxDropletLifetime - 1)); // Will be 0 at last iteration

                // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
                PointD cellOffset = pos.IntegerOffset(); // 0 <= offset < 1

                // Calculate droplet's height and direction of flow with bilinear interpolation of surrounding heights
                HeightAndGradient heightAndGradient = CalculateHeightAndGradient(pos, face, _faces);

                // Update the droplet's direction and position (move position 1 unit regardless of speed)
                dir = (dir * inertia - heightAndGradient.Gradient * (1 - inertia));

                // Normalize direction
                dir = dir.Normalize();
                CubeMapFace faceOld = face;
                PointD posOld = pos;
                pos += dir;

                // Stop simulating droplet if it's not moving
                if (dir.X == 0 && dir.Y == 0)
                {
                    break;
                }

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
                double newHeight = CalculateHeightAndGradient(pos, face, _faces).Height;
                double deltaHeight = newHeight - heightAndGradient.Height;

                // Calculate the droplet's sediment capacity (higher when moving fast down a slope and contains lots of water)
                double sedimentCapacity = Math.Max(-deltaHeight * speed * waterModified * sedimentCapacityFactor, minSedimentCapacity);


                //if (_debugMode && DebugTileWaterFlow != null)
                //{
                //    DebugTileWaterFlow[posOldI.X, posOldI.Y] = waterModified;
                //}

                // If carrying more sediment than capacity, or if flowing uphill:
                var oldPoint = new CubeMapPoint(_faces, posOldI.X, posOldI.Y, faceOld);
                if (sediment > sedimentCapacity || deltaHeight > 0)
                {
                    // If moving uphill (deltaHeight > 0) try fill up to the current height, otherwise deposit a fraction of the excess sediment
                    double amountToDeposit = (deltaHeight > 0) ? Math.Min(deltaHeight, sediment) : (sediment - sedimentCapacity) * depositSpeed;

                    //if (_debugMode)
                    //    DebugTileDeposit[posOldI.X, posOldI.Y] = amountToDeposit;

                    if (amountToDeposit != 0)
                    {
                        ApplyBrush(depositBrush, oldPoint, posOld, amountToDeposit);
                        sediment -= amountToDeposit;
                    }
                }
                else
                {
                    // Erode a fraction of the droplet's current carry capacity.
                    // Clamp the erosion to the change in height so that it doesn't dig a hole in the terrain behind the droplet
                    double amountToErode = Math.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);

                    //if (_debugMode && DebugTileErode != null)
                    //    DebugTileErode[posOldI.X, posOldI.Y] = amountToErode;

                    if (amountToErode != 0)
                    {
                        ApplyBrush(erodeBrush, oldPoint, posOld, -amountToErode);
                        sediment += amountToErode;
                    }
                }
                // Update delta
                deltaHeight = newHeight - CalculateHeightAndGradient(posOld, faceOld, _faces).Height;

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
        }

        // This code does not apply the duplicate pixel rule for map edges. Resulting height map is not seamless!
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
                                // This is a linear function. I don't like it.
                                double newWeight = 1 - distance / brushRadius;
                                // Lets use good old Euler and make the brush more pointy:
                                if (BrushPointiness > 0.0)
                                {
                                    newWeight *= Math.Exp(-BrushPointiness * distance);
                                }
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

                var brushPoint = brushPoints[ii];

                // Use sediment system for erosion
                if (_sedimentsEnabled)
                {
                    if (brushpart < 0) // Erode
                    {
                        bool skip = false;
                        // First erode deposited soft sediment.
                        double softErosionFactor = 2.0;
                        if (_depositedSedimentMap.TryGetValue(brushPoint.Face, out var map))
                        {
                            var depositedSediment = map[brushPoint.PosX, brushPoint.PosY];
                            if (depositedSediment > 0)
                            {
                                depositedSediment += brushpart * softErosionFactor; // brushpart is negative!
                                if (depositedSediment > 0)
                                {
                                    map[brushPoint.PosX, brushPoint.PosY] = depositedSediment;
                                    brushpart *= softErosionFactor;
                                    skip = true;
                                }
                                else
                                {
                                    // There is more stuff to erode, update value
                                    // Undo the factor  that we added
                                    brushpart = depositedSediment / softErosionFactor; // depositedSediment is negative!
                                }
                            }
                        }
                        if (!skip) // We enter here if no deposited sediment is left to erode
                        {
                            // We need to loop in case we erode through multiple layers of sediment
                            if (_sedimentLayerIndexes.TryGetValue(brushPoint.Face, out var indexes))
                            {
                                var index = indexes[brushPoint.PosX, brushPoint.PosY];
                                double layerThickness = 1d / 65536d;
                                double amountToErode = brushpart; // brushpart is negative !
                                double erodedAmount = 0;
                                double currentHeight = brushPoint.Value;
                                while (amountToErode < 0)
                                {
                                    var val = (ushort)Math.Min(65535, Math.Max(0, currentHeight * 65535));
                                    var hardness = _sedimentLayers[index][val];

                                    var remaining = currentHeight % layerThickness;
                                    if (remaining == 0) remaining = layerThickness; // Prevent endless loop
                                    // 32768 ist default
                                    // 0 is soft, 65535 is hard
                                    // Use linear function:
                                    // y = mx+b with b=1.9 and y(65536) = 0.1
                                    var factor = -1.8 * hardness / 65536 + 1.9;
                                    // Value range: 0.1 (Hard) .. .. 1.9 (Soft)

                                    var sedimentToBeEroded = amountToErode * factor;
                                    // Check how much we have to erode to reach next sediment layer


                                    if (-1 * sedimentToBeEroded > remaining)
                                    {
                                        // Only erode layer, then iterate
                                        erodedAmount -= remaining;
                                        currentHeight -= remaining;
                                        amountToErode += remaining;
                                        if (currentHeight <= 0) break;
                                    }
                                    else
                                    {
                                        // We eroded less that a layer, abort
                                        erodedAmount += sedimentToBeEroded;
                                        break;
                                    }
                                }
                                brushpart =  erodedAmount;
                            }
                        }
                    }
                    if (brushpart > 0) // Deposit
                    {
                        if (_depositedSedimentMap.TryGetValue(brushPoint.Face, out var map))
                        {
                            map[brushPoint.PosX, brushPoint.PosY] += brushpart;
                        }
                    }
                }
                brushPoint.Value += brushpart;
                if (brushPoint.Value > 1) brushPoint.Value = 1;
                else if (brushPoint.Value < 0) brushPoint.Value = 0;
            }

            // Try to make map seamless by equalizing neighboring points between faces.
            for (int ii = 0; ii < brushWeights.Count; ++ii)
            {
                var pt = brushPoints[ii];
                int dx = 0;
                int dy = 0;
                bool triplet = false;
                if (pt.PosX == 0 && pt.PosY > 0) dx = -1;
                else if (pt.PosX == 2047 && pt.PosY > 0) dx = 1;
                else if (pt.PosY == 0 && pt.PosX > 0) dy = -1;
                else if (pt.PosY == 2047 && pt.PosX > 0) dy = 1;
                else if (pt.PosX == 0 && pt.PosY == 0)
                {
                    triplet = true; dx = -1; dy = -1;
                }
                else if (pt.PosX == 2047 && pt.PosY == 0)
                {
                    triplet = true; dx = 1; dy = -1;
                }
                else if (pt.PosX == 0 && pt.PosY == 2047)
                {
                    triplet = true; dx = -1; dy = 1;
                }
                else if (pt.PosX == 2047 && pt.PosY == 2047)
                {
                    triplet = true; dx = 1; dy = 1;
                }
                else
                {
                    continue;
                }
                if (triplet)
                {
                    var n1 = pt.GetPointRelative(dx, 0);
                    var n2 = pt.GetPointRelative(0, dy);
                    var avg = (pt.Value + n1.Value + n2.Value) / 3;
                    pt.Value = avg;
                    n1.Value = avg;
                    n2.Value = avg;
                }
                else
                {
                    var n = pt.GetPointRelative(dx, dy);
                    var avg = (pt.Value + n.Value) / 2;
                    pt.Value = avg;
                    n.Value = avg;
                }
            }
        }

        public static HeightAndGradient CalculateHeightAndGradient(PointD pos, CubeMapFace face, Dictionary<CubeMapFace, double[,]> faces)
        {
            int coordX = (int)pos.X;
            int coordY = (int)pos.Y;
            // Calculate droplet's offset inside the cell (0,0) = at NW node, (1,1) = at SE node
            // Remarks: x and y are not coordinates, but offsets!
            double x = pos.X - coordX;
            double y = pos.Y - coordY;

            var point = new CubeMapPoint(faces, coordX, coordY, face);

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
    }
}

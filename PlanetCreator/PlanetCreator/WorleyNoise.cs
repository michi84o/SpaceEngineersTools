using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetCreator
{
    public class SphericalWorleyNoise
    {
        private readonly int _seed;
        private readonly int _numCells;
        private readonly Random _random;
        private readonly List<double[]> _cellPoints;
        private readonly double _radius; // Radius of sphere

        /// <summary>
        ///
        /// </summary>
        /// <param name="seed"></param>
        /// <param name="radius">Always use same radius as in generator (1000).</param>
        /// <param name="numCells">
        /// Warning, values above 20 take an eternity to finish!
        /// 40 will give structures with a diameter of 500m.
        /// Smaller values will give larger structures.</param>
        public SphericalWorleyNoise(int seed, double radius, int numCells = 20)
        {
            _seed = seed;
            _radius = radius;
            _numCells = numCells;
            _random = new Random(seed);

            int numCellsCubed = _numCells * _numCells * _numCells;
            _cellPoints = Enumerable.Repeat<double[]>(null, numCellsCubed).ToList();

            Parallel.For(0, numCellsCubed, i =>
            {
                // Random points within cube [-1, 1]
                double x, y, z;
                lock (_random)
                {
                    x = 2.0 * _random.NextDouble() - 1.0;
                    y = 2.0 * _random.NextDouble() - 1.0;
                    z = 2.0 * _random.NextDouble() - 1.0;
                }
                // Normalize to get values within sphere
                double magnitude = Math.Sqrt(x * x + y * y + z * z);
                if (magnitude > 0)
                {
                    _cellPoints[i] = new double[] { x / magnitude, y / magnitude, z / magnitude };
                }
            });
        }

        public double GetValue3D(double x, double y, double z)
        {
            // We expect that (x, y, z) is a surface point on the sphere.
            // Length of vector should be radius.
            double magnitude = Math.Sqrt(x * x + y * y + z * z);
            if (Math.Abs(magnitude - _radius) > 1e-6)
            {
                // Optional: Scale point to unit sphere for search
                x /= magnitude;
                y /= magnitude;
                z /= magnitude;
            }

            double minDistSq = double.MaxValue;

            // GetValue3D is already beeing called from parallel for-loop.
            //Parallel.ForEach(_cellPoints, cellPoint =>
            foreach (var cellPoint in _cellPoints)
            {
                double dx = x - cellPoint[0];
                double dy = y - cellPoint[1];
                double dz = z - cellPoint[2];
                double distSq = dx * dx + dy * dy + dz * dz;
                minDistSq = Math.Min(minDistSq, distSq);
            };//);

            return Math.Sqrt(minDistSq);
        }
    }


    //   public class WorleyNoise
    //   {
    //	private readonly int _seed;
    //	private readonly int _numCells;
    //	private readonly double _frequency;
    //	private readonly Random _random;
    //	private readonly List<double[]> _cellPoints;

    //	public WorleyNoise(int seed, int numCells = 20, double frequency = 1.0)
    //	{
    //		_seed = seed;
    //		_numCells = numCells;
    //		_frequency = frequency;
    //		_random = new Random(seed);
    //		_cellPoints = new List<double[]>();
    //		GenerateCellPoints();
    //	}

    //	private void GenerateCellPoints()
    //	{
    //		for (int x = 0; x < _numCells; x++)
    //		{
    //			for (int y = 0; y < _numCells; y++)
    //			{
    //				for (int z = 0; z < _numCells; z++)
    //				{
    //					double rx = _random.NextDouble();
    //					double ry = _random.NextDouble();
    //					double rz = _random.NextDouble();
    //					_cellPoints.Add(new double[] { x + rx, y + ry, z + rz });
    //				}
    //			}
    //		}
    //	}

    //	public double GetValue3D(double x, double y, double z)
    //	{
    //		x *= _frequency;
    //		y *= _frequency;
    //		z *= _frequency;

    //		int floorX = (int)Math.Floor(x);
    //		int floorY = (int)Math.Floor(y);
    //		int floorZ = (int)Math.Floor(z);

    //		double minDistSq = double.MaxValue;

    //		for (int offsetX = -1; offsetX <= 1; offsetX++)
    //		{
    //			for (int offsetY = -1; offsetY <= 1; offsetY++)
    //			{
    //				for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
    //				{
    //					int cellX = floorX + offsetX;
    //					int cellY = floorY + offsetY;
    //					int cellZ = floorZ + offsetZ;

    //					// Hash the cell coordinates to get a consistent random index
    //					int cellIndex = Hash(cellX, cellY, cellZ) % _cellPoints.Count;
    //					double[] cellPoint = _cellPoints[cellIndex];

    //					double dx = x - cellPoint[0];
    //					double dy = y - cellPoint[1];
    //					double dz = z - cellPoint[2];
    //					double distSq = dx * dx + dy * dy + dz * dz;

    //					minDistSq = Math.Min(minDistSq, distSq);
    //				}
    //			}
    //		}

    //		return Math.Sqrt(minDistSq); // Return the actual distance
    //	}

    //	private int Hash(int x, int y, int z)
    //	{
    //		// A simple hashing function to ensure consistent random access
    //		return (1619 * x + 6949 * y + 829 * z + _seed) & 0x7FFFFFFF;
    //	}
    //}
}

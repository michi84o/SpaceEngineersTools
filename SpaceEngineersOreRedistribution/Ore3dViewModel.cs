using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpaceEngineersOreRedistribution
{
    public class Ore3dViewModel : PropChangeNotifier
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<ModelVisual3D> Cuboids { get; } = new ObservableCollection<ModelVisual3D>();

        int _rectSize = 20;
        public int RectSize
        {
            get => _rectSize;
            set => SetProp(ref _rectSize, value);
        }


        double _radius = 120;
        public double Radius
        {
            get => _radius;
            set
            {
                if (SetProp(ref _radius, value))
                {
                    UpdateCamera();
                }
            }
        }

        double _longitude = 270;
        public double Longitude
        {
            get => _longitude;
            set
            {
                if (SetProp(ref _longitude, value))
                {
                    UpdateCamera();
                }
            }
        }

        double _latitude = 45;
        public double Latitude
        {
            get => _latitude;
            set
            {
                if (SetProp(ref _latitude, value))
                {
                    UpdateCamera();
                }
            }
        }

        double _zOffset = 0;
        public double ZOffset
        {
            get => _zOffset;
            set
            {
                if (SetProp(ref _zOffset, value))
                {
                    UpdateCamera();
                }
            }
        }

        void UpdateCamera()
        {
            // Assume center of spere is ground center
            My3dHelper.CalculateCamera(Radius, Longitude, Latitude, ZOffset, RectSize, out var direction, out var position);
            CamDir = direction;
            CamPos = new Point3D(position.X, position.Y, position.Z);
        }

        double _camX = 40; double _camY; double _camZ;
        public double CamX
        {
            get => _camX;
            set => SetProp(ref _camX, value);
        }
        public double CamY
        {
            get => _camY;
            set => SetProp(ref _camY, value);
        }
        public double CamZ
        {
            get => _camZ;
            set => SetProp(ref _camZ, value);
        }

        double _camDistX = 50;
        public double CamDistX
        {
            get => _camDistX;
            set
            {
                if (SetProp(ref _camDistX, value))
                {
                    CamPos = new Point3D(value, CamDistY, CamDistZ);
                }
            }
        }

        double _camDistY = 40;
        public double CamDistY
        {
            get => _camDistY;
            set
            {
                if (SetProp(ref _camDistY, value))
                {
                    CamPos = new Point3D(CamDistX, value, CamDistZ);
                }
            }
        }

        double _camDistZ = 60;
        public double CamDistZ
        {
            get => _camDistZ;
            set
            {
                if (SetProp(ref _camDistZ, value))
                {
                    CamPos = new Point3D(CamDistX, CamDistY, value);
                }
            }
        }

        double _camDirX = 0;
        public double CamDirX
        {
            get => _camDirX;
            set
            {
                if (SetProp(ref _camDirX, value))
                {
                    CamDir = new Vector3D(value, CamDirY, CamDirZ);
                }
            }
        }

        double _camDirY = 0;
        public double CamDirY
        {
            get => _camDirY;
            set
            {
                if (SetProp(ref _camDirY, value))
                {
                    CamDir = new Vector3D(CamDirX, value, CamDirZ);
                }
            }
        }

        double _camDirZ = -1;
        public double CamDirZ
        {
            get => _camDirZ;
            set
            {
                if (SetProp(ref _camDirZ, value))
                {
                    CamDir = new Vector3D(CamDirX, CamDirY, value);
                }
            }
        }

        Vector3D _camDir = new Vector3D(0, 0, -1);
        public Vector3D CamDir
        {
            get => _camDir;
            set => SetProp(ref _camDir, value);
        }


        Point3D _camPos = new Point3D(50,40,120);
        public Point3D CamPos
        {
            get => _camPos;
            set => SetProp(ref _camPos, value);
        }

        public Ore3dViewModel()
        {
            UpdateCamera();
        }

        public void AddCuboids(OreMapping[,] oreMap)
        {
            Cuboids.Clear();

            for (int x = 0; x < oreMap.GetLength(0); ++x)
            {
                for (int y = 0; y < oreMap.GetLength(1); ++y)
                {
                    if (oreMap[x, y] == null) continue;
                    var map = oreMap[x, y];
                    // Depth & Start are in meters
                    // Base unit is 50m
                    var start = map.Start / 50.0;
                    var depth = map.Depth / 50.0;
                    Cuboids.Add(My3dHelper.CreateCuboid(x, oreMap.GetLength(1) - y, start, depth, map.MapRgbValue));
                }
            }

            //Cuboids.Add(My3dHelper.CreateCuboid(0, 0, 0, 2));
            //Cuboids.Add(My3dHelper.CreateCuboid(-2, 0, 0, 1));
        }
    }
}

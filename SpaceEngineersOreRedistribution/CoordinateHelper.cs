using SpaceEngineersToolsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpaceEngineersOreRedistribution
{
    public static class CoordinateHelper
    {
        public static Point3D GetNormalizedSphereCoordinates(CubeMapFace face, int x, int y, double cubeWidth)
        {
            Point3D origin = new Point3D();

            // It makes sense if you draw a picture. See below
            double offset = (cubeWidth - 1) / 2.0;
            // w=5: [0][1][2][3][4] -> middle = 2,   [0] on 3d axis = -2
            //             | middle (0 in xyz)
            // w=4   [0][1]|[2][3]  -> middle = 1.5, [0] on 3d axis = -1.5

            // offset at 2048 -> 1023.5

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

        // google Bard probably stole this from somewhere.
        // It forgot the -90 for latitude despite me telling it to output -90 to +90. Fail!
        public static (double longitude, double latitude) ToLongitudeLatitude(this Point3D point)
        {
            double longitude = Math.Atan2(point.Y, point.X) * 180 / Math.PI;
            if (longitude < 0)
            {
                longitude += 360;
            }

            double latitude = Math.Acos(point.Z / 1) * 180 / Math.PI;
            if (latitude < 0)
            {
                latitude += 180;
            }

            return (longitude, latitude-90);
        }
    }
}

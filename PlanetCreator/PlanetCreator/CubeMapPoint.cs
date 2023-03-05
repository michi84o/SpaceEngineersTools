using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PlanetCreator
{
    struct CubeMapPointLight
    {
        public int X;
        public int Y;
        public CubeMapFace Face;

        public static double GetValue(CubeMapPointLight point, Dictionary<CubeMapFace, double[,]> faces)
        {
            return faces[point.Face][point.X, point.Y];
        }

        public static CubeMapPointLight GetPointRelativeTo(CubeMapPointLight origin, int dx, int dy)
        {
            var x = origin.X + dx;
            var y = origin.Y + dy;
            if (x >= 0 && x < 2048 && y >= 0 && y < 2048) return new CubeMapPointLight { X = x, Y = y, Face = origin.Face };

            int backup;
            // Move in X direction first:
            var currentFace = origin.Face;
            var currentX = x;
            var currentY = y;
            if (currentX < 0) // West
            {
                switch (currentFace)
                {
                    case CubeMapFace.Up:
                        // West of 'Up' is 'Left', rotated clockwise by 90°
                        // x/y flipped!
                        backup = currentX;
                        currentX = origin.Y;
                        currentY = (-1 * backup) - 1;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                                Face = CubeMapFace.Left,
                                X = currentX,
                                Y = currentY,
                            }, dy, 0);
                    case CubeMapFace.Down:
                        // West of 'Down' is 'Right', rotated counterclockwise by 90°
                        // x/y flipped!
                        currentY = 2048 + currentX;
                        currentX = 2047 - origin.Y;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                               Face = CubeMapFace.Right,
                                X = currentX,
                                Y = currentY,
                            }, -dy, 0);
                    case CubeMapFace.Left:
                        // West of 'Left' is 'Back'
                        currentX += 2048;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Right:
                        // West of 'Right' is 'Front'
                        currentX += 2048;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Front:
                        // West of 'Right' is 'Left'
                        currentX += 2048;
                        currentFace = CubeMapFace.Left;
                        break;
                    case CubeMapFace.Back:
                        // West of 'Back' is 'Right'
                        currentX += 2048;
                        currentFace = CubeMapFace.Right;
                        break;
                }
            }
            else if (currentX > 2047) // East
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // East of 'Up' is 'Right' rotated counterclockwise by 90°
                        // x/y flipped! dy & velocityXY must be converted!
                        currentY = currentX - 2048;
                        currentX = 2047 - origin.Y;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                                Face = CubeMapFace.Right,
                                X = currentX,
                                Y = currentY,
                            }, -dy, 0);
                    case CubeMapFace.Down:
                        // East of 'Down' is 'Left', rotated clockwise by 90°
                        // x/y flipped! dy & velocityXY must be converted!
                        currentY = (2047 + 2048) - currentX; // range: 2047..->..0 bottom to top
                        currentX = origin.Y;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                               Face = CubeMapFace.Left,
                               X = currentX,
                               Y = currentY,
                            }, dy, 0);
                    case CubeMapFace.Left:
                        // East of 'Left' is 'Front'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Right:
                        // East of 'Right' is 'Back'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Front:
                        // East of 'Front' is 'Right'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Right;
                        break;
                    case CubeMapFace.Back:
                        // East of 'Back' is 'Left'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Left;
                        break;
                }
            }

            // Now move in Y direction:
            currentY = currentY + dy;
            if (currentY < 0) // North
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // North of 'Up' is 'Back' rotated by 180°
                        currentX = 2047 - currentX;
                        currentY = (-1 * currentY) - 1;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Down:
                        // North of 'Down' is 'Back'
                        currentY = 2048 + currentY;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Left:
                        // North of 'Left' is 'Up' rotated counterclockwise by 90°
                        backup = currentX;
                        currentX = (-1 * currentY) - 1;
                        currentY = backup;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Right:
                        // North of 'Right' is 'Up' rotated clockwise by 90!
                        backup = currentX;
                        currentX = currentY + 2048;
                        currentY = 2047 - backup;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Front:
                        // North of 'Front' is 'Up'
                        currentY = currentY + 2048;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Back:
                        // North of 'Back is 'Up' rotated by 180°
                        backup = currentX;
                        currentX = 2047 - currentX;
                        currentY = (-1 * currentY) - 1;
                        currentFace = CubeMapFace.Up;
                        break;
                }
            }
            else if (currentY > 2047) // South
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // South of 'Up' is 'Front'
                        currentY = currentY - 2048;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Down:
                        // South of 'Down' is 'Front' rotated by 180°
                        currentX = 2047 - currentX;
                        currentY = (2047 + 2048) - currentY;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Left:
                        // South of 'Left' is 'Down' rotated counterclockwise by 90°
                        backup = currentX;
                        currentX = (2047 + 2048) - currentY;
                        currentY = backup;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Right:
                        // South of 'Right' is 'Down' rotated clockwise by 90°
                        backup = currentX;
                        currentX = currentY - 2048;
                        currentY = 2047 - backup;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Front:
                        // South of 'Front' is 'Down' rotated by 180°
                        backup = currentX;
                        currentX = 2047 - currentX;
                        currentY = (2047 + 2048) - currentY;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Back:
                        // South of 'Back' is 'Down'
                        currentY = currentY - 2048;
                        currentFace = CubeMapFace.Down;
                        break;
                }
            }
            return new CubeMapPointLight
            {
                Face = currentFace,
                X = currentX,
                Y = currentY
            };
        }
    }

    class CubeMapPoint
    {
        public CubeMapFace Face { get; set; } = CubeMapFace.Up;

        public double Value
        {
            get => _faces[Face][PosX, PosY];
            set => _faces[Face][PosX, PosY] = value;
        }

        // 0 <= offset < 1
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }


        public int PosX { get; set; }
        public int PosY { get; set; }

        public double VelocityX;
        public double VelocityY;
        public double VelocityLength => Math.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY);

        Dictionary<CubeMapFace, double[,]> _faces;

        public CubeMapPoint(Dictionary<CubeMapFace, double[,]> faces, int posX, int posY, CubeMapFace face)
        {
            _faces = faces;
            Face = face;
            PosX = posX;
            PosY = posY;
        }

        public CubeMapPoint Clone()
        {
            return new CubeMapPoint(_faces, PosX, PosY, Face)
            {
                VelocityX = VelocityX,
                VelocityY = VelocityY
            };
        }

        public static void CalculateDirection(double vx, double vy, out int dx, out int dy)
        {
            dx = 0;
            dy = 0;

            if (vy == 0 && vx == 0) return;

            var angle = Math.Atan2(vy, vx) * 180 / Math.PI; ;
            if (angle >= -22.5 && angle < 22.5)
            {
                dx = 1;
            }
            else if (angle >= 22.5 && angle < 67.5)
            {
                dx = 1;
                dy = 1;
            }
            else if (angle >= 67.5 && angle < 112.5)
            {
                dy = 1;
            }
            else if (angle >= 112.5 && angle < 157.5)
            {
                dx = -1;
                dy = 1;
            }
            else if (angle >= 157.5 || angle < -157.5)
            {
                dx = -1;
            }
            else if (angle >= -157.5 && angle < -112.5)
            {
                dx = -1;
                dy = -1;
            }
            else if (angle >= -112.5 && angle < -67.5)
            {
                dy = -1;
            }
            else
            {
                dx = 1;
                dy = -1;
            }
        }

        //[Obsolete("Use instance method")]
        //public static CubeMapPoint GetPointRelativeTo(CubeMapPoint origin, int dx, int dy)
        //{
        //    return GetPointRelativeTo(origin, dx, dy, origin._faces);
        //}

        public CubeMapPoint GetPointRelative(int dx, int dy)
        {
            return GetPointRelativeTo(this, dx, dy, _faces);
        }

        /// <summary>
        /// Do not use dx or dy values greater than 2048!!!
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        static CubeMapPoint GetPointRelativeTo(CubeMapPoint origin, int dx, int dy, Dictionary<CubeMapFace, double[,]> faces)
        {
            if (dx == 0 && dy == 0)
                return origin.Clone();

            int backup;
            // Move in X direction first:
            var currentFace = origin.Face;
            var currentX = origin.PosX + dx;
            var currentY = origin.PosY;
            if (currentX < 0) // West
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // West of 'Up' is 'Left', rotated clockwise by 90°
                        // x/y flipped!
                        backup = currentX;
                        currentX = origin.PosY;
                        currentY = (-1 * backup) - 1;
                        return GetPointRelativeTo(
                            new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Left)
                            {
                                VelocityX = origin.VelocityY,
                                VelocityY = -1 * origin.VelocityX,
                                OffsetX = origin.OffsetY,
                                OffsetY = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0)
                            }, dy, 0, faces);
                    case CubeMapFace.Down:
                        // West of 'Down' is 'Right', rotated counterclockwise by 90°
                        // x/y flipped!
                        currentY = 2048 + currentX;
                        currentX = 2047 - origin.PosY;
                        return GetPointRelativeTo(
                            new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Right)
                            {
                                VelocityX = -1 * origin.VelocityY,
                                VelocityY = origin.VelocityX,
                                OffsetX = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0),
                                OffsetY = origin.OffsetX,
                            }, -dy, 0, faces);
                    case CubeMapFace.Left:
                        // West of 'Left' is 'Back'
                        currentX += 2048;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Right:
                        // West of 'Right' is 'Front'
                        currentX += 2048;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Front:
                        // West of 'Right' is 'Left'
                        currentX += 2048;
                        currentFace = CubeMapFace.Left;
                        break;
                    case CubeMapFace.Back:
                        // West of 'Back' is 'Right'
                        currentX += 2048;
                        currentFace = CubeMapFace.Right;
                        break;
                }
            }
            else if (currentX > 2047) // East
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // East of 'Up' is 'Right' rotated counterclockwise by 90°
                        // x/y flipped! dy & velocityXY must be converted!
                        currentY = currentX - 2048;
                        currentX = 2047 - origin.PosY;
                        return GetPointRelativeTo(
                            new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Right)
                            {
                                VelocityX = -1 * origin.VelocityY,
                                VelocityY = origin.VelocityX,
                                OffsetX = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0),
                                OffsetY = origin.OffsetX
                            }, -dy, 0, faces);
                    case CubeMapFace.Down:
                        // East of 'Down' is 'Left', rotated clockwise by 90°
                        // x/y flipped! dy & velocityXY must be converted!
                        currentY = (2047 + 2048) - currentX; // range: 2047..->..0 bottom to top
                        currentX = origin.PosY;
                        return GetPointRelativeTo(
                            new CubeMapPoint(faces, currentX, currentY, CubeMapFace.Left)
                            {
                                VelocityX = origin.VelocityY,
                                VelocityY = -1 * origin.VelocityX,
                                OffsetX = origin.OffsetY,
                                OffsetY = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0)
                            }, dy, 0, faces);
                    case CubeMapFace.Left:
                        // East of 'Left' is 'Front'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Right:
                        // East of 'Right' is 'Back'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Front:
                        // East of 'Front' is 'Right'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Right;
                        break;
                    case CubeMapFace.Back:
                        // East of 'Back' is 'Left'
                        currentX = currentX - 2048;
                        currentFace = CubeMapFace.Left;
                        break;
                }
            }

            // Now move in Y direction:
            currentY = currentY + dy;
            var currentVelocityX = origin.VelocityX;
            var currentVelocityY = origin.VelocityY;
            var currentOffsetX = origin.OffsetX;
            var currentOffsetY = origin.OffsetY;
            if (currentY < 0) // North
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // North of 'Up' is 'Back' rotated by 180°
                        currentX = 2047 - currentX;
                        currentY = (-1 * currentY) - 1;
                        currentVelocityX = -1 * origin.VelocityX;
                        currentVelocityY = -1 * origin.VelocityY;
                        currentOffsetX = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0);
                        currentOffsetY = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0);
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Down:
                        // North of 'Down' is 'Back'
                        currentY = 2048 + currentY;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Left:
                        // North of 'Left' is 'Up' rotated counterclockwise by 90°
                        backup = currentX;
                        currentX = (-1 * currentY) - 1;
                        currentY = backup;
                        currentVelocityX = -1 * origin.VelocityY;
                        currentVelocityY = origin.VelocityX;
                        currentOffsetX = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0);
                        currentOffsetY = origin.OffsetX;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Right:
                        // North of 'Right' is 'Up' rotated clockwise by 90!
                        backup = currentX;
                        currentX = currentY + 2048;
                        currentY = 2047 - backup;
                        currentVelocityX = origin.VelocityY;
                        currentVelocityY = -1 * origin.VelocityX;
                        currentOffsetX = origin.OffsetY;
                        currentOffsetY = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0);
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Front:
                        // North of 'Front' is 'Up'
                        currentY = currentY + 2048;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Back:
                        // North of 'Back is 'Up' rotated by 180°
                        backup = currentX;
                        currentX = 2047 - currentX;
                        currentY = (-1 * currentY) - 1;
                        currentVelocityX = -1 * origin.VelocityX;
                        currentVelocityY = -1 * origin.VelocityY;
                        currentOffsetX = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0);
                        currentOffsetY = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0);
                        currentFace = CubeMapFace.Up;
                        break;
                }
            }
            else if (currentY > 2047) // South
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // South of 'Up' is 'Front'
                        currentY = currentY - 2048;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Down:
                        // South of 'Down' is 'Front' rotated by 180°
                        currentX = 2047 - currentX;
                        currentY = (2047 + 2048) - currentY;
                        currentVelocityX = currentVelocityX * -1;
                        currentVelocityY = currentVelocityY * -1;
                        currentOffsetX = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0);
                        currentOffsetY = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0);
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Left:
                        // South of 'Left' is 'Down' rotated counterclockwise by 90°
                        backup = currentX;
                        currentX = (2047 + 2048) - currentY;
                        currentY = backup;
                        currentVelocityX = -1 * origin.VelocityY;
                        currentVelocityY = origin.VelocityX;
                        currentOffsetX = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0);
                        currentOffsetY = origin.OffsetX;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Right:
                        // South of 'Right' is 'Down' rotated clockwise by 90°
                        backup = currentX;
                        currentX = currentY - 2048;
                        currentY = 2047 - backup;
                        currentVelocityX = origin.VelocityY;
                        currentVelocityY = -1 * origin.VelocityX;
                        currentOffsetX = origin.OffsetY;
                        currentOffsetY = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0);
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Front:
                        // South of 'Front' is 'Down' rotated by 180°
                        backup = currentX;
                        currentX = 2047 - currentX;
                        currentY = (2047 + 2048) - currentY;
                        currentVelocityX = -1 * origin.VelocityX;
                        currentVelocityY = -1 * origin.VelocityY;
                        currentOffsetX = (origin.OffsetX > 0 ? 1 - origin.OffsetX : 0);
                        currentOffsetY = (origin.OffsetY > 0 ? 1 - origin.OffsetY : 0);
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Back:
                        // South of 'Back' is 'Down'
                        currentY = currentY - 2048;
                        currentFace = CubeMapFace.Down;
                        break;
                }
            }
            return new CubeMapPoint(faces, currentX, currentY, currentFace)
            {
                VelocityX = currentVelocityX,
                VelocityY = currentVelocityY,
                OffsetX = currentOffsetX,
                OffsetY = currentOffsetY,
            };
        }

    }

}

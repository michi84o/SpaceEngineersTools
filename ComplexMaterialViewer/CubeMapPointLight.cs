using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComplexMaterialViewer
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
}

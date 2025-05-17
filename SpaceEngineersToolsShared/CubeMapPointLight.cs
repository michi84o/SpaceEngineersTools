using MathNet.Numerics.RootFinding;
using System.Collections.Generic;

namespace SpaceEngineersToolsShared
{
    public struct CubeMapPointLight
    {
        public int X;
        public int Y;
        public CubeMapFace Face;
        public ushort TileWidth = 2048;

        public CubeMapPointLight()
        {
            X = 0;
            Y = 0;
            Face = CubeMapFace.Front;
            TileWidth = 2048;
        }

        public CubeMapPointLight(CubeMapFace face, int x, int y, int tileWidth = 2048)
        {
            Face = face;
            X = x;
            Y = y;
            TileWidth = (ushort)tileWidth;
        }

        public static double GetValue(CubeMapPointLight point, Dictionary<CubeMapFace, double[,]> faces)
        {
            return faces[point.Face][point.X, point.Y];
        }

        public static CubeMapPointLight GetPointRelativeTo(CubeMapPointLight origin, int dx, int dy)
        {
            var x = origin.X + dx;
            var y = origin.Y + dy;
            if (x >= 0 && x < origin.TileWidth && y >= 0 && y < origin.TileWidth) return new CubeMapPointLight { X = x, Y = y, Face = origin.Face, TileWidth=origin.TileWidth };

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
                                TileWidth=origin.TileWidth
                            }, dy, 0);
                    case CubeMapFace.Down:
                        // West of 'Down' is 'Right', rotated counterclockwise by 90°
                        // x/y flipped!
                        currentY = origin.TileWidth + currentX;
                        currentX = (origin.TileWidth-1) - origin.Y;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                               Face = CubeMapFace.Right,
                                X = currentX,
                                Y = currentY,
                                TileWidth = origin.TileWidth
                            }, (ushort)-dy, 0);
                    case CubeMapFace.Left:
                        // West of 'Left' is 'Back'
                        currentX += origin.TileWidth;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Right:
                        // West of 'Right' is 'Front'
                        currentX += origin.TileWidth;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Front:
                        // West of 'Right' is 'Left'
                        currentX += origin.TileWidth;
                        currentFace = CubeMapFace.Left;
                        break;
                    case CubeMapFace.Back:
                        // West of 'Back' is 'Right'
                        currentX += origin.TileWidth;
                        currentFace = CubeMapFace.Right;
                        break;
                }
            }
            else if (currentX > (origin.TileWidth-1)) // East
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // East of 'Up' is 'Right' rotated counterclockwise by 90°
                        // x/y flipped! dy & velocityXY must be converted!
                        currentY = currentX - origin.TileWidth;
                        currentX = (origin.TileWidth-1) - origin.Y;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                                Face = CubeMapFace.Right,
                                X = currentX,
                                Y = currentY,
                                TileWidth = origin.TileWidth
                            }, -dy, 0);
                    case CubeMapFace.Down:
                        // East of 'Down' is 'Left', rotated clockwise by 90°
                        // x/y flipped! dy & velocityXY must be converted!
                        currentY = ((origin.TileWidth-1) + origin.TileWidth) - currentX; // range: (origin.TileWidth-1)..->..0 bottom to top
                        currentX = origin.Y;
                        return GetPointRelativeTo(
                            new CubeMapPointLight
                            {
                               Face = CubeMapFace.Left,
                               X = currentX,
                               Y = currentY,
                               TileWidth= origin.TileWidth
                            }, dy, 0);
                    case CubeMapFace.Left:
                        // East of 'Left' is 'Front'
                        currentX = currentX - origin.TileWidth;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Right:
                        // East of 'Right' is 'Back'
                        currentX = currentX - origin.TileWidth;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Front:
                        // East of 'Front' is 'Right'
                        currentX = currentX - origin.TileWidth;
                        currentFace = CubeMapFace.Right;
                        break;
                    case CubeMapFace.Back:
                        // East of 'Back' is 'Left'
                        currentX = currentX - origin.TileWidth;
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
                        currentX = (origin.TileWidth-1) - currentX;
                        currentY = (-1 * currentY) - 1;
                        currentFace = CubeMapFace.Back;
                        break;
                    case CubeMapFace.Down:
                        // North of 'Down' is 'Back'
                        currentY = origin.TileWidth + currentY;
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
                        currentX = currentY + origin.TileWidth;
                        currentY = (origin.TileWidth-1) - backup;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Front:
                        // North of 'Front' is 'Up'
                        currentY = currentY + origin.TileWidth;
                        currentFace = CubeMapFace.Up;
                        break;
                    case CubeMapFace.Back:
                        // North of 'Back is 'Up' rotated by 180°
                        backup = currentX;
                        currentX = (origin.TileWidth-1) - currentX;
                        currentY = (-1 * currentY) - 1;
                        currentFace = CubeMapFace.Up;
                        break;
                }
            }
            else if (currentY > (origin.TileWidth-1)) // South
            {
                switch (origin.Face)
                {
                    case CubeMapFace.Up:
                        // South of 'Up' is 'Front'
                        currentY = currentY - origin.TileWidth;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Down:
                        // South of 'Down' is 'Front' rotated by 180°
                        currentX = (origin.TileWidth-1) - currentX;
                        currentY = ((origin.TileWidth-1) + origin.TileWidth) - currentY;
                        currentFace = CubeMapFace.Front;
                        break;
                    case CubeMapFace.Left:
                        // South of 'Left' is 'Down' rotated counterclockwise by 90°
                        backup = currentX;
                        currentX = ((origin.TileWidth-1) + origin.TileWidth) - currentY;
                        currentY = backup;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Right:
                        // South of 'Right' is 'Down' rotated clockwise by 90°
                        backup = currentX;
                        currentX = currentY - origin.TileWidth;
                        currentY = (origin.TileWidth-1) - backup;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Front:
                        // South of 'Front' is 'Down' rotated by 180°
                        backup = currentX;
                        currentX = (origin.TileWidth-1) - currentX;
                        currentY = ((origin.TileWidth-1) + origin.TileWidth) - currentY;
                        currentFace = CubeMapFace.Down;
                        break;
                    case CubeMapFace.Back:
                        // South of 'Back' is 'Down'
                        currentY = currentY - origin.TileWidth;
                        currentFace = CubeMapFace.Down;
                        break;
                }
            }
            return new CubeMapPointLight
            {
                Face = currentFace,
                X = currentX,
                Y = currentY,
                TileWidth = origin.TileWidth
            };
        }

        public List<CubeMapPointLight> GetNeighbors()
        {
            List<CubeMapPointLight> neighbors = new();
            neighbors.Add(GetPointRelativeTo(this, -1, 0));
            neighbors.Add(GetPointRelativeTo(this,  1, 0));
            neighbors.Add(GetPointRelativeTo(this,  0, -1));
            neighbors.Add(GetPointRelativeTo(this,  0,  1));
            return neighbors;
        }
    }

}

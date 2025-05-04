using System;

namespace SpaceEngineersToolsShared
{
    public struct PointD
    {
        public double X;
        public double Y;

        public static PointD operator +(PointD p1, PointD p2) => new PointD { X = p1.X + p2.X, Y = p1.Y + p2.Y };
        public static PointD operator +(PointD p1) => p1;
        public static PointD operator -(PointD p1, PointD p2) => new PointD { X = p1.X - p2.X, Y = p1.Y - p2.Y };
        public static PointD operator -(PointD p1) => new PointD { X = -p1.X, Y = -p1.Y };
        public static PointD operator /(PointD p1, double d) => new PointD { X = p1.X / d, Y = p1.Y / d };
        public static PointD operator /(PointD p1, PointD p2) => new PointD { X = p1.X / p2.X, Y = p1.Y / p2.Y };
        public static PointD operator *(PointD p1, double d) => new PointD { X = p1.X * d, Y = p1.Y * d };
        public static PointD operator *(PointD p1, PointD p2) => new PointD { X = p1.X * p2.X, Y = p1.Y * p2.Y };

        public double Length => Math.Sqrt(X* X + Y* Y);

        public PointD Normalize()
        {
            var len = Length;
            if (len == 0) return this;
            return this / len;
        }

        // !!! (int)(-0.1) = 0 !!! But we want -1 !!!
        public PointI ToIntegerPoint() => new PointI() { X = X < 0? (int)(X-1) : (int)X, Y = Y < 0 ? (int)(Y - 1) : (int)Y };
        public PointD IntegerOffset() => new PointD() { X = X - (int)X, Y = Y - (int)Y };
    }

}

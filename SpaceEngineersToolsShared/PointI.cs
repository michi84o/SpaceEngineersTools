using System;

namespace SpaceEngineersToolsShared
{
    public struct PointI
    {
        public int X;
        public int Y;
        public double DistanceTo(PointI p)
        {
            var dx = p.X - X;
            var dy = p.Y - Y;
            return Math.Sqrt(1.0* dx * dx + dy * dy);
        }
        public bool IsWithinRadius(PointI p, double radius)
        {
            var dx = p.X - X;
            var dy = p.Y - Y;
            if (dx < - radius || dx > radius || dy < -radius || dy > radius) return false;
            return Math.Sqrt(1.0 * dx * dx + dy * dy) <= radius;
        }
    }

}

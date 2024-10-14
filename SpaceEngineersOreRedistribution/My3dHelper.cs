using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace SpaceEngineersOreRedistribution
{
    public static class My3dHelper
    {
        const double BaseUnit = 5;
        const double DepthUnit = 5;
        public static ModelVisual3D CreateCuboid(int x, int y, double start, double depth)
        {
            var z1 = -1 * start * DepthUnit;
            var z2 = (-1 * start * DepthUnit - depth * DepthUnit);
            var x1 = x * BaseUnit;
            var x2 = x * BaseUnit + BaseUnit;
            var y1 = y * BaseUnit;
            var y2 = y * BaseUnit + BaseUnit;

            Model3DGroup cuboid = new Model3DGroup();

            Point3D p0 = new Point3D(x1, y1, z1);
            Point3D p1 = new Point3D(x2, y1, z1);
            Point3D p2 = new Point3D(x2, y2, z1);
            Point3D p3 = new Point3D(x1, y2, z1);
            Point3D p4 = new Point3D(x1, y1, z2);
            Point3D p5 = new Point3D(x2, y1, z2);
            Point3D p6 = new Point3D(x2, y2, z2);
            Point3D p7 = new Point3D(x1, y2, z2);

            // Don't change order of points inside CreateTriangle
            // or surface direction might be incorrect.
            // Pattern:
            // (a, b, c) // First point identical
            // (a, c, d) // c crossed
            // flip b & d to change surface direction

            var red = 255 - (int)(start*50);

            var brush = new SolidColorBrush(Color.FromArgb(128, (byte)red, 0, 0));

            //front
            cuboid.Children.Add(CreateTriangle(p0, p1, p2, brush));
            cuboid.Children.Add(CreateTriangle(p0, p2, p3, brush));

            //right
            cuboid.Children.Add(CreateTriangle(p1, p5, p6, brush));
            cuboid.Children.Add(CreateTriangle(p1, p6, p2, brush));

            //back
            cuboid.Children.Add(CreateTriangle(p6, p5, p4, brush));
            cuboid.Children.Add(CreateTriangle(p6, p4, p7, brush));

            //left
            cuboid.Children.Add(CreateTriangle(p0, p3, p7, brush));
            cuboid.Children.Add(CreateTriangle(p0, p7, p4, brush));

            //top
            cuboid.Children.Add(CreateTriangle(p3, p2, p6, brush));
            cuboid.Children.Add(CreateTriangle(p3, p6, p7, brush));

            //bottom
            cuboid.Children.Add(CreateTriangle(p0, p4, p5, brush));
            cuboid.Children.Add(CreateTriangle(p0, p5, p1, brush));

            ModelVisual3D model = new ModelVisual3D();
            model.Content = cuboid;
            return model;
        }

        public static ModelVisual3D CreateSurface()
        {
            Model3DGroup surface = new Model3DGroup();

            var p0 = new Point3D(0 * BaseUnit, 0 * BaseUnit, .1);
            var p1 = new Point3D(20 * BaseUnit, 0 * BaseUnit, .1);
            var p2 = new Point3D(0 * BaseUnit, 20 * BaseUnit, .1);
            var p3 = new Point3D(20 * BaseUnit, 20 * BaseUnit, .1);

            var brush = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128));

            surface.Children.Add(CreateTriangle(p2, p0, p1, brush));
            surface.Children.Add(CreateTriangle(p2, p1, p3, brush));

            ModelVisual3D model = new ModelVisual3D();
            model.Content = surface;

            return model;
        }

        static public Model3DGroup CreateTriangle(Point3D p0, Point3D p1, Point3D p2, Brush brush)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(2);

            Vector3D normal = CalcNormal(p0, p1, p2);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);

            Material material = new DiffuseMaterial(brush); //new DiffuseMaterial(Brushes.Red);
            MaterialGroup materialGroup = new MaterialGroup();
            materialGroup.Children.Add(material);
            //materialGroup.Children.Add(new EmissiveMaterial(Brushes.DarkBlue));

            GeometryModel3D model = new GeometryModel3D(mesh, materialGroup);
            model.BackMaterial = materialGroup;
            Model3DGroup group = new Model3DGroup();
            group.Children.Add(model);
            return group;

        }

        public static Vector3D CalcNormal(Point3D p0, Point3D p1, Point3D p2)
        {
            Vector3D v0 = new Vector3D(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3D v1 = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
            return Vector3D.CrossProduct(v0, v1);
        }
    }
}

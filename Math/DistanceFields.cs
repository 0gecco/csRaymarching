using System.Numerics;

namespace csRaymarching.Math
{
    /// <summary>
    /// Signed Distance Field (SDF) functions for primitive shapes
    /// </summary>
    public static class DistanceFields
    {
        // Basic Primitives and Shapes
        public static float Sphere(Vector3 point, float radius)
        {
            return point.Length() - radius;
        }

        public static float Box(Vector3 point, Vector3 size)
        {
            Vector3 q = Abs(point) - size;
            return Max(q).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0f);
        }

        public static float Plane(Vector3 point, Vector3 normal, float distance)
        {
            return Vector3.Dot(point, normal) + distance;
        }

        public static float Torus(Vector3 point, float majorRadius, float minorRadius)
        {
            Vector2 q = new(
                new Vector2(point.X, point.Z).Length() - majorRadius,
                point.Y
            );
            return q.Length() - minorRadius;
        }

        public static float Cylinder(Vector3 point, float radius, float height)
        {
            Vector2 d = Abs(new Vector2(
                new Vector2(point.X, point.Z).Length(),
                point.Y
            )) - new Vector2(radius, height);

            return MathF.Min(MathF.Max(d.X, d.Y), 0f) + Max(d).Length();
        }

        public static float Capsule(Vector3 point, Vector3 a, Vector3 b, float radius)
        {
            Vector3 pa = point - a;
            Vector3 ba = b - a;
            float h = MathF.Max(0f, MathF.Min(1f, Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba)));
            return (pa - ba * h).Length() - radius;
        }

        // Fractals and Complex Shapes
        public static float SierpinskiPyramid(Vector3 p, int iterations)
        {
            float scale = 2f;
            Vector3 offset = new(1f);

            // Not really working? Idk
            for (int i = 0; i < iterations; i++)
            {
                // Fold into pyramid shape
                if (p.X + p.Y < 0) { float t = -p.Y; p.Y = -p.X; p.X = t; }
                if (p.X + p.Z < 0) { float t = -p.Z; p.Z = -p.X; p.X = t; }
                if (p.Y + p.Z < 0) { float t = -p.Z; p.Z = -p.Y; p.Y = t; }

                // Scale and translate
                p = p * scale - offset * (scale - 1f);
            }

            // Tetrahedron
            return (Tetrahedron(p)) / MathF.Pow(scale, iterations);
        }

        private static float Tetrahedron(Vector3 p)
        {
            float md = MathF.Max(MathF.Max(-p.X - p.Y - p.Z, p.X + p.Y - p.Z),
                                 MathF.Max(-p.X + p.Y + p.Z, p.X - p.Y + p.Z));
            return md / MathF.Sqrt(3f);
        }

        // Boolean Operations
        public static float Union(float d1, float d2)
        {
            return MathF.Min(d1, d2);
        }

        public static float Subtraction(float d1, float d2)
        {
            return MathF.Max(-d1, d2);
        }

        public static float Intersection(float d1, float d2)
        {
            return MathF.Max(d1, d2);
        }

        public static float SmoothUnion(float d1, float d2, float k)
        {
            float h = MathF.Max(k - MathF.Abs(d1 - d2), 0f) / k;
            return MathF.Min(d1, d2) - h * h * k * 0.25f;
        }

        // Domain Operations
        public static Vector3 Repeat(Vector3 point, Vector3 spacing)
        {
            return new Vector3(
                Mod(point.X + spacing.X * 0.5f, spacing.X) - spacing.X * 0.5f,
                Mod(point.Y + spacing.Y * 0.5f, spacing.Y) - spacing.Y * 0.5f,
                Mod(point.Z + spacing.Z * 0.5f, spacing.Z) - spacing.Z * 0.5f
            );
        }

        public static Vector3 RotateY(Vector3 point, float angle)
        {
            float c = MathF.Cos(angle);
            float s = MathF.Sin(angle);
            return new Vector3(
                point.X * c - point.Z * s,
                point.Y,
                point.X * s + point.Z * c
            );
        }

        // Helpers
        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));
        }

        private static Vector2 Abs(Vector2 v)
        {
            return new Vector2(MathF.Abs(v.X), MathF.Abs(v.Y));
        }

        private static Vector3 Max(Vector3 v)
        {
            return new Vector3(
                MathF.Max(v.X, 0),
                MathF.Max(v.Y, 0),
                MathF.Max(v.Z, 0)
            );
        }

        private static Vector2 Max(Vector2 v)
        {
            return new Vector2(MathF.Max(v.X, 0), MathF.Max(v.Y, 0));
        }

        private static float Mod(float x, float y)
        {
            return x - y * MathF.Floor(x / y);
        }
    }
}
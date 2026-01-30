using csRaymarching.Math;
using System.Numerics;
using System.Runtime.InteropServices;

namespace csRaymarching.Render
{
    public sealed class Scene
    {
        public enum ObjectType
        {
            Sphere,
            Box,
            Plane,
            Torus,
            MengerSponge,
            Cylinder,
            Capsule
        }

        public struct SceneObject
        {
            public ObjectType Type;
            public Vector3 Position;
            public Vector3 Size;
            public Vector3 Color;
        }

        private readonly List<SceneObject> _objects = [];
        private float _time = 0f;

        public void Update(double deltaTime) => _time += (float)deltaTime;
        public void Clear() => _objects.Clear();

        // Object creation
        public void AddSphere(Vector3 position, float radius, Vector3 color) =>
            _objects.Add(new SceneObject { Type = ObjectType.Sphere, Position = position, Size = new Vector3(radius, 0, 0), Color = color });

        public void AddBox(Vector3 position, Vector3 size, Vector3 color) =>
            _objects.Add(new SceneObject { Type = ObjectType.Box, Position = position, Size = size, Color = color });

        public void AddPlane(Vector3 normal, float distance, Vector3 color) =>
            _objects.Add(new SceneObject { Type = ObjectType.Plane, Position = normal, Size = new Vector3(distance, 0, 0), Color = color });

        public void AddTorus(Vector3 position, float majorRadius, float minorRadius, Vector3 color) =>
            _objects.Add(new SceneObject { Type = ObjectType.Torus, Position = position, Size = new Vector3(majorRadius, minorRadius, 0), Color = color });

        public void AddCylinder(Vector3 position, float radius, float height, Vector3 color) =>
            _objects.Add(new SceneObject { Type = ObjectType.Cylinder, Position = position, Size = new Vector3(radius, height, 0), Color = color });

        public void AddCapsule(Vector3 position, float radius, float halfHeight, Vector3 color) =>
            _objects.Add(new SceneObject { Type = ObjectType.Capsule, Position = position, Size = new Vector3(radius, halfHeight, 0), Color = color });

        // Fast scene query helpers
        public Vector3 GetColorByIndex(int idx)
        {
            if ((uint)idx >= (uint)_objects.Count) return new Vector3(0.5f);
            return _objects[idx].Color;
        }

        // Distance to one specific object
        public float GetDistanceToObject(Vector3 point, int i)
        {
            // Safe for callers that might pass -1
            if ((uint)i >= (uint)_objects.Count) return 1e6f;

            SceneObject obj = _objects[i];

            return obj.Type switch
            {
                ObjectType.Sphere => DistanceFields.Sphere(point - obj.Position, obj.Size.X),
                ObjectType.Box => DistanceFields.Box(point - obj.Position, obj.Size),
                ObjectType.Plane => DistanceFields.Plane(point, obj.Position, obj.Size.X),
                ObjectType.Torus => DistanceFields.Torus(point - obj.Position, obj.Size.X, obj.Size.Y),
                ObjectType.Cylinder => DistanceFields.Cylinder(point - obj.Position, obj.Size.X, obj.Size.Y),
                ObjectType.Capsule => DistanceFields.Capsule(
                    point,
                    obj.Position + new Vector3(0, -obj.Size.Y, 0),
                    obj.Position + new Vector3(0, obj.Size.Y, 0),
                    obj.Size.X),
                _ => 1e6f
            };
        }

        public Vector3 GetNormalForObject(Vector3 point, int objIdx)
        {
            const float eps = 0.001f;
            float d = GetDistanceToObject(point, objIdx);

            return Vector3.Normalize(new Vector3(
                GetDistanceToObject(point + new Vector3(eps, 0, 0), objIdx) - d,
                GetDistanceToObject(point + new Vector3(0, eps, 0), objIdx) - d,
                GetDistanceToObject(point + new Vector3(0, 0, eps), objIdx) - d
            ));
        }

        public (float dist, int objIndex) GetDistanceAndObject(Vector3 point)
        {
            if (_objects.Count == 0) return (1e6f, -1);

            float minDist = 1e6f;
            int minIdx = -1;

            var span = CollectionsMarshal.AsSpan(_objects);
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly SceneObject obj = ref span[i];

                float dist = obj.Type switch
                {
                    ObjectType.Sphere => DistanceFields.Sphere(point - obj.Position, obj.Size.X),
                    ObjectType.Box => DistanceFields.Box(point - obj.Position, obj.Size),
                    ObjectType.Plane => DistanceFields.Plane(point, obj.Position, obj.Size.X),
                    ObjectType.Torus => DistanceFields.Torus(point - obj.Position, obj.Size.X, obj.Size.Y),
                    ObjectType.Cylinder => DistanceFields.Cylinder(point - obj.Position, obj.Size.X, obj.Size.Y),
                    ObjectType.Capsule => DistanceFields.Capsule(
                        point,
                        obj.Position + new Vector3(0, -obj.Size.Y, 0),
                        obj.Position + new Vector3(0, obj.Size.Y, 0),
                        obj.Size.X),
                    _ => 1e6f
                };

                if (dist < minDist)
                {
                    minDist = dist;
                    minIdx = i;
                }
            }

            return (minDist, minIdx);
        }

        // Only for debugging/utilities/EnsureOutside/camera checks
        public float GetDistance(Vector3 point)
        {
            return GetDistanceAndObject(point).dist;
        }

        public Vector3 GetNormal(Vector3 point)
        {
            const float eps = 0.001f;
            float d = GetDistance(point);

            return Vector3.Normalize(new Vector3(
                GetDistance(point + new Vector3(eps, 0, 0)) - d,
                GetDistance(point + new Vector3(0, eps, 0)) - d,
                GetDistance(point + new Vector3(0, 0, eps)) - d
            ));
        }

        // Legacy helper; not needed with hitObj flow, but harmless
        public Vector3 GetColorAt(Vector3 point)
        {
            var (_, idx) = GetDistanceAndObject(point);
            return GetColorByIndex(idx);
        }



        // ============================ < SCENE PRESETS > ============================
        public void CreateZenScene()
        {
            Clear();

            // Sand floor
            AddPlane(Vector3.UnitY, 2f, new Vector3(0.35f, 0.32f, 0.28f));

            // Rocks (spheres partially in ground)
            AddSphere(new Vector3(-3, -1.3f, 2), 1f, new Vector3(0.4f, 0.4f, 0.4f));
            AddSphere(new Vector3(-2.5f, -1.5f, 3), 0.8f, new Vector3(0.45f, 0.45f, 0.45f));
            AddSphere(new Vector3(3, -1.4f, 4), 1.1f, new Vector3(0.38f, 0.38f, 0.38f));

            // Balanced stones (stacked spheres, smaller on top)
            AddSphere(new Vector3(0, -0.8f, 3), 1.2f, new Vector3(0.5f, 0.5f, 0.52f));
            AddSphere(new Vector3(0, 0.6f, 3), 0.8f, new Vector3(0.48f, 0.48f, 0.5f));
            AddSphere(new Vector3(0, 1.6f, 3), 0.5f, new Vector3(0.46f, 0.46f, 0.48f));

            // Torii gate (traditional Japanese gate)
            // Top beam
            AddBox(new Vector3(0, 3.3f, 6), new Vector3(3f, 0.3f, 0.4f), new Vector3(0.6f, 0.2f, 0.2f));
            // Upper beam
            AddBox(new Vector3(0, 2.8f, 6), new Vector3(2.5f, 0.25f, 0.35f), new Vector3(0.6f, 0.2f, 0.2f));

            // Decorative torus (abstract art piece)
            AddTorus(new Vector3(-4, 0.5f, 5), 0.8f, 0.25f, new Vector3(0.3f, 0.5f, 0.3f));
        }

        public void CreateRingsScene()
        {
            Clear();

            // Ground plane
            AddPlane(Vector3.UnitY, 2f, new Vector3(0.3f, 0.3f, 0.35f));

            // Top sphere
            AddSphere(new Vector3(0, 8, 0), 1.2f, new Vector3(1f, 0.5f, 0.3f));

            // Orbital rings (tori at different heights)
            AddTorus(new Vector3(0, 3, 0), 4f, 0.4f, new Vector3(0.3f, 1f, 1f));
            AddTorus(new Vector3(0, 5, 0), 3.5f, 0.35f, new Vector3(1f, 0.3f, 1f));
            AddTorus(new Vector3(0, 7, 0), 3f, 0.3f, new Vector3(1f, 1f, 0.3f));

            // Support pillars (boxes at cardinal directions)
            AddBox(new Vector3(5, 0, 0), new Vector3(0.5f, 3f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddBox(new Vector3(-5, 0, 0), new Vector3(0.5f, 3f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddBox(new Vector3(0, 0, 5), new Vector3(0.5f, 3f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddBox(new Vector3(0, 0, -5), new Vector3(0.5f, 3f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
        }

        public void CreatePlaygroundScene()
        {
            Clear();

            // Ground
            AddPlane(Vector3.UnitY, 2f, new Vector3(0.3f, 0.5f, 0.3f)); // Grass-ish

            // Jungle gym (interconnected boxes)
            AddBox(new Vector3(-2, 0, 3), new Vector3(0.2f, 2f, 0.2f), new Vector3(1f, 0.3f, 0.3f));
            AddBox(new Vector3(2, 0, 3), new Vector3(0.2f, 2f, 0.2f), new Vector3(1f, 0.3f, 0.3f));
            AddBox(new Vector3(0, 1.8f, 3), new Vector3(2.2f, 0.2f, 0.2f), new Vector3(1f, 0.3f, 0.3f));

            // Slide (angled box - won't look perfect but interesting)
            AddBox(new Vector3(4, 0.5f, 2), new Vector3(1.5f, 0.1f, 0.8f), new Vector3(0.3f, 0.3f, 1f));

            // Balls (multicolored spheres)
            AddSphere(new Vector3(-3, -0.5f, 1), 0.8f, new Vector3(1f, 0.2f, 0.2f));
            AddSphere(new Vector3(-1, -0.5f, 0), 0.8f, new Vector3(0.2f, 1f, 0.2f));
            AddSphere(new Vector3(1, -0.5f, 1), 0.8f, new Vector3(0.2f, 0.2f, 1f));
            AddSphere(new Vector3(3, -0.5f, 0), 0.8f, new Vector3(1f, 1f, 0.2f));

            // Rings (tori as hoops)
            AddTorus(new Vector3(0, 1, 6), 1.2f, 0.2f, new Vector3(1f, 0.5f, 0.2f));
            AddTorus(new Vector3(-3, 1.5f, 5), 0.9f, 0.18f, new Vector3(0.2f, 1f, 0.5f));
            AddTorus(new Vector3(3, 1.5f, 5), 0.9f, 0.18f, new Vector3(0.5f, 0.2f, 1f));
        }

        public void CreateDefaultScene()
        {
            Clear();

            // Ground plane
            AddPlane(Vector3.UnitY, 2f, new Vector3(0.3f, 0.3f, 0.35f));

            // Center sphere
            AddSphere(new Vector3(0, 0, 0), 1f, new Vector3(1f, 0.3f, 0.3f));

            // Side boxes
            AddBox(new Vector3(-3, 0, 0), new Vector3(0.8f, 0.8f, 0.8f), new Vector3(0.3f, 1f, 0.3f));
            AddBox(new Vector3(3, 0, 0), new Vector3(0.8f, 0.8f, 0.8f), new Vector3(0.3f, 0.3f, 1f));

            // Torus behind
            AddTorus(new Vector3(0, 0, 5), 1.5f, 0.4f, new Vector3(1f, 1f, 0.3f));
        }

        // This one doesn't play very nicely with the fog...
        public (Vector3 position, float yaw, float pitch) CreateCorridorScene()
        {
            Clear();

            // Floor at y = -2
            AddPlane(Vector3.UnitY, 2f, new Vector3(0.2f, 0.2f, 0.22f));

            // Ceiling at y = 8  (FIX: +8, not -8)
            AddPlane(-Vector3.UnitY, 8f, new Vector3(0.18f, 0.18f, 0.2f));

            // Walls at x = -5 and x = 5
            AddPlane(Vector3.UnitX, 5f, new Vector3(0.25f, 0.22f, 0.2f));
            AddPlane(-Vector3.UnitX, 5f, new Vector3(0.25f, 0.22f, 0.2f));

            // Pillars along corridor
            for (int i = 0; i < 8; i++)
            {
                float z = i * 5f + 2f;
                float x = (i % 2 == 0) ? -4f : 4f;

                AddCylinder(new Vector3(x, 0, z), 0.5f, 6f, new Vector3(0.4f, 0.35f, 0.3f));
                AddSphere(new Vector3(x, 6.3f, z), 0.6f, new Vector3(1f, 0.8f, 0.3f));
            }

            AddTorus(new Vector3(0, 3, 15), 2f, 0.5f, new Vector3(0.3f, 0.8f, 1f));
            AddSphere(new Vector3(0, 3, 35), 2.5f, new Vector3(1f, 0.3f, 0.3f));

            // Make sure starting position is outside geometry lol
            return (new Vector3(0, 1.5f, -10f), 0f, -0.05f);
        }

        public void CreateShowcaseScene()
        {
            Clear();

            // Ground
            AddPlane(Vector3.UnitY, 2f, new Vector3(0.3f, 0.3f, 0.35f));

            // Basic shapes
            AddSphere(new Vector3(-6, 0, 2), 0.8f, new Vector3(1f, 0.3f, 0.3f));
            AddBox(new Vector3(-4, 0, 2), new Vector3(0.8f), new Vector3(0.3f, 1f, 0.3f));
            AddCylinder(new Vector3(-2, 0, 2), 0.6f, 1.5f, new Vector3(0.3f, 0.3f, 1f));

            // Advanced shapes
            AddCapsule(new Vector3(0, 0.5f, 2), 0.4f, 1f, new Vector3(1f, 1f, 0.3f));
            AddTorus(new Vector3(2, 1, 2), 0.8f, 0.3f, new Vector3(1f, 0.3f, 1f));

            // Labels (using small spheres)
            AddSphere(new Vector3(-6, -1.5f, 1.5f), 0.1f, new Vector3(1f, 1f, 1f));
        }
    }
}
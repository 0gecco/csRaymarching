using System.Numerics;

namespace csRaymarching.Core
{
    public readonly struct Ray(Vector3 origin, Vector3 direction)
    {
        public readonly Vector3 Origin = origin;
        public readonly Vector3 Direction = Vector3.Normalize(direction);

        public Vector3 GetPoint(float distance)
        {
            return Origin + Direction * distance;
        }
    }
}
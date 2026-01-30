using System.Numerics;

namespace csRaymarching.Math
{
    public static class MathUtils
    {
        public const float Pi = MathF.PI;
        public const float TwoPi = MathF.PI * 2f;
        public const float HalfPi = MathF.PI * 0.5f;
        public const float Epsilon = 0.001f;

        public static float Clamp(float value, float min, float max)
        {
            return MathF.Max(min, MathF.Min(max, value));
        }

        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            return a + (b - a) * t;
        }

        public static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float DegToRad(float degrees)
        {
            return degrees * (Pi / 180f);
        }

        public static float RadToDeg(float radians)
        {
            return radians * (180f / Pi);
        }

        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
        {
            float sqrMag = Vector3.Dot(planeNormal, planeNormal);
            if (sqrMag < Epsilon)
                return vector;

            float dot = Vector3.Dot(vector, planeNormal);
            return vector - planeNormal * dot / sqrMag;
        }
    }
}
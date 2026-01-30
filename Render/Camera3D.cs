using csRaymarching.Core;

using System.Numerics;

namespace csRaymarching.Render
{
    public sealed class Camera3D
    {
        public Vector3 Position { get; set; }
        public float Yaw { get; set; }      // Horizontal rotation (radians)
        public float Pitch { get; set; }    // Vertical rotation (radians)
        public float FieldOfView { get; set; } = 60f;

        // Store initial state for reset
        private readonly Vector3 _initialPosition;
        private readonly float _initialYaw;
        private readonly float _initialPitch;

        private Vector3 _forward;
        private Vector3 _right;
        private Vector3 _up;
        private bool _vectorsDirty = true; // Not implemented yet, but could be used for optimization

        public Camera3D(Vector3 position, float yaw = 0f, float pitch = 0f)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;

            // Store initial values
            _initialPosition = position;
            _initialYaw = yaw;
            _initialPitch = pitch;

            UpdateVectors();
        }

        public void Reset()
        {
            Position = _initialPosition;
            Yaw = _initialYaw;
            Pitch = _initialPitch;
            _vectorsDirty = true;
            UpdateVectors();
        }

        public void Update(double deltaTime, InputState input, Settings settings)
        {
            // Update rotation
            Yaw += input.Yaw * settings.MouseSensitivity * (float)deltaTime;

            // Keep yaw bounded to avoid float precision decay over long runtimes
            Yaw = WrapAnglePi(Yaw);

            Pitch += input.Pitch * settings.MouseSensitivity * (float)deltaTime;
            Pitch = MathF.Max(-MathF.PI * 0.49f, MathF.Min(MathF.PI * 0.49f, Pitch));

            static float WrapAnglePi(float a)
            {
                const float TwoPi = MathF.PI * 2f;
                a %= TwoPi;
                if (a > MathF.PI) a -= TwoPi;
                if (a < -MathF.PI) a += TwoPi;
                return a;
            }
            UpdateVectors();

            // Calculate movement speed
            float speed = settings.MoveSpeed * (float)deltaTime;
            if (input.Sprint) speed *= 2f;

            // Move
            if (input.Forward != 0)
                Position += _forward * (input.Forward * speed);
            if (input.Right != 0)
                Position += _right * (input.Right * speed);
            if (input.Up != 0)
                Position += Vector3.UnitY * (input.Up * speed);
        }

        private void UpdateVectors()
        {
            // Calculate forward vector from yaw and pitch
            _forward = new Vector3(
                MathF.Cos(Pitch) * MathF.Sin(Yaw),
                MathF.Sin(Pitch),
                MathF.Cos(Pitch) * MathF.Cos(Yaw)
            );
            _forward = Vector3.Normalize(_forward);

            // Right vector is perpendicular to forward on XZ plane
            _right = Vector3.Normalize(Vector3.Cross(_forward, Vector3.UnitY));

            // Up vector
            _up = Vector3.Cross(_right, _forward);
        }

        public Ray GetRay(int x, int y, int screenWidth, int screenHeight)
        {
            // Convert screen coordinates to NDC (-1 to 1)
            float ndcX = (2f * x / screenWidth) - 1f;
            float ndcY = 1f - (2f * y / screenHeight);

            // Calculate aspect ratio
            float aspect = (float)screenWidth / screenHeight;

            // Calculate FOV scaling
            float fovScale = MathF.Tan(FieldOfView * 0.5f * MathF.PI / 180f);

            // Create ray direction in camera space
            Vector3 rayDir = Vector3.Normalize(
                _right * (ndcX * aspect * fovScale) +
                _up * (ndcY * fovScale) +
                _forward
            );

            return new Ray(Position, rayDir);
        }

        public Vector3 Forward => _forward;
        public Vector3 Right => _right;
        public Vector3 Up => _up;
    }
}
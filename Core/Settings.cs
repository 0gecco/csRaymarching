namespace csRaymarching.Core
{
    public sealed class Settings
    {
        // Rendering
        public int TargetFps { get; set; } = 60;
        public bool UseHalfBlocks { get; set; } = true;
        public int MaxRaymarchSteps { get; set; } = 80;
        public float MaxRenderDistance { get; set; } = 100f;

        // Camera
        public float MouseSensitivity { get; set; } = 0.15f;      // Might be worth implementing real mouse input later
        public float MoveSpeed { get; set; } = 3.5f;
        public float FieldOfView { get; set; } = 80f;

        // Quality
        public bool EnableShadows { get; set; } = false;          // Not implemented yet
        public bool EnableAmbientOcclusion { get; set; } = false; // Not implemented yet
        public int ResolutionScale { get; set; } = 1;             // Not implemented yet

        // Visuals
        public bool ShowDebugInfo { get; set; } = true;
        public bool EnableFog { get; set; } = true;
        public float FogDensity { get; set; } = 0.03f;
        public bool MapColorsWithGamma { get; set; } = true;
    }
}
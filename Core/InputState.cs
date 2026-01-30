namespace csRaymarching.Core
{
    public sealed class InputState
    {
        // Movement
        public float Forward { get; set; }  // -1 to 1
        public float Right { get; set; }    // -1 to 1
        public float Up { get; set; }       // -1 to 1

        // Look
        public float Yaw { get; set; }      // Horizontal rotation
        public float Pitch { get; set; }    // Vertical rotation

        // Actions
        public bool Sprint { get; set; }
        public bool Interact { get; set; }  // No use for this yet...

        public void Reset()
        {
            Forward = 0;
            Right = 0;
            Up = 0;
        }
    }
}
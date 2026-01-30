using static csRaymarching.Console.ConsoleInteraction;
using csRaymarching.Core;

using System.Numerics;

namespace csRaymarching.Render
{
    public static class Shading
    {
        private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(0.5f, 1f, -0.3f));

        public static string ComputeShading(
            Vector3 hitPoint,
            Vector3 normal,
            Vector3 materialColor,
            float distance,
            Settings settings,
            ConsoleTheme theme)
        {
            // Lambertian diffuse
            float diffuse = MathF.Max(0, Vector3.Dot(normal, LightDirection));

            const float ambient = 0.25f; // Ambient light intesnity

            float lighting = ambient + diffuse * (1f - ambient);

            if (settings.EnableFog)
            {
                float fogFactor = 1f - MathF.Exp(-distance * settings.FogDensity);
                lighting *= (1f - fogFactor);
            }

            // Apply material color influence
            float colorIntensity = (materialColor.X + materialColor.Y + materialColor.Z) / 3f;
            lighting *= (0.7f + colorIntensity * 0.3f);

            // Map to console colors
            if (!settings.MapColorsWithGamma)
            {
                return MapToColor(lighting, materialColor, theme);
            }
            else
            {
                return MapToColorWithGamma(lighting, materialColor, theme);
            }
        }
        
        private static string MapToColor(float intensity, Vector3 materialColor, ConsoleTheme theme)
        {
            // Apply lighting intensity to material color
            Vector3 finalColor = materialColor * intensity;

            // Clamp and convert to 0-255 range
            int r = (int)MathF.Round(System.Math.Clamp(finalColor.X, 0f, 1f) * 255);
            int g = (int)MathF.Round(System.Math.Clamp(finalColor.Y, 0f, 1f) * 255);
            int b = (int)MathF.Round(System.Math.Clamp(finalColor.Z, 0f, 1f) * 255);

            // True color ANSI escape sequence
            return $"\u001b[38;2;{r};{g};{b}m";
        }

        private static string MapToColorWithGamma(float intensity, Vector3 materialColor, ConsoleTheme theme)
        {
            Vector3 finalColor = materialColor * intensity;

            // Apply gamma correction (sRGB approximation)
            const float gamma = 0.8f;
            float r = MathF.Pow(System.Math.Clamp(finalColor.X, 0f, 1f), 1f / gamma);
            float g = MathF.Pow(System.Math.Clamp(finalColor.Y, 0f, 1f), 1f / gamma);
            float b = MathF.Pow(System.Math.Clamp(finalColor.Z, 0f, 1f), 1f / gamma);

            return $"\u001b[38;2;{(int)(r * 255)};{(int)(g * 255)};{(int)(b * 255)}m";
        }

        public static string GetBackgroundColor(ConsoleTheme theme)
        {
            return theme switch
            {
                ConsoleTheme.Light => AnsiConsoleVT.LightWhite,
                _ => AnsiConsoleVT.Black
            };
        }
    }
}
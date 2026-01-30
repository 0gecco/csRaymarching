using csRaymarching.Core;

using System.Numerics;

namespace csRaymarching.Render
{
    public sealed class RaymarchRenderer(Settings settings, ConsoleTheme theme)
    {
        private const float MinHitDistance = 0.001f;
        private ConsoleTheme _theme = theme;
        private readonly Settings _settings = settings;

        // Performance tracking
        private int _totalRays;
        private int _totalSteps;

        public void SetTheme(ConsoleTheme theme)
        {
            _theme = theme;
        }

        public (int rays, int steps) GetStats()
        {
            var result = (_totalRays, _totalSteps);
            _totalRays = 0;
            _totalSteps = 0;
            return result;
        }

        public void Render(Scene scene,Camera3D camera,char[,] canvas,string?[,] colors)
        {
            int width = canvas.GetLength(1);
            int height = canvas.GetLength(0);

            if (_settings.UseHalfBlocks)
            {
                RenderHalfBlocks(scene, camera, canvas, colors, width, height);
            }
            else
            {
                RenderFullBlocks(scene, camera, canvas, colors, width, height);
            }
        }

        private void RenderHalfBlocks(Scene scene,Camera3D camera,char[,] canvas,string?[,] colors,int width,int height)
        {
            int resolutionY = height * 2;

            Parallel.For(0, height, y =>
            {
                int localSteps = 0;
                int localRays = 0;

                for (int x = 0; x < width; x++)
                {
                    var ray1 = camera.GetRay(x, y * 2, width, resolutionY);
                    var ray2 = camera.GetRay(x, y * 2 + 1, width, resolutionY);

                    var (dist1, steps1, obj1) = Raymarch(scene, ray1);
                    var (dist2, steps2, obj2) = Raymarch(scene, ray2);

                    localSteps += steps1 + steps2;
                    localRays += 2;

                    string color1 = ShadePixel(scene, ray1, dist1, obj1);
                    string color2 = ShadePixel(scene, ray2, dist2, obj2);

                    SetHalfBlockWithColors(canvas, colors, y, x, color1, color2);
                }

                Interlocked.Add(ref _totalSteps, localSteps);
                Interlocked.Add(ref _totalRays, localRays);
            });
        }

        private void RenderFullBlocks(Scene scene,Camera3D camera,char[,] canvas,string?[,] colors,int width,int height)
        {
            Parallel.For(0, height, y =>
            {
                int localSteps = 0;
                int localRays = 0;

                for (int x = 0; x < width; x++)
                {
                    var ray = camera.GetRay(x, y, width, height);

                    var (dist, steps, obj) = Raymarch(scene, ray);

                    localSteps += steps;
                    localRays++;

                    string color = ShadePixel(scene, ray, dist, obj);

                    canvas[y, x] = '█';
                    colors[y, x] = color;
                }

                Interlocked.Add(ref _totalSteps, localSteps);
                Interlocked.Add(ref _totalRays, localRays);
            });
        }

        private (float distance, int steps, int hitObj) Raymarch(Scene scene, Ray ray)
        {
            float totalDist = 0f;

            for (int steps = 0; steps < _settings.MaxRaymarchSteps; steps++)
            {
                Vector3 pos = ray.GetPoint(totalDist);

                var (dist, objIdx) = scene.GetDistanceAndObject(pos);

                if (dist < MinHitDistance)
                    return (totalDist, steps, objIdx);

                totalDist += dist;

                if (totalDist > _settings.MaxRenderDistance)
                    return (_settings.MaxRenderDistance, steps, -1);
            }

            return (_settings.MaxRenderDistance, _settings.MaxRaymarchSteps, -1);
        }

        private string ShadePixel(Scene scene, Ray ray, float depth, int hitObj)
        {
            if (depth >= _settings.MaxRenderDistance || hitObj < 0)
                return Shading.GetBackgroundColor(_theme);

            Vector3 hitPoint = ray.GetPoint(depth);

            // Fast normal
            Vector3 normal = scene.GetNormalForObject(hitPoint, hitObj);

            Vector3 materialColor = scene.GetColorByIndex(hitObj);

            return Shading.ComputeShading(hitPoint, normal, materialColor, depth, _settings, _theme);
        }

        private static string ConvertToBgColor(string fg)
        {
            if (fg is null) return "\x1b[49m";

            // Truecolor: ESC[38;2;r;g;bm -> ESC[48;2;r;g;bm
            if (fg.Contains("\x1b[38;2;"))
                return fg.Replace("\x1b[38;2;", "\x1b[48;2;");

            // 256-color: ESC[38;5;Nm -> ESC[48;5;Nm
            if (fg.Contains("\x1b[38;5;"))
                return fg.Replace("\x1b[38;5;", "\x1b[48;5;");

            // A bit hacky?... Standard Colors
            return fg
                .Replace("\x1b[30m", "\x1b[40m")
                .Replace("\x1b[31m", "\x1b[41m")
                .Replace("\x1b[32m", "\x1b[42m")
                .Replace("\x1b[33m", "\x1b[43m")
                .Replace("\x1b[34m", "\x1b[44m")
                .Replace("\x1b[35m", "\x1b[45m")
                .Replace("\x1b[36m", "\x1b[46m")
                .Replace("\x1b[37m", "\x1b[47m")
                .Replace("\x1b[90m", "\x1b[100m")
                .Replace("\x1b[91m", "\x1b[101m")
                .Replace("\x1b[92m", "\x1b[102m")
                .Replace("\x1b[93m", "\x1b[103m")
                .Replace("\x1b[94m", "\x1b[104m")
                .Replace("\x1b[95m", "\x1b[105m")
                .Replace("\x1b[96m", "\x1b[106m")
                .Replace("\x1b[97m", "\x1b[107m");
        }


        // In half-block mode, every _colors[y,x] must set BOTH foreground and background ANSI
        private static void SetHalfBlockWithColors(
            char[,] canvas,
            string?[,] colors,
            int y,
            int x,
            string topColor,
            string bottomColor)
        {
            if (y < 0 || y >= canvas.GetLength(0) || x < 0 || x >= canvas.GetLength(1))
                return;

            canvas[y, x] = '▀';
            colors[y, x] = topColor + ConvertToBgColor(bottomColor);
        }
    }
}
using static csRaymarching.Console.ConsoleInteraction;
using csRaymarching.Render;
using csRaymarching.Core;
using csRaymarching.Console;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Numerics;
using System.Text;

// ####  csRaymarching  ####
// ####  2026 © NiTROZ  ####

internal static partial class Program
{
    [LibraryImport("kernel32.dll")] static private partial IntPtr GetConsoleWindow();
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static private partial bool ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);
    [LibraryImport("user32.dll")] static private partial short GetAsyncKeyState(int vKey);

    static bool KeyDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

    const int MinWidth = 60;
    const int MinHeight = 40;

    // App state
    static ConsoleTheme _theme = ConsoleTheme.Dark;
    static volatile bool _paused = false;
    static int _lastW = -1, _lastH = -1;
    static bool _firstFrame = true;
    private static readonly CancellationTokenSource _cts = new();

    // Input state
    static readonly InputState _input = new();

    // Used for edge-triggered toggle keys (0 .. 255)
    static readonly bool[] _prevDown = new bool[256];

    static bool Pressed(int vKey)
    {
        if ((uint)vKey >= (uint)_prevDown.Length)
        {
            bool down = KeyDown(vKey);
            return down;
        }

        bool downNow = KeyDown(vKey);
        bool wasDown = _prevDown[vKey];
        _prevDown[vKey] = downNow;
        return downNow && !wasDown;
    }

    // Canvas
    static char[,] _canvas = new char[0, 0];
    static string?[,] _colors = new string?[0, 0];

    // Previous frame buffers for dirty-row rendering
    static char[,] _prevCanvas = new char[0, 0];
    static string?[,] _prevColors = new string?[0, 0];
    static uint[] _prevRowHash = [];
    static bool _prevValid = false;

    // Settings
    static readonly Settings _settings = new()
    {
        TargetFps = 60,
        UseHalfBlocks = true,
        MaxRaymarchSteps = 80,
        MaxRenderDistance = 100f,
        MouseSensitivity = 2.5f,
        MoveSpeed = 3.0f,
        FieldOfView = 60f,
        ShowDebugInfo = true,
        EnableFog = false, 
        FogDensity = 0.02f,
        MapColorsWithGamma = true
    };

    const float ArrowLookScale = 0.55f; // Arrow key sensitivity multiplier, higher = faster

    // Performance tracking
    static double _lastFrameMs = 0;
    static int _fps = 0;
    static int _frameCount = 0;
    static double _fpsTimer = 0;

    static void SyncBufferToWindow()
    {
        try
        {
            int w = Math.Max(MinWidth, Console.WindowWidth);
            int h = Math.Max(MinHeight, Console.WindowHeight);

#pragma warning disable CA1416
            if (Console.BufferWidth < w) Console.SetBufferSize(w, Console.BufferHeight);
            if (Console.BufferHeight < h) Console.SetBufferSize(Console.BufferWidth, h);

            Console.SetWindowSize(w, h);
            Console.SetBufferSize(w, h);
#pragma warning restore CA1416
        }
        catch
        {
            /* ignore */
            // Resize races or terminal limitations
        }
    }

    static void ReinitPrevBuffers()
    {
        int h = _canvas.GetLength(0);
        int w = _canvas.GetLength(1);

        _prevCanvas = new char[h, w];
        _prevColors = new string?[h, w];
        _prevRowHash = new uint[h];

        _prevValid = false; // Force full redraw once after resize
    }

    static bool HandleResizeIfNeeded()
    {
        int curW = Math.Max(MinWidth, Console.WindowWidth);
        int curH = Math.Max(MinHeight, Console.WindowHeight);

        if (_firstFrame || curW != _lastW || curH != _lastH)
        {
            try { Console.Write("\x1b[2J\x1b[H"); } catch { }

            _firstFrame = false;
            _lastW = curW;
            _lastH = curH;

            SyncBufferToWindow();
            ConsoleCanvas.Ensure();

            // Explicitly refresh canvas refs + prev buffers on resize
            _canvas = ConsoleCanvas.Chars;
            _colors = ConsoleCanvas.Colors;
            ReinitPrevBuffers();

            return true;
        }

        return false;
    }

    private static void StartupConsoleSequence()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                ShowScrollBar(GetConsoleWindow(), 1, false);
            }
            catch { }
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {   // Finalizer guard
                Console.Write("\x1b[?25h\x1b[?7h\x1b[?1049l\x1b[0m");
            }
            catch { }
        };

        Console.Write("\x1b[?1049h");   // Enter alt screen
        Console.Write("\x1b[2J");       // Clear
        Console.Write("\x1b[3J");       // Clear scrollback
        Console.Write("\x1b[H");        // Home cursor
        Console.Write("\x1b[?7l");      // Disable line wrap
        Console.Write("\x1b[?25l");     // Hide cursor

        SyncBufferToWindow();
        Thread.Sleep(50);

        _lastW = Console.WindowWidth;
        _lastH = Console.WindowHeight;

        ConsoleCanvas.Ensure();
        _canvas = ConsoleCanvas.Chars;
        _colors = ConsoleCanvas.Colors;
        ReinitPrevBuffers();
    }

    private static void UpdateTitle()
    {
        Console.Title = $"csRaymarching | FPS: {_fps} (Target: {_settings.TargetFps}) | {(_paused ? "PAUSED" : "Running")}";
    }

    public static void Main()
    {
        StartupConsoleSequence();

        Console.Title = "...crunching bytes...";
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        AnsiConsoleVT.EnableVirtualTerminal();

        if (OperatingSystem.IsWindows())
            WindowsConsole.CenterConsoleOnMonitor();

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        // Initialize renderer and scene
        var renderer = new RaymarchRenderer(_settings, _theme);
        var scene = new Scene();
        scene.CreateDefaultScene();

        var camera = new Camera3D(
            position: new Vector3(0, 1, -8),
            yaw: 0f,
            pitch: 0f
        );

        // Main render thread (update + input + draw)
        var renderTask = Task.Run(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.Normal;
            RenderLoop(renderer, scene, camera, _cts.Token);
        });

        // Main thread waits until exit
        while (!_cts.IsCancellationRequested)
            Thread.Sleep(25);

        try { renderTask.Wait(); } catch { /* ignore */ }

        Console.CursorVisible = true;
        Console.Write("\x1b[?7h\x1b[?1049l");
        AnsiConsoleVT.WriteReset();
    }

    /// <summary>
    /// Only toggles / one-shot controls. Safe to call while paused.
    /// Returns false when app should exit.
    /// </summary>
    static bool PollToggles(Scene scene, Camera3D camera)
    {
        if (Pressed(VkKeyCodes.VK_ESCAPE)) { _cts.Cancel(); return false; }
        if (Pressed(VkKeyCodes.VK_SPACE)) _paused = !_paused;
        if (Pressed(VkKeyCodes.VK_C)) _theme = _theme.Next();
        if (Pressed(VkKeyCodes.VK_H)) _settings.UseHalfBlocks = !_settings.UseHalfBlocks;
        if (Pressed(VkKeyCodes.VK_F)) _settings.EnableFog = !_settings.EnableFog;
        if (Pressed(VkKeyCodes.VK_G)) _settings.MapColorsWithGamma = !_settings.MapColorsWithGamma;
        if (Pressed(VkKeyCodes.VK_1)) _settings.MoveSpeed = 1f;
        if (Pressed(VkKeyCodes.VK_2)) _settings.MoveSpeed = 3f;
        if (Pressed(VkKeyCodes.VK_3)) _settings.MoveSpeed = 6f;
        if (Pressed(VkKeyCodes.VK_4)) _settings.MoveSpeed = 12f;

        // R resets scene AND camera
        if (Pressed(VkKeyCodes.VK_R))
        {
            scene.CreateDefaultScene();
            camera.Reset();
        }

        if (Pressed(VkKeyCodes.VK_I)) _settings.ShowDebugInfo = !_settings.ShowDebugInfo;
        if (Pressed(VkKeyCodes.VK_F1)) _settings.TargetFps = 30;
        if (Pressed(VkKeyCodes.VK_F2)) _settings.TargetFps = 60;
        if (Pressed(VkKeyCodes.VK_F3)) _settings.TargetFps = 120;
        if (Pressed(VkKeyCodes.VK_F4)) _settings.TargetFps = 144;

        // Scene switching with camera reset
        if (Pressed(VkKeyCodes.VK_NUMPAD0)) { scene.CreateDefaultScene(); camera.Reset(); }
        if (Pressed(VkKeyCodes.VK_NUMPAD1)) { scene.CreateRingsScene(); camera.Reset(); }
        if (Pressed(VkKeyCodes.VK_NUMPAD2)) { scene.CreateZenScene(); camera.Reset(); }
        if (Pressed(VkKeyCodes.VK_NUMPAD3))
        {
            var (pos, yaw, pitch) = scene.CreateCorridorScene();
            camera.Position = pos;
            camera.Yaw = yaw;
            camera.Pitch = pitch;
        }
        if (Pressed(VkKeyCodes.VK_NUMPAD4)) { scene.CreatePlaygroundScene(); camera.Reset(); }
        if (Pressed(VkKeyCodes.VK_NUMPAD5)) { scene.CreateShowcaseScene(); camera.Reset(); }

        return true;
    }

    /// <summary>
    /// Movement/look polling. Not called while paused.
    /// </summary>
    static void PollMovement()
    {
        float fwd = 0f;
        if (KeyDown(VkKeyCodes.VK_W)) fwd += 1f;
        if (KeyDown(VkKeyCodes.VK_S)) fwd -= 1f;

        float right = 0f;
        if (KeyDown(VkKeyCodes.VK_D)) right += 1f;
        if (KeyDown(VkKeyCodes.VK_A)) right -= 1f;

        float up = 0f;
        if (KeyDown(VkKeyCodes.VK_E)) up += 1f;
        if (KeyDown(VkKeyCodes.VK_Q)) up -= 1f;

        float yaw = 0f;
        // Camera system inverted: left = +yaw, right = -yaw
        if (KeyDown(VkKeyCodes.VK_LEFT)) yaw += ArrowLookScale;
        if (KeyDown(VkKeyCodes.VK_RIGHT)) yaw -= ArrowLookScale;

        float pitch = 0f;
        if (KeyDown(VkKeyCodes.VK_UP)) pitch += ArrowLookScale;
        if (KeyDown(VkKeyCodes.VK_DOWN)) pitch -= ArrowLookScale;

        _input.Sprint = KeyDown(VkKeyCodes.VK_SHIFT);
        _input.Forward = fwd;
        _input.Right = right;
        _input.Up = up;
        _input.Yaw = yaw;
        _input.Pitch = pitch;
    }

    static void ClearMovementInput()
    {
        _input.Sprint = false;
        _input.Forward = 0f;
        _input.Right = 0f;
        _input.Up = 0f;
        _input.Yaw = 0f;
        _input.Pitch = 0f;
    }

    static void RenderLoop(RaymarchRenderer renderer, Scene scene, Camera3D camera, CancellationToken token)
    {
        // Frame pacing state
        int lastTargetFps = Math.Max(1, _settings.TargetFps);
        long frameTicks = (long)(Stopwatch.Frequency / (double)lastTargetFps);

        long nextTick = Stopwatch.GetTimestamp() + frameTicks;
        long lastFrameTick = Stopwatch.GetTimestamp();

        // Hitch harder than this then resync instead of chasing forever
        long maxLagTicks = Stopwatch.Frequency / 2; // 0.5s

        var sb = new StringBuilder(1 << 16);

        while (!token.IsCancellationRequested)
        {
            // Honor the requested 'TargetFps'
            int targetFps = Math.Max(1, _settings.TargetFps);
            if (targetFps != lastTargetFps)
            {
                lastTargetFps = targetFps;
                frameTicks = (long)(Stopwatch.Frequency / (double)lastTargetFps);

                // Resync schedule to avoid burst/stutter after changing FPS
                long now0 = Stopwatch.GetTimestamp();
                nextTick = now0 + frameTicks;
            }

            long now = Stopwatch.GetTimestamp();

            // Spin until we reach our scheduled tick
            if (now < nextTick)
            {
                int remainingMs = (int)(((nextTick - now) * 1000) / Stopwatch.Frequency);
                if (remainingMs > 2) Thread.Sleep(remainingMs - 1);
                else Thread.SpinWait(200);
                continue;
            }

            // If we are too far behind (GC pause, resize drag, debugger, whatever...), resync
            if (now > nextTick + maxLagTicks)
                nextTick = now;

            // Catch up schedule in one go
            do { nextTick += frameTicks; }
            while (now >= nextTick);

            // Base Delta time on real elapsed
            double dt = Math.Max(1e-6, (now - lastFrameTick) / (double)Stopwatch.Frequency);
            lastFrameTick = now;

            bool resized = HandleResizeIfNeeded();
            if (!resized)
            {
                ConsoleCanvas.Ensure();
                _canvas = ConsoleCanvas.Chars;
                _colors = ConsoleCanvas.Colors;
            }

            // If 'Ensure' changed dimensions unexpectedly, keep prev buffers in sync
            if (_prevCanvas.GetLength(0) != _canvas.GetLength(0) || _prevCanvas.GetLength(1) != _canvas.GetLength(1))
                ReinitPrevBuffers();

            if (!PollToggles(scene, camera) || token.IsCancellationRequested)
                break;

            if (_paused)
            {
                ClearMovementInput();
                DrawPausedScreen();
                DrawUI(renderer);
                FlushCanvasDirtyRows(sb);
                continue;
            }

            PollMovement();

            // Update scene/camera
            scene.Update(dt);
            camera.Update(dt, _input, _settings);

            // Render
            long renderStart = Stopwatch.GetTimestamp();
            renderer.Render(scene, camera, _canvas, _colors);
            long renderEnd = Stopwatch.GetTimestamp();

            _lastFrameMs = 1000.0 * (renderEnd - renderStart) / Stopwatch.Frequency;

            // FPS counter
            _frameCount++;
            _fpsTimer += dt;
            if (_fpsTimer >= 0.5)
            {
                _fps = (int)(_frameCount / _fpsTimer);
                _frameCount = 0;
                _fpsTimer = 0;
                UpdateTitle();
            }

            // UI + Flush
            DrawUI(renderer);
            FlushCanvasDirtyRows(sb);
        }
    }

    static void DrawUI(RaymarchRenderer renderer)
    {
        int width = _canvas.GetLength(1);

        string controls =
            "[WASD:Move]  [←→↑↓:Look]  [E/Q:Up/Down]  [1-4:Speed]  [SPCEBAR:Pause]  [C:Color]  [R:Reset]  [I:Hide Metrics]  [ESC:Quit]  [F1-4:FPS Target] [NUMPAD0-5:Cycle Scenes]";
        if (controls.Length > width)
            controls = controls[..Math.Max(0, width - 3)] + "...";

        ConsoleCanvas.PutString(_canvas, _colors, 0, 0, controls, AnsiConsoleVT.BrightBlack);

        if (_settings.ShowDebugInfo)
        {
            var (rays, steps) = renderer.GetStats();
            string info =
                $"FPS: {_fps} | Frame: {_lastFrameMs:F1}ms | Rays: {rays:N0} | Steps: {steps:N0} | Speed: {_settings.MoveSpeed:F1}x";

            if (info.Length > width)
                info = $"FPS: {_fps} | {_lastFrameMs:F1}ms";

            ConsoleCanvas.PutString(_canvas, _colors, 1, 0, info, AnsiConsoleVT.White);
        }

        int y = _settings.ShowDebugInfo ? 2 : 1;
        int x = 0;

        string halfBlk = _settings.UseHalfBlocks ? "[H]HalfBlkRender:ON " : "[H]HalfBlkRender:OFF ";
        ConsoleCanvas.PutString(
            _canvas, _colors, y, x, halfBlk,
            _settings.UseHalfBlocks ? AnsiConsoleVT.Green : AnsiConsoleVT.Red);
        x += halfBlk.Length;

        string fog = _settings.EnableFog ? "[F]Fog:ON " : "[F]Fog:OFF ";
        ConsoleCanvas.PutString(
            _canvas, _colors, y, x, fog,
            _settings.EnableFog ? AnsiConsoleVT.Green : AnsiConsoleVT.Red);

        string gamma = _settings.MapColorsWithGamma ? "[G]GammaColorMap:ON " : "[G]GammaColorMap:OFF ";
        ConsoleCanvas.PutString(
            _canvas, _colors, y, x + fog.Length, gamma,
            _settings.MapColorsWithGamma ? AnsiConsoleVT.Green : AnsiConsoleVT.Red);

        if (_paused)
        {
            x += fog.Length;
            ConsoleCanvas.PutString(_canvas, _colors, y, x, "PAUSED ", AnsiConsoleVT.BrightYellow);
        }
    }

    static void DrawPausedScreen()
    {
        int height = _canvas.GetLength(0);
        int width = _canvas.GetLength(1);
        int midY = height / 2;
        int midX = width / 2;

        const string msg = "[ PAUSED ]";
        int msgX = Math.Max(0, midX - msg.Length / 2);

        ConsoleCanvas.PutString(_canvas, _colors, midY, msgX, msg, AnsiConsoleVT.BrightYellow);
    }

    // Fast-ish per-row hash (FNV-1a) across chars + color identity
    // Using RuntimeHelpers.GetHashCode for reference-identity-ish hashing of strings
    // This is stable for the life of the object and cheap
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint HashRow(int y, int W)
    {
        const uint FNV_OFFSET = 2166136261u;
        const uint FNV_PRIME = 16777619u;

        uint h = FNV_OFFSET;

        for (int x = 0; x < W; x++)
        {
            char ch = _canvas[y, x];
            h ^= (ushort)ch;
            h *= FNV_PRIME;

            string? col = _colors[y, x];
            int colId = col is null ? 0 : RuntimeHelpers.GetHashCode(col);
            h ^= (uint)colId;
            h *= FNV_PRIME;
        }

        return h;
    }

    /// <summary>
    /// Dirty-row flush:
    /// - Uses a per-row hash to skip unchanged rows quickly.
    /// - Only copies prev buffers for rows that changed.
    /// - On resize / first draw, forces a full redraw.
    /// </summary>
    static void FlushCanvasDirtyRows(StringBuilder sb)
    {
        sb.Clear();

        try
        {
            int winH = Console.WindowHeight;
            int winW = Console.WindowWidth;
            if (winH <= 0 || winW <= 0) return;

            int H = Math.Min(_canvas.GetLength(0), winH);
            int W = Math.Min(_canvas.GetLength(1), winW);

            // Ensure prev buffers match current
            if (_prevCanvas.GetLength(0) != _canvas.GetLength(0) || _prevCanvas.GetLength(1) != _canvas.GetLength(1))
                ReinitPrevBuffers();

            bool anyWrites = false;

            for (int y = 0; y < H; y++)
            {
                bool changed;

                uint rowHash = HashRow(y, W);

                if (!_prevValid)
                {
                    changed = true;
                }
                else
                {
                    // Fast path, if row hash matches, assume unchanged
                    changed = rowHash != _prevRowHash[y];
                }

                if (!changed)
                    continue;

                // Copy changed row into prev buffers (no need to copy unchanged rows)
                for (int x = 0; x < W; x++)
                {
                    _prevCanvas[y, x] = _canvas[y, x];
                    _prevColors[y, x] = _colors[y, x];
                }
                _prevRowHash[y] = rowHash;

                anyWrites = true;

                // Move cursor to row start (1-based coords)
                sb.Append("\x1b[");
                sb.Append(y + 1);
                sb.Append(";1H");

                string? last = null;

                for (int x = 0; x < W; x++)
                {
                    string? col = _colors[y, x];
                    if (col != last)
                    {
                        sb.Append(col ?? AnsiConsoleVT.ResetCode);
                        last = col;
                    }

                    char ch = _canvas[y, x];
                    sb.Append(ch == '\0' ? ' ' : ch);
                }

                sb.Append(AnsiConsoleVT.ResetCode);
                sb.Append("\x1b[0K"); // Clear to end-of-line
            }

            _prevValid = true;

            if (!anyWrites)
                return;

            Console.Write(sb.ToString());
        }
        catch (ArgumentOutOfRangeException)
        {
            // Resize race
            ConsoleCanvas.Ensure();
            _canvas = ConsoleCanvas.Chars;
            _colors = ConsoleCanvas.Colors;
            ReinitPrevBuffers();
        }
        catch (IOException)
        {
            /* ignore */
            // Redirected/terminal hiccup
        }
        catch
        {
            /* ignore */
            // Keep loop alive on terminal quirks
        }
    }
}
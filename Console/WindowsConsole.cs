using System.Runtime.InteropServices;

namespace csRaymarching.Console
{
    public static class WindowsConsole
    {
        // I suppose these should be LibraryImports?...
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint GetConsoleWindow();

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;  // Total monitor area
            public RECT rcWork;     // Work area (Taskbar excluded)
            public uint dwFlags;
        }

        public static void EnableVirtualTerminalProcessing()
        {
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(handle, out uint mode)) 
                return;
            mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            SetConsoleMode(handle, mode);
        }

        public static void CenterConsoleOnMonitor()
        {
            try
            {
                var hwnd = GetConsoleWindow();
                if (hwnd == nint.Zero) 
                    return;

                // Current console rect
                if (!GetWindowRect(hwnd, out var wr)) 
                    return;
                int width = wr.Right - wr.Left;
                int height = wr.Bottom - wr.Top;

                // Monitor work area for the window
                nint hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(hmon, ref mi)) 
                    return;

                // Center within work area
                int workW = mi.rcWork.Right - mi.rcWork.Left;
                int workH = mi.rcWork.Bottom - mi.rcWork.Top;
                int x = mi.rcWork.Left + (workW - width) / 2;
                int y = mi.rcWork.Top + (workH - height) / 2;

                MoveWindow(hwnd, x, y, width, height, true);
            }
            catch { /* ignore */ }
        }
    }
}

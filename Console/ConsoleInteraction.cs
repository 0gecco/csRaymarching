namespace csRaymarching.Console
{
    public class ConsoleInteraction
    {
        public static class AnsiConsoleVT
        {
            // Basic ANSI helpers, on Windows 10+ enable VT mode for colors
            public const string ResetCode = "\u001b[0m";
            public static string Fg(string colorCode) => colorCode;

            // Standard colors
            public static string Black => "\u001b[30m";
            public static string Red => "\u001b[31m";
            public static string Green => "\u001b[32m";
            public static string Yellow => "\u001b[33m";
            public static string Blue => "\u001b[34m";
            public static string Magenta => "\u001b[35m";
            public static string Cyan => "\u001b[36m";
            public static string White => "\u001b[97m";         // Supposed to be light gray, but inverted with brightwhite for backwards compatibility

            // Bright colors
            public static string BrightBlack => "\u001b[90m";   // Dark gray
            public static string BrightRed => "\u001b[91m";
            public static string BrightGreen => "\u001b[92m";
            public static string BrightYellow => "\u001b[93m";
            public static string BrightBlue => "\u001b[94m";
            public static string BrightMagenta => "\u001b[95m";
            public static string BrightCyan => "\u001b[96m";
            public static string LightWhite => "\u001b[37m";    // Darker shade

            public static void EnableVirtualTerminal()
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        WindowsConsole.EnableVirtualTerminalProcessing();
                    }
                }
                catch { /* ignore */ }
            }

            public static void WriteReset()
            {
                try { System.Console.Write(ResetCode); } catch { }
            }
        }
    }
}

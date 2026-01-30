using static csRaymarching.Console.ConsoleInteraction;

namespace csRaymarching.Core
{
    public enum ConsoleTheme
    {
        Dark,
        Light,
        Cyan,
        Green,
        Amber
    }

    public static class ConsoleThemeExtensions
    {
        public static ConsoleTheme Next(this ConsoleTheme theme)
        {
            var values = Enum.GetValues<ConsoleTheme>();
            int next = ((int)theme + 1) % values.Length;
            return values[next];
        }

        public static string Primary(this ConsoleTheme theme) => theme switch
        {
            ConsoleTheme.Dark => AnsiConsoleVT.Cyan,
            ConsoleTheme.Light => AnsiConsoleVT.Blue,
            ConsoleTheme.Cyan => AnsiConsoleVT.BrightCyan,
            ConsoleTheme.Green => AnsiConsoleVT.BrightGreen,
            ConsoleTheme.Amber => AnsiConsoleVT.Yellow,
            _ => AnsiConsoleVT.Cyan
        };

        public static string Secondary(this ConsoleTheme theme) => theme switch
        {
            ConsoleTheme.Dark => AnsiConsoleVT.BrightBlack,
            ConsoleTheme.Light => AnsiConsoleVT.LightWhite,
            ConsoleTheme.Cyan => AnsiConsoleVT.Cyan,
            ConsoleTheme.Green => AnsiConsoleVT.Green,
            ConsoleTheme.Amber => AnsiConsoleVT.BrightYellow,
            _ => AnsiConsoleVT.BrightBlack
        };
    }
}
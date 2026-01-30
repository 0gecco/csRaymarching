using System;

// ####  2026 © NiTROZ  ####

namespace csRaymarching.Console
{
    /// <summary>
    /// Provides a 2D character-based canvas for rendering to the console with color support
    /// and half-block Unicode graphics for higher vertical resolution.
    /// </summary>
    public sealed class ConsoleCanvas
    {
        // Half-block Unicode characters for pseudo-pixel rendering
        private const char EmptyChar = ' ';
        private const char FullBlock = '█';
        private const char UpperHalfBlock = '▀';
        private const char LowerHalfBlock = '▄';

        // Bit flags for half-block state
        private const int TopHalfMask = 1;
        private const int BottomHalfMask = 2;
        private const int BothHalvesMask = TopHalfMask | BottomHalfMask;

        private static readonly object _lock = new();

        /// <summary>
        /// The character buffer representing the canvas content.
        /// Dimensions are [height, width].
        /// </summary>
        public static char[,] Chars { get; private set; } = new char[0, 0];

        /// <summary>
        /// The color buffer for each character position.
        /// Null entries indicate no color override (use default).
        /// Dimensions are [height, width].
        /// </summary>
        public static string?[,] Colors { get; private set; } = new string?[0, 0];

        /// <summary>
        /// Current canvas width in characters.
        /// </summary>
        public static int Width => Chars.GetLength(1);

        /// <summary>
        /// Current canvas height in characters.
        /// </summary>
        public static int Height => Chars.GetLength(0);

        /// <summary>
        /// Ensures the canvas buffers match the current console window size.
        /// Reinitializes buffers if the size has changed, otherwise clears them.
        /// </summary>
        public static void Ensure()
        {
            int width, height;

            try
            {
                width = System.Console.WindowWidth;
                height = System.Console.WindowHeight;
            }
            catch (IOException)
            {
                // Console not available (e.g., redirected output)
                return;
            }

            if (width <= 0 || height <= 0)
                return;

            lock (_lock)
            {
                bool sizeChanged = Height != height || Width != width;

                if (sizeChanged)
                {
                    Chars = new char[height, width];
                    Colors = new string?[height, width];
                    FillWithSpaces(Chars);
                }
                else
                {
                    ClearBuffers();
                }
            }
        }

        /// <summary>
        /// Writes a string to the canvas at the specified position.
        /// Characters outside the canvas bounds are clipped.
        /// </summary>
        /// <param name="canvas">The character buffer to write to.</param>
        /// <param name="colors">The color buffer to write to.</param>
        /// <param name="row">The row (y-coordinate) to write at.</param>
        /// <param name="col">The starting column (x-coordinate).</param>
        /// <param name="text">The string to write.</param>
        /// <param name="color">Optional color for all characters (null preserves existing colors).</param>
        public static void PutString(
            char[,] canvas,
            string?[,] colors,
            int row,
            int col,
            string text,
            string? color = null)
        {
            if (canvas is null || colors is null || text is null)
                return;

            int canvasHeight = canvas.GetLength(0);
            int canvasWidth = canvas.GetLength(1);

            if (row < 0 || row >= canvasHeight)
                return;

            for (int i = 0; i < text.Length; i++)
            {
                int targetCol = col + i;

                if (targetCol < 0)
                    continue;

                if (targetCol >= canvasWidth)
                    break; // No point continuing past the right edge

                canvas[row, targetCol] = text[i];

                if (color is not null)
                    colors[row, targetCol] = color;
            }
        }

        /// <summary>
        /// Checks whether the specified position is empty (space or null character).
        /// </summary>
        /// <param name="canvas">The character buffer to check.</param>
        /// <param name="row">The row to check.</param>
        /// <param name="col">The column to check.</param>
        /// <returns>True if the position is within bounds and empty; false otherwise.</returns>
        public static bool IsEmptyAt(char[,] canvas, int row, int col)
        {
            if (canvas is null)
                return false;

            if (!IsInBounds(canvas, row, col))
                return false;

            char c = canvas[row, col];
            return c == '\0' || c == EmptyChar;
        }

        /// <summary>
        /// Sets a half-block character at the specified position, combining with any
        /// existing half-block to create full blocks when both halves are filled.
        /// This enables pseudo-pixel rendering with double vertical resolution.
        /// </summary>
        /// <param name="canvas">The character buffer to modify.</param>
        /// <param name="colors">The color buffer to modify.</param>
        /// <param name="row">The row to modify.</param>
        /// <param name="col">The column to modify.</param>
        /// <param name="isTopHalf">True for upper half-block, false for lower.</param>
        /// <param name="color">The color to apply.</param>
        public static void SetHalfBlock(
            char[,] canvas,
            string?[,] colors,
            int row,
            int col,
            bool isTopHalf,
            string color)
        {
            if (canvas is null || colors is null)
                return;

            if (!IsInBounds(canvas, row, col))
                return;

            int currentMask = DecodeHalfBlockMask(canvas[row, col]);
            int newMask = currentMask | (isTopHalf ? TopHalfMask : BottomHalfMask);

            canvas[row, col] = EncodeHalfBlockChar(newMask);
            colors[row, col] = color;
        }

        /// <summary>
        /// Checks whether the specified coordinates are within the canvas bounds.
        /// </summary>
        private static bool IsInBounds(char[,] canvas, int row, int col)
            => row >= 0
            && row < canvas.GetLength(0)
            && col >= 0
            && col < canvas.GetLength(1);

        /// <summary>
        /// Decodes a half-block character into its bit mask representation.
        /// </summary>
        private static int DecodeHalfBlockMask(char c) => c switch
        {
            FullBlock => BothHalvesMask,
            UpperHalfBlock => TopHalfMask,
            LowerHalfBlock => BottomHalfMask,
            _ => 0
        };

        /// <summary>
        /// Encodes a bit mask into the corresponding half-block character.
        /// </summary>
        private static char EncodeHalfBlockChar(int mask) => mask switch
        {
            TopHalfMask => UpperHalfBlock,
            BottomHalfMask => LowerHalfBlock,
            BothHalvesMask => FullBlock,
            _ => EmptyChar
        };

        /// <summary>
        /// Fills the entire character buffer with spaces.
        /// </summary>
        private static void FillWithSpaces(char[,] canvas)
        {
            int height = canvas.GetLength(0);
            int width = canvas.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    canvas[y, x] = EmptyChar;
                }
            }
        }

        /// <summary>
        /// Clears both buffers - fills Chars with spaces and Colors with null.
        /// </summary>
        private static void ClearBuffers()
        {
            FillWithSpaces(Chars);
            Array.Clear(Colors, 0, Colors.Length);
        }
    }
}
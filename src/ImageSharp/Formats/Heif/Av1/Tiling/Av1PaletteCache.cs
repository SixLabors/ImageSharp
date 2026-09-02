// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Builds the sorted unique palette-color cache shared by AV1 encoder and decoder syntax.
/// </summary>
internal static class Av1PaletteCache
{
    /// <summary>
    /// Merges the sorted colors from the available above and left block palettes.
    /// </summary>
    /// <param name="aboveColors">The above block's sorted base colors.</param>
    /// <param name="leftColors">The left block's sorted base colors.</param>
    /// <param name="cache">The destination cache, which can hold both palettes.</param>
    /// <returns>The number of unique colors written to <paramref name="cache"/>.</returns>
    public static int Merge(
        ReadOnlySpan<ushort> aboveColors,
        ReadOnlySpan<ushort> leftColors,
        Span<ushort> cache)
    {
        int aboveIndex = 0;
        int leftIndex = 0;
        int count = 0;
        while (aboveIndex < aboveColors.Length && leftIndex < leftColors.Length)
        {
            ushort aboveColor = aboveColors[aboveIndex];
            ushort leftColor = leftColors[leftIndex];
            if (leftColor < aboveColor)
            {
                Add(cache, ref count, leftColor);
                leftIndex++;
            }
            else
            {
                Add(cache, ref count, aboveColor);
                aboveIndex++;
                if (leftColor == aboveColor)
                {
                    leftIndex++;
                }
            }
        }

        while (aboveIndex < aboveColors.Length)
        {
            Add(cache, ref count, aboveColors[aboveIndex++]);
        }

        while (leftIndex < leftColors.Length)
        {
            Add(cache, ref count, leftColors[leftIndex++]);
        }

        return count;
    }

    private static void Add(Span<ushort> cache, ref int count, ushort color)
    {
        if (count == 0 || cache[count - 1] != color)
        {
            cache[count++] = color;
        }
    }
}

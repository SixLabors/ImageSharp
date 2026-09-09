// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Derives AV1 palette color-index ordering and entropy contexts from preceding spatial indices.
/// </summary>
internal static class Av1PaletteColorMap
{
    /// <summary>
    /// Derives the palette color order, current color-order index, and entropy context for one map position.
    /// </summary>
    /// <param name="colorIndexMap">The partially or completely populated color-index map.</param>
    /// <param name="row">The current map row.</param>
    /// <param name="column">The current map column.</param>
    /// <param name="paletteSize">The number of colors in the palette.</param>
    /// <param name="colorIndex">The current palette index, or a negative value while decoding.</param>
    /// <param name="colorOrder">The destination color order for the current context.</param>
    /// <param name="colorOrderIndex">The current index in <paramref name="colorOrder" />, or a negative value while decoding.</param>
    /// <returns>The color-index entropy context in the range from zero through four.</returns>
    public static int GetContext(
        Buffer2DRegion<byte> colorIndexMap,
        int row,
        int column,
        int paletteSize,
        int colorIndex,
        Span<byte> colorOrder,
        out int colorOrderIndex)
    {
        Span<int> scores = stackalloc int[Av1Constants.PaletteMaxSize];
        scores.Clear();
        ReadOnlySpan<byte> currentRow = colorIndexMap.DangerousGetRowSpan(row);
        if (column > 0)
        {
            scores[currentRow[column - 1]] += 2;
        }

        if (row > 0)
        {
            ReadOnlySpan<byte> aboveRow = colorIndexMap.DangerousGetRowSpan(row - 1);
            if (column > 0)
            {
                scores[aboveRow[column - 1]]++;
            }

            scores[aboveRow[column]] += 2;
        }

        Span<int> inverseColorOrder = stackalloc int[Av1Constants.PaletteMaxSize];
        for (int i = 0; i < Av1Constants.PaletteMaxSize; i++)
        {
            colorOrder[i] = (byte)i;
            inverseColorOrder[i] = i;
        }

        // Stable descending score order keeps lower palette indices ahead when neighboring scores tie.
        for (int i = 0; i < 3; i++)
        {
            int maximumScore = scores[i];
            int maximumIndex = i;
            for (int j = i + 1; j < paletteSize; j++)
            {
                if (scores[j] > maximumScore)
                {
                    maximumScore = scores[j];
                    maximumIndex = j;
                }
            }

            if (maximumIndex != i)
            {
                byte maximumColor = colorOrder[maximumIndex];
                for (int j = maximumIndex; j > i; j--)
                {
                    scores[j] = scores[j - 1];
                    colorOrder[j] = colorOrder[j - 1];
                    inverseColorOrder[colorOrder[j]] = j;
                }

                scores[i] = maximumScore;
                colorOrder[i] = maximumColor;
                inverseColorOrder[maximumColor] = i;
            }
        }

        colorOrderIndex = colorIndex < 0 ? -1 : inverseColorOrder[colorIndex];
        int contextHash = scores[0] + (2 * scores[1]) + (2 * scores[2]);
        return contextHash switch
        {
            2 => 0,
            5 => 4,
            6 => 3,
            7 => 2,
            8 => 1,
            _ => -1
        };
    }
}

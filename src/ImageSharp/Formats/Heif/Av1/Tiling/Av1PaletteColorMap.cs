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

    /// <summary>
    /// Derives the palette color context and the coded color rank for one map sample while encoding.
    /// </summary>
    /// <remarks>
    /// The encoder needs only the rank of the current color, not the complete neighbor-ordered color list that
    /// the decoder maintains. With at most three neighbors (left, above, above-left), merging duplicates and
    /// ordering the survivors takes a few comparisons, so no score table, inverse order, or selection sort is
    /// needed. The result equals <see cref="GetContext"/> for every map.
    /// </remarks>
    /// <param name="colorIndexMap">The palette index map, addressed with <paramref name="stride"/>.</param>
    /// <param name="stride">The number of map samples between rows.</param>
    /// <param name="row">The sample row. The first sample of the map is coded separately.</param>
    /// <param name="column">The sample column.</param>
    /// <param name="colorOrderIndex">The rank of the sample's color in the neighbor-ordered color list.</param>
    /// <returns>The color-index probability context.</returns>
    public static int GetEncoderContext(
        ReadOnlySpan<byte> colorIndexMap,
        int stride,
        int row,
        int column,
        out int colorOrderIndex)
    {
        int index = (row * stride) + column;
        int currentColor = colorIndexMap[index];
        bool hasAbove = row > 0;
        bool hasLeft = column > 0;
        if (hasAbove != hasLeft)
        {
            // A first-row or first-column sample has one neighbor. That neighbor leads the color order, so every
            // color below it moves down one rank. Its score of two always selects context zero.
            int neighbor = hasAbove ? colorIndexMap[index - stride] : colorIndexMap[index - 1];
            colorOrderIndex = neighbor > currentColor ? currentColor + 1 : neighbor == currentColor ? 0 : currentColor;
            return 0;
        }

        // Visit left, above, then above-left. Direct neighbors weigh two and the diagonal weighs one, so distinct
        // neighbors are already in descending score order with the lower palette index first on the only
        // possible tie.
        const int invalid = byte.MaxValue;
        int color0 = colorIndexMap[index - 1];
        int color1 = colorIndexMap[index - stride];
        int color2 = colorIndexMap[index - stride - 1];
        int score0 = 2;
        int score1 = 2;
        int score2 = 1;
        int validCount = 3;

        // Merge the scores of equal neighbors.
        if (color0 == color1)
        {
            score0 += score1;
            color1 = invalid;
            validCount--;
            if (color0 == color2)
            {
                score0 += score2;
                validCount--;
            }
        }
        else if (color0 == color2)
        {
            score0 += score2;
            validCount--;
        }
        else if (color1 == color2)
        {
            score1 += score2;
            validCount--;
        }

        if (validCount > 1)
        {
            if (color1 == invalid)
            {
                score1 = score2;
                color1 = color2;
            }

            // Equal scores keep the lower palette index first.
            if (score0 < score1 || (score0 == score1 && color0 > color1))
            {
                (score0, score1) = (score1, score0);
                (color0, color1) = (color1, color0);
            }

            if (validCount > 2)
            {
                if (score0 < score2)
                {
                    (score0, score2) = (score2, score0);
                    (color0, color2) = (color2, color0);
                }

                if (score1 < score2)
                {
                    (score1, score2) = (score2, score1);
                    (color1, color2) = (color2, color1);
                }
            }
        }

        // Each ranked neighbor above the current color moves it down one rank, unless the current color is
        // itself a ranked neighbor.
        colorOrderIndex = currentColor;
        if (color0 == currentColor)
        {
            colorOrderIndex = 0;
        }
        else
        {
            if (color0 > currentColor)
            {
                colorOrderIndex++;
            }

            if (validCount > 1)
            {
                if (color1 == currentColor)
                {
                    colorOrderIndex = 1;
                }
                else
                {
                    if (color1 > currentColor)
                    {
                        colorOrderIndex++;
                    }

                    if (validCount > 2)
                    {
                        if (color2 == currentColor)
                        {
                            colorOrderIndex = 2;
                        }
                        else if (color2 > currentColor)
                        {
                            colorOrderIndex++;
                        }
                    }
                }
            }
        }

        // The hash weights the ranked scores 1, 2, 2. Its values 5 through 8 map to contexts 4 through 1.
        int hash = score0;
        if (validCount > 1)
        {
            hash += 2 * score1;
            if (validCount > 2)
            {
                hash += 2 * score2;
            }
        }

        return 9 - hash;
    }
}

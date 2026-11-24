// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

/// <summary>
/// Creates the exact expected frame of a decode that stops early, for formats whose data has no fixed map from bytes
/// to pixels. A decoder of such a format keeps the units that it decodes before the end, in decode order. Each unit
/// after them gets a fill value.
/// </summary>
public static class PartialDecodeExpectation
{
    /// <summary>
    /// Creates the expected frame. The units before the first unit that differs from the complete decode keep the complete
    /// decode. That unit and all units after it get the fill value. Compare the result with the actual frame through
    /// <see cref="ImageComparer"/>. The compare then fails when a unit is only partly decoded, when a unit after the end
    /// is not the fill value, or when a decoded unit comes after a filled unit.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="complete">The decode of the complete data.</param>
    /// <param name="actual">The decode of the data that ends early.</param>
    /// <param name="unitWidth">The number of pixels in one decode unit of a row. Use the frame width for whole rows.</param>
    /// <param name="bottomUp">Whether the decoder writes the rows from the bottom row up.</param>
    /// <param name="fill">Gets the fill value of a pixel from the pixel of the complete decode.</param>
    /// <returns>The expected frame as a single-frame image.</returns>
    public static Image<TPixel> Create<TPixel>(
        ImageFrame<TPixel> complete,
        ImageFrame<TPixel> actual,
        int unitWidth,
        bool bottomUp,
        Func<TPixel, TPixel> fill)
        where TPixel : unmanaged, IPixel<TPixel>
        => Create(complete, actual, unitWidth, bottomUp, fill, null);

    /// <summary>
    /// Creates the expected frame for a decoder that also keeps the decoded part of the unit in which the data ends.
    /// That unit keeps the complete decode up to its first pixel that differs, and its other pixels get
    /// <paramref name="partialUnitFill"/>. The units after it get <paramref name="fill"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="complete">The decode of the complete data.</param>
    /// <param name="actual">The decode of the data that ends early.</param>
    /// <param name="unitWidth">The number of pixels in one decode unit of a row. Use the frame width for whole rows.</param>
    /// <param name="bottomUp">Whether the decoder writes the rows from the bottom row up.</param>
    /// <param name="fill">Gets the fill value of a pixel from the pixel of the complete decode.</param>
    /// <param name="partialUnitFill">
    /// Gets the value of a pixel after the end of the data in the unit in which the data ends, or <see langword="null"/>
    /// when the decoder fills that whole unit with <paramref name="fill"/>.
    /// </param>
    /// <returns>The expected frame as a single-frame image.</returns>
    public static Image<TPixel> Create<TPixel>(
        ImageFrame<TPixel> complete,
        ImageFrame<TPixel> actual,
        int unitWidth,
        bool bottomUp,
        Func<TPixel, TPixel> fill,
        Func<TPixel, TPixel> partialUnitFill)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Assert.Equal(complete.Size, actual.Size);
        Assert.Equal(0, complete.Width % unitWidth);

        int unitsPerRow = complete.Width / unitWidth;
        int unitCount = unitsPerRow * complete.Height;
        int firstFilledUnit = unitCount;
        int decodedPixels = 0;
        for (int unit = 0; unit < unitCount; unit++)
        {
            int y = GetRow(unit / unitsPerRow, complete.Height, bottomUp);
            int x = (unit % unitsPerRow) * unitWidth;
            Span<TPixel> completeUnit = complete.PixelBuffer.DangerousGetRowSpan(y).Slice(x, unitWidth);
            Span<TPixel> actualUnit = actual.PixelBuffer.DangerousGetRowSpan(y).Slice(x, unitWidth);
            if (!completeUnit.SequenceEqual(actualUnit))
            {
                firstFilledUnit = unit;
                decodedPixels = completeUnit.CommonPrefixLength(actualUnit);
                break;
            }
        }

        // The data ends early, so the decode must keep some units and fill some units.
        Assert.InRange(firstFilledUnit, 1, unitCount - 1);

        Image<TPixel> expected = new(complete.Width, complete.Height);
        for (int unit = 0; unit < unitCount; unit++)
        {
            int y = GetRow(unit / unitsPerRow, complete.Height, bottomUp);
            int x = (unit % unitsPerRow) * unitWidth;
            Span<TPixel> source = complete.PixelBuffer.DangerousGetRowSpan(y).Slice(x, unitWidth);
            Span<TPixel> destination = expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y).Slice(x, unitWidth);
            for (int i = 0; i < unitWidth; i++)
            {
                if (unit < firstFilledUnit)
                {
                    destination[i] = source[i];
                }
                else if (unit == firstFilledUnit && partialUnitFill is not null)
                {
                    destination[i] = i < decodedPixels ? source[i] : partialUnitFill(source[i]);
                }
                else
                {
                    destination[i] = fill(source[i]);
                }
            }
        }

        return expected;
    }

    /// <summary>
    /// Gets the image row of a row in decode order.
    /// </summary>
    /// <param name="decodeRow">The row index in decode order.</param>
    /// <param name="height">The image height.</param>
    /// <param name="bottomUp">Whether the decoder writes the rows from the bottom row up.</param>
    /// <returns>The image row index.</returns>
    private static int GetRow(int decodeRow, int height, bool bottomUp)
        => bottomUp ? height - 1 - decodeRow : decodeRow;
}

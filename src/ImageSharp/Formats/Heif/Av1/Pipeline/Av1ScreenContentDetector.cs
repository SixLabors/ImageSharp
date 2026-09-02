// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Detects frames that can benefit from AV1 screen-content coding tools.
/// </summary>
internal static class Av1ScreenContentDetector
{
    private const int DetectionBlockLength = 16;
    private const int DetectionBlockArea = DetectionBlockLength * DetectionBlockLength;
    private const int MaximumPaletteColorCount = 4;

    /// <summary>
    /// Converts native source samples to the eight-bit domain used by screen-content detection.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    private interface ISampleOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Converts one native sample to eight-bit precision.
        /// </summary>
        /// <param name="value">The source sample.</param>
        /// <param name="bitDepthShift">The number of low bits removed from high-bit-depth samples.</param>
        /// <returns>The normalized sample.</returns>
        public static abstract int ToEightBit(TSample value, int bitDepthShift);
    }

    /// <summary>
    /// Detects palette-friendly content in an eight-bit source frame.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <returns><see langword="true"/> when palette tools should be enabled; otherwise, <see langword="false"/>.</returns>
    public static bool IsPaletteLikely(Av1EncoderFrame<byte> source)
        => IsPaletteLikely<byte, ByteSampleOperator>(source);

    /// <summary>
    /// Detects palette-friendly content in a high-bit-depth source frame.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <returns><see langword="true"/> when palette tools should be enabled; otherwise, <see langword="false"/>.</returns>
    public static bool IsPaletteLikely(Av1EncoderFrame<ushort> source)
        => IsPaletteLikely<ushort, UShortSampleOperator>(source);

    private static bool IsPaletteLikely<TSample, TOperator>(Av1EncoderFrame<TSample> source)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        Av1EncoderFrame<TSample>.PlanarView view = source.View;
        int width = source.Width;
        int height = source.Height;
        long frameArea = (long)width * height;
        int bitDepthShift = source.LumaBitDepth - 8;
        int paletteBlockCount = 0;
        Span<ulong> seenColors = stackalloc ulong[4];

        // Complete 16x16 blocks and the strict frame-area threshold preserve the reference detector's decision.
        for (int blockRow = 0; blockRow + DetectionBlockLength <= height; blockRow += DetectionBlockLength)
        {
            for (int blockColumn = 0; blockColumn + DetectionBlockLength <= width; blockColumn += DetectionBlockLength)
            {
                seenColors.Clear();
                int colorCount = 0;
                for (int row = 0; row < DetectionBlockLength && colorCount <= MaximumPaletteColorCount; row++)
                {
                    ReadOnlySpan<TSample> samples = view
                        .GetLumaRowSpan(blockRow + row)
                        .Slice(blockColumn, DetectionBlockLength);

                    // Histogram updates depend on each sample value, so a compact scalar bitset avoids gather/scatter overhead.
                    for (int column = 0; column < samples.Length; column++)
                    {
                        int value = TOperator.ToEightBit(samples[column], bitDepthShift);
                        int wordIndex = value >> 6;
                        ulong mask = 1UL << (value & 63);
                        ref ulong word = ref seenColors[wordIndex];
                        if ((word & mask) == 0)
                        {
                            word |= mask;
                            colorCount++;
                            if (colorCount > MaximumPaletteColorCount)
                            {
                                break;
                            }
                        }
                    }
                }

                if (colorCount > 1 && colorCount <= MaximumPaletteColorCount)
                {
                    paletteBlockCount++;
                    if ((long)paletteBlockCount * DetectionBlockArea * 10 > frameArea)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Preserves native eight-bit samples.
    /// </summary>
    private readonly struct ByteSampleOperator : ISampleOperator<byte>
    {
        /// <inheritdoc/>
        public static int ToEightBit(byte value, int bitDepthShift) => value;
    }

    /// <summary>
    /// Normalizes high-bit-depth samples to eight-bit precision.
    /// </summary>
    private readonly struct UShortSampleOperator : ISampleOperator<ushort>
    {
        /// <inheritdoc/>
        public static int ToEightBit(ushort value, int bitDepthShift) => value >> bitDepthShift;
    }
}

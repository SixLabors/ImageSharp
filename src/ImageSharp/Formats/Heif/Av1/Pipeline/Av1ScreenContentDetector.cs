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
        /// Converts one native sample to an integer without changing its precision.
        /// </summary>
        /// <param name="value">The source sample.</param>
        /// <returns>The native sample value.</returns>
        public static abstract int ToInt32(TSample value);
    }

    /// <summary>
    /// Detects palette-friendly content in an eight-bit source frame.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <returns><see langword="true"/> when palette tools should be enabled; otherwise, <see langword="false"/>.</returns>
    public static bool IsPaletteLikely(Av1EncoderFrame<byte> source)
    {
        Detect(source, out bool allowScreenContentTools, out _);
        return allowScreenContentTools;
    }

    /// <summary>
    /// Detects palette-friendly content in a high-bit-depth source frame.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <returns><see langword="true"/> when palette tools should be enabled; otherwise, <see langword="false"/>.</returns>
    public static bool IsPaletteLikely(Av1EncoderFrame<ushort> source)
    {
        Detect(source, out bool allowScreenContentTools, out _);
        return allowScreenContentTools;
    }

    /// <summary>
    /// Detects palette and intra-block-copy content in an eight-bit source frame.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <param name="allowScreenContentTools">Receives whether palette syntax should be enabled.</param>
    /// <param name="allowIntraBlockCopy">Receives whether intra-block copy should be enabled.</param>
    public static void Detect(
        Av1EncoderFrame<byte> source,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => Detect<byte, ByteSampleOperator>(source, out allowScreenContentTools, out allowIntraBlockCopy);

    /// <summary>
    /// Detects palette and intra-block-copy content in a high-bit-depth source frame.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <param name="allowScreenContentTools">Receives whether palette syntax should be enabled.</param>
    /// <param name="allowIntraBlockCopy">Receives whether intra-block copy should be enabled.</param>
    public static void Detect(
        Av1EncoderFrame<ushort> source,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => Detect<ushort, UShortSampleOperator>(source, out allowScreenContentTools, out allowIntraBlockCopy);

    private static void Detect<TSample, TOperator>(
        Av1EncoderFrame<TSample> source,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        Av1EncoderFrame<TSample>.PlanarView view = source.View;
        int width = source.Width;
        int height = source.Height;
        long frameArea = (long)width * height;
        int bitDepthShift = source.LumaBitDepth - 8;
        int paletteBlockCount = 0;
        int intraBlockCopyBlockCount = 0;
        Span<ulong> seenColors = stackalloc ulong[4];
        allowScreenContentTools = false;
        allowIntraBlockCopy = false;

        // Complete 16x16 blocks and the strict frame-area threshold preserve the reference detector's decision.
        for (int blockRow = 0; blockRow + DetectionBlockLength <= height; blockRow += DetectionBlockLength)
        {
            for (int blockColumn = 0; blockColumn + DetectionBlockLength <= width; blockColumn += DetectionBlockLength)
            {
                seenColors.Clear();
                int colorCount = 0;
                long sum = 0;
                long sumOfSquares = 0;
                for (int row = 0; row < DetectionBlockLength && colorCount <= MaximumPaletteColorCount; row++)
                {
                    ReadOnlySpan<TSample> samples = view
                        .GetLumaRowSpan(blockRow + row)
                        .Slice(blockColumn, DetectionBlockLength);

                    // Histogram updates depend on each sample value, so a compact scalar bitset avoids gather/scatter overhead.
                    for (int column = 0; column < samples.Length; column++)
                    {
                        int nativeValue = TOperator.ToInt32(samples[column]);
                        int value = nativeValue >> bitDepthShift;
                        int wordIndex = value >> 6;
                        ulong mask = 1UL << (value & 63);
                        ref ulong word = ref seenColors[wordIndex];
                        int centeredValue = nativeValue - (128 << bitDepthShift);
                        sum += centeredValue;
                        sumOfSquares += (long)centeredValue * centeredValue;
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
                    long normalizedSum = sum;
                    long normalizedSumOfSquares = sumOfSquares;
                    if (bitDepthShift != 0)
                    {
                        normalizedSum = RoundPowerOfTwo(sum, bitDepthShift);
                        normalizedSumOfSquares = RoundPowerOfTwo(sumOfSquares, bitDepthShift * 2);
                    }

                    long variance = normalizedSumOfSquares - ((normalizedSum * normalizedSum) >> 8);
                    if (variance >= DetectionBlockArea / 2)
                    {
                        intraBlockCopyBlockCount++;
                    }

                    allowScreenContentTools = (long)paletteBlockCount * DetectionBlockArea * 10 > frameArea;
                    allowIntraBlockCopy = allowScreenContentTools &&
                        (long)intraBlockCopyBlockCount * DetectionBlockArea * 12 > frameArea;

                    if (allowIntraBlockCopy)
                    {
                        return;
                    }
                }
            }
        }
    }

    private static long RoundPowerOfTwo(long value, int shift)
        => (value + (1L << (shift - 1))) >> shift;

    /// <summary>
    /// Preserves native eight-bit samples.
    /// </summary>
    private readonly struct ByteSampleOperator : ISampleOperator<byte>
    {
        /// <inheritdoc/>
        public static int ToInt32(byte value) => value;
    }

    /// <summary>
    /// Normalizes high-bit-depth samples to eight-bit precision.
    /// </summary>
    private readonly struct UShortSampleOperator : ISampleOperator<ushort>
    {
        /// <inheritdoc/>
        public static int ToInt32(ushort value) => value;
    }
}

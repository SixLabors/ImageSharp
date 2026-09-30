// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Detects frames that can benefit from AV1 screen-content coding tools.
/// </summary>
internal static partial class Av1ScreenContentDetector
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
        /// Converts an integer in the native sample range to a sample.
        /// </summary>
        /// <param name="value">The native sample value.</param>
        /// <returns>The sample.</returns>
        public static abstract TSample FromInt32(int value);

        /// <summary>
        /// Drops the bits above eight from one row of samples.
        /// </summary>
        /// <param name="source">The native samples.</param>
        /// <param name="shift">The number of bits above eight.</param>
        /// <param name="destination">Receives the eight-bit samples.</param>
        public static abstract void ToEightBit(ReadOnlySpan<TSample> source, int shift, Span<byte> destination);

        /// <summary>
        /// Measures the signed sum and squared sum of the differences between a 16x16 block and a repeated row.
        /// </summary>
        /// <param name="source">The block samples, starting at its top-left sample.</param>
        /// <param name="sourceStride">The distance, in samples, between block rows.</param>
        /// <param name="reference">The reference row compared with every block row.</param>
        /// <param name="sum">Receives the sum of the differences.</param>
        /// <param name="sumOfSquares">Receives the sum of the squared differences.</param>
        public static abstract void GetMoments(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            out int sum,
            out long sumOfSquares);
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
    /// <returns>Whether the frame is classified as screen content for encoder decisions.</returns>
    public static bool Detect(
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
    /// <returns>Whether the frame is classified as screen content for encoder decisions.</returns>
    public static bool Detect(
        Av1EncoderFrame<ushort> source,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => Detect<ushort, UShortSampleOperator>(source, out allowScreenContentTools, out allowIntraBlockCopy);

    private static bool Detect<TSample, TOperator>(
        Av1EncoderFrame<TSample> source,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        Av1EncoderFrame<TSample>.PlanarView view = source.CodedView;
        int width = (source.Width + 7) & ~7;
        int height = (source.Height + 7) & ~7;
        long frameArea = (long)width * height;
        int bitDepthShift = source.LumaBitDepth - 8;
        int paletteBlockCount = 0;
        int intraBlockCopyBlockCount = 0;
        Span<byte> block = stackalloc byte[DetectionBlockArea];
        allowScreenContentTools = false;
        allowIntraBlockCopy = false;

        // The moments are measured against the middle of the native sample range.
        Span<TSample> middle = stackalloc TSample[DetectionBlockLength];
        middle.Fill(TOperator.FromInt32(128 << bitDepthShift));

        // Analyze complete 16x16 blocks in the source's eight-sample-aligned extent. Padding participates in
        // both color counting and the area thresholds, while any final partial block is omitted.
        for (int blockRow = 0; blockRow + DetectionBlockLength <= height; blockRow += DetectionBlockLength)
        {
            for (int blockColumn = 0; blockColumn + DetectionBlockLength <= width; blockColumn += DetectionBlockLength)
            {
                // Colors are counted in the eight-bit domain, and the moments only matter for a palette block.
                CopyEightBitBlock<TSample, TOperator>(view, blockRow, blockColumn, bitDepthShift, block);
                CountColorsWithThreshold(block, MaximumPaletteColorCount, out int colorCount);
                if (colorCount > 1 && colorCount <= MaximumPaletteColorCount)
                {
                    paletteBlockCount++;
                    GetBlockMoments<TSample, TOperator>(view, blockRow, blockColumn, middle, out int sum, out long sumOfSquares);
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
                        return true;
                    }
                }
            }
        }

        return (long)paletteBlockCount * DetectionBlockArea * 10 > frameArea * 4 &&
            (long)intraBlockCopyBlockCount * DetectionBlockArea * 30 > frameArea;
    }

    /// <summary>
    /// Decides the screen-content tools of an eight-bit frame, as <c>av1_set_screen_content_options</c> does for
    /// automatic screen-content selection.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <param name="allIntra">Whether the encoder runs the reference's all-intra mode.</param>
    /// <param name="speed">The encoder speed.</param>
    /// <param name="allowScreenContentTools">Receives whether palette syntax should be enabled.</param>
    /// <param name="allowIntraBlockCopy">Receives whether intra-block copy should be enabled.</param>
    /// <returns>Whether the frame is classified as screen content for encoder decisions.</returns>
    public static bool SetScreenContentOptions(
        Av1EncoderFrame<byte> source,
        bool allIntra,
        HeifEncodingSpeed speed,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => SetScreenContentOptions<byte, ByteSampleOperator>(source, allIntra, speed, out allowScreenContentTools, out allowIntraBlockCopy);

    /// <summary>
    /// Decides the screen-content tools of a high-bit-depth frame, as <c>av1_set_screen_content_options</c> does
    /// for automatic screen-content selection.
    /// </summary>
    /// <param name="source">The converted source frame.</param>
    /// <param name="allIntra">Whether the encoder runs the reference's all-intra mode.</param>
    /// <param name="speed">The encoder speed.</param>
    /// <param name="allowScreenContentTools">Receives whether palette syntax should be enabled.</param>
    /// <param name="allowIntraBlockCopy">Receives whether intra-block copy should be enabled.</param>
    /// <returns>Whether the frame is classified as screen content for encoder decisions.</returns>
    public static bool SetScreenContentOptions(
        Av1EncoderFrame<ushort> source,
        bool allIntra,
        HeifEncodingSpeed speed,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => SetScreenContentOptions<ushort, UShortSampleOperator>(source, allIntra, speed, out allowScreenContentTools, out allowIntraBlockCopy);

    private static bool SetScreenContentOptions<TSample, TOperator>(
        Av1EncoderFrame<TSample> source,
        bool allIntra,
        HeifEncodingSpeed speed,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        // Realtime encoding never evaluates the tools. Neither does all-intra speed 9, which picks modes
        // without rate-distortion search and without the hybrid intra search.
        bool realtime = !allIntra && speed >= HeifEncodingSpeed.Level7;
        if (realtime || (allIntra && speed >= HeifEncodingSpeed.Level9))
        {
            allowScreenContentTools = false;
            allowIntraBlockCopy = false;
            return false;
        }

        // All-intra mode defaults to the anti-aliasing aware detection, sampling
        // half of the blocks from speed 3.
        return allIntra
            ? DetectAntialiasingAware<TSample, TOperator>(
                source, speed >= HeifEncodingSpeed.Level3, out allowScreenContentTools, out allowIntraBlockCopy)
            : Detect<TSample, TOperator>(source, out allowScreenContentTools, out allowIntraBlockCopy);
    }

    /// <summary>
    /// Classifies 16x16 luma blocks as simple palette, complex palette, or photo-like blocks, dilating complex
    /// blocks with their dominant color so anti-aliased edge colors do not count, as
    /// <c>estimate_screen_content_antialiasing_aware</c> does.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The native sample operations.</typeparam>
    /// <param name="source">The converted source frame.</param>
    /// <param name="fastDetection">Whether to visit only the blocks of a checkerboard and count each twice.</param>
    /// <param name="allowScreenContentTools">Receives whether palette syntax should be enabled.</param>
    /// <param name="allowIntraBlockCopy">Receives whether intra-block copy should be enabled.</param>
    /// <returns>Whether the frame is classified as screen content for encoder decisions.</returns>
    private static bool DetectAntialiasingAware<TSample, TOperator>(
        Av1EncoderFrame<TSample> source,
        bool fastDetection,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        // Text and graphics without anti-aliasing, or graphics with four colors.
        const int simpleColorThreshold = 4;

        // Candidates for anti-aliased text and graphics with a wider palette, before and after dilation.
        const int complexInitialColorThreshold = 40;
        const int complexFinalColorThreshold = 6;

        // The per-sample variance that separates low- and high-variance blocks.
        const int varianceThreshold = 5;

        Av1EncoderFrame<TSample>.PlanarView view = source.CodedView;
        int width = (source.Width + 7) & ~7;
        int height = (source.Height + 7) & ~7;
        long area = (long)width * height;
        int bitDepthShift = source.LumaBitDepth - 8;
        Span<byte> block = stackalloc byte[DetectionBlockArea];
        Span<byte> dilated = stackalloc byte[DetectionBlockArea];

        // Variance is measured against a flat block at the middle of the native sample range (get_var_offs).
        Span<TSample> middle = stackalloc TSample[DetectionBlockLength];
        middle.Fill(TOperator.FromInt32(128 << bitDepthShift));

        long paletteCount = 0;
        long intraBlockCopyCount = 0;
        long photoCount = 0;
        int multiplier = fastDetection ? 2 : 1;
        for (int row = 0; row + DetectionBlockLength <= height; row += DetectionBlockLength)
        {
            // Fast detection offsets alternate block rows by one block, forming a checkerboard.
            int firstColumn = fastDetection && ((row / DetectionBlockLength) & 1) != 0 ? DetectionBlockLength : 0;
            for (int column = firstColumn; column + DetectionBlockLength <= width; column += DetectionBlockLength * multiplier)
            {
                // Colors are counted in the eight-bit domain.
                CopyEightBitBlock<TSample, TOperator>(view, row, column, bitDepthShift, block);
                bool underThreshold = CountColorsWithThreshold(block, complexInitialColorThreshold, out int colorCount);
                if (colorCount > 1 && underThreshold)
                {
                    if (colorCount <= simpleColorThreshold)
                    {
                        // A simple block can use palette mode; high variance also makes it an intra block copy candidate.
                        paletteCount++;
                        if (GetPerPixelVariance<TSample, TOperator>(view, row, column, middle, bitDepthShift) > varianceThreshold)
                        {
                            intraBlockCopyCount++;
                        }
                    }
                    else
                    {
                        // A complex block counts only when its dominant color, grown over its neighbors, leaves a
                        // small palette, and only when its variance is high.
                        DilateBlock(block, dilated);
                        underThreshold = CountColorsWithThreshold(dilated, complexFinalColorThreshold, out _);
                        if (underThreshold &&
                            GetPerPixelVariance<TSample, TOperator>(view, row, column, middle, bitDepthShift) > varianceThreshold)
                        {
                            paletteCount++;
                            intraBlockCopyCount++;
                        }
                    }
                }
                else if (colorCount > complexInitialColorThreshold)
                {
                    photoCount++;
                }
            }
        }

        // Fast detection weighs each visited block twice.
        paletteCount *= multiplier;
        intraBlockCopyCount *= multiplier;
        photoCount *= multiplier;

        // Photo-like blocks count against the palette and intra block copy candidates at 1/16 weight. Intra block
        // copy disables the loop filters, so it needs the stricter threshold.
        allowScreenContentTools = (paletteCount - (photoCount / 16)) * DetectionBlockArea * 10 > area;
        allowIntraBlockCopy = allowScreenContentTools &&
            (intraBlockCopyCount - (photoCount / 16)) * DetectionBlockArea * 12 > area;

        return allowIntraBlockCopy ||
            (paletteCount * DetectionBlockArea * 15 > area * 4 && intraBlockCopyCount * DetectionBlockArea * 30 > area);
    }

    /// <summary>
    /// Measures the per-sample variance of a 16x16 source block against a flat mid-range block, as
    /// <c>av1_get_perpixel_variance</c> does with the 8-, 10-, or 12-bit variance function.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The native sample operations.</typeparam>
    /// <param name="view">The source planes.</param>
    /// <param name="row">The block's top row.</param>
    /// <param name="column">The block's left column.</param>
    /// <param name="middle">One row of mid-range samples.</param>
    /// <param name="bitDepthShift">The number of bits above eight.</param>
    /// <returns>The rounded per-sample variance.</returns>
    private static long GetPerPixelVariance<TSample, TOperator>(
        Av1EncoderFrame<TSample>.PlanarView view,
        int row,
        int column,
        ReadOnlySpan<TSample> middle,
        int bitDepthShift)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        GetBlockMoments<TSample, TOperator>(view, row, column, middle, out int sum, out long sumOfSquares);

        // High bit depths round both moments back to the eight-bit scale before combining them, and a
        // negative result clamps to zero.
        long normalizedSum = sum;
        long normalizedSumOfSquares = sumOfSquares;
        if (bitDepthShift != 0)
        {
            normalizedSum = RoundPowerOfTwo(sum, bitDepthShift);
            normalizedSumOfSquares = RoundPowerOfTwo(sumOfSquares, bitDepthShift * 2);
        }

        long variance = Math.Max(normalizedSumOfSquares - (normalizedSum * normalizedSum / DetectionBlockArea), 0);
        return RoundPowerOfTwo(variance, 8);
    }

    /// <summary>
    /// Measures the signed sum and squared sum of the differences between a 16x16 luma block and a flat block.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The native sample operations.</typeparam>
    /// <param name="view">The source planes.</param>
    /// <param name="row">The block's top row.</param>
    /// <param name="column">The block's left column.</param>
    /// <param name="middle">One row of the flat block's samples.</param>
    /// <param name="sum">Receives the sum of the differences.</param>
    /// <param name="sumOfSquares">Receives the sum of the squared differences.</param>
    private static void GetBlockMoments<TSample, TOperator>(
        Av1EncoderFrame<TSample>.PlanarView view,
        int row,
        int column,
        ReadOnlySpan<TSample> middle,
        out int sum,
        out long sumOfSquares)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        Av1PlaneRegion<TSample> luma = view.GetPlane(Av1Plane.Y);
        TOperator.GetMoments(luma.Samples[luma.GetOffset(column, row)..], luma.Stride, middle, out sum, out sumOfSquares);
    }

    /// <summary>
    /// Copies a 16x16 luma block to eight-bit samples.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The native sample operations.</typeparam>
    /// <param name="view">The source planes.</param>
    /// <param name="row">The block's top row.</param>
    /// <param name="column">The block's left column.</param>
    /// <param name="bitDepthShift">The number of bits above eight.</param>
    /// <param name="block">Receives the eight-bit block, one row after another.</param>
    private static void CopyEightBitBlock<TSample, TOperator>(
        Av1EncoderFrame<TSample>.PlanarView view,
        int row,
        int column,
        int bitDepthShift,
        Span<byte> block)
        where TSample : unmanaged
        where TOperator : struct, ISampleOperator<TSample>
    {
        for (int y = 0; y < DetectionBlockLength; y++)
        {
            TOperator.ToEightBit(
                view.GetLumaRowSpan(row + y).Slice(column, DetectionBlockLength),
                bitDepthShift,
                block.Slice(y * DetectionBlockLength, DetectionBlockLength));
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
        public static byte FromInt32(int value) => (byte)value;

        /// <inheritdoc/>
        public static void ToEightBit(ReadOnlySpan<byte> source, int shift, Span<byte> destination)
            => source.CopyTo(destination);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> reference,
            out int sum,
            out long sumOfSquares)
            => Av1ResidualBuilder.GetMoments(
                source, sourceStride, reference, 0, DetectionBlockLength, DetectionBlockLength, out sum, out sumOfSquares);
    }

    /// <summary>
    /// Normalizes high-bit-depth samples to eight-bit precision.
    /// </summary>
    private readonly struct UShortSampleOperator : ISampleOperator<ushort>
    {
        /// <inheritdoc/>
        public static ushort FromInt32(int value) => (ushort)value;

        /// <inheritdoc/>
        public static void ToEightBit(ReadOnlySpan<ushort> source, int shift, Span<byte> destination)
            => Av1ImagePyramid.Narrow(source, destination, shift);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> reference,
            out int sum,
            out long sumOfSquares)
            => Av1ResidualBuilder.GetMoments(
                source, sourceStride, reference, 0, DetectionBlockLength, DetectionBlockLength, out sum, out sumOfSquares);
    }
}

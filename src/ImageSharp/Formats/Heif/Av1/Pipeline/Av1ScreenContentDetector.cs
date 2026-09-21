// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

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

        /// <summary>
        /// Converts an integer in the native sample range to a sample.
        /// </summary>
        /// <param name="value">The native sample value.</param>
        /// <returns>The sample.</returns>
        public static abstract TSample FromInt32(int value);

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
        Span<ulong> seenColors = stackalloc ulong[4];
        allowScreenContentTools = false;
        allowIntraBlockCopy = false;

        // Analyze complete 16x16 blocks in the source's eight-sample-aligned extent. Padding participates in
        // both color counting and the area thresholds, while any final partial block is omitted.
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
    /// automatic screen-content selection (encoder.c L2447-2486).
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
    /// for automatic screen-content selection (encoder.c L2447-2486).
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
        // without rate-distortion search and without the hybrid intra search (speed_features.c L233-253).
        bool realtime = !allIntra && speed >= HeifEncodingSpeed.Level7;
        if (realtime || (allIntra && speed >= HeifEncodingSpeed.Level9))
        {
            allowScreenContentTools = false;
            allowIntraBlockCopy = false;
            return false;
        }

        // All-intra mode defaults to the anti-aliasing aware detection (av1_cx_iface.c L3091-3093), sampling
        // half of the blocks from speed 3 (speed_features.c L98).
        return allIntra
            ? DetectAntialiasingAware<TSample, TOperator>(
                source, speed >= HeifEncodingSpeed.Level3, out allowScreenContentTools, out allowIntraBlockCopy)
            : Detect<TSample, TOperator>(source, out allowScreenContentTools, out allowIntraBlockCopy);
    }

    /// <summary>
    /// Classifies 16x16 luma blocks as simple palette, complex palette, or photo-like blocks, dilating complex
    /// blocks with their dominant color so anti-aliased edge colors do not count, as
    /// <c>estimate_screen_content_antialiasing_aware</c> does (encoder.c L2228-2439).
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
                for (int y = 0; y < DetectionBlockLength; y++)
                {
                    ReadOnlySpan<TSample> samples = view.GetLumaRowSpan(row + y).Slice(column, DetectionBlockLength);
                    Span<byte> blockRow = block.Slice(y * DetectionBlockLength, DetectionBlockLength);
                    for (int x = 0; x < DetectionBlockLength; x++)
                    {
                        blockRow[x] = (byte)(TOperator.ToInt32(samples[x]) >> bitDepthShift);
                    }
                }

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
    /// Counts the distinct values of a block, stopping once the count exceeds a threshold, as
    /// <c>av1_count_colors_with_threshold</c> does (intra_mode_search.c L381-400).
    /// </summary>
    /// <param name="block">The eight-bit block samples.</param>
    /// <param name="threshold">The largest count of interest.</param>
    /// <param name="colorCount">Receives the count, which is one above the threshold when the scan stopped early.</param>
    /// <returns><see langword="true"/> when the count does not exceed the threshold.</returns>
    private static bool CountColorsWithThreshold(ReadOnlySpan<byte> block, int threshold, out int colorCount)
    {
        Span<ulong> seen = stackalloc ulong[4];
        seen.Clear();
        colorCount = 0;
        for (int i = 0; i < block.Length; i++)
        {
            int value = block[i];
            ref ulong word = ref seen[value >> 6];
            ulong mask = 1UL << (value & 63);
            if ((word & mask) == 0)
            {
                word |= mask;
                if (++colorCount > threshold)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Grows the most frequent value of a block over its eight neighbors, as <c>av1_dilate_block</c> does
    /// (encoder.c L2160-2211). The first value to reach the highest count is the dominant one
    /// (<c>av1_find_dominant_value</c>, encoder.c L2118-2138).
    /// </summary>
    /// <param name="block">The eight-bit block samples.</param>
    /// <param name="dilated">The dilated block.</param>
    private static void DilateBlock(ReadOnlySpan<byte> block, Span<byte> dilated)
    {
        Span<int> counts = stackalloc int[256];
        counts.Clear();
        int dominantCount = 0;
        byte dominant = 0;
        for (int i = 0; i < block.Length; i++)
        {
            byte value = block[i];
            if (++counts[value] > dominantCount)
            {
                dominant = value;
                dominantCount = counts[value];
            }
        }

        block.CopyTo(dilated);
        const int last = DetectionBlockLength - 1;
        for (int row = 0; row < DetectionBlockLength; row++)
        {
            for (int column = 0; column < DetectionBlockLength; column++)
            {
                if (block[(row * DetectionBlockLength) + column] != dominant)
                {
                    continue;
                }

                // The dominant value covers every neighbor inside the block, including the diagonals.
                int top = Math.Max(row - 1, 0);
                int bottom = Math.Min(row + 1, last);
                int left = Math.Max(column - 1, 0);
                int right = Math.Min(column + 1, last);
                for (int y = top; y <= bottom; y++)
                {
                    dilated.Slice((y * DetectionBlockLength) + left, right - left + 1).Fill(dominant);
                }
            }
        }
    }

    /// <summary>
    /// Measures the per-sample variance of a 16x16 source block against a flat mid-range block, as
    /// <c>av1_get_perpixel_variance</c> does with the 8-, 10-, or 12-bit variance function (encodeframe.c L190-203,
    /// variance.c L328-360).
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
        Buffer2DRegion<TSample> luma = view.GetPlane(Av1Plane.Y);
        int offset = ((luma.Bounds.Y + row) * luma.Stride) + luma.Bounds.X + column;
        TOperator.GetMoments(
            luma.Buffer.DangerousGetSingleSpan()[offset..],
            luma.Stride,
            middle,
            out int sum,
            out long sumOfSquares);

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

    private static long RoundPowerOfTwo(long value, int shift)
        => (value + (1L << (shift - 1))) >> shift;

    /// <summary>
    /// Preserves native eight-bit samples.
    /// </summary>
    private readonly struct ByteSampleOperator : ISampleOperator<byte>
    {
        /// <inheritdoc/>
        public static int ToInt32(byte value) => value;

        /// <inheritdoc/>
        public static byte FromInt32(int value) => (byte)value;

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
        public static int ToInt32(ushort value) => value;

        /// <inheritdoc/>
        public static ushort FromInt32(int value) => (ushort)value;

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

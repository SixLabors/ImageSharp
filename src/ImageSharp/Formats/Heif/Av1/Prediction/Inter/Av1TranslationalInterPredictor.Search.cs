// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// The sample capacity for a 128-column search prediction and its eight-tap vertical support.
    /// </summary>
    public const int SearchPredictionBufferLength = 136 * 128;

    /// <summary>
    /// Produces a search prediction, rounding and clipping each separable pass to the component precision.
    /// </summary>
    /// <param name="source">The bordered reference plane.</param>
    /// <param name="sourceStride">The reference row stride in samples.</param>
    /// <param name="sourceOrigin">The integer prediction origin.</param>
    /// <param name="buffer">The borrowed search buffer; the packed result occupies its first width times height samples.</param>
    /// <param name="width">The prediction width.</param>
    /// <param name="height">The prediction height.</param>
    /// <param name="horizontalPhase">The horizontal fraction in eighth-sample units.</param>
    /// <param name="verticalPhase">The vertical fraction in eighth-sample units.</param>
    /// <param name="taps">The selected two-, four-, or eight-tap search filter.</param>
    public static void PredictForSearch(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> buffer,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int taps)
    {
        if ((horizontalPhase | verticalPhase) == 0)
        {
            Copy(source, sourceStride, sourceOrigin, buffer, width, width, height);
            return;
        }

        ReadOnlySpan<short> table = taps == 2 ? Bilinear : taps == 4 ? RegularFourTap : RegularEightTap;
        ReadOnlySpan<short> horizontal = GetPhase(table, horizontalPhase * 2);
        ReadOnlySpan<short> vertical = GetPhase(table, verticalPhase * 2);
        GetEffectiveKernel(horizontal, out int horizontalFirst, out int horizontalCount);
        GetEffectiveKernel(vertical, out int verticalFirst, out int verticalCount);

        // Search interpolation applies a single Q7 rounding and clips after each pass. Keeping both
        // passes in sample storage preserves those clipped values when they feed the vertical filter.
        if (verticalPhase == 0)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                buffer,
                width,
                width,
                height,
                horizontal[horizontalFirst..],
                horizontalCount,
                horizontalFirst - 3,
                1,
                FilterBits,
                0);

            return;
        }

        if (horizontalPhase == 0)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                buffer,
                width,
                width,
                height,
                vertical[verticalFirst..],
                verticalCount,
                (verticalFirst - 3) * sourceStride,
                sourceStride,
                FilterBits,
                0);

            return;
        }

        // The vertical support occupies seven additional rows at a fixed 128-sample stride. The final
        // top-to-bottom pass packs its output over consumed rows of the same buffer: width never exceeds
        // that stride, and a written column cannot affect another column's vertical convolution.
        FilterDirect(
            source,
            sourceStride,
            sourceOrigin - (3 * sourceStride),
            buffer,
            128,
            width,
            height + 7,
            horizontal[horizontalFirst..],
            horizontalCount,
            horizontalFirst - 3,
            1,
            FilterBits,
            0);

        FilterDirect(
            buffer,
            128,
            3 * 128,
            buffer,
            width,
            width,
            height,
            vertical[verticalFirst..],
            verticalCount,
            (verticalFirst - 3) * 128,
            128,
            FilterBits,
            0);
    }

    /// <summary>
    /// Produces a search prediction, rounding and clipping each separable pass to the component precision.
    /// </summary>
    /// <param name="source">The bordered reference plane.</param>
    /// <param name="sourceStride">The reference row stride in samples.</param>
    /// <param name="sourceOrigin">The integer prediction origin.</param>
    /// <param name="buffer">The borrowed search buffer; the packed result occupies its first width times height samples.</param>
    /// <param name="width">The prediction width.</param>
    /// <param name="height">The prediction height.</param>
    /// <param name="horizontalPhase">The horizontal fraction in eighth-sample units.</param>
    /// <param name="verticalPhase">The vertical fraction in eighth-sample units.</param>
    /// <param name="taps">The selected two-, four-, or eight-tap search filter.</param>
    /// <param name="bitDepth">The coded component precision.</param>
    public static void PredictForSearch(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> buffer,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int taps,
        int bitDepth)
    {
        if ((horizontalPhase | verticalPhase) == 0)
        {
            Copy(source, sourceStride, sourceOrigin, buffer, width, width, height);
            return;
        }

        ReadOnlySpan<short> table = taps == 2 ? Bilinear : taps == 4 ? RegularFourTap : RegularEightTap;
        ReadOnlySpan<short> horizontal = GetPhase(table, horizontalPhase * 2);
        ReadOnlySpan<short> vertical = GetPhase(table, verticalPhase * 2);
        GetEffectiveKernel(horizontal, out int horizontalFirst, out int horizontalCount);
        GetEffectiveKernel(vertical, out int verticalFirst, out int verticalCount);

        // Search interpolation applies a single Q7 rounding and clips after each pass. Keeping both
        // passes in sample storage preserves those clipped values when they feed the vertical filter.
        if (verticalPhase == 0)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                buffer,
                width,
                width,
                height,
                horizontal[horizontalFirst..],
                horizontalCount,
                horizontalFirst - 3,
                1,
                FilterBits,
                0,
                bitDepth);

            return;
        }

        if (horizontalPhase == 0)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                buffer,
                width,
                width,
                height,
                vertical[verticalFirst..],
                verticalCount,
                (verticalFirst - 3) * sourceStride,
                sourceStride,
                FilterBits,
                0,
                bitDepth);

            return;
        }

        // The vertical support occupies seven additional rows at a fixed 128-sample stride. The final
        // top-to-bottom pass packs its output over consumed rows of the same buffer: width never exceeds
        // that stride, and a written column cannot affect another column's vertical convolution.
        FilterDirect(
            source,
            sourceStride,
            sourceOrigin - (3 * sourceStride),
            buffer,
            128,
            width,
            height + 7,
            horizontal[horizontalFirst..],
            horizontalCount,
            horizontalFirst - 3,
            1,
            FilterBits,
            0,
            bitDepth);

        FilterDirect(
            buffer,
            128,
            3 * 128,
            buffer,
            width,
            width,
            height,
            vertical[verticalFirst..],
            verticalCount,
            (verticalFirst - 3) * 128,
            128,
            FilterBits,
            0,
            bitDepth);
    }
}

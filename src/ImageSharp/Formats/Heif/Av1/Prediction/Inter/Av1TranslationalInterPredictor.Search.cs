// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides the interpolated predictions of the subpixel motion search.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// The sample capacity for a 128-column search prediction and its eight-tap vertical support.
    /// </summary>
    public const int SearchPredictionBufferLength = 136 * 128;

    /// <summary>
    /// Produces a search prediction. Each separable pass rounds and clips to the component precision.
    /// </summary>
    /// <param name="source">The bordered reference plane.</param>
    /// <param name="sourceStride">The reference row stride in samples.</param>
    /// <param name="sourceOrigin">The integer prediction origin.</param>
    /// <param name="buffer">The borrowed search buffer. The packed result fills its first width times height samples.</param>
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

        // Each pass of the search filter rounds once by the Q7 shift and clips to the sample range.
        // The horizontal pass writes samples, so the vertical pass reads the clipped values.
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

        // The horizontal pass writes height + 7 rows at a fixed stride of 128 samples. The 7 extra rows hold the vertical support.
        // The vertical pass runs from top to bottom and packs its output over rows of the same buffer that it already read.
        // The width is at most the stride, and a written column does not change the vertical convolution of another column.
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
    /// Produces a search prediction. Each separable pass rounds and clips to the component precision.
    /// </summary>
    /// <param name="source">The bordered reference plane.</param>
    /// <param name="sourceStride">The reference row stride in samples.</param>
    /// <param name="sourceOrigin">The integer prediction origin.</param>
    /// <param name="buffer">The borrowed search buffer. The packed result fills its first width times height samples.</param>
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

        // Each pass of the search filter rounds once by the Q7 shift and clips to the sample range.
        // The horizontal pass writes samples, so the vertical pass reads the clipped values.
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

        // The horizontal pass writes height + 7 rows at a fixed stride of 128 samples. The 7 extra rows hold the vertical support.
        // The vertical pass runs from top to bottom and packs its output over rows of the same buffer that it already read.
        // The width is at most the stride, and a written column does not change the vertical convolution of another column.
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

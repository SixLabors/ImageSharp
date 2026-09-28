// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Provides row and column projection motion estimation for variance partitioning.
/// </content>
internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Searches normalized row and column projections, then compares their motion with zero displacement.
    /// </summary>
    /// <param name="source">The source samples beginning at the block origin.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="reference">The complete bordered prediction storage.</param>
    /// <param name="referenceStride">The prediction row stride.</param>
    /// <param name="referenceOrigin">The prediction index corresponding to the source block origin.</param>
    /// <param name="block">The source block's position and dimensions.</param>
    /// <param name="frameSize">The visible frame dimensions.</param>
    /// <param name="border">The allocated border in samples.</param>
    /// <param name="horizontalRange">The requested horizontal search radius.</param>
    /// <param name="verticalRange">The requested vertical search radius.</param>
    /// <param name="screenContent">Whether every projected displacement is searched and motion is restricted to one axis.</param>
    /// <param name="scrollSuperblock">Whether the expanded superblock search replaces local two-dimensional refinement.</param>
    /// <param name="bounds">The permitted final full-sample displacements.</param>
    /// <param name="scratch">Reusable storage for the two prediction projections and two source projections.</param>
    /// <param name="vector">The selected displacement in eighth-sample units.</param>
    /// <param name="zeroSad">The absolute-difference sum at zero displacement.</param>
    /// <returns>The selected prediction's absolute-difference estimate.</returns>
    public static uint SearchProjection(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> reference,
        int referenceStride,
        int referenceOrigin,
        Rectangle block,
        Size frameSize,
        int border,
        int horizontalRange,
        int verticalRange,
        bool screenContent,
        bool scrollSuperblock,
        Rectangle bounds,
        Span<short> scratch,
        out Av1MotionVector vector,
        out uint zeroSad)
    {
        border &= ~15;
        int left = horizontalRange;
        int right = horizontalRange;
        int top = verticalRange;
        int bottom = verticalRange;
        if (scrollSuperblock)
        {
            left = Math.Min(left, block.X + border);
            right = Math.Min(right, frameSize.Width + border - block.Right);
            top = Math.Min(top, block.Y + border);
            bottom = Math.Min(bottom, frameSize.Height + border - block.Bottom);
        }
        else
        {
            if (block.X - left < -border || block.Right + right > frameSize.Width + border)
            {
                left = Math.Min(border, block.X + border);
                right = Math.Min(border, frameSize.Width + border - block.Right);
            }

            if (block.Y - top < -border || block.Bottom + bottom > frameSize.Height + border)
            {
                top = Math.Min(border, block.Y + border);
                bottom = Math.Min(border, frameSize.Height + border - block.Bottom);
            }
        }

        // Coarse projection matching advances sixteen samples at a time. Align both ends
        // independently so the requested search remains inside the physically extended border.
        left &= ~15;
        right &= ~15;
        top &= ~15;
        bottom &= ~15;
        int horizontalCount = left + right + block.Width;
        int verticalCount = top + bottom + block.Height;
        Span<short> horizontal = scratch[..horizontalCount];
        Span<short> vertical = scratch.Slice(horizontalCount, verticalCount);
        Span<short> sourceHorizontal = scratch.Slice(horizontalCount + verticalCount, block.Width);
        Span<short> sourceVertical = scratch.Slice(horizontalCount + verticalCount + block.Width, block.Height);
        int horizontalShift = BitOperations.Log2((uint)block.Height) - 1;
        int verticalShift = 3 + (block.Width >> 5);

        // Projection sums fit in signed sixteen-bit storage for blocks up to 128 samples.
        // Normalize only after summing an entire column or row; individual-sample rounding
        // would change both the projected variance and the selected displacement.
        // Reference: the aom_int_pro_row and aom_int_pro_col calls of av1_int_pro_motion_estimation().
        Av1IntegralProjection.ProjectColumns(horizontal, reference[(referenceOrigin - left)..], referenceStride, horizontalCount, block.Height, horizontalShift);
        Av1IntegralProjection.ProjectRows(vertical, reference[(referenceOrigin - (top * referenceStride))..], referenceStride, block.Width, verticalCount, verticalShift);
        Av1IntegralProjection.ProjectColumns(sourceHorizontal, source, sourceStride, block.Width, block.Height, horizontalShift);
        Av1IntegralProjection.ProjectRows(sourceVertical, source, sourceStride, block.Width, block.Height, verticalShift);

        int column = MatchProjection(horizontal, sourceHorizontal, left, right, screenContent, out int columnError);
        int rowOffset = MatchProjection(vertical, sourceVertical, top, bottom, screenContent, out int rowError);
        if (screenContent)
        {
            if (columnError < rowError)
            {
                rowOffset = 0;
            }
            else
            {
                column = 0;
            }
        }

        Point selected = new(column, rowOffset);
        Point searchCenter = selected;
        int predictionOrigin = referenceOrigin + (selected.Y * referenceStride) + selected.X;
        uint bestSad = (uint)ByteOperator.SumAbsoluteDifferences(
            source, sourceStride, reference[predictionOrigin..], referenceStride, block.Width, block.Height, 1);

        if (!screenContent && scrollSuperblock)
        {
            // Expanded motion compares its projected one-axis estimates with the two-axis SAD.
            // Keep those comparison domains and strict ties in their original decision order.
            if (columnError < rowError && columnError < bestSad)
            {
                selected.Y = 0;
                bestSad = (uint)columnError;
            }
            else if (rowError < columnError && rowError < bestSad)
            {
                selected.X = 0;
                bestSad = (uint)rowError;
            }
        }

        if (selected != Point.Empty)
        {
            zeroSad = (uint)ByteOperator.SumAbsoluteDifferences(
                source, sourceStride, reference[referenceOrigin..], referenceStride, block.Width, block.Height, 1);

            if (zeroSad < bestSad)
            {
                selected = Point.Empty;
                searchCenter = Point.Empty;
                predictionOrigin = referenceOrigin;
                bestSad = zeroSad;
            }
        }
        else
        {
            zeroSad = bestSad;
        }

        if (!scrollSuperblock)
        {
            ReadOnlySpan<Point> offsets = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];
            InlineArray4<uint> errors = default;
            for (int index = 0; index < offsets.Length; index++)
            {
                Point offset = offsets[index];
                int candidateOrigin = predictionOrigin + (offset.Y * referenceStride) + offset.X;
                uint sad = (uint)ByteOperator.SumAbsoluteDifferences(
                    source, sourceStride, reference[candidateOrigin..], referenceStride, block.Width, block.Height, 1);

                errors[index] = sad;
                if (sad < bestSad)
                {
                    bestSad = sad;
                    selected = searchCenter + new Size(offset.X, offset.Y);
                }
            }

            // Refine the diagonal formed by the better horizontal and vertical neighbors,
            // even when neither individual neighbor improved the center.
            Point diagonal = searchCenter + new Size(errors[1] < errors[2] ? -1 : 1, errors[0] < errors[3] ? -1 : 1);
            int diagonalOrigin = referenceOrigin + (diagonal.Y * referenceStride) + diagonal.X;
            uint diagonalSad = (uint)ByteOperator.SumAbsoluteDifferences(
                source, sourceStride, reference[diagonalOrigin..], referenceStride, block.Width, block.Height, 1);

            if (diagonalSad < bestSad)
            {
                bestSad = diagonalSad;
                selected = diagonal;
            }
        }

        vector = new Av1MotionVector(
            Math.Clamp(selected.Y, bounds.Top, bounds.Bottom - 1) << 3,
            Math.Clamp(selected.X, bounds.Left, bounds.Right - 1) << 3);
        return bestSad;
    }

    /// <summary>
    /// Finds a projected displacement with either unit steps or a coarse-to-fine search.
    /// </summary>
    /// <param name="reference">The complete projected search interval.</param>
    /// <param name="source">The projected source block.</param>
    /// <param name="before">The number of candidate positions before zero motion.</param>
    /// <param name="after">The number of candidate positions after zero motion.</param>
    /// <param name="fullSearch">Whether every candidate position is evaluated.</param>
    /// <param name="error">The winning centered squared error.</param>
    /// <returns>The displacement from the zero-motion position.</returns>
    private static int MatchProjection(
        ReadOnlySpan<short> reference,
        ReadOnlySpan<short> source,
        int before,
        int after,
        bool fullSearch,
        out int error)
    {
        int limit = before + after;
        int center = 0;
        error = int.MaxValue;
        int increment = fullSearch ? 1 : 16;
        for (int position = 0; position <= limit; position += increment)
        {
            int candidate = GetProjectionVariance(reference[position..], source);
            if (candidate < error)
            {
                error = candidate;
                center = position;
            }
        }

        if (!fullSearch)
        {
            for (int step = 8; step != 0; step >>= 1)
            {
                int origin = center;
                for (int offset = -step; offset <= step; offset += 2 * step)
                {
                    int position = origin + offset;
                    if ((uint)position > (uint)limit)
                    {
                        continue;
                    }

                    int candidate = GetProjectionVariance(reference[position..], source);
                    if (candidate < error)
                    {
                        error = candidate;
                        center = position;
                    }
                }
            }
        }

        return center - before;
    }

    /// <summary>
    /// Measures centered squared error between equally normalized sample projections.
    /// </summary>
    /// <param name="reference">The candidate projection.</param>
    /// <param name="source">The source projection and its comparison length.</param>
    /// <returns>The squared error with its mean component removed.</returns>
    private static int GetProjectionVariance(ReadOnlySpan<short> reference, ReadOnlySpan<short> source)
        => Av1IntegralProjection.GetVariance(reference, source);
}

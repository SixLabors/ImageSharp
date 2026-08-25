// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Provides reference-sample preparation and filter selection.
/// </content>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// Gets the angular-distance threshold for reference filtering at each supported block size.
    /// </summary>
    private static ReadOnlySpan<byte> ReferenceFilterThresholds => [10, 7, 1, 0];

    /// <summary>
    /// Gets the temporary sample count required while preparing prediction references.
    /// </summary>
    /// <param name="log2Size">The base-two logarithm of the square prediction-block side.</param>
    /// <param name="unitWidth">The horizontal availability-unit width in plane samples.</param>
    /// <returns>The required number of <see cref="ushort"/> elements.</returns>
    public static int GetReferenceScratchLength(int log2Size, int unitWidth)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        return (4 << log2Size) + unitWidth;
    }

    /// <summary>
    /// Determines whether the selected mode uses filtered prediction references.
    /// </summary>
    /// <param name="plane">The reconstructed plane.</param>
    /// <param name="mode">The effective prediction mode in the inclusive range zero through thirty-four.</param>
    /// <param name="log2Size">The base-two logarithm of the square prediction-block side.</param>
    /// <param name="chromaFormat">The HEVC chroma-format identifier.</param>
    /// <param name="intraSmoothingDisabled">Whether the sequence disables intra-reference smoothing.</param>
    /// <returns><see langword="true"/> when the prepared references require filtering; otherwise, <see langword="false"/>.</returns>
    public static bool ShouldFilterReferenceSamples(HevcPlane plane, int mode, int log2Size, byte chromaFormat, bool intraSmoothingDisabled)
    {
        DebugGuard.MustBeBetweenOrEqualTo(mode, PlanarMode, 34, nameof(mode));
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        if (intraSmoothingDisabled || (plane != HevcPlane.Y && chromaFormat != 3) || mode == DcMode)
        {
            return false;
        }

        int angularDistance = Math.Min(Math.Abs(mode - HorizontalMode), Math.Abs(mode - VerticalMode));
        return angularDistance > ReferenceFilterThresholds[log2Size - 2];
    }

    /// <summary>
    /// Prepares substituted top and left references from one reconstructed picture plane.
    /// </summary>
    /// <param name="picture">The reconstructed still-picture planes.</param>
    /// <param name="plane">The plane containing the prediction block.</param>
    /// <param name="x">The prediction-block left coordinate in plane samples.</param>
    /// <param name="y">The prediction-block top coordinate in plane samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square prediction-block side.</param>
    /// <param name="unitWidth">The horizontal availability-unit width in plane samples.</param>
    /// <param name="unitHeight">The vertical availability-unit height in plane samples.</param>
    /// <param name="availableUnits">
    /// The availability flags ordered from the bottom-most below-left unit upward through top-left, then from the
    /// left-most above unit through the right-most above-right unit.
    /// </param>
    /// <param name="top">The destination top-left, top, and top-right reference samples.</param>
    /// <param name="left">The destination top-left, left, and below-left reference samples.</param>
    /// <param name="scratch">The caller-owned temporary storage sized by <see cref="GetReferenceScratchLength(int, int)"/>.</param>
    public static void PrepareReferenceSamples(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int log2Size,
        int unitWidth,
        int unitHeight,
        ReadOnlySpan<bool> availableUnits,
        Span<ushort> top,
        Span<ushort> left,
        Span<ushort> scratch)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        int size = 1 << log2Size;
        int referenceLength = (size * 2) + 1;
        int leftUnitCount = (size * 2) / unitHeight;
        int aboveUnitCount = (size * 2) / unitWidth;
        int totalUnitCount = leftUnitCount + aboveUnitCount + 1;
        ReadOnlySpan<bool> availability = availableUnits[..totalUnitCount];

        if (availability.IndexOf(true) < 0)
        {
            ushort midpoint = (ushort)(1 << (picture.GetBitDepth(plane) - 1));
            top[..referenceLength].Fill(midpoint);
            left[..referenceLength].Fill(midpoint);
            return;
        }

        if (availability.IndexOf(false) < 0)
        {
            picture.GetRowSpan(plane, y - 1).Slice(x - 1, referenceLength).CopyTo(top);
            left[0] = top[0];
            for (int i = 1; i < referenceLength; i++)
            {
                left[i] = picture.GetRowSpan(plane, y + i - 1)[x - 1];
            }

            return;
        }

        int leftSampleCount = size * 2;
        int lineLength = leftSampleCount + unitWidth + (size * 2);
        Span<ushort> line = scratch[..lineLength];
        line.Fill((ushort)(1 << (picture.GetBitDepth(plane) - 1)));

        // The logical line runs from the bottom-most below-left sample towards the corner and then to the farthest
        // above-right sample. This makes substitution a forward fill across availability units.
        for (int unit = 0; unit < leftUnitCount; unit++)
        {
            if (!availability[unit])
            {
                continue;
            }

            int sourceY = y + ((leftUnitCount - unit - 1) * unitHeight);
            int destinationEnd = ((unit + 1) * unitHeight) - 1;
            for (int offset = 0; offset < unitHeight; offset++)
            {
                line[destinationEnd - offset] = picture.GetRowSpan(plane, sourceY + offset)[x - 1];
            }
        }

        int cornerUnit = leftUnitCount;
        if (availability[cornerUnit])
        {
            line.Slice(leftSampleCount, unitWidth).Fill(picture.GetRowSpan(plane, y - 1)[x - 1]);
        }

        ReadOnlySpan<ushort> aboveRow = picture.GetRowSpan(plane, y - 1);
        int topStart = leftSampleCount + unitWidth;
        for (int unit = 0; unit < aboveUnitCount; unit++)
        {
            if (availability[cornerUnit + unit + 1])
            {
                aboveRow.Slice(x + (unit * unitWidth), unitWidth).CopyTo(line.Slice(topStart + (unit * unitWidth), unitWidth));
            }
        }

        int firstAvailableUnit = availability.IndexOf(true);
        int firstAvailableOffset = firstAvailableUnit < leftUnitCount
            ? firstAvailableUnit * unitHeight
            : leftSampleCount + ((firstAvailableUnit - leftUnitCount) * unitWidth);

        int lineOffset = 0;
        ushort precedingSample = line[firstAvailableOffset];
        for (int unit = 0; unit < totalUnitCount; unit++)
        {
            int sampleCount = unit < leftUnitCount ? unitHeight : unitWidth;
            Span<ushort> unitSamples = line.Slice(lineOffset, sampleCount);
            if (!availability[unit])
            {
                unitSamples.Fill(precedingSample);
            }

            precedingSample = unitSamples[^1];
            lineOffset += sampleCount;
        }

        int cornerOffset = leftSampleCount + unitWidth - 1;
        top[0] = left[0] = line[cornerOffset];
        line.Slice(topStart, size * 2).CopyTo(top[1..]);
        for (int i = 1; i < referenceLength; i++)
        {
            left[i] = line[leftSampleCount - i];
        }
    }
}

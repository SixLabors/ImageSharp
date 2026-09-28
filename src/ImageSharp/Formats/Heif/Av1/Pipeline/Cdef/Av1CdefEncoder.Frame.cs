// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefEncoder
{
    /// <summary>
    /// Predicts the frame strengths from the quantizer. Reference: av1_pick_cdef_from_qp(), without its screen-content fit.
    /// </summary>
    /// <param name="picture">The frame receiving predicted strengths.</param>
    /// <param name="avoidChroma">Whether chroma stays unfiltered.</param>
    private static void PredictStrengths(Av1PictureControlSet picture, bool avoidChroma)
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        Av1BitDepth bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
        int q = Av1QuantizationLookup.GetAcQuant(header.QuantizationParameters.BaseQIndex, 0, bitDepth) >> (bitDepth.GetBitCount() - 8);

        // The fit consumes the AC step normalized to eight bits. Its single-precision polynomial
        // rounds halfway values away from zero before clipping to the transmitted field widths.
        // Intra and inter frames use separate fits.
        float yPrimaryEstimate;
        float ySecondaryEstimate;
        float uvPrimaryEstimate;
        float uvSecondaryEstimate;
        if (header.IsIntra)
        {
            yPrimaryEstimate = (q * q * 0.0000033731974F) + (q * 0.008070594F) + 0.0187634F;
            ySecondaryEstimate = (q * q * 0.0000029167343F) + (q * 0.0027798624F) + 0.0079405F;
            uvPrimaryEstimate = (q * q * -0.0000130790995F) + (q * 0.012892405F) - 0.00748388F;
            uvSecondaryEstimate = (q * q * 0.0000032651783F) + (q * 0.00035520183F) + 0.00228092F;
        }
        else
        {
            yPrimaryEstimate = (q * q * -0.0000023593946F) + (q * 0.0068615186F) + 0.02709886F;
            ySecondaryEstimate = (q * q * -0.00000057629734F) + (q * 0.0013993345F) + 0.03831067F;
            uvPrimaryEstimate = (q * q * -0.0000007095069F) + (q * 0.0034628846F) + 0.00887099F;
            uvSecondaryEstimate = (q * q * 0.00000023874085F) + (q * 0.00028223585F) + 0.05576307F;
        }

        int yPrimary = Math.Clamp((int)MathF.Round(yPrimaryEstimate, MidpointRounding.AwayFromZero), 0, 15);
        int ySecondary = Math.Clamp((int)MathF.Round(ySecondaryEstimate, MidpointRounding.AwayFromZero), 0, 3);
        int uvPrimary = Math.Clamp((int)MathF.Round(uvPrimaryEstimate, MidpointRounding.AwayFromZero), 0, 15);
        int uvSecondary = Math.Clamp((int)MathF.Round(uvSecondaryEstimate, MidpointRounding.AwayFromZero), 0, 3);

        header.CdefParameters.YStrength[0] = (yPrimary << 2) + ySecondary;
        header.CdefParameters.UvStrength[0] = avoidChroma ? 0 : (uvPrimary << 2) + uvSecondary;
        if (picture.Parent.SpeedSettings.SkipCdefSuperblock)
        {
            // A second, empty strength lets a 64x64 unit leave CDEF off. Mode decision already stored each
            // unit's index, so the indices stay as they are.
            header.CdefParameters.BitCount = 1;
            header.CdefParameters.YStrength[1] = 0;
            header.CdefParameters.UvStrength[1] = 0;
            return;
        }

        header.CdefParameters.BitCount = 0;
        for (int row = 0; row < header.ModeInfoRowCount; row += 16)
        {
            for (int column = 0; column < header.ModeInfoColumnCount; column += 16)
            {
                picture.GetFromModeInfoGrid(new Point(column, row)).CdefStrength = 0;
            }
        }
    }

    /// <summary>
    /// Applies the selected strengths while retaining only the unfiltered borders needed by later units.
    /// </summary>
    /// <typeparam name="TSample">The native component storage type.</typeparam>
    /// <typeparam name="TOperator">The closed component operations.</typeparam>
    /// <param name="allocator">The allocator for frame-scoped border storage.</param>
    /// <param name="picture">The selected strengths and block decisions.</param>
    /// <param name="reconstruction">The deblocked samples to replace.</param>
    /// <param name="input">The reusable bordered unit workspace.</param>
    /// <param name="directions">The reusable luma directions.</param>
    /// <param name="variances">The reusable luma direction variances.</param>
    /// <param name="blocks">The reusable block list.</param>
    private static void FilterFrame<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> reconstruction,
        Span<ushort> input,
        Span<int> directions,
        Span<int> variances,
        Span<ushort> blocks)
        where TSample : unmanaged
        where TOperator : struct, IEncodingOperator<TSample>
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ObuConstraintDirectionalEnhancementFilterParameters parameters = header.CdefParameters;
        int planeCount = reconstruction.IsMonochrome ? 1 : 3;
        bool enabled = false;
        for (int index = 0; index < 1 << parameters.BitCount; index++)
        {
            enabled |= parameters.YStrength[index] != 0 || (planeCount > 1 && parameters.UvStrength[index] != 0);
        }

        if (!enabled)
        {
            return;
        }

        Span<int> lineOffsets = stackalloc int[3];
        int lineLength = 0;
        for (int plane = 0; plane < planeCount; plane++)
        {
            lineOffsets[plane] = lineLength;
            int subX = plane == 0 ? 0 : reconstruction.ChromaSubsamplingX;
            lineLength += (header.ModeInfoColumnCount << (2 - subX)) * VerticalBorder * 2;
        }

        // Two alternating line slots keep the previous unit row intact while the next is saved.
        // Each plane also retains its preceding unit's right border. Storage grows with width only.
        const int columnLength = (64 + (2 * VerticalBorder)) * HorizontalBorder;
        using IMemoryOwner<ushort> borderOwner = allocator.Allocate<ushort>(lineLength + (planeCount * columnLength));
        Span<ushort> borders = borderOwner.Memory.Span;
        Span<bool> leftFiltered = stackalloc bool[3];
        int shift = reconstruction.LumaBitDepth - 8;
        for (int unitRow = 0; unitRow < header.ModeInfoRowCount; unitRow += 16)
        {
            leftFiltered.Clear();
            int rowIndex = unitRow >> 4;
            if (unitRow + 16 < header.ModeInfoRowCount)
            {
                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    int subX = planeIndex == 0 ? 0 : reconstruction.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : reconstruction.ChromaSubsamplingY;
                    int planeWidth = header.ModeInfoColumnCount << (2 - subX);
                    int row = ((unitRow + 16) << (2 - subY)) - VerticalBorder;
                    Buffer2DRegion<TSample> plane = reconstruction.CodedView.GetPlane((Av1Plane)planeIndex);
                    ReadOnlySpan<TSample> storage = plane.Buffer.DangerousGetSingleSpan();
                    int offset = ((plane.Bounds.Y + row) * plane.Stride) + plane.Bounds.X;
                    int lineOffset = lineOffsets[planeIndex] + ((rowIndex & 1) * VerticalBorder * planeWidth);
                    TOperator.Copy(storage[offset..], plane.Stride, borders[lineOffset..], planeWidth, planeWidth, VerticalBorder);
                }
            }

            for (int unitColumn = 0; unitColumn < header.ModeInfoColumnCount; unitColumn += 16)
            {
                Point position = new(unitColumn, unitRow);
                int strengthIndex = picture.GetFromModeInfoGrid(position).CdefStrength;
                int yStrength = parameters.YStrength[strengthIndex];
                int uvStrength = parameters.UvStrength[strengthIndex];
                int width = Math.Min(16, header.ModeInfoColumnCount - unitColumn);
                int height = Math.Min(16, header.ModeInfoRowCount - unitRow);
                int count = GetBlocks(picture, position, width, height, blocks);
                if (count == 0 || (yStrength == 0 && (planeCount == 1 || uvStrength == 0)))
                {
                    leftFiltered.Clear();
                    continue;
                }

                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    int strength = planeIndex == 0 ? yStrength : uvStrength;
                    if (planeIndex != 0 && strength == 0)
                    {
                        leftFiltered[planeIndex] = false;
                        continue;
                    }

                    int subX = planeIndex == 0 ? 0 : reconstruction.ChromaSubsamplingX;
                    int subY = planeIndex == 0 ? 0 : reconstruction.ChromaSubsamplingY;
                    int x = unitColumn << (2 - subX);
                    int y = unitRow << (2 - subY);
                    int planeWidth = header.ModeInfoColumnCount << (2 - subX);
                    int planeHeight = header.ModeInfoRowCount << (2 - subY);
                    int unitWidth = width << (2 - subX);
                    int unitHeight = height << (2 - subY);
                    Buffer2DRegion<TSample> plane = reconstruction.CodedView.GetPlane((Av1Plane)planeIndex);
                    CopyUnit<TSample, TOperator>(plane, x, y, unitWidth, unitHeight, planeWidth, planeHeight, input);
                    if (unitRow != 0)
                    {
                        int left = x == 0 ? 0 : HorizontalBorder;
                        int right = x + unitWidth == planeWidth ? 0 : HorizontalBorder;
                        int lineOffset = lineOffsets[planeIndex] + (((rowIndex - 1) & 1) * VerticalBorder * planeWidth) + x - left;
                        Av1CdefFilter.CopyPlane(
                            borders,
                            lineOffset,
                            planeWidth,
                            input,
                            HorizontalBorder - left,
                            SourceStride,
                            left + unitWidth + right,
                            VerticalBorder);
                    }

                    int preservedHeight = VerticalBorder + unitHeight + (y + unitHeight == planeHeight ? 0 : VerticalBorder);
                    Span<ushort> column = borders.Slice(lineLength + (planeIndex * columnLength), columnLength);
                    if (leftFiltered[planeIndex])
                    {
                        Av1CdefFilter.CopyPlane(column, 0, HorizontalBorder, input, 0, SourceStride, HorizontalBorder, preservedHeight);
                    }

                    if (planeIndex == 0)
                    {
                        FindDirections(input, blocks[..count], directions, variances, shift);
                    }

                    if (strength == 0)
                    {
                        leftFiltered[planeIndex] = false;
                        continue;
                    }

                    // Capture the right edge before replacing any samples in this unit. Subsequent
                    // units restore these samples over their already-filtered left neighborhood.
                    Av1CdefFilter.CopyPlane(input, unitWidth, SourceStride, column, 0, HorizontalBorder, HorizontalBorder, preservedHeight);
                    Span<TSample> storage = plane.Buffer.DangerousGetSingleSpan();
                    int offset = ((plane.Bounds.Y + y) * plane.Stride) + plane.Bounds.X + x;
                    FilterUnit<TSample, TOperator>(
                        input,
                        storage[offset..],
                        plane.Stride,
                        blocks[..count],
                        directions,
                        variances,
                        subX,
                        subY,
                        strength,
                        parameters.Damping,
                        shift,
                        planeIndex == 0);

                    leftFiltered[planeIndex] = true;
                }
            }
        }
    }
}

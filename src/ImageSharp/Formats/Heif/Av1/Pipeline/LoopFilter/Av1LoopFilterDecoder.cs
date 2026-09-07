// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Applies the AV1 in-loop deblocking stage to a reconstructed still-image frame.
/// </summary>
internal sealed class Av1LoopFilterDecoder
{
    /// <summary>
    /// The sequence-level superblock and color configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame dimensions, segmentation state, and loop-filter parameters.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The decoded mode and superblock delta information.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// The reconstructed plane samples modified by deblocking.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The per-plane transform-size map populated during reconstruction.
    /// </summary>
    private readonly Av1LoopFilterContext loopFilterContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopFilterDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining superblock size and color layout.</param>
    /// <param name="frameHeader">The frame header defining dimensions and filter parameters.</param>
    /// <param name="frameInfo">The decoded block-mode and superblock delta information.</param>
    /// <param name="frameBuffer">The reconstructed frame samples to filter.</param>
    /// <param name="loopFilterContext">The transform-size map populated during reconstruction.</param>
    public Av1LoopFilterDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopFilterContext loopFilterContext)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.loopFilterContext = loopFilterContext;
    }

    /// <summary>
    /// Filters every enabled plane in maximum-superblock row bands.
    /// </summary>
    public void DecodeFrame()
    {
        ObuLoopFilterParameters filterParameters = this.frameHeader.LoopFilterParameters;
        if (filterParameters.FilterLevel[0] == 0 && filterParameters.FilterLevel[1] == 0)
        {
            return;
        }

        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        int modeInfoRowsPerBand = 1 << (Av1Constants.MaxSuperBlockSizeLog2 - Av1Constants.ModeInfoSizeLog2);

        // Complete one 128-sample band at a time. Vertical and horizontal passes modify intersecting
        // neighborhoods, so changing their order across bands changes the reconstructed samples.
        for (int rowStart = 0; rowStart < this.frameHeader.ModeInfoRowCount; rowStart += modeInfoRowsPerBand)
        {
            int rowEnd = Math.Min(rowStart + modeInfoRowsPerBand, this.frameHeader.ModeInfoRowCount);

            for (int planeIndex = 0; planeIndex < colorConfig.PlaneCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int planeFilterLevel = plane switch
                {
                    Av1Plane.U => filterParameters.FilterLevelU,
                    Av1Plane.V => filterParameters.FilterLevelV,
                    _ => Math.Max(filterParameters.FilterLevel[0], filterParameters.FilterLevel[1])
                };

                if (planeFilterLevel == 0)
                {
                    continue;
                }

                int subX = plane != Av1Plane.Y && colorConfig.SubSamplingX ? 1 : 0;
                int subY = plane != Av1Plane.Y && colorConfig.SubSamplingY ? 1 : 0;
                if (this.frameBuffer.BytesPerSample == 2)
                {
                    Span<short> signedSamples = this.frameBuffer.DeriveBlockPointer16(plane, Point.Empty, subX, subY, out int stride);
                    Av1LoopFilterBase.FilterBand<ushort, Av1LoopFilterDecoder, Av1BlockModeInfo, FrameOperator,
                        Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator>(
                            this,
                            this.frameHeader,
                            plane,
                            rowStart,
                            rowEnd,
                            subX,
                            subY,
                            MemoryMarshal.Cast<short, ushort>(signedSamples),
                            stride,
                            stride,
                            this.frameBuffer.BitDepth.GetBitCount());
                }
                else
                {
                    Span<byte> samples = this.frameBuffer.DeriveBlockPointer(plane, Point.Empty, subX, subY, out int stride);
                    Av1LoopFilterBase.FilterBand<byte, Av1LoopFilterDecoder, Av1BlockModeInfo, FrameOperator,
                        Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator>(
                            this, this.frameHeader, plane, rowStart, rowEnd, subX, subY, samples, stride, stride, 8);
                }
            }
        }
    }

    /// <summary>
    /// Derives the adaptive filter level for one block, plane, and boundary direction.
    /// </summary>
    /// <param name="modeInfo">The decoded mode and segment information.</param>
    /// <param name="modeInfoPosition">The frame-relative position in luma 4x4 units.</param>
    /// <param name="plane">The color plane.</param>
    /// <param name="pass">Zero for a vertical boundary; one for a horizontal boundary.</param>
    /// <returns>The filter level in the AV1 zero-to-63 domain.</returns>
    private int GetFilterLevel(ref Av1BlockModeInfo modeInfo, Point modeInfoPosition, Av1Plane plane, int pass)
    {
        int filterIndex = plane == Av1Plane.Y ? pass : (int)plane + 1;
        ObuLoopFilterParameters parameters = this.frameHeader.LoopFilterParameters;
        int baseLevel = filterIndex switch
        {
            0 => parameters.FilterLevel[0],
            1 => parameters.FilterLevel[1],
            2 => parameters.FilterLevelU,
            _ => parameters.FilterLevelV
        };

        int superblockShift = this.sequenceHeader.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        Point superblockPosition = new(modeInfoPosition.X >> superblockShift, modeInfoPosition.Y >> superblockShift);
        Span<int> deltaLoopFilter = this.frameInfo.GetSuperblock(superblockPosition).SuperblockDeltaLoopFilter;
        int delta = this.frameHeader.DeltaLoopFilterParameters.IsMulti ? deltaLoopFilter[filterIndex] : deltaLoopFilter[0];
        return Av1LoopFilterBase.GetFilterLevel(
            this.frameHeader, filterIndex, baseLevel, delta, modeInfo.SegmentId, modeInfo.ReferenceFrames[0], modeInfo.YMode);
    }

    /// <summary>
    /// Reads deblocking parameters from decoded modes and the reconstructed transform map.
    /// </summary>
    private readonly struct FrameOperator : Av1LoopFilterBase.IFrameOperator<Av1LoopFilterDecoder, Av1BlockModeInfo>
    {
        /// <inheritdoc/>
        public static void GetParameters(
            Av1LoopFilterDecoder state,
            Point position,
            Av1Plane plane,
            int pass,
            int subX,
            int subY,
            out int blockIndex,
            out bool skippedTransform,
            out Av1TransformSize transformSize,
            out Av1BlockModeInfo mode)
        {
            mode = state.frameInfo.GetModeInfoAt(position);
            blockIndex = mode.ModeInfoIndex;
            skippedTransform = mode.Skip && mode.ReferenceFrames[0] > Av1ReferenceFrameType.Intra;
            transformSize = state.loopFilterContext.GetTransformSize(plane, new Point(position.X >> subX, position.Y >> subY));
        }

        /// <inheritdoc/>
        public static int GetFilterLevel(Av1LoopFilterDecoder state, ref Av1BlockModeInfo mode, Point position, Av1Plane plane, int pass)
            => state.GetFilterLevel(ref mode, position, plane, pass);
    }
}

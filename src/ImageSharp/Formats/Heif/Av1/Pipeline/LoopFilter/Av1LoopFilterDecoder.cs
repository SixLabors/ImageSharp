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
internal class Av1LoopFilterDecoder
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

        // libaom's loop_filter_rows processes one MAX_MIB_SIZE band at a time so that a completed band can be
        // presented before the remainder of the frame. The ordering is observable because the vertical and
        // horizontal passes modify intersecting sample neighborhoods in place.
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
                Span<byte> lowBitDepthSamples = default;
                Span<ushort> highBitDepthSamples = default;
                int stride;

                if (this.frameBuffer.BytesPerSample == 2)
                {
                    Span<short> signedSamples = this.frameBuffer.DeriveBlockPointer16(plane, Point.Empty, subX, subY, out stride);
                    highBitDepthSamples = MemoryMarshal.Cast<short, ushort>(signedSamples);
                }
                else
                {
                    lowBitDepthSamples = this.frameBuffer.DeriveBlockPointer(plane, Point.Empty, subX, subY, out stride);
                }

                this.FilterPlane(plane, rowStart, rowEnd, subX, subY, stride, lowBitDepthSamples, highBitDepthSamples);
            }
        }
    }

    /// <summary>
    /// Filters one plane band in the AV1 vertical-then-horizontal boundary order.
    /// </summary>
    /// <param name="plane">The color plane to filter.</param>
    /// <param name="rowStart">The inclusive band origin in luma 4x4 units.</param>
    /// <param name="rowEnd">The exclusive band limit in luma 4x4 units.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">The plane stride in logical samples.</param>
    /// <param name="lowBitDepthSamples">The low-bit-depth plane storage, when active.</param>
    /// <param name="highBitDepthSamples">The high-bit-depth plane storage, when active.</param>
    private void FilterPlane(
        Av1Plane plane,
        int rowStart,
        int rowEnd,
        int subX,
        int subY,
        int stride,
        Span<byte> lowBitDepthSamples,
        Span<ushort> highBitDepthSamples)
    {
        int rowStep = 1 << subY;
        int columnStep = 1 << subX;

        // The vertical pass advances across each row because successive vertical edges do not share modified samples.
        // Chroma rows retain luma-grid coordinates and therefore advance by two mode-info units when subsampled.
        for (int row = rowStart; row < rowEnd; row += rowStep)
        {
            for (int column = 0; column < this.frameHeader.ModeInfoColumnCount; column += columnStep)
            {
                this.FilterEdge(
                    plane,
                    0,
                    row,
                    column,
                    subX,
                    subY,
                    stride,
                    lowBitDepthSamples,
                    highBitDepthSamples);
            }
        }

        // Horizontal edges are visited down each column. This ordering is observable because adjacent horizontal
        // filters can modify samples that a later edge reads, so it must match libaom's av1_filter_block_plane_horz.
        for (int column = 0; column < this.frameHeader.ModeInfoColumnCount; column += columnStep)
        {
            for (int row = rowStart; row < rowEnd; row += rowStep)
            {
                this.FilterEdge(
                    plane,
                    1,
                    row,
                    column,
                    subX,
                    subY,
                    stride,
                    lowBitDepthSamples,
                    highBitDepthSamples);
            }
        }
    }

    /// <summary>
    /// Derives and applies the filter for one 4x4 luma-grid boundary.
    /// </summary>
    /// <param name="plane">The color plane to filter.</param>
    /// <param name="pass">Zero for a vertical boundary; one for a horizontal boundary.</param>
    /// <param name="row">The boundary row in luma 4x4 units.</param>
    /// <param name="column">The boundary column in luma 4x4 units.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="stride">The plane stride in logical samples.</param>
    /// <param name="lowBitDepthSamples">The low-bit-depth plane storage, when active.</param>
    /// <param name="highBitDepthSamples">The high-bit-depth plane storage, when active.</param>
    private void FilterEdge(
        Av1Plane plane,
        int pass,
        int row,
        int column,
        int subX,
        int subY,
        int stride,
        Span<byte> lowBitDepthSamples,
        Span<ushort> highBitDepthSamples)
    {
        int x = column << Av1Constants.ModeInfoSizeLog2;
        int y = row << Av1Constants.ModeInfoSizeLog2;
        bool verticalBoundary = pass == 0;
        if (x >= this.frameHeader.FrameSize.FrameWidth ||
            y >= this.frameHeader.FrameSize.FrameHeight ||
            (verticalBoundary ? x == 0 : y == 0))
        {
            return;
        }

        int adjustedRow = row | subY;
        int adjustedColumn = column | subX;
        int previousRow = adjustedRow - (verticalBoundary ? 0 : 1 << subY);
        int previousColumn = adjustedColumn - (verticalBoundary ? 1 << subX : 0);
        Point modeInfoPosition = new(adjustedColumn, adjustedRow);
        Point previousModeInfoPosition = new(previousColumn, previousRow);
        Point planeTransformPosition = new(adjustedColumn >> subX, adjustedRow >> subY);
        Point previousPlaneTransformPosition = new(previousColumn >> subX, previousRow >> subY);
        Av1BlockModeInfo modeInfo = this.frameInfo.GetModeInfoAt(modeInfoPosition);
        Av1BlockModeInfo previousModeInfo = this.frameInfo.GetModeInfoAt(previousModeInfoPosition);
        Av1TransformSize transformSize = this.loopFilterContext.GetTransformSize(plane, planeTransformPosition);
        Av1TransformSize previousTransformSize = this.loopFilterContext.GetTransformSize(plane, previousPlaneTransformPosition);
        int planeX = x >> subX;
        int planeY = y >> subY;
        bool isTransformEdge = verticalBoundary
            ? planeX % transformSize.GetWidth() == 0
            : planeY % transformSize.GetHeight() == 0;

        // A skipped intra block still has reconstructed prediction samples and is not a skipped inter transform.
        // libaom therefore applies the skip predicate only when the corresponding primary reference is inter.
        bool currentSkippedTransform = modeInfo.Skip && modeInfo.ReferenceFrames[0] > Av1ReferenceFrameType.Intra;
        bool previousSkippedTransform = previousModeInfo.Skip && previousModeInfo.ReferenceFrames[0] > Av1ReferenceFrameType.Intra;

        // The mode-info map stores one object for every covered position, so object identity is the exact equivalent
        // of libaom's current-versus-previous MB_MODE_INFO pointer comparison at a prediction-unit boundary.
        bool isBlockEdge = !ReferenceEquals(modeInfo, previousModeInfo);
        bool applyFilter = isTransformEdge && (isBlockEdge || !currentSkippedTransform || !previousSkippedTransform);
        if (!applyFilter)
        {
            return;
        }

        int currentLevel = this.GetFilterLevel(modeInfo, modeInfoPosition, plane, pass);
        int filterLevel = currentLevel != 0
            ? currentLevel
            : this.GetFilterLevel(previousModeInfo, previousModeInfoPosition, plane, pass);

        if (filterLevel == 0)
        {
            return;
        }

        int baseFilterSize = verticalBoundary
            ? Math.Min(transformSize.GetWidth(), previousTransformSize.GetWidth())
            : Math.Min(transformSize.GetHeight(), previousTransformSize.GetHeight());

        int maximumFilterSize = plane == Av1Plane.Y ? 16 : 8;
        int filterSize = Math.Min(maximumFilterSize, baseFilterSize);
        int kernelLength = plane == Av1Plane.Y
            ? filterSize switch
            {
                4 => 4,
                8 => 8,
                _ => 14
            }
            : filterSize == 4 ? 4 : 6;

        int sharpness = this.frameHeader.LoopFilterParameters.SharpnessLevel;
        int shift = sharpness > 4 ? 2 : sharpness > 0 ? 1 : 0;
        int limit = sharpness > 0
            ? Av1Math.Clip3(1, 9 - sharpness, filterLevel >> shift)
            : Math.Max(1, filterLevel >> shift);

        int boundaryLimit = (2 * (filterLevel + 2)) + limit;
        int highEdgeVarianceThreshold = filterLevel >> 4;
        int q0Offset = stride + (planeY * stride) + planeX;

        if (this.frameBuffer.BytesPerSample == 2)
        {
            if (verticalBoundary)
            {
                Av1DeblockingFilter.FilterVertical(
                    highBitDepthSamples,
                    q0Offset,
                    stride,
                    kernelLength,
                    limit,
                    boundaryLimit,
                    highEdgeVarianceThreshold,
                    this.frameBuffer.BitDepth.GetBitCount());
            }
            else
            {
                Av1DeblockingFilter.FilterHorizontal(
                    highBitDepthSamples,
                    q0Offset,
                    stride,
                    kernelLength,
                    limit,
                    boundaryLimit,
                    highEdgeVarianceThreshold,
                    this.frameBuffer.BitDepth.GetBitCount());
            }
        }
        else if (verticalBoundary)
        {
            Av1DeblockingFilter.FilterVertical(
                lowBitDepthSamples,
                q0Offset,
                stride,
                kernelLength,
                limit,
                boundaryLimit,
                highEdgeVarianceThreshold);
        }
        else
        {
            Av1DeblockingFilter.FilterHorizontal(
                lowBitDepthSamples,
                q0Offset,
                stride,
                kernelLength,
                limit,
                boundaryLimit,
                highEdgeVarianceThreshold);
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
    private int GetFilterLevel(Av1BlockModeInfo modeInfo, Point modeInfoPosition, Av1Plane plane, int pass)
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
        int level = Av1Math.Clip3(0, Av1Constants.MaxLoopFilter, baseLevel + delta);
        ObuSegmentationLevelFeature feature = (ObuSegmentationLevelFeature)((int)ObuSegmentationLevelFeature.AlternativeLoopFilterYVertical + filterIndex);
        ObuSegmentationParameters segmentation = this.frameHeader.SegmentationParameters;

        if (segmentation.IsFeatureActive(modeInfo.SegmentId, feature))
        {
            level = Av1Math.Clip3(
                0,
                Av1Constants.MaxLoopFilter,
                level + segmentation.FeatureData[modeInfo.SegmentId, (int)feature]);
        }

        if (parameters.ReferenceDeltaModeEnabled)
        {
            int referenceScale = 1 << (level >> 5);
            Av1ReferenceFrameType referenceFrame = modeInfo.ReferenceFrames[0];
            level = Av1Math.Clip3(
                0,
                Av1Constants.MaxLoopFilter,
                level + (parameters.ReferenceDeltas[(int)referenceFrame] * referenceScale));

            if (referenceFrame > Av1ReferenceFrameType.Intra)
            {
                // AV1's second mode-delta class contains every inter mode except the two global-motion modes.
                // Keeping this classification next to the level arithmetic mirrors libaom's mode_lf_lut lookup.
                int modeDeltaIndex = modeInfo.YMode is Av1PredictionMode.GlobalMotionVector or
                    Av1PredictionMode.GlobalGlobalMotionVector ? 0 : 1;

                level = Av1Math.Clip3(
                    0,
                    Av1Constants.MaxLoopFilter,
                    level + (parameters.ModeDeltas[modeDeltaIndex] * referenceScale));
            }
        }

        return level;
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Provides frame traversal for the AV1 in-loop deblocking stage.
/// </summary>
internal class Av1LoopFilterDecoder
{
    /// <summary>
    /// The sequence-level superblock configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame dimensions and loop-filter parameters.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The decoded block-mode information addressed by superblock origin.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// The reconstructed plane samples supplied to in-loop filtering.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The per-frame filter context retained across superblocks.
    /// </summary>
    private readonly Av1LoopFilterContext loopFilterContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopFilterDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header that supplies the superblock size.</param>
    /// <param name="frameHeader">The frame header that supplies dimensions and filter parameters.</param>
    /// <param name="frameInfo">The decoded block-mode information for the frame.</param>
    /// <param name="frameBuffer">The reconstructed frame samples to filter.</param>
    public Av1LoopFilterDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo, Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.loopFilterContext = new();
    }

    /// <summary>
    /// Traverses each superblock in raster order when loop filtering is enabled for the decode pass.
    /// </summary>
    /// <param name="doLoopFilterFlag">Whether the frame should run the deblocking stage.</param>
    /// <exception cref="NotImplementedException">The superblock filtering operation has not been implemented.</exception>
    public void DecodeFrame(bool doLoopFilterFlag)
    {
        Guard.NotNull(this.sequenceHeader);
        Guard.NotNull(this.frameHeader);
        Guard.NotNull(this.frameInfo);

        if (!doLoopFilterFlag)
        {
            return;
        }

        int superblockSizeLog2 = this.sequenceHeader.SuperblockSizeLog2;
        int frameWidthInSuperblocks = Av1Math.DivideLog2Ceiling(this.frameHeader.FrameSize.FrameWidth, this.sequenceHeader.SuperblockSizeLog2);
        int frameHeightInSuperblocks = Av1Math.DivideLog2Ceiling(this.frameHeader.FrameSize.FrameHeight, this.sequenceHeader.SuperblockSizeLog2);

        // Filtering proceeds in raster order because vertical and horizontal edges depend on already reconstructed
        // neighboring blocks, while the final superblock in each row requires distinct delayed-edge handling.
        for (int superblockIndexY = 0; superblockIndexY < frameHeightInSuperblocks; ++superblockIndexY)
        {
            for (int superblockIndexX = 0; superblockIndexX < frameWidthInSuperblocks; ++superblockIndexX)
            {
                int superblockOriginX = superblockIndexX << superblockSizeLog2;
                int superblockOriginY = superblockIndexY << superblockSizeLog2;
                bool endOfRowFlag = superblockIndexX == frameWidthInSuperblocks - 1;

                Point superblockPoint = new(superblockOriginX, superblockOriginY);
                Av1SuperblockInfo superblockInfo = this.frameInfo.GetSuperblock(superblockPoint);

                // Mode-info coordinates are measured in 4x4 units, whereas the frame and superblock origins are pixels.
                Point superblockOriginInModeInfo = new(superblockOriginX >> 2, superblockOriginY >> 2);

                this.DecodeForSuperblock(
                    superblockInfo,
                    superblockOriginInModeInfo,
                    Av1Plane.Y,
                    3,
                    endOfRowFlag,
                    superblockInfo.SuperblockDeltaLoopFilter);
            }
        }
    }

    /// <summary>
    /// Represents the not-yet-implemented deblocking operation for one superblock and plane range.
    /// </summary>
    /// <param name="superblockInfo">The decoded modes and delta values for the superblock.</param>
    /// <param name="modeInfoLocation">The superblock origin in 4x4 mode-info units.</param>
    /// <param name="startPlane">The first color plane to filter.</param>
    /// <param name="endPlane">The exclusive color-plane index at which filtering stops.</param>
    /// <param name="endOfRowFlag">Whether the superblock is the final block in its raster row.</param>
    /// <param name="superblockDeltaLoopFilter">The per-superblock loop-filter strength adjustments.</param>
    /// <exception cref="NotImplementedException">Always thrown because superblock deblocking has not been implemented.</exception>
    private void DecodeForSuperblock(Av1SuperblockInfo superblockInfo, Point modeInfoLocation, Av1Plane startPlane, int endPlane, bool endOfRowFlag, Span<int> superblockDeltaLoopFilter)
        => throw new NotImplementedException();
}

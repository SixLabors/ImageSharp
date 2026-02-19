// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

internal class Av1LoopFilterDecoder
{
    private readonly ObuSequenceHeader sequenceHeader;
    private readonly ObuFrameHeader frameHeader;
    private readonly Av1FrameInfo frameInfo;
    private readonly Av1FrameBuffer<byte> frameBuffer;
    private readonly Av1LoopFilterContext loopFilterContext;

    public Av1LoopFilterDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo, Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.loopFilterContext = new();
    }

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

        // Loop over a frame : tregger dec_loop_filter_sb for each SB
        for (int superblockIndexY = 0; superblockIndexY < frameHeightInSuperblocks; ++superblockIndexY)
        {
            for (int superblockIndexX = 0; superblockIndexX < frameWidthInSuperblocks; ++superblockIndexX)
            {
                int superblockOriginX = superblockIndexX << superblockSizeLog2;
                int superblockOriginY = superblockIndexY << superblockSizeLog2;
                bool endOfRowFlag = superblockIndexX == frameWidthInSuperblocks - 1;

                Point superblockPoint = new(superblockOriginX, superblockOriginY);
                Av1SuperblockInfo superblockInfo = this.frameInfo.GetSuperblock(superblockPoint);
                Point superblockOriginInModeInfo = new(superblockOriginX >> 2, superblockOriginY >> 2);

                // LF function for a SB
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

    private void DecodeForSuperblock(Av1SuperblockInfo superblockInfo, Point modeInfoLocation, Av1Plane startPlane, int endPlane, bool endOfRowFlag, Span<int> superblockDeltaLoopFilter)
        => throw new NotImplementedException();
}

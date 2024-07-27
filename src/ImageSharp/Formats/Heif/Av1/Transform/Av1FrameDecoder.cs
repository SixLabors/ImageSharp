// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1FrameDecoder
{
    private ObuSequenceHeader sequenceHeader;
    private ObuFrameHeader frameHeader;
    private Av1FrameBuffer frameBuffer;

    public Av1FrameDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameBuffer frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameBuffer = frameBuffer;
    }

    public void DecodeFrame()
    {
        Guard.NotNull(this.sequenceHeader);

        // TODO: Implement.
    }
}

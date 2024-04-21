// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1DecoderHandle
{
    public Av1DecoderHandle()
    {
        this.FrameInfo = new ObuFrameHeader();
        this.SequenceHeader = new ObuSequenceHeader();
        this.TileInfo = new ObuTileInfo();
    }

    public bool SequenceHeaderDone { get; set; }

    public bool ShowExistingFrame { get; set; }

    public bool SeenFrameHeader { get; set; }

    public ObuFrameHeader FrameInfo { get; }

    public ObuSequenceHeader SequenceHeader { get; }

    public ObuTileInfo TileInfo { get; }
}

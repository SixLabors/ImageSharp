// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1TileDecoderStub : IAv1TileDecoder
{
    public bool SequenceHeaderDone { get; set; }

    public bool ShowExistingFrame { get; set; }

    public bool SeenFrameHeader { get; set; }

    public ObuFrameHeader FrameInfo { get; } = new ObuFrameHeader();

    public ObuSequenceHeader SequenceHeader { get; } = new ObuSequenceHeader();

    public ObuTileInfo TileInfo { get; } = new ObuTileInfo();

    public void DecodeTile(Span<byte> tileData, int tileNum)
    {
    }

    public void FinishDecodeTiles(bool doCdef, bool doLoopRestoration)
    {
    }
}

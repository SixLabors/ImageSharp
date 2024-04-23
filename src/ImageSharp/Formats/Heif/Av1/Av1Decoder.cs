// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1Decoder : IAv1TileDecoder
{
    public Av1Decoder()
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

    public void Decode(Span<byte> buffer)
    {
        Av1BitStreamReader reader = new(buffer);
        ObuReader.Read(ref reader, buffer.Length, this, false);
    }

    public void DecodeTile(ref Av1BitStreamReader reader, int tileNum)
    {
        // TODO: Implement
    }

    public void FinishDecodeTiles(ref Av1BitStreamReader reader, bool doCdef, bool doLoopRestoration)
    {
        // TODO: Implement
    }

    private static void DecodeBlock(Av1BlockModeInfo blockMode, int rowIndex, int columnIndex)
    {
        int block4x4Width = blockMode.BlockSize.Get4x4WideCount();
        int block4x4Height = blockMode.BlockSize.Get4x4HighCount();
    }
}

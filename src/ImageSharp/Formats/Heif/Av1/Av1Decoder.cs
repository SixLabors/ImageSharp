// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1Decoder : IAv1TileDecoder
{
    private readonly Av1TileDecoder tileDecoder;

    public Av1Decoder()
    {
        this.FrameInfo = new ObuFrameHeader();
        this.SequenceHeader = new ObuSequenceHeader();
        this.TileInfo = new ObuTileInfo();
        this.SeenFrameHeader = false;
        this.tileDecoder = new Av1TileDecoder(this.SequenceHeader, this.FrameInfo, this.TileInfo);
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

    public void DecodeTile(Span<byte> tileData, int tileNum)
        => this.tileDecoder.DecodeTile(tileData, tileNum);

    public void FinishDecodeTiles(bool doCdef, bool doLoopRestoration)
        => this.tileDecoder.FinishDecodeTiles(doCdef, doLoopRestoration);
}

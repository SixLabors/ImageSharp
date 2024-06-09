// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1Decoder : IAv1TileDecoder
{
    private readonly ObuReader obuReader;
    private Av1TileDecoder? tileDecoder;
    private Av1FrameBuffer? frameBuffer;

    public Av1Decoder() => this.obuReader = new();

    public ObuFrameHeader? FrameHeader { get; private set; }

    public ObuSequenceHeader? SequenceHeader { get; private set; }

    public void Decode(Span<byte> buffer)
    {
        Av1BitStreamReader reader = new(buffer);
        this.obuReader.Read(ref reader, buffer.Length, this, false);
        this.frameBuffer = this.tileDecoder?.FrameBuffer;
    }

    public void StartDecodeTiles()
    {
        this.SequenceHeader = this.obuReader.SequenceHeader;
        this.FrameHeader = this.obuReader.FrameHeader;
        this.tileDecoder = new Av1TileDecoder(this.SequenceHeader!, this.FrameHeader!);
    }

    public void DecodeTile(Span<byte> tileData, int tileNum)
        => this.tileDecoder!.DecodeTile(tileData, tileNum);

    public void FinishDecodeTiles(bool doCdef, bool doLoopRestoration)
        => this.tileDecoder!.FinishDecodeTiles(doCdef, doLoopRestoration);
}

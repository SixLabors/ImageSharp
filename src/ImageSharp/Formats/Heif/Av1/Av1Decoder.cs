// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1Decoder : IAv1TileReader
{
    private readonly ObuReader obuReader;
    private Av1TileReader? tileReader;

    public Av1Decoder() => this.obuReader = new();

    public ObuFrameHeader? FrameHeader { get; private set; }

    public ObuSequenceHeader? SequenceHeader { get; private set; }

    public Av1FrameBuffer? FrameBuffer { get; private set; }

    public void Decode(Span<byte> buffer)
    {
        Av1BitStreamReader reader = new(buffer);
        this.obuReader.ReadAll(ref reader, buffer.Length, this, false);
        this.FrameBuffer = this.tileReader?.FrameBuffer;

        // TODO: Decode the FrameBuffer
    }

    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        if (this.tileReader == null)
        {
            this.SequenceHeader = this.obuReader.SequenceHeader;
            this.FrameHeader = this.obuReader.FrameHeader;
            this.tileReader = new Av1TileReader(this.SequenceHeader!, this.FrameHeader!);
        }

        this.tileReader.ReadTile(tileData, tileNum);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal class Av1Decoder : IAv1TileReader
{
    private readonly ObuReader obuReader;
    private readonly Configuration configuration;
    private Av1TileReader? tileReader;
    private Av1FrameDecoder? frameDecoder;

    public Av1Decoder(Configuration configuration)
    {
        this.configuration = configuration;
        this.obuReader = new();
    }

    public ObuFrameHeader? FrameHeader { get; private set; }

    public ObuSequenceHeader? SequenceHeader { get; private set; }

    public Av1FrameInfo? FrameInfo { get; private set; }

    public Av1FrameBuffer? FrameBuffer { get; private set; }

    public void Decode(Span<byte> buffer)
    {
        Av1BitStreamReader reader = new(buffer);
        this.obuReader.ReadAll(ref reader, buffer.Length, this, false);
        Guard.NotNull(this.tileReader, nameof(this.tileReader));
        Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
        Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));

        this.FrameInfo = this.tileReader.FrameInfo;
        this.FrameBuffer = new(this.configuration, this.SequenceHeader, this.SequenceHeader.ColorConfig.GetColorFormat(), false);
        this.frameDecoder = new(this.SequenceHeader, this.FrameHeader, this.FrameInfo, this.FrameBuffer);
        this.frameDecoder.DecodeFrame();
    }

    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        if (this.tileReader == null)
        {
            this.SequenceHeader = this.obuReader.SequenceHeader;
            this.FrameHeader = this.obuReader.FrameHeader;
            Guard.NotNull(this.tileReader, nameof(this.tileReader));
            Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
            Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));
            this.FrameInfo = new(this.SequenceHeader);
            this.FrameBuffer = new(this.configuration, this.SequenceHeader, this.SequenceHeader.ColorConfig.GetColorFormat(), false);
            this.frameDecoder = new(this.SequenceHeader, this.FrameHeader, this.FrameInfo, this.FrameBuffer);
            this.tileReader = new Av1TileReader(this.configuration, this.SequenceHeader, this.FrameHeader, this.frameDecoder);
        }

        this.tileReader.ReadTile(tileData, tileNum);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Implementation of section 2.3.3 of AV1 Codec ISO Media File Format Binding specification v1.2.0.
/// See https://aomediacodec.github.io/av1-isobmff/v1.2.0.html#av1codecconfigurationbox-syntax.
/// </summary>
internal struct Av1CodecConfiguration
{
    public Av1CodecConfiguration(Span<byte> boxBuffer)
    {
        Av1BitStreamReader reader = new(boxBuffer);

        this.Marker = (byte)reader.ReadLiteral(1);
        this.Version = (byte)reader.ReadLiteral(7);
        this.SeqProfile = (byte)reader.ReadLiteral(3);
        this.SeqLevelIdx0 = (byte)reader.ReadLiteral(5);
        this.SeqTier0 = (byte)reader.ReadLiteral(1);
        this.HighBitdepth = (byte)reader.ReadLiteral(1);
        this.TwelveBit = reader.ReadLiteral(1) == 1;
        this.MonoChrome = reader.ReadLiteral(1) == 1;
        this.ChromaSubsamplingX = reader.ReadLiteral(1) == 1;
        this.ChromaSubsamplingY = reader.ReadLiteral(1) == 1;
        this.ChromaSamplePosition = (byte)reader.ReadLiteral(2);

        // 3 bits are reserved.
        reader.ReadLiteral(3);

        this.InitialPresentationDelayPresent = reader.ReadLiteral(1) == 1;
        if (this.InitialPresentationDelayPresent)
        {
            byte initialPresentationDelayMinusOne = (byte)reader.ReadLiteral(4);
            this.InitialPresentationDelay = (byte)(initialPresentationDelayMinusOne + 1);
        }
    }

    public byte Marker { get; }

    public byte Version { get; }

    public byte SeqProfile { get; }

    public byte SeqLevelIdx0 { get; }

    public byte SeqTier0 { get; }

    public byte HighBitdepth { get; }

    public bool TwelveBit { get; }

    public bool MonoChrome { get; }

    public bool ChromaSubsamplingX { get; }

    public bool ChromaSubsamplingY { get; }

    public byte ChromaSamplePosition { get; }

    public bool InitialPresentationDelayPresent { get; }

    public byte InitialPresentationDelay { get; }
}

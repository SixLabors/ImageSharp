// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Represents the decoder configuration fields stored in an AV1 codec-configuration property.
/// </summary>
internal struct Av1CodecConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CodecConfiguration"/> struct from an AV1 codec-configuration payload.
    /// </summary>
    /// <param name="boxBuffer">The configuration payload beginning with the marker and version fields.</param>
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

    /// <summary>
    /// Gets the one-bit configuration marker.
    /// </summary>
    public byte Marker { get; }

    /// <summary>
    /// Gets the codec-configuration record version.
    /// </summary>
    public byte Version { get; }

    /// <summary>
    /// Gets the sequence profile declared by the configuration record.
    /// </summary>
    public byte SeqProfile { get; }

    /// <summary>
    /// Gets the first operating point's sequence level index.
    /// </summary>
    public byte SeqLevelIdx0 { get; }

    /// <summary>
    /// Gets the first operating point's sequence tier flag.
    /// </summary>
    public byte SeqTier0 { get; }

    /// <summary>
    /// Gets the high-bit-depth flag.
    /// </summary>
    public byte HighBitdepth { get; }

    /// <summary>
    /// Gets a value indicating whether the sequence uses twelve-bit samples.
    /// </summary>
    public bool TwelveBit { get; }

    /// <summary>
    /// Gets a value indicating whether the sequence contains only a luma plane.
    /// </summary>
    public bool MonoChrome { get; }

    /// <summary>
    /// Gets a value indicating whether chroma is horizontally subsampled.
    /// </summary>
    public bool ChromaSubsamplingX { get; }

    /// <summary>
    /// Gets a value indicating whether chroma is vertically subsampled.
    /// </summary>
    public bool ChromaSubsamplingY { get; }

    /// <summary>
    /// Gets the chroma sample-position code.
    /// </summary>
    public byte ChromaSamplePosition { get; }

    /// <summary>
    /// Gets a value indicating whether an initial presentation delay is declared.
    /// </summary>
    public bool InitialPresentationDelayPresent { get; }

    /// <summary>
    /// Gets the initial presentation delay in decoded frames, or zero when no delay is declared.
    /// </summary>
    public byte InitialPresentationDelay { get; }
}

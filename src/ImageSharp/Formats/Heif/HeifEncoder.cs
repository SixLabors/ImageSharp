// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image encoder for writing image data to a stream in a HEIF container.
/// </summary>
public sealed class HeifEncoder : AnimatedImageEncoder
{
    /// <summary>
    /// Backing field for <see cref="Quality"/>.
    /// </summary>
    private int? quality;

    /// <summary>
    /// Backing field for <see cref="AlphaQuality"/>.
    /// </summary>
    private int? alphaQuality;

    /// <summary>
    /// Backing field for <see cref="KeyFrameInterval"/>.
    /// </summary>
    private int? keyFrameInterval;

    /// <summary>
    /// Backing field for <see cref="Sharpness"/>.
    /// </summary>
    private int? sharpness;

    /// <summary>
    /// Gets the lossy compression quality, or <see langword="null"/> to use the default quality of 60.
    /// Valid values range from 0 for the lowest quality to 100 for the highest quality. A value of 100 does not
    /// enable <see cref="Lossless"/> encoding.
    /// </summary>
    /// <exception cref="ArgumentException">The quality is outside the range 0 to 100.</exception>
    public int? Quality
    {
        get => this.quality;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentException("Quality must be in the range [0..100].");
            }

            this.quality = value;
        }
    }

    /// <summary>
    /// Gets the lossy compression quality for the auxiliary alpha image, or <see langword="null"/> to use the
    /// effective <see cref="Quality"/>. Valid values range from 0 for the lowest quality to 100 for the highest
    /// quality. This option has no effect when the encoded image does not require an auxiliary alpha image.
    /// </summary>
    /// <exception cref="ArgumentException">The alpha quality is outside the range 0 to 100.</exception>
    public int? AlphaQuality
    {
        get => this.alphaQuality;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentException("Alpha quality must be in the range [0..100].");
            }

            this.alphaQuality = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the primary and auxiliary alpha images are encoded without loss. When
    /// <see langword="true"/>, <see cref="Quality"/> and <see cref="AlphaQuality"/> do not affect the encoded image.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool Lossless { get; init; }

    /// <summary>
    /// Gets the encoding speed. Higher levels encode faster but make a larger file.
    /// Defaults to <see cref="HeifEncodingSpeed.Level6"/>.
    /// </summary>
    public HeifEncodingSpeed Speed { get; init; } = HeifEncodingSpeed.Level6;

    /// <summary>
    /// Gets the sharpness, from 0 to 7, or <see langword="null"/> to let the encoder decide. Higher values keep
    /// more fine detail and sharper edges, and make a larger file. At lower quality the encoder reduces how much
    /// the sharpness changes block edges.
    /// </summary>
    /// <exception cref="ArgumentException">The sharpness is outside the range 0 to 7.</exception>
    public int? Sharpness
    {
        get => this.sharpness;
        init
        {
            if (value is < 0 or > 7)
            {
                throw new ArgumentException("Sharpness must be in the range [0..7].");
            }

            this.sharpness = value;
        }
    }

    /// <summary>
    /// Gets the encoded precision of each image component, or <see langword="null"/> to use the HEIF metadata bit
    /// depth. Metadata that does not specify a bit depth defaults to <see cref="HeifBitDepth.Bit8"/>.
    /// </summary>
    public HeifBitDepth? BitDepth { get; init; }

    /// <summary>
    /// Gets the encoded chroma sampling, or <see langword="null"/> to choose it from the source:
    /// <see cref="HeifChromaSubsampling.Monochrome"/> for a luminance source, the source's own sampling for a
    /// 4:2:0, 4:2:2 or 4:4:4 JPEG source, and <see cref="HeifChromaSubsampling.Yuv444"/> otherwise, including all
    /// lossless encoding. Oversized still images use <see cref="HeifChromaSubsampling.Yuv444"/> when a subsampled
    /// AVIF grid cannot represent an odd output dimension.
    /// </summary>
    public HeifChromaSubsampling? ChromaSubsampling { get; init; }

    /// <summary>
    /// Gets the largest number of frames from one key frame to the next in an animation, or <see langword="null"/>
    /// to let the encoder decide. A value of 1 makes every frame a key frame. A player can start or seek only at a
    /// key frame, and more key frames make a larger file.
    /// </summary>
    /// <exception cref="ArgumentException">The interval is less than 1.</exception>
    public int? KeyFrameInterval
    {
        get => this.keyFrameInterval;
        init
        {
            if (value < 1)
            {
                throw new ArgumentException("Key frame interval must be 1 or more.");
            }

            this.keyFrameInterval = value;
        }
    }

    /// <summary>
    /// Gets the largest number of tile rows. Small images get fewer rows. More tiles let a parallel decoder work
    /// faster but compress less well. Defaults to <see cref="HeifTileCount.One"/>.
    /// </summary>
    public HeifTileCount TileRows { get; init; } = HeifTileCount.One;

    /// <summary>
    /// Gets the largest number of tile columns. Small images get fewer columns, and images wider than 4096 pixels
    /// get more. More tiles let a parallel decoder work faster but compress less well.
    /// Defaults to <see cref="HeifTileCount.One"/>.
    /// </summary>
    public HeifTileCount TileColumns { get; init; } = HeifTileCount.One;

    /// <summary>
    /// Gets a value indicating whether the encoder chooses the tile rows and columns from the image size.
    /// When <see langword="true"/>, <see cref="TileRows"/> and <see cref="TileColumns"/> are ignored.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool AutoTiling { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        HeifEncoderCore encoder = new(image.Configuration, this);
        encoder.Encode(image, stream, cancellationToken);
    }
}

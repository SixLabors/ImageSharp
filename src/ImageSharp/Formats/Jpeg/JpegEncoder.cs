// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Encoder for writing the data image to a stream in jpeg format.
/// </summary>
public sealed class JpegEncoder : ImageEncoder
{
    /// <summary>
    /// Backing field for <see cref="Quality"/>.
    /// </summary>
    private int? quality;

    /// <summary>
    /// Backing field for <see cref="ProgressiveScans"/>
    /// </summary>
    private int progressiveScans = 4;

    /// <summary>
    /// Backing field for <see cref="RestartInterval"/>
    /// </summary>
    private int restartInterval;

    /// <summary>
    /// Gets the quality, that will be used to encode the image. Quality
    /// index must be between 1 and 100 (compression from max to min).
    /// Defaults to <value>75</value>.
    /// </summary>
    /// <exception cref="ArgumentException">Quality factor must be in [1..100] range.</exception>
    public int? Quality
    {
        get => this.quality;
        init
        {
            if (value is < 1 or > 100)
            {
                throw new ArgumentException("Quality factor must be in [1..100] range.");
            }

            this.quality = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether progressive encoding is used.
    /// </summary>
    public bool Progressive { get; init; }

    /// <summary>
    /// Gets number of scans per component for progressive encoding.
    /// Defaults to <value>4</value>.
    /// </summary>
    /// <remarks>
    /// Number of scans must be between 2 and 64.
    /// There is at least one scan for the DC coefficients and one for the remaining 63 AC coefficients.
    /// </remarks>
    /// <exception cref="ArgumentException">Progressive scans must be in [2..64] range.</exception>
    public int ProgressiveScans
    {
        get => this.progressiveScans;
        init
        {
            if (value is < 2 or > 64)
            {
                throw new ArgumentException("Progressive scans must be in [2..64] range.");
            }

            this.progressiveScans = value;
        }
    }

    /// <summary>
    /// Gets numbers of MCUs between restart markers.
    /// Defaults to <value>0</value>.
    /// </summary>
    /// <remarks>
    /// Currently supported in progressive encoding only.
    /// </remarks>
    /// <exception cref="ArgumentException">Restart interval must be in [0..65535] range.</exception>
    public int RestartInterval
    {
        get => this.restartInterval;
        init
        {
            if (value is < 0 or > 65535)
            {
                throw new ArgumentException("Restart interval must be in [0..65535] range.");
            }

            this.restartInterval = value;
        }
    }

    /// <summary>
    /// Gets the component encoding mode.
    /// </summary>
    /// <remarks>
    /// Interleaved encoding mode encodes all color components in a single scan.
    /// Non-interleaved encoding mode encodes each color component in a separate scan.
    /// </remarks>
    public bool? Interleaved { get; init; }

    /// <summary>
    /// Gets the jpeg color for encoding.
    /// </summary>
    public JpegColorType? ColorType { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        JpegEncoderCore encoder = new(this);
        encoder.Encode(image, stream, cancellationToken);
    }
}

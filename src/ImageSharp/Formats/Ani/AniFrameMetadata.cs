// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Provides Ani specific metadata information for the image.
/// </summary>
public class AniFrameMetadata : IFormatFrameMetadata<AniFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AniFrameMetadata"/> class.
    /// </summary>
    public AniFrameMetadata()
    {
    }

    /// <summary>
    /// Gets or sets the display time for this frame (in 1/60 seconds)
    /// </summary>
    public uint FrameDelay { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of current frame.
    /// </summary>
    public int SequenceNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the encoding width. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels or greater.
    /// </summary>
    public byte? EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoding height. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels or greater.
    /// </summary>
    public byte? EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame will be encoded as an ICO or CUR or BMP file.
    /// </summary>
    public AniFrameFormat FrameFormat { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IcoFrameMetadata"/> of one "icon" chunk.
    /// </summary>
    public IcoFrameMetadata? IcoFrameMetadata { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="CurFrameMetadata"/> of one "icon" chunk.
    /// </summary>
    public CurFrameMetadata? CurFrameMetadata { get; set; }

    /// <inheritdoc/>
    public static AniFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata) =>
        new()
        {
            FrameDelay = (uint)metadata.Duration.TotalSeconds * 60
        };

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata() => new FormatConnectingFrameMetadata() { Duration = TimeSpan.FromSeconds(this.FrameDelay / 60d) };

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    public AniFrameMetadata DeepClone() => new()
    {
        FrameDelay = this.FrameDelay,
        EncodingHeight = this.EncodingHeight,
        EncodingWidth = this.EncodingWidth,
        SequenceNumber = this.SequenceNumber,
        IsIco = this.IsIco,
        IcoFrameMetadata = this.IcoFrameMetadata?.DeepClone(),
        CurFrameMetadata = this.CurFrameMetadata?.DeepClone(),

        // TODO SubImageMetadata
    };
}

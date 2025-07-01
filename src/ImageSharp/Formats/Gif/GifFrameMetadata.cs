// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Provides Gif specific metadata information for the image frame.
/// </summary>
public class GifFrameMetadata : IFormatFrameMetadata<GifFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GifFrameMetadata"/> class.
    /// </summary>
    public GifFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GifFrameMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private GifFrameMetadata(GifFrameMetadata other)
    {
        this.ColorTableMode = other.ColorTableMode;
        this.FrameDelay = other.FrameDelay;
        this.DisposalMode = other.DisposalMode;

        if (other.LocalColorTable?.Length > 0)
        {
            this.LocalColorTable = other.LocalColorTable.Value.ToArray();
        }

        this.HasTransparency = other.HasTransparency;
        this.TransparencyIndex = other.TransparencyIndex;
    }

    /// <summary>
    /// Gets or sets the color table mode.
    /// </summary>
    public FrameColorTableMode ColorTableMode { get; set; }

    /// <summary>
    /// Gets or sets the local color table, if any.
    /// The underlying pixel format is represented by <see cref="Rgb24"/>.
    /// </summary>
    public ReadOnlyMemory<Color>? LocalColorTable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame has transparency
    /// </summary>
    public bool HasTransparency { get; set; }

    /// <summary>
    /// Gets or sets the transparency index.
    /// When <see cref="HasTransparency"/> is set to <see langword="true"/> this value indicates the index within
    /// the color palette at which the transparent color is located.
    /// </summary>
    public byte TransparencyIndex { get; set; }

    /// <summary>
    /// Gets or sets the frame delay for animated images.
    /// If not 0, when utilized in Gif animation, this field specifies the number of hundredths (1/100) of a second to
    /// wait before continuing with the processing of the Data Stream.
    /// The clock starts ticking immediately after the graphic is rendered.
    /// </summary>
    public int FrameDelay { get; set; }

    /// <summary>
    /// Gets or sets the disposal method for animated images.
    /// Primarily used in Gif animation, this field indicates the way in which the graphic is to
    /// be treated after being displayed.
    /// </summary>
    public FrameDisposalMode DisposalMode { get; set; }

    /// <inheritdoc />
    public static GifFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new()
        {
            ColorTableMode = metadata.ColorTableMode,
            FrameDelay = (int)Math.Round(metadata.Duration.TotalMilliseconds / 10),
            DisposalMode = metadata.DisposalMode,
        };

    /// <inheritdoc />
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
    {
        // For most scenarios we would consider the blend method to be 'Over' however if a frame has a disposal method of 'RestoreToBackground' or
        // has a local palette with 256 colors and is not transparent we should use 'Source'.
        bool blendSource = this.DisposalMode == FrameDisposalMode.RestoreToBackground || (this.LocalColorTable?.Length == 256 && !this.HasTransparency);

        // If the color table is global and frame has no transparency. Consider it 'Source' also.
        blendSource |= this.ColorTableMode == FrameColorTableMode.Global && !this.HasTransparency;

        return new FormatConnectingFrameMetadata
        {
            ColorTableMode = this.ColorTableMode,
            Duration = TimeSpan.FromMilliseconds(this.FrameDelay * 10),
            DisposalMode = this.DisposalMode,
            BlendMode = blendSource ? FrameBlendMode.Source : FrameBlendMode.Over,
        };
    }

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.LocalColorTable = null;

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public GifFrameMetadata DeepClone() => new(this);
}

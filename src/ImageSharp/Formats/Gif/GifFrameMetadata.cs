// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Provides Gif specific metadata information for the image frame.
/// </summary>
public class GifFrameMetadata : IDeepCloneable
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
        this.DisposalMethod = other.DisposalMethod;

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
    public GifColorTableMode ColorTableMode { get; set; }

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
    public GifDisposalMethod DisposalMethod { get; set; }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new GifFrameMetadata(this);

    internal static GifFrameMetadata FromAnimatedMetadata(AnimatedImageFrameMetadata metadata)
        => new()
        {
            // Do not copy the color table or transparency data.
            // This will lead to a mismatch when the image is comprised of frames
            // extracted individually from a multi-frame image.
            ColorTableMode = metadata.ColorTableMode == FrameColorTableMode.Global ? GifColorTableMode.Global : GifColorTableMode.Local,
            FrameDelay = (int)Math.Round(metadata.Duration.TotalMilliseconds / 10),
            DisposalMethod = GetMode(metadata.DisposalMode)
        };

    private static GifDisposalMethod GetMode(FrameDisposalMode mode) => mode switch
    {
        FrameDisposalMode.DoNotDispose => GifDisposalMethod.NotDispose,
        FrameDisposalMode.RestoreToBackground => GifDisposalMethod.RestoreToBackground,
        FrameDisposalMode.RestoreToPrevious => GifDisposalMethod.RestoreToPrevious,
        _ => GifDisposalMethod.Unspecified,
    };
}

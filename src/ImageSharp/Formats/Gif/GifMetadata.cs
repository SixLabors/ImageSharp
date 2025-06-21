// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Provides Gif specific metadata information for the image.
/// </summary>
public class GifMetadata : IFormatMetadata<GifMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GifMetadata"/> class.
    /// </summary>
    public GifMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GifMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private GifMetadata(GifMetadata other)
    {
        this.RepeatCount = other.RepeatCount;
        this.ColorTableMode = other.ColorTableMode;
        this.BackgroundColorIndex = other.BackgroundColorIndex;

        if (other.GlobalColorTable?.Length > 0)
        {
            this.GlobalColorTable = other.GlobalColorTable.Value.ToArray();
        }

        for (int i = 0; i < other.Comments.Count; i++)
        {
            this.Comments.Add(other.Comments[i]);
        }
    }

    /// <summary>
    /// Gets or sets the number of times any animation is repeated.
    /// <remarks>
    /// 0 means to repeat indefinitely, count is set as repeat n-1 times. Defaults to 1.
    /// </remarks>
    /// </summary>
    public ushort RepeatCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the color table mode.
    /// </summary>
    public FrameColorTableMode ColorTableMode { get; set; }

    /// <summary>
    /// Gets or sets the global color table, if any.
    /// The underlying pixel format is represented by <see cref="Rgb24"/>.
    /// </summary>
    public ReadOnlyMemory<Color>? GlobalColorTable { get; set; }

    /// <summary>
    /// Gets or sets the index at the <see cref="GlobalColorTable"/> for the background color.
    /// The background color is the color used for those pixels on the screen that are not covered by an image.
    /// </summary>
    public byte BackgroundColorIndex { get; set; }

    /// <summary>
    /// Gets or sets the collection of comments about the graphics, credits, descriptions or any
    /// other type of non-control and non-graphic data.
    /// </summary>
    public IList<string> Comments { get; set; } = [];

    /// <inheritdoc/>
    public static GifMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
        => new()
        {
            // Do not copy the color table or bit depth.
            // This will lead to a mismatch when the image is comprised of frames
            // extracted individually from a multi-frame image.
            ColorTableMode = metadata.ColorTableMode,
            RepeatCount = metadata.RepeatCount,
        };

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = this.ColorTableMode == FrameColorTableMode.Global && this.GlobalColorTable.HasValue
            ? Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(this.GlobalColorTable.Value.Length), 1, 8)
            : 8;

        return new(bpp)
        {
            ColorType = PixelColorType.Indexed,
            ComponentInfo = PixelComponentInfo.Create(1, bpp, bpp),
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            AnimateRootFrame = true,
            ColorTableMode = this.ColorTableMode,
            PixelTypeInfo = this.GetPixelTypeInfo(),
            RepeatCount = this.RepeatCount,
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.GlobalColorTable = null;

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public GifMetadata DeepClone() => new(this);
}

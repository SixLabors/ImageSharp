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
    {
        int index = 0;
        Color background = metadata.BackgroundColor;
        if (metadata.ColorTable.HasValue)
        {
            ReadOnlySpan<Color> colorTable = metadata.ColorTable.Value.Span;
            for (int i = 0; i < colorTable.Length; i++)
            {
                if (background != colorTable[i])
                {
                    continue;
                }

                index = i;
                break;
            }
        }

        return new()
        {
            GlobalColorTable = metadata.ColorTable,
            ColorTableMode = metadata.ColorTableMode,
            RepeatCount = metadata.RepeatCount,
            BackgroundColorIndex = (byte)Numerics.Clamp(index, 0, 255),
        };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = this.GlobalColorTable.HasValue
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
    {
        Color color = this.GlobalColorTable.HasValue && this.GlobalColorTable.Value.Span.Length > this.BackgroundColorIndex
            ? this.GlobalColorTable.Value.Span[this.BackgroundColorIndex]
            : Color.Transparent;

        return new()
        {
            AnimateRootFrame = true,
            BackgroundColor = color,
            ColorTable = this.GlobalColorTable,
            ColorTableMode = this.ColorTableMode,
            PixelTypeInfo = this.GetPixelTypeInfo(),
            RepeatCount = this.RepeatCount,
        };
    }

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public GifMetadata DeepClone() => new(this);
}

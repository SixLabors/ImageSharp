// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png.Chunks;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Provides Png specific metadata information for the image.
/// </summary>
public class PngMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PngMetadata"/> class.
    /// </summary>
    public PngMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PngMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private PngMetadata(PngMetadata other)
    {
        this.BitDepth = other.BitDepth;
        this.ColorType = other.ColorType;
        this.Gamma = other.Gamma;
        this.InterlaceMethod = other.InterlaceMethod;
        this.TransparentColor = other.TransparentColor;
        this.RepeatCount = other.RepeatCount;
        this.AnimateRootFrame = other.AnimateRootFrame;

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }

        for (int i = 0; i < other.TextData.Count; i++)
        {
            this.TextData.Add(other.TextData[i]);
        }
    }

    /// <summary>
    /// Gets or sets the number of bits per sample or per palette index (not per pixel).
    /// Not all values are allowed for all <see cref="ColorType"/> values.
    /// </summary>
    public PngBitDepth? BitDepth { get; set; }

    /// <summary>
    /// Gets or sets the color type.
    /// </summary>
    public PngColorType? ColorType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance should write an Adam7 interlaced image.
    /// </summary>
    public PngInterlaceMode? InterlaceMethod { get; set; } = PngInterlaceMode.None;

    /// <summary>
    /// Gets or sets the gamma value for the image.
    /// </summary>
    public float Gamma { get; set; }

    /// <summary>
    /// Gets or sets the color table, if any.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <summary>
    /// Gets or sets the transparent color used with non palette based images, if a transparency chunk and markers were decoded.
    /// </summary>
    public Color? TransparentColor { get; set; }

    /// <summary>
    /// Gets or sets the collection of text data stored within the iTXt, tEXt, and zTXt chunks.
    /// Used for conveying textual information associated with the image.
    /// </summary>
    public IList<PngTextData> TextData { get; set; } = new List<PngTextData>();

    /// <summary>
    /// Gets or sets the number of times to loop this APNG.  0 indicates infinite looping.
    /// </summary>
    public uint RepeatCount { get; set; } = 1;

    /// <summary>
    ///  Gets or sets a value indicating whether the root frame is shown as part of the animated sequence
    /// </summary>
    public bool AnimateRootFrame { get; set; } = true;

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new PngMetadata(this);

    internal static PngMetadata FromAnimatedMetadata(AnimatedImageMetadata metadata)
    {
        // Should the conversion be from a format that uses a 24bit palette entries (gif)
        // we need to clone and adjust the color table to allow for transparency.
        Color[]? colorTable = metadata.ColorTable?.ToArray();
        if (colorTable != null)
        {
            for (int i = 0; i < colorTable.Length; i++)
            {
                ref Color c = ref colorTable[i];
                if (c == metadata.BackgroundColor)
                {
                    // Png treats background as fully empty
                    c = Color.Transparent;
                    break;
                }
            }
        }

        return new()
        {
            ColorType = colorTable != null ? PngColorType.Palette : null,
            BitDepth = colorTable != null
                        ? (PngBitDepth)Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(colorTable.Length), 1, 8)
                        : null,
            ColorTable = colorTable,
            RepeatCount = metadata.RepeatCount,
        };
    }
}

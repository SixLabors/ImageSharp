// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL specific metadata information for the image.
/// </summary>
public class JxlMetadata : IFormatMetadata<JxlMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="JxlMetadata"/> class.</summary>
    public JxlMetadata()
    {
    }

    private JxlMetadata(JxlMetadata other) => this.HasAlpha = other.HasAlpha;

    /// <summary>Gets or sets a value indicating whether the image has an alpha channel.</summary>
    public bool HasAlpha { get; set; }

    /// <inheritdoc/>
    public static JxlMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
        => new() { HasAlpha = metadata.PixelTypeInfo.ColorType.HasFlag(PixelColorType.Alpha) };

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        if (this.HasAlpha)
        {
            return new PixelTypeInfo(32)
            {
                AlphaRepresentation = PixelAlphaRepresentation.Unassociated,
                ColorType = PixelColorType.RGB | PixelColorType.Alpha,
                ComponentInfo = PixelComponentInfo.Create(4, 32, 8, 8, 8, 8),
            };
        }

        return new PixelTypeInfo(24)
        {
            ColorType = PixelColorType.RGB,
            ComponentInfo = PixelComponentInfo.Create(3, 24, 8, 8, 8),
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new() { PixelTypeInfo = this.GetPixelTypeInfo() };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public JxlMetadata DeepClone() => new(this);
}

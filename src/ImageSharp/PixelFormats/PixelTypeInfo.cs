// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

// TODO: Review this type as it's used to represent 2 different things.
// 1. The encoded image pixel format.
// 2. The pixel format of the decoded image.
// Only the bits per pixel is used by the decoder, we should make it a property of the image metadata.
namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Contains information about the pixels that make up an images visual data.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PixelTypeInfo"/> struct.
/// </remarks>
/// <param name="bitsPerPixel">Color depth, in number of bits per pixel.</param>
public readonly struct PixelTypeInfo(int bitsPerPixel)
{
    /// <summary>
    /// Gets color depth, in number of bits per pixel.
    /// </summary>
    public int BitsPerPixel { get; init; } = bitsPerPixel;

    /// <summary>
    /// Gets the component bit depth and padding within the pixel.
    /// </summary>
    public PixelComponentInfo? ComponentInfo { get; init; }

    /// <summary>
    /// Gets the pixel color type.
    /// </summary>
    public PixelColorType ColorType { get; init; }

    /// <summary>
    /// Gets the pixel alpha transparency behavior.
    /// <see langword="null"/> means unknown, unspecified.
    /// </summary>
    public PixelAlphaRepresentation AlphaRepresentation { get; init; }

    /// <summary>
    /// Creates a new <see cref="PixelTypeInfo"/> instance.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="info">The pixel component info.</param>
    /// <param name="colorType">The pixel color type.</param>
    /// <param name="alphaRepresentation">The pixel alpha representation.</param>
    /// <returns>The <see cref="PixelComponentInfo"/>.</returns>
    public static PixelTypeInfo Create<TPixel>(
        PixelComponentInfo info,
        PixelColorType colorType,
        PixelAlphaRepresentation alphaRepresentation)
        where TPixel : unmanaged, IPixel<TPixel>
        => new()
        {
            BitsPerPixel = Unsafe.SizeOf<TPixel>() * 8,
            ComponentInfo = info,
            ColorType = colorType,
            AlphaRepresentation = alphaRepresentation
        };
}

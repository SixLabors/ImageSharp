// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

// TODO: Review this type as it's used to represent 2 different things.
// 1.The encoded image pixel format.
// 2. The pixel format of the decoded image.
namespace SixLabors.ImageSharp.Formats;

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
    /// Gets the count of the color components
    /// </summary>
    public int ComponentCount { get; init; }

    /// <summary>
    /// Gets the maximum precision of components within the pixel.
    /// </summary>
    public PixelComponentPrecision? MaxComponentPrecision { get; init; }

    /// <summary>
    /// Gets the pixel alpha transparency behavior.
    /// <see langword="null"/> means unknown, unspecified.
    /// </summary>
    public PixelAlphaRepresentation? AlphaRepresentation { get; init; }

    internal static PixelTypeInfo Create<TPixel>(
        byte componentCount,
        PixelComponentPrecision componentPrecision,
        PixelAlphaRepresentation pixelAlphaRepresentation)
        where TPixel : unmanaged, IPixel<TPixel>
        => new()
        {
            BitsPerPixel = Unsafe.SizeOf<TPixel>() * 8,
            ComponentCount = componentCount,
            MaxComponentPrecision = componentPrecision,
            AlphaRepresentation = pixelAlphaRepresentation
        };
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

// TODO: Review this class as it's used to represent 2 different things.
// 1.The encoded image pixel format.
// 2. The pixel format of the decoded image.
namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Contains information about the pixels that make up an images visual data.
/// </summary>
public readonly struct PixelTypeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PixelTypeInfo"/> class.
    /// </summary>
    /// <param name="bitsPerPixel">Color depth, in number of bits per pixel.</param>
    public PixelTypeInfo(int bitsPerPixel)
        => this.BitsPerPixel = bitsPerPixel;

    /// <summary>
    /// Gets color depth, in number of bits per pixel.
    /// </summary>
    public int BitsPerPixel { get; init; }

    public byte ComponentCount { get; init; }

    /// <summary>
    /// Gets the pixel alpha transparency behavior.
    /// <see langword="null"/> means unknown, unspecified.
    /// </summary>
    public PixelAlphaRepresentation? AlphaRepresentation { get; init; }

    internal static PixelTypeInfo Create<TPixel>(byte componentCount, PixelAlphaRepresentation pixelAlphaRepresentation)
        where TPixel : unmanaged, IPixel<TPixel>
        => new()
        {
            BitsPerPixel = Unsafe.SizeOf<TPixel>() * 8,
            ComponentCount = componentCount,
            AlphaRepresentation = pixelAlphaRepresentation
        };
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats;

/// <summary>
/// Provides an image and a palette for the reuse of a decoded palette. The two colors differ only in red, by 8 and 12.
/// Both fall in the same bucket of the coarse color cache, which keeps 5 bits of each channel, so coarse matching maps
/// both colors to one entry. Only exact matching keeps each pixel's color.
/// </summary>
internal static class NearColorPalette
{
    /// <summary>
    /// The first palette color.
    /// </summary>
    private static readonly Rgba32 First = new(8, 16, 16, 255);

    /// <summary>
    /// The second palette color.
    /// </summary>
    private static readonly Rgba32 Second = new(12, 16, 16, 255);

    /// <summary>
    /// Gets the palette: the two colors of <see cref="CreateImage"/>.
    /// </summary>
    public static ReadOnlyMemory<Color> Palette { get; } = new[] { Color.FromPixel(First), Color.FromPixel(Second) };

    /// <summary>
    /// Creates an 8x2 image with the two palette colors in a checkerboard.
    /// </summary>
    /// <returns>The image.</returns>
    public static Image<Rgba32> CreateImage()
    {
        Image<Rgba32> image = new(8, 2);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = (x + y) % 2 == 0 ? First : Second;
            }
        }

        return image;
    }
}

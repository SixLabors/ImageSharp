// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Converts rows of PGM and PPM samples to pixels.
/// </summary>
internal static class PbmSampleScaler
{
    /// <summary>
    /// Selects the conversion path of the image. Full-range samples, with Maxval 255 for 8-bit samples or 65535 for 16-bit samples, keep the byte and ushort
    /// conversion paths. Other ranges need normalized floats until the destination pixel format packs the values. Thus the method rents a vector row for them.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="isWide">True for 16-bit samples. False for 8-bit samples.</param>
    /// <param name="maxPixelValue">The Maxval that the header declares, from 1 to 65535.</param>
    /// <returns>
    /// The vector row for scaled samples, or <see langword="null"/> for full-range samples. The caller disposes it. For full-range samples, the caller uses
    /// <see cref="ToPixels{TPixel}"/>.
    /// </returns>
    public static IMemoryOwner<Vector4>? AllocateScaledRow(MemoryAllocator allocator, int width, bool isWide, int maxPixelValue)
        => maxPixelValue == (isWide ? ushort.MaxValue : byte.MaxValue) ? null : allocator.Allocate<Vector4>(width);

    /// <summary>
    /// Converts one decoded row of full-range samples to pixels with one bulk conversion.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="samples">
    /// The full-range samples of the row: one gray sample, or red, green and blue samples, for each pixel. 16-bit samples are in native byte order, which is
    /// the <see cref="L16"/> and <see cref="Rgb48"/> layout.
    /// </param>
    /// <param name="destination">The destination pixels. Its length is the number of pixels to convert.</param>
    /// <param name="isRgb">True for red, green and blue samples. False for gray samples.</param>
    /// <param name="isWide">True for 16-bit samples. False for 8-bit samples.</param>
    public static void ToPixels<TPixel>(Configuration configuration, ReadOnlySpan<byte> samples, Span<TPixel> destination, bool isRgb, bool isWide)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int count = destination.Length;

        // The sample layouts match the L8, L16, Rgb24 and Rgb48 layouts, so each row goes to one bulk conversion.
        if (isRgb)
        {
            if (isWide)
            {
                PixelOperations<TPixel>.Instance.FromRgb48Bytes(configuration, samples, destination, count);
            }
            else
            {
                PixelOperations<TPixel>.Instance.FromRgb24Bytes(configuration, samples, destination, count);
            }
        }
        else if (isWide)
        {
            PixelOperations<TPixel>.Instance.FromL16Bytes(configuration, samples, destination, count);
        }
        else
        {
            PixelOperations<TPixel>.Instance.FromL8Bytes(configuration, samples, destination, count);
        }
    }

    /// <summary>
    /// Normalizes the encoded red, green and blue magnitudes of one pixel to [0, 1]. Values outside the declared range saturate at its endpoints.
    /// </summary>
    /// <param name="red">The red sample magnitude, or the gray sample magnitude.</param>
    /// <param name="green">The green sample magnitude, or the gray sample magnitude.</param>
    /// <param name="blue">The blue sample magnitude, or the gray sample magnitude.</param>
    /// <param name="maxPixelValue">The maxval declared in the header, between 1 and 65535.</param>
    /// <returns>Normalized pixel components with opaque alpha.</returns>
    public static Vector4 Scale(int red, int green, int blue, int maxPixelValue)
    {
        // RGB holds the encoded magnitudes. Alpha is Maxval, so the normalization gives an alpha that is exactly opaque.
        // Maxval is a validated positive integer. Division maps Maxval to exactly one, including the alpha lane.
        // Saturation is a numeric conversion rule and does not depend on SegmentIntegrityHandling.
        Vector4 value = ColorNumerics.ClampToFinite(new Vector4(red, green, blue, maxPixelValue), Vector4.Zero, new Vector4(maxPixelValue));
        return value / maxPixelValue;
    }
}

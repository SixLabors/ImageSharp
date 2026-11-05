// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsPixelCompatible
{
    /// <summary>
    /// Converts the pixel data of the specified image from the source color profile to the target color profile using
    /// the provided color profile converter.
    /// </summary>
    /// <remarks>
    /// This method modifies the source image in place by converting its pixel data according to the
    /// color profiles specified in the converter. The method does not verify whether the profiles are RGB compatible;
    /// if they are not, the conversion may produce incorrect results. Ensure that both the source and target ICC
    /// profiles are set on the converter before calling this method.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="source">
    /// The image whose pixel data will be converted. The conversion is performed in place, modifying the original
    /// image.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the converter's source or target ICC profile is not specified.
    /// </exception>
    public static void Convert<TPixel>(this ColorProfileConverter converter, Image<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // These checks actually take place within the converter, but we want to fail fast here.
        // Note. we do not check to see whether the profiles themselves are RGB compatible,
        // if they are not, then the converter will simply produce incorrect results.
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        // Process the rows in parallel chunks, the converter itself is thread safe.
        // Request straight scaled vectors on both sides of ICC. The processor uses the destination pixel's rules to restore association.
        source.Mutate(o => o.ProcessPixelRowsAsVector4(
            row =>
            {
                // Sixteen RGB pixels occupy 192 bytes and supply 16 lanes per channel for Vector512<float>.
                // Limit each call to this capacity and reuse the storage instead of renting another row buffer.
                // Keep alpha in the processor's vector row while the typed profile conversion changes only RGB.
                // This inline storage has no cache-line alignment guarantee; SIMD loads support unaligned addresses.
                InlineArray16<Rgb> storage = default;
                Span<Rgb> rgb = storage;

                while (!row.IsEmpty)
                {
                    int length = Math.Min(row.Length, rgb.Length);
                    converter.Convert(row[..length], rgb[..length]);
                    row = row[length..];
                }
            },
            PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply));
    }

    /// <summary>
    /// Converts source pixels between the configured ICC profiles or RGB working spaces as straight, scaled vectors.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="converter">The converter configured with source and target profiles or working spaces.</param>
    /// <param name="configuration">The configuration used for pixel conversion.</param>
    /// <param name="source">The source pixels, which remain unchanged.</param>
    /// <param name="destination">The destination vectors.</param>
    public static void Convert<TPixel>(
        this ColorProfileConverter converter, Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Extract the complete pixel row through its bulk implementation before changing RGB.
        // PixelOperations validates the destination and selects the source alpha conversion.
        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;
        pixelOperations.ToUnassociatedScaledVector4(configuration, source, destination);
        destination = destination[..source.Length];
        InlineArray64<Rgb> storage = default;
        Span<Rgb> rgb = storage;

        // Reuse bounded RGB staging for the complete row. Separate staging keeps the profile
        // conversion from overwriting unread vector components. Alpha remains in the vector row.
        while (!destination.IsEmpty)
        {
            int count = Math.Min(destination.Length, rgb.Length);
            Span<Vector4> block = destination[..count];
            converter.Convert(block, rgb[..count]);
            destination = destination[count..];
        }
    }

    /// <summary>
    /// Converts encoded colors in place through the configured ICC profiles and packs them in the destination pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="converter">The converter configured with source and target ICC profiles.</param>
    /// <param name="colors">The encoded colors to convert.</param>
    public static void Convert<TPixel>(this ColorProfileConverter converter, Span<Color> colors)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<Vector4> vectorBuffer = converter.Options.MemoryAllocator.Allocate<Vector4>(colors.Length);
        using IMemoryOwner<Rgb> rgbBuffer = converter.Options.MemoryAllocator.Allocate<Rgb>(colors.Length);
        Span<Vector4> vectors = vectorBuffer.Memory.Span[..colors.Length];
        Span<Rgb> rgb = rgbBuffer.Memory.Span[..colors.Length];

        // Color preserves the encoded sample precision and alpha until the ICC result reaches the destination pixel format.
        for (int i = 0; i < colors.Length; i++)
        {
            vectors[i] = colors[i].ToScaledVector4(PixelAlphaRepresentation.Unassociated);
        }

        converter.Convert(vectors, rgb);

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.FromPixel(TPixel.FromUnassociatedScaledVector4(vectors[i]));
        }
    }

    /// <summary>
    /// Converts a color through the configured ICC profiles and returns it with the destination pixel format's precision and alpha representation.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="converter">The converter configured with source and target ICC profiles.</param>
    /// <param name="color">The encoded color to convert.</param>
    /// <returns>The converted color.</returns>
    public static Color Convert<TPixel>(this ColorProfileConverter converter, Color color)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4 source = color.ToScaledVector4(PixelAlphaRepresentation.Unassociated);
        Rgb converted = converter.ConvertUsingIccProfile<Rgb, Rgb>(Rgb.FromScaledVector4(source));

        // ICC transforms straight RGB; the original alpha is packed by the destination pixel's own rules.
        Vector4 result = new(converted.ToScaledVector3(), source.W);
        return Color.FromPixel(TPixel.FromUnassociatedScaledVector4(result));
    }

    /// <summary>
    /// Converts the RGB components of a pixel-vector row through typed profiles while retaining its alpha.
    /// </summary>
    /// <remarks>
    /// RGB values can remain outside [0,1]. The caller applies destination pixel storage bounds after this conversion.
    /// </remarks>
    /// <param name="converter">The converter configured with the source and target profiles or working spaces.</param>
    /// <param name="row">The pixel vectors updated in place. Alpha remains unchanged.</param>
    /// <param name="rgbSpan">Existing RGB storage with one entry per pixel. It must not overlap the vector row.</param>
    private static void Convert(this ColorProfileConverter converter, Span<Vector4> row, Span<Rgb> rgbSpan)
    {
        // Keep alpha in the pixel row while the existing typed API transforms the separate RGB row.
        // Replace only XYZ afterward, without applying pixel storage bounds or changing alpha bits.
        Rgb.FromScaledVector4(row, rgbSpan);
        converter.Convert(rgbSpan, rgbSpan);
        Numerics.WithXYZ(MemoryMarshal.Cast<Rgb, Vector3>(rgbSpan), row);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
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
        // The converter converts RGB in the vector row of the processor and keeps its alpha. Thus, no other row storage is necessary.
        source.Mutate(o => o.ProcessPixelRowsAsVector4(
            row => converter.Convert(row),
            PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply));
    }

    /// <summary>
    /// Converts source pixels from the converter's source profile to its target profile as straight, scaled vectors.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="configuration">The configuration used for pixel conversion.</param>
    /// <param name="source">The source pixels, which remain unchanged.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="rgb">Caller-owned storage that gets a copy of the converted RGB components.</param>
    public static void Convert<TPixel>(
        this ColorProfileConverter converter, Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination, Span<Rgb> rgb)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // ICC consumes straight RGB. Extract associated pixels as straight vectors without changing the source pixels.
        PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(configuration, source, destination);

        Span<Vector4> row = destination[..source.Length];
        converter.Convert(row);

        // Planar encoders read the converted RGB from this copy.
        Rgb.FromScaledVector4(row, rgb[..source.Length]);
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
        Span<Vector4> vectors = vectorBuffer.Memory.Span;

        // Color preserves the encoded sample precision and alpha until the ICC result reaches the destination pixel format.
        for (int i = 0; i < colors.Length; i++)
        {
            vectors[i] = colors[i].ToScaledVector4(PixelAlphaRepresentation.Unassociated);
        }

        converter.Convert(vectors[..colors.Length]);

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
    /// Converts scaled vector RGB components in place while preserving their alpha components.
    /// </summary>
    /// <remarks>
    /// RGB remains unclipped. Destination pixel conversion applies integer bounds; floating-point pixels retain extended values.
    /// </remarks>
    /// <param name="converter">The color profile converter configured with source and target ICC profiles.</param>
    /// <param name="row">The scaled vectors whose RGB components are converted.</param>
    public static void Convert(this ColorProfileConverter converter, Span<Vector4> row)
    {
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        // The row holds RGB colors. Thus, W is alpha and not color data, also when a profile has four channels.
        // The operator gets W = 1, and the row keeps its alpha.
        // The result is the same as a conversion through Rgb storage, which has W = 1. But this method does not copy the row.
        ColorProfileConverterExtensionsIcc.IccTransform transform = converter.GetIccTransform();
        transform.Converter.Convert(row, row, false);
    }
}

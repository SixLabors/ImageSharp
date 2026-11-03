// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Allows conversion between two color profiles based on the CIE XYZ color space.
/// </summary>
public static class ColorProfileConverterExtensionsCieXyzCieXyz
{
    /// <summary>
    /// Converts a color value from one color profile to another using the specified color profile converter.
    /// </summary>
    /// <remarks>
    /// The conversion process may use ICC profiles if available; otherwise, it performs a manual
    /// conversion through the profile connection space (PCS) with chromatic adaptation as needed. The method requires
    /// both source and target types to be value types implementing the appropriate color profile interface.
    /// </remarks>
    /// <typeparam name="TFrom">The source color profile type. Must implement <see cref="IColorProfile{TFrom, CieXyz}"/>.</typeparam>
    /// <typeparam name="TTo">The target color profile type. Must implement <see cref="IColorProfile{TTo, CieXyz}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion.</param>
    /// <param name="source">The source color value to convert.</param>
    /// <returns>A value of type <typeparamref name="TTo"/> representing the converted color in the target color profile.</returns>
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieXyz>
        where TTo : struct, IColorProfile<TTo, CieXyz>
    {
        if (converter.ShouldUseIccProfiles())
        {
            return converter.ConvertUsingIccProfile<TFrom, TTo>(source);
        }

        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieXyz pcsFrom = source.ToProfileConnectingSpace(options);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        pcsFrom = VonKriesChromaticAdaptation.Transform(in pcsFrom, whitePoints, options.AdaptationMatrix, options.InverseAdaptationMatrix);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, in pcsFrom);
    }

    /// <summary>
    /// Converts a span of color values from one color profile to another using the specified color profile converter.
    /// </summary>
    /// <remarks>
    /// This method performs color conversion between two color profiles, handling necessary
    /// transformations such as profile connection space conversion and chromatic adaptation. If ICC profiles are
    /// available and applicable, the conversion uses them for improved accuracy. The method does not allocate memory
    /// for the destination; the caller is responsible for providing a suitably sized span.
    /// </remarks>
    /// <typeparam name="TFrom">The type representing the source color profile. Must implement <see cref="IColorProfile{TFrom, CieXyz}"/>.</typeparam>
    /// <typeparam name="TTo">The type representing the destination color profile. Must implement <see cref="IColorProfile{TTo, CieXyz}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion operation.</param>
    /// <param name="source">A read-only span containing the source color values to convert.</param>
    /// <param name="destination">A span that receives the converted color values. Must be at least as long as the source span.</param>
    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieXyz>
        where TTo : struct, IColorProfile<TTo, CieXyz>
    {
        if (converter.ShouldUseIccProfiles())
        {
            converter.ConvertUsingIccProfile(source, destination);
            return;
        }

        // Validate the complete destination before block traversal can write a partial result.
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        ColorConversionOptions options = converter.Options;

        // Reuse one 3 KiB XYZ block for the complete span. Row callers need no allocator rental.
        // Each block contains sixteen complete Vector512 component batches, or smaller SIMD batches.
        // The source conversion writes every component before adaptation or destination conversion reads it.
        Unsafe.SkipInit(out InlineArray256<CieXyz> storage);
        Span<CieXyz> connection = storage;
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();

        while (!source.IsEmpty)
        {
            int count = Math.Min(source.Length, connection.Length);
            Span<CieXyz> block = connection[..count];

            // Read the complete input block into separate XYZ storage before writing destination colors.
            // This permits in-place conversion between the three-component color models.
            TFrom.ToProfileConnectionSpace(options, source[..count], block);

            // Change the XYZ reference white before converting to the destination color model.
            VonKriesChromaticAdaptation.Transform(block, block, whitePoints, options.AdaptationMatrix, options.InverseAdaptationMatrix);

            // Each model's bulk conversion handles complete SIMD batches and any remaining colors.
            TTo.FromProfileConnectionSpace(options, block, destination[..count]);

            source = source[count..];
            destination = destination[count..];
        }
    }
}

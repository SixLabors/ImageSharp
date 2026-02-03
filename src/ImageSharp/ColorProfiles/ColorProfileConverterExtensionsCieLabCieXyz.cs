// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Allows conversion between two color profiles based on the CIE Lab and CIE XYZ color spaces.
/// </summary>
public static class ColorProfileConverterExtensionsCieLabCieXyz
{
    /// <summary>
    /// Converts a color value from one color profile to another using the specified color profile converter.
    /// </summary>
    /// <remarks>
    /// The conversion process may use ICC profiles if available; otherwise, it performs a manual
    /// conversion through the profile connection space (PCS) with chromatic adaptation as needed. The method requires
    /// both source and target types to be value types implementing the appropriate color profile interface.
    /// </remarks>
    /// <typeparam name="TFrom">The source color profile type. Must implement <see cref="IColorProfile{TFrom, CieLab}"/>.</typeparam>
    /// <typeparam name="TTo">The target color profile type. Must implement <see cref="IColorProfile{TTo, CieXyz}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion.</param>
    /// <param name="source">The source color value to convert.</param>
    /// <returns>A value of type <typeparamref name="TTo"/> representing the converted color in the target color profile.</returns>
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, CieXyz>
    {
        if (converter.ShouldUseIccProfiles())
        {
            return converter.ConvertUsingIccProfile<TFrom, TTo>(source);
        }

        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieLab pcsFrom = source.ToProfileConnectingSpace(options);

        // Convert between PCS
        CieXyz pcsTo = pcsFrom.ToProfileConnectingSpace(options);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        pcsTo = VonKriesChromaticAdaptation.Transform(in pcsTo, whitePoints, options.AdaptationMatrix);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, in pcsTo);
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
    /// <typeparam name="TFrom">The type representing the source color profile. Must implement <see cref="IColorProfile{TFrom, CieLab}"/>.</typeparam>
    /// <typeparam name="TTo">The type representing the destination color profile. Must implement <see cref="IColorProfile{TTo, CieXyz}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion operation.</param>
    /// <param name="source">A read-only span containing the source color values to convert.</param>
    /// <param name="destination">A span that receives the converted color values. Must be at least as long as the source span.</param>
    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, CieXyz>
    {
        if (converter.ShouldUseIccProfiles())
        {
            converter.ConvertUsingIccProfile(source, destination);
            return;
        }

        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<CieLab> pcsFromOwner = options.MemoryAllocator.Allocate<CieLab>(source.Length);
        Span<CieLab> pcsFrom = pcsFromOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFrom);

        // Convert between PCS.
        using IMemoryOwner<CieXyz> pcsToOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsTo = pcsToOwner.GetSpan();
        CieLab.ToProfileConnectionSpace(options, pcsFrom, pcsTo);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        VonKriesChromaticAdaptation.Transform(pcsTo, pcsTo, whitePoints, options.AdaptationMatrix);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsTo, destination);
    }
}

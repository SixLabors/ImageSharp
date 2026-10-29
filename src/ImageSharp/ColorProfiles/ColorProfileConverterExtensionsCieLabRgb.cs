// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Allows conversion between two color profiles based on the CIE Lab and RGB color spaces.
/// </summary>
public static class ColorProfileConverterExtensionsCieLabRgb
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
    /// <typeparam name="TTo">The target color profile type. Must implement <see cref="IColorProfile{TTo, Rgb}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion.</param>
    /// <param name="source">The source color value to convert.</param>
    /// <returns>A value of type <typeparamref name="TTo"/> representing the converted color in the target color profile.</returns>
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        if (converter.ShouldUseIccProfiles())
        {
            return converter.ConvertUsingIccProfile<TFrom, TTo>(source);
        }

        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieLab pcsFromA = source.ToProfileConnectingSpace(options);
        CieXyz pcsFromB = pcsFromA.ToProfileConnectingSpace(options);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        pcsFromB = VonKriesChromaticAdaptation.Transform(in pcsFromB, whitePoints, options.AdaptationMatrix, options.InverseAdaptationMatrix);

        // Convert between PCS
        Rgb pcsTo = Rgb.FromProfileConnectingSpace(options, in pcsFromB);

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
    /// <typeparam name="TTo">The type representing the destination color profile. Must implement <see cref="IColorProfile{TTo, Rgb}"/>.</typeparam>
    /// <param name="converter">The color profile converter to use for the conversion operation.</param>
    /// <param name="source">A read-only span containing the source color values to convert.</param>
    /// <param name="destination">A span that receives the converted color values. Must be at least as long as the source span.</param>
    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        if (converter.ShouldUseIccProfiles())
        {
            converter.ConvertUsingIccProfile(source, destination);
            return;
        }

        ColorConversionOptions options = converter.Options;

        // Sixteen colors fill a 512-bit register per component. Reuse these stack blocks across the full input;
        // callers that convert image rows do not rent three temporary buffers for each row.
        InlineArray16<CieLab> inputStorage = default;
        InlineArray16<CieXyz> connectionStorage = default;
        InlineArray16<Rgb> outputStorage = default;

        Span<CieLab> input = inputStorage;
        Span<CieXyz> connection = connectionStorage;
        Span<Rgb> output = outputStorage;
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();

        while (!source.IsEmpty)
        {
            int count = Math.Min(source.Length, input.Length);
            Span<CieLab> inputBlock = input[..count];
            Span<CieXyz> connectionBlock = connection[..count];
            Span<Rgb> outputBlock = output[..count];

            // Read the entire block before writing its result, so matching input and output spans can share storage.
            TFrom.ToProfileConnectionSpace(options, source[..count], inputBlock);
            CieLab.ToProfileConnectionSpace(options, inputBlock, connectionBlock);

            // Adapt XYZ from the Lab white point to the target RGB white point before computing RGB components.
            VonKriesChromaticAdaptation.Transform(connectionBlock, connectionBlock, whitePoints, options.AdaptationMatrix, options.InverseAdaptationMatrix);

            Rgb.FromProfileConnectionSpace(options, connectionBlock, outputBlock);
            TTo.FromProfileConnectionSpace(options, outputBlock, destination[..count]);

            source = source[count..];
            destination = destination[count..];
        }
    }
}

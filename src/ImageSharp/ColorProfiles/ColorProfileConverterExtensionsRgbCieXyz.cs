// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsRgbCieXyz
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, CieXyz>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        Rgb pcsFrom = source.ToProfileConnectingSpace(options);

        // Convert between PCS
        CieXyz pcsTo = pcsFrom.ToProfileConnectingSpace(options);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        pcsTo = VonKriesChromaticAdaptation.Transform(in pcsTo, whitePoints, options.AdaptationMatrix);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, in pcsTo);
    }

    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, CieXyz>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<Rgb> pcsFromOwner = options.MemoryAllocator.Allocate<Rgb>(source.Length);
        Span<Rgb> pcsFrom = pcsFromOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFrom);

        // Convert between PCS.
        using IMemoryOwner<CieXyz> pcsToOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsTo = pcsToOwner.GetSpan();
        Rgb.ToProfileConnectionSpace(options, pcsFrom, pcsTo);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        VonKriesChromaticAdaptation.Transform(pcsTo, pcsTo, whitePoints, options.AdaptationMatrix);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsTo, destination);
    }
}

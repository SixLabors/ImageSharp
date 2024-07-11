// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsRgbRgb
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        Rgb pcsFromA = source.ToProfileConnectingSpace(options);
        CieXyz pcsFromB = pcsFromA.ToProfileConnectingSpace(options);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        pcsFromB = VonKriesChromaticAdaptation.Transform(in pcsFromB, whitePoints, options.AdaptationMatrix);

        // Convert between PCS
        Rgb pcsTo = Rgb.FromProfileConnectingSpace(options, in pcsFromB);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, in pcsTo);
    }

    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<Rgb> pcsFromToOwner = options.MemoryAllocator.Allocate<Rgb>(source.Length);
        Span<Rgb> pcsFromTo = pcsFromToOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFromTo);

        using IMemoryOwner<CieXyz> pcsFromOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsFrom = pcsFromOwner.GetSpan();
        Rgb.ToProfileConnectionSpace(options, pcsFromTo, pcsFrom);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        VonKriesChromaticAdaptation.Transform(pcsFrom, pcsFrom, whitePoints, options.AdaptationMatrix);

        // Convert between PCS.
        Rgb.FromProfileConnectionSpace(options, pcsFrom, pcsFromTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsFromTo, destination);
    }
}

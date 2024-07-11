// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsCieXyzRgb
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieXyz>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieXyz pcsFrom = source.ToProfileConnectingSpace(options);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        pcsFrom = VonKriesChromaticAdaptation.Transform(in pcsFrom, whitePoints, options.AdaptationMatrix);

        // Convert between PCS
        Rgb pcsTo = Rgb.FromProfileConnectingSpace(options, in pcsFrom);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, in pcsTo);
    }

    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieXyz>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<CieXyz> pcsFromOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsFrom = pcsFromOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFrom);

        // Adapt to target white point
        (CieXyz From, CieXyz To) whitePoints = converter.GetChromaticAdaptionWhitePoints<TFrom, TTo>();
        VonKriesChromaticAdaptation.Transform(pcsFrom, pcsFrom, whitePoints, options.AdaptationMatrix);

        // Convert between PCS.
        using IMemoryOwner<Rgb> pcsToOwner = options.MemoryAllocator.Allocate<Rgb>(source.Length);
        Span<Rgb> pcsTo = pcsToOwner.GetSpan();
        Rgb.FromProfileConnectionSpace(options, pcsFrom, pcsTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsTo, destination);
    }
}

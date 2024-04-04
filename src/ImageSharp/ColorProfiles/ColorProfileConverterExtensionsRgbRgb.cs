// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsRgbRgb
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, TFrom source)
        where TFrom : struct, IColorProfile<TFrom, Rgb>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        Rgb pcsFromA = source.ToProfileConnectingSpace(options);
        CieXyz pcsFromB = pcsFromA.ToProfileConnectingSpace(options);

        // Adapt to target white point
        pcsFromB = VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, in pcsFromB);

        // Convert between PCS
        Rgb pcsTo = Rgb.FromProfileConnectingSpace(options, in pcsFromB);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, pcsTo);
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
        VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, pcsFrom, pcsFrom);

        // Convert between PCS.
        Rgb.FromProfileConnectionSpace(options, pcsFrom, pcsFromTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsFromTo, destination);
    }
}

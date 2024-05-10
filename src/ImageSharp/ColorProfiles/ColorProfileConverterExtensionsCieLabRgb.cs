// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsCieLabRgb
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieLab pcsFromA = source.ToProfileConnectingSpace(options);
        CieXyz pcsFromB = pcsFromA.ToProfileConnectingSpace(options);

        // Adapt to target white point
        pcsFromB = VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, in pcsFromB);

        // Convert between PCS
        Rgb pcsTo = Rgb.FromProfileConnectingSpace(options, in pcsFromB);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, pcsTo);
    }

    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, Rgb>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<CieLab> pcsFromAOwner = options.MemoryAllocator.Allocate<CieLab>(source.Length);
        Span<CieLab> pcsFromA = pcsFromAOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFromA);

        using IMemoryOwner<CieXyz> pcsFromBOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsFromB = pcsFromBOwner.GetSpan();
        CieLab.ToProfileConnectionSpace(options, pcsFromA, pcsFromB);

        // Adapt to target white point
        VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, pcsFromB, pcsFromB);

        // Convert between PCS.
        using IMemoryOwner<Rgb> pcsToOwner = options.MemoryAllocator.Allocate<Rgb>(source.Length);
        Span<Rgb> pcsTo = pcsToOwner.GetSpan();
        Rgb.FromProfileConnectionSpace(options, pcsFromB, pcsTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsTo, destination);
    }
}

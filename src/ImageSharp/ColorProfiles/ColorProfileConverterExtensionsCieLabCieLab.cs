// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsCieLabCieLab
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, CieLab>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieLab pcsFromA = source.ToProfileConnectingSpace(options);
        CieXyz pcsFromB = pcsFromA.ToProfileConnectingSpace(options);

        // Adapt to target white point
        pcsFromB = VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, in pcsFromB);

        // Convert between PCS
        CieLab pcsTo = CieLab.FromProfileConnectingSpace(options, in pcsFromB);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, pcsTo);
    }

    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieLab>
        where TTo : struct, IColorProfile<TTo, CieLab>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<CieLab> pcsFromToOwner = options.MemoryAllocator.Allocate<CieLab>(source.Length);
        Span<CieLab> pcsFromTo = pcsFromToOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFromTo);

        using IMemoryOwner<CieXyz> pcsFromOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsFrom = pcsFromOwner.GetSpan();
        CieLab.ToProfileConnectionSpace(options, pcsFromTo, pcsFrom);

        // Adapt to target white point
        VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, pcsFrom, pcsFrom);

        // Convert between PCS.
        CieLab.FromProfileConnectionSpace(options, pcsFrom, pcsFromTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsFromTo, destination);
    }
}

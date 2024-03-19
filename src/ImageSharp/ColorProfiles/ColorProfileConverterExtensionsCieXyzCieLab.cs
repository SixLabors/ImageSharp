// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsCieXyzCieLab
{
    public static TTo Convert<TFrom, TTo>(this ColorProfileConverter converter, TFrom source)
        where TFrom : struct, IColorProfile<TFrom, CieXyz>
        where TTo : struct, IColorProfile<TTo, CieLab>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS
        CieXyz pcsFrom = source.ToProfileConnectingSpace(options);

        // Adapt to target white point
        VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, in pcsFrom);

        // Convert between PCS
        CieLab pcsTo = CieLab.FromProfileConnectingSpace(options, in pcsFrom);

        // Convert to output from PCS
        return TTo.FromProfileConnectingSpace(options, pcsTo);
    }

    public static void Convert<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom, CieXyz>
        where TTo : struct, IColorProfile<TTo, CieLab>
    {
        ColorConversionOptions options = converter.Options;

        // Convert to input PCS.
        using IMemoryOwner<CieXyz> pcsFromOwner = options.MemoryAllocator.Allocate<CieXyz>(source.Length);
        Span<CieXyz> pcsFrom = pcsFromOwner.GetSpan();
        TFrom.ToProfileConnectionSpace(options, source, pcsFrom);

        // Adapt to target white point
        VonKriesChromaticAdaptation.Transform<TFrom, TTo>(options, pcsFrom, pcsFrom);

        // Convert between PCS.
        using IMemoryOwner<CieLab> pcsToOwner = options.MemoryAllocator.Allocate<CieLab>(source.Length);
        Span<CieLab> pcsTo = pcsToOwner.GetSpan();
        CieLab.FromProfileConnectionSpace(options, pcsFrom, pcsTo);

        // Convert to output from PCS
        TTo.FromProfileConnectionSpace(options, pcsTo, destination);
    }
}

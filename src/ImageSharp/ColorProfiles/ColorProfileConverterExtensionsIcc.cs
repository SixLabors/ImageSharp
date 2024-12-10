// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsIcc
{
    internal static TTo ConvertUsingIccProfile<TFrom, TTo>(this ColorProfileConverter converter, in TFrom source)
        where TFrom : struct, IColorProfile<TFrom>
        where TTo : struct, IColorProfile<TTo>
    {
        // TODO: Validation of ICC Profiles against color profile. Is this possible?
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        ColorProfileConverter pcsConverter = new(new ColorConversionOptions()
        {
            MemoryAllocator = converter.Options.MemoryAllocator,

            // TODO: Double check this but I think these are normalized values.
            SourceWhitePoint = CieXyz.FromScaledVector4(new(converter.Options.SourceIccProfile.Header.PcsIlluminant, 1F)),
            TargetWhitePoint = CieXyz.FromScaledVector4(new(converter.Options.TargetIccProfile.Header.PcsIlluminant, 1F)),
        });

        IccDataToPcsConverter sourceConverter = new(converter.Options.SourceIccProfile);
        IccPcsToDataConverter targetConverter = new(converter.Options.TargetIccProfile);
        IccColorSpaceType sourcePcsType = converter.Options.SourceIccProfile.Header.ProfileConnectionSpace;
        IccColorSpaceType targetPcsType = converter.Options.TargetIccProfile.Header.ProfileConnectionSpace;
        IccVersion sourceVersion = converter.Options.SourceIccProfile.Header.Version;
        IccVersion targetVersion = converter.Options.TargetIccProfile.Header.Version;

        Vector4 pcs = sourceConverter.Calculate(source.ToScaledVector4());

        // Profile connecting spaces can only be Lab, XYZ.
        if (sourcePcsType is IccColorSpaceType.CieLab && targetPcsType is IccColorSpaceType.CieXyz)
        {
            // Convert from Lab to XYZ.
            CieLab lab = CieLab.FromScaledVector4(pcs);
            CieXyz xyz = pcsConverter.Convert<CieLab, CieXyz>(in lab);
            pcs = xyz.ToScaledVector4();
        }
        else if (sourcePcsType is IccColorSpaceType.CieXyz && targetPcsType is IccColorSpaceType.CieLab)
        {
            // Convert from XYZ to Lab.
            CieXyz xyz = CieXyz.FromScaledVector4(pcs);
            CieLab lab = pcsConverter.Convert<CieXyz, CieLab>(in xyz);
            pcs = lab.ToScaledVector4();
        }
        else if (sourcePcsType is IccColorSpaceType.CieXyz && targetPcsType is IccColorSpaceType.CieXyz)
        {
            // Convert from XYZ to XYZ.
            CieXyz xyz = CieXyz.FromScaledVector4(pcs);
            CieXyz targetXyz = pcsConverter.Convert<CieXyz, CieXyz>(in xyz);
            pcs = targetXyz.ToScaledVector4();
        }
        else if (sourcePcsType is IccColorSpaceType.CieLab && targetPcsType is IccColorSpaceType.CieLab)
        {
            // Convert from Lab to Lab.
            if (sourceVersion.Major == 4 && targetVersion.Major == 2)
            {
                // Convert from Lab v4 to Lab v2.
                pcs = LabToLabV2(pcs);
            }
            else if (sourceVersion.Major == 2 && targetVersion.Major == 4)
            {
                // Convert from Lab v2 to Lab v4.
                pcs = LabV2ToLab(pcs);
            }

            CieLab lab = CieLab.FromScaledVector4(pcs);
            CieLab targetLab = pcsConverter.Convert<CieLab, CieLab>(in lab);
            pcs = targetLab.ToScaledVector4();
        }

        // Convert to the target space.
        return TTo.FromScaledVector4(targetConverter.Calculate(pcs));
    }

    internal static void ConvertUsingIccProfile<TFrom, TTo>(this ColorProfileConverter converter, ReadOnlySpan<TFrom> source, Span<TTo> destination)
        where TFrom : struct, IColorProfile<TFrom>
        where TTo : struct, IColorProfile<TTo>
    {
        // TODO: Validation of ICC Profiles against color profile. Is this possible?
        if (converter.Options.SourceIccProfile is null)
        {
            throw new InvalidOperationException("Source ICC profile is missing.");
        }

        if (converter.Options.TargetIccProfile is null)
        {
            throw new InvalidOperationException("Target ICC profile is missing.");
        }

        Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(destination));

        ColorProfileConverter pcsConverter = new(new ColorConversionOptions()
        {
            MemoryAllocator = converter.Options.MemoryAllocator,

            // TODO: Double check this but I think these are normalized values.
            SourceWhitePoint = CieXyz.FromScaledVector4(new(converter.Options.SourceIccProfile.Header.PcsIlluminant, 1F)),
            TargetWhitePoint = CieXyz.FromScaledVector4(new(converter.Options.TargetIccProfile.Header.PcsIlluminant, 1F)),
        });

        IccDataToPcsConverter sourceConverter = new(converter.Options.SourceIccProfile);
        IccPcsToDataConverter targetConverter = new(converter.Options.TargetIccProfile);
        IccColorSpaceType sourcePcsType = converter.Options.SourceIccProfile.Header.ProfileConnectionSpace;
        IccColorSpaceType targetPcsType = converter.Options.TargetIccProfile.Header.ProfileConnectionSpace;
        IccVersion sourceVersion = converter.Options.SourceIccProfile.Header.Version;
        IccVersion targetVersion = converter.Options.TargetIccProfile.Header.Version;

        using IMemoryOwner<Vector4> pcsBuffer = converter.Options.MemoryAllocator.Allocate<Vector4>(source.Length);
        Span<Vector4> pcsNormalized = pcsBuffer.GetSpan();

        // First normalize the values.
        TFrom.ToScaledVector4(source, pcsNormalized);

        // Now convert to the PCS space.
        sourceConverter.Calculate(pcsNormalized, pcsNormalized);

        // Profile connecting spaces can only be Lab, XYZ.
        if (sourcePcsType is IccColorSpaceType.CieLab && targetPcsType is IccColorSpaceType.CieXyz)
        {
            // Convert from Lab to XYZ.
            using IMemoryOwner<CieLab> pcsFromBuffer = converter.Options.MemoryAllocator.Allocate<CieLab>(source.Length);
            Span<CieLab> pcsFrom = pcsFromBuffer.GetSpan();
            CieLab.FromScaledVector4(pcsNormalized, pcsFrom);

            using IMemoryOwner<CieXyz> pcsToBuffer = converter.Options.MemoryAllocator.Allocate<CieXyz>(source.Length);
            Span<CieXyz> pcsTo = pcsToBuffer.GetSpan();
            pcsConverter.Convert<CieLab, CieXyz>(pcsFrom, pcsTo);

            // Convert to the target normalized PCS space.
            CieXyz.ToScaledVector4(pcsTo, pcsNormalized);
        }
        else if (sourcePcsType is IccColorSpaceType.CieXyz && targetPcsType is IccColorSpaceType.CieLab)
        {
            // Convert from XYZ to Lab.
            using IMemoryOwner<CieXyz> pcsFromBuffer = converter.Options.MemoryAllocator.Allocate<CieXyz>(source.Length);
            Span<CieXyz> pcsFrom = pcsFromBuffer.GetSpan();
            CieXyz.FromScaledVector4(pcsNormalized, pcsFrom);

            using IMemoryOwner<CieLab> pcsToBuffer = converter.Options.MemoryAllocator.Allocate<CieLab>(source.Length);
            Span<CieLab> pcsTo = pcsToBuffer.GetSpan();
            pcsConverter.Convert<CieXyz, CieLab>(pcsFrom, pcsTo);

            // Convert to the target normalized PCS space.
            CieLab.ToScaledVector4(pcsTo, pcsNormalized);
        }
        else if (sourcePcsType is IccColorSpaceType.CieXyz && targetPcsType is IccColorSpaceType.CieXyz)
        {
            // Convert from XYZ to XYZ.
            using IMemoryOwner<CieXyz> pcsFromToBuffer = converter.Options.MemoryAllocator.Allocate<CieXyz>(source.Length);
            Span<CieXyz> pcsFromTo = pcsFromToBuffer.GetSpan();
            CieXyz.FromScaledVector4(pcsNormalized, pcsFromTo);

            pcsConverter.Convert<CieXyz, CieXyz>(pcsFromTo, pcsFromTo);

            // Convert to the target normalized PCS space.
            CieXyz.ToScaledVector4(pcsFromTo, pcsNormalized);
        }
        else if (sourcePcsType is IccColorSpaceType.CieLab && targetPcsType is IccColorSpaceType.CieLab)
        {
            // Convert from Lab to Lab.
            if (sourceVersion.Major == 4 && targetVersion.Major == 2)
            {
                // Convert from Lab v4 to Lab v2.
                LabToLabV2(pcsNormalized, pcsNormalized);
            }
            else if (sourceVersion.Major == 2 && targetVersion.Major == 4)
            {
                // Convert from Lab v2 to Lab v4.
                LabV2ToLab(pcsNormalized, pcsNormalized);
            }

            using IMemoryOwner<CieLab> pcsFromToBuffer = converter.Options.MemoryAllocator.Allocate<CieLab>(source.Length);
            Span<CieLab> pcsFromTo = pcsFromToBuffer.GetSpan();
            CieLab.FromScaledVector4(pcsNormalized, pcsFromTo);

            pcsConverter.Convert<CieLab, CieLab>(pcsFromTo, pcsFromTo);

            // Convert to the target normalized PCS space.
            CieLab.ToScaledVector4(pcsFromTo, pcsNormalized);
        }

        // Convert to the target space.
        targetConverter.Calculate(pcsNormalized, pcsNormalized);
        TTo.FromScaledVector4(pcsNormalized, destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 LabToLabV2(Vector4 input)
        => input * 65280F / 65535F;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 LabV2ToLab(Vector4 input)
        => input * 65535F / 65280F;

    private static void LabToLabV2(Span<Vector4> source, Span<Vector4> destination)
        => LabToLab(source, destination, 65280F / 65535F);

    private static void LabV2ToLab(Span<Vector4> source, Span<Vector4> destination)
        => LabToLab(source, destination, 65535F / 65280F);

    private static void LabToLab(Span<Vector4> source, Span<Vector4> destination, [ConstantExpected] float scale)
    {
        if (Vector.IsHardwareAccelerated)
        {
            Vector<float> vScale = new(scale);
            int i = 0;

            // SIMD loop
            int simdBatchSize = Vector<float>.Count / 4; // Number of Vector4 elements per SIMD batch
            for (; i <= source.Length - simdBatchSize; i += simdBatchSize)
            {
                // Load the vector from source span
                Vector<float> v = Unsafe.ReadUnaligned<Vector<float>>(ref Unsafe.As<Vector4, byte>(ref source[i]));

                // Scale the vector
                v *= vScale;

                // Write the scaled vector to the destination span
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector4, byte>(ref destination[i]), v);
            }

            // Scalar fallback for remaining elements
            for (; i < source.Length; i++)
            {
                destination[i] = source[i] * scale;
            }
        }
        else
        {
            // Scalar fallback if SIMD is not supported
            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = source[i] * scale;
            }
        }
    }
}

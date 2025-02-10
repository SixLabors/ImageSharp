// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
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

        ConversionParams sourceParams = new(converter.Options.SourceIccProfile, toPcs: true);
        ConversionParams targetParams = new(converter.Options.TargetIccProfile, toPcs: false);

        ColorProfileConverter pcsConverter = new(new ColorConversionOptions
        {
            MemoryAllocator = converter.Options.MemoryAllocator,
            SourceWhitePoint = new CieXyz(converter.Options.SourceIccProfile.Header.PcsIlluminant),
            TargetWhitePoint = new CieXyz(converter.Options.TargetIccProfile.Header.PcsIlluminant),
        });

        // output of Matrix TRC calculator is descaled XYZ, needs to be re-scaled to be used as PCS
        Vector4 sourcePcs = sourceParams.Converter.Calculate(source.ToScaledVector4());
        Vector4 targetPcs;

        // if both profiles need PCS adjustment, they both share the same unadjusted PCS space
        // cancelling out the need to make the adjustment
        // TODO: handle PCS adjustment for absolute intent? would make this a lot more complicated
        // TODO: alternatively throw unsupported error, since most profiles headers contain perceptual (i've encountered a couple of relative intent, but so far no saturation or absolute)
        if (sourceParams.AdjustPcsForPerceptual ^ targetParams.AdjustPcsForPerceptual)
        {
            targetPcs = GetTargetPcsWithPerceptualV2Adjustment(sourcePcs, sourceParams, targetParams, pcsConverter);
        }
        else
        {
            targetPcs = GetTargetPcsWithoutAdjustment(sourcePcs, sourceParams, targetParams, pcsConverter);
        }

        // Convert to the target space.
        // input to Matrix TRC calculator is descaled XYZ, need to descale PCS before use
        return TTo.FromScaledVector4(targetParams.Converter.Calculate(targetPcs));
    }

    // TODO: update to match workflow of the function above
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

    private static Vector4 GetTargetPcsWithoutAdjustment(
        Vector4 sourcePcs,
        ConversionParams sourceParams,
        ConversionParams targetParams,
        ColorProfileConverter pcsConverter)
    {
        // Profile connecting spaces can only be Lab, XYZ.
        // 16-bit Lab encodings changed from v2 to v4, but 16-bit LUTs always use the legacy encoding regardless of version
        // so ensure that Lab is using the correct encoding when a 16-bit LUT is used
        switch (sourceParams.PcsType)
        {
            // Convert from Lab to XYZ.
            case IccColorSpaceType.CieLab when targetParams.PcsType is IccColorSpaceType.CieXyz:
            {
                sourcePcs = sourceParams.Is16BitLutEntry ? LabV2ToLab(sourcePcs) : sourcePcs;
                CieLab lab = CieLab.FromScaledVector4(sourcePcs);
                CieXyz xyz = pcsConverter.Convert<CieLab, CieXyz>(in lab);

                // DemoMaxICC clips negatives as part of IccUtil.cpp : icLabToXYZ > icICubeth
                xyz = new CieXyz(Vector3.Clamp(xyz.ToVector3(), Vector3.Zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)));
                return xyz.ToScaledVector4();
            }

            // Convert from XYZ to Lab.
            case IccColorSpaceType.CieXyz when targetParams.PcsType is IccColorSpaceType.CieLab:
            {
                CieXyz xyz = CieXyz.FromScaledVector4(sourcePcs);
                CieLab lab = pcsConverter.Convert<CieXyz, CieLab>(in xyz);
                Vector4 targetPcs = lab.ToScaledVector4();
                return targetParams.Is16BitLutEntry ? LabToLabV2(targetPcs) : targetPcs;
            }

            // Convert from XYZ to XYZ.
            case IccColorSpaceType.CieXyz when targetParams.PcsType is IccColorSpaceType.CieXyz:
            {
                CieXyz xyz = CieXyz.FromScaledVector4(sourcePcs);
                CieXyz targetXyz = pcsConverter.Convert<CieXyz, CieXyz>(in xyz);
                return targetXyz.ToScaledVector4();
            }

            // Convert from Lab to Lab.
            case IccColorSpaceType.CieLab when targetParams.PcsType is IccColorSpaceType.CieLab:
            {
                // if both source and target LUT use same v2 LAB encoding, no need to correct them
                if (sourceParams.Is16BitLutEntry && targetParams.Is16BitLutEntry)
                {
                    CieLab sourceLab = CieLab.FromScaledVector4(sourcePcs);
                    CieLab targetLab = pcsConverter.Convert<CieLab, CieLab>(in sourceLab);
                    return targetLab.ToScaledVector4();
                }
                else
                {
                    sourcePcs = sourceParams.Is16BitLutEntry ? LabV2ToLab(sourcePcs) : sourcePcs;
                    CieLab sourceLab = CieLab.FromScaledVector4(sourcePcs);
                    CieLab targetLab = pcsConverter.Convert<CieLab, CieLab>(in sourceLab);
                    Vector4 targetPcs = targetLab.ToScaledVector4();
                    return targetParams.Is16BitLutEntry ? LabToLabV2(targetPcs) : targetPcs;
                }
            }

            default:
                throw new ArgumentOutOfRangeException($"Source PCS {sourceParams.PcsType} to target PCS {targetParams.PcsType} is not supported");
        }
    }

    /// <summary>
    /// Effectively this is <see cref="GetTargetPcsWithoutAdjustment"/> with an extra step in the middle.
    /// It adjusts PCS by compensating for the black point used for perceptual intent in v2 profiles.
    /// The adjustment needs to be performed in XYZ space, potentially an overhead of 2 more conversions.
    /// Not required if both spaces need adjustment, since they both have the same understanding of the PCS.
    /// Not compatible with PCS adjustment for absolute intent.
    /// </summary>
    /// <param name="sourcePcs">The source PCS values.</param>
    /// <param name="sourceParams">The source profile parameters.</param>
    /// <param name="targetParams">The target profile parameters.</param>
    /// <param name="pcsConverter">The converter to use for the PCS adjustments.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source or target PCS is not supported.</exception>
    private static Vector4 GetTargetPcsWithPerceptualV2Adjustment(
        Vector4 sourcePcs,
        ConversionParams sourceParams,
        ConversionParams targetParams,
        ColorProfileConverter pcsConverter)
    {
        // all conversions are funneled through XYZ in case PCS adjustments need to be made
        CieXyz xyz;

        switch (sourceParams.PcsType)
        {
            // 16-bit Lab encodings changed from v2 to v4, but 16-bit LUTs always use the legacy encoding regardless of version
            // so convert Lab to modern v4 encoding when returned from a 16-bit LUT
            case IccColorSpaceType.CieLab:
                sourcePcs = sourceParams.Is16BitLutEntry ? LabV2ToLab(sourcePcs) : sourcePcs;
                CieLab lab = CieLab.FromScaledVector4(sourcePcs);
                xyz = pcsConverter.Convert<CieLab, CieXyz>(in lab);

                // DemoMaxICC clips negatives as part of IccUtil.cpp : icLabToXYZ > icICubeth
                xyz = new CieXyz(Vector3.Max(xyz.ToVector3(), Vector3.Zero));
                break;
            case IccColorSpaceType.CieXyz:
                xyz = CieXyz.FromScaledVector4(sourcePcs);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Source PCS {sourceParams.PcsType} is not supported");
        }

        // when converting from device to PCS with v2 perceptual intent
        // the black point needs to be adjusted to v4 after converting the PCS values
        if (sourceParams.AdjustPcsForPerceptual)
        {
            xyz = new CieXyz(AdjustPcsFromV2BlackPoint(xyz.ToVector3()));
        }

        // when converting from PCS to device with v2 perceptual intent
        // the black point needs to be adjusted to v2 before converting the PCS values
        if (targetParams.AdjustPcsForPerceptual)
        {
            xyz = new CieXyz(AdjustPcsToV2BlackPoint(xyz.ToVector3()));
        }

        Vector4 targetPcs;
        switch (targetParams.PcsType)
        {
            // 16-bit Lab encodings changed from v2 to v4, but 16-bit LUTs always use the legacy encoding regardless of version
            // so convert Lab back to legacy encoding before using in a 16-bit LUT
            case IccColorSpaceType.CieLab:
                CieLab lab = pcsConverter.Convert<CieXyz, CieLab>(in xyz);
                targetPcs = lab.ToScaledVector4();
                return targetParams.Is16BitLutEntry ? LabToLabV2(targetPcs) : targetPcs;
            case IccColorSpaceType.CieXyz:
                return xyz.ToScaledVector4();
            default:
                throw new ArgumentOutOfRangeException($"Target PCS {targetParams.PcsType} is not supported");
        }
    }

    // as per DemoIccMAX icPerceptual values in IccCmm.h
    // refBlack = 0.00336F, 0.0034731F, 0.00287F
    // refWhite = 0.9642F, 1.0000F, 0.8249F
    // scale = 1 - (refBlack / refWhite)
    // offset = refBlack
    private static Vector3 AdjustPcsFromV2BlackPoint(Vector3 xyz)
        => (xyz * new Vector3(0.9965153F, 0.9965269F, 0.9965208F)) + new Vector3(0.00336F, 0.0034731F, 0.00287F);

    // as per DemoIccMAX icPerceptual values in IccCmm.h
    // refBlack = 0.00336F, 0.0034731F, 0.00287F
    // refWhite = 0.9642F, 1.0000F, 0.8249F
    // scale = 1 / (1 - (refBlack / refWhite))
    // offset = -refBlack * scale
    private static Vector3 AdjustPcsToV2BlackPoint(Vector3 xyz)
        => (xyz * new Vector3(1.0034969F, 1.0034852F, 1.0034913F)) - new Vector3(0.0033717495F, 0.0034852044F, 0.0028800198F);

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

    private class ConversionParams
    {
        private readonly IccProfile profile;

        internal ConversionParams(IccProfile profile, bool toPcs)
        {
            this.profile = profile;
            this.Converter = toPcs ? new IccDataToPcsConverter(profile) : new IccPcsToDataConverter(profile);
        }

        internal IccConverterBase Converter { get; }

        internal IccProfileHeader Header => this.profile.Header;

        internal IccRenderingIntent Intent => this.Header.RenderingIntent;

        internal IccColorSpaceType PcsType => this.Header.ProfileConnectionSpace;

        internal IccVersion Version => this.Header.Version;

        internal bool AdjustPcsForPerceptual => this.Intent == IccRenderingIntent.Perceptual && this.Version.Major == 2;

        internal bool Is16BitLutEntry => this.Converter.Is16BitLutEntry;
    }
}

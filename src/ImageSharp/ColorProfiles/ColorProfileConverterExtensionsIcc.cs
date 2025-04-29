// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles;

internal static class ColorProfileConverterExtensionsIcc
{
    private static readonly float[] PcsV2FromBlackPointScale =
        [0.9965153F, 0.9965269F, 0.9965208F, 1F,
         0.9965153F, 0.9965269F, 0.9965208F, 1F,
         0.9965153F, 0.9965269F, 0.9965208F, 1F,
         0.9965153F, 0.9965269F, 0.9965208F, 1F];

    private static readonly float[] PcsV2FromBlackPointAdd =
        [0.00336F, 0.0034731F, 0.00287F, 0F,
         0.00336F, 0.0034731F, 0.00287F, 0F,
         0.00336F, 0.0034731F, 0.00287F, 0F,
         0.00336F, 0.0034731F, 0.00287F, 0F];

    private static readonly float[] PcsV2ToBlackPointScale =
        [1.0034969F, 1.0034852F, 1.0034913F, 1F,
         1.0034969F, 1.0034852F, 1.0034913F, 1F,
         1.0034969F, 1.0034852F, 1.0034913F, 1F,
         1.0034969F, 1.0034852F, 1.0034913F, 1F];

    private static readonly float[] PcsV2ToBlackPointAdd =
        [0.0033717495F, 0.0034852044F, 0.0028800198F, 0F,
         0.0033717495F, 0.0034852044F, 0.0028800198F, 0F,
         0.0033717495F, 0.0034852044F, 0.0028800198F, 0F,
         0.0033717495F, 0.0034852044F, 0.0028800198F, 0F];

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

        // Normalize the source, then convert to the PCS space.
        Vector4 sourcePcs = sourceParams.Converter.Calculate(source.ToScaledVector4());

        // If both profiles need PCS adjustment, they both share the same unadjusted PCS space
        // cancelling out the need to make the adjustment
        // except if using TRC transforms, which always requires perceptual handling
        // TODO: this does not include adjustment for absolute intent, which would double existing complexity, suggest throwing exception and addressing in future update
        bool anyProfileNeedsPerceptualAdjustment = sourceParams.HasNoPerceptualHandling || targetParams.HasNoPerceptualHandling;
        bool oneProfileHasV2PerceptualAdjustment = sourceParams.HasV2PerceptualHandling ^ targetParams.HasV2PerceptualHandling;

        Vector4 targetPcs = anyProfileNeedsPerceptualAdjustment || oneProfileHasV2PerceptualAdjustment
            ? GetTargetPcsWithPerceptualAdjustment(sourcePcs, sourceParams, targetParams, pcsConverter)
            : GetTargetPcsWithoutAdjustment(sourcePcs, sourceParams, targetParams, pcsConverter);

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

        ConversionParams sourceParams = new(converter.Options.SourceIccProfile, toPcs: true);
        ConversionParams targetParams = new(converter.Options.TargetIccProfile, toPcs: false);

        ColorProfileConverter pcsConverter = new(new ColorConversionOptions
        {
            MemoryAllocator = converter.Options.MemoryAllocator,
            SourceWhitePoint = new CieXyz(converter.Options.SourceIccProfile.Header.PcsIlluminant),
            TargetWhitePoint = new CieXyz(converter.Options.TargetIccProfile.Header.PcsIlluminant),
        });

        using IMemoryOwner<Vector4> pcsBuffer = converter.Options.MemoryAllocator.Allocate<Vector4>(source.Length);
        Span<Vector4> pcs = pcsBuffer.GetSpan();

        // Normalize the source, then convert to the PCS space.
        TFrom.ToScaledVector4(source, pcs);
        sourceParams.Converter.Calculate(pcs, pcs);

        // If both profiles need PCS adjustment, they both share the same unadjusted PCS space
        // cancelling out the need to make the adjustment
        // except if using TRC transforms, which always requires perceptual handling
        // TODO: this does not include adjustment for absolute intent, which would double existing complexity, suggest throwing exception and addressing in future update
        bool anyProfileNeedsPerceptualAdjustment = sourceParams.HasNoPerceptualHandling || targetParams.HasNoPerceptualHandling;
        bool oneProfileHasV2PerceptualAdjustment = sourceParams.HasV2PerceptualHandling ^ targetParams.HasV2PerceptualHandling;

        if (anyProfileNeedsPerceptualAdjustment || oneProfileHasV2PerceptualAdjustment)
        {
            GetTargetPcsWithPerceptualAdjustment(pcs, sourceParams, targetParams, pcsConverter);
        }
        else
        {
            GetTargetPcsWithoutAdjustment(pcs, sourceParams, targetParams, pcsConverter);
        }

        // Convert to the target space.
        targetParams.Converter.Calculate(pcs, pcs);
        TTo.FromScaledVector4(pcs, destination);
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

    private static void GetTargetPcsWithoutAdjustment(
        Span<Vector4> pcs,
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
                if (sourceParams.Is16BitLutEntry)
                {
                    LabV2ToLab(pcs, pcs);
                }

                using IMemoryOwner<CieLab> pcsFromBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieLab>(pcs.Length);
                Span<CieLab> pcsFrom = pcsFromBuffer.GetSpan();

                using IMemoryOwner<CieXyz> pcsToBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieXyz>(pcs.Length);
                Span<CieXyz> pcsTo = pcsToBuffer.GetSpan();

                CieLab.FromScaledVector4(pcs, pcsFrom);
                pcsConverter.Convert<CieLab, CieXyz>(pcsFrom, pcsTo);

                CieXyz.ToScaledVector4(pcsTo, pcs);
                break;
            }

            // Convert from XYZ to Lab.
            case IccColorSpaceType.CieXyz when targetParams.PcsType is IccColorSpaceType.CieLab:
            {
                using IMemoryOwner<CieXyz> pcsFromBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieXyz>(pcs.Length);
                Span<CieXyz> pcsFrom = pcsFromBuffer.GetSpan();

                using IMemoryOwner<CieLab> pcsToBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieLab>(pcs.Length);
                Span<CieLab> pcsTo = pcsToBuffer.GetSpan();

                CieXyz.FromScaledVector4(pcs, pcsFrom);
                pcsConverter.Convert<CieXyz, CieLab>(pcsFrom, pcsTo);

                CieLab.ToScaledVector4(pcsTo, pcs);

                if (targetParams.Is16BitLutEntry)
                {
                    LabToLabV2(pcs, pcs);
                }

                break;
            }

            // Convert from XYZ to XYZ.
            case IccColorSpaceType.CieXyz when targetParams.PcsType is IccColorSpaceType.CieXyz:
            {
                using IMemoryOwner<CieXyz> pcsFromToBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieXyz>(pcs.Length);
                Span<CieXyz> pcsFromTo = pcsFromToBuffer.GetSpan();

                CieXyz.FromScaledVector4(pcs, pcsFromTo);
                pcsConverter.Convert<CieXyz, CieXyz>(pcsFromTo, pcsFromTo);

                CieXyz.ToScaledVector4(pcsFromTo, pcs);
                break;
            }

            // Convert from Lab to Lab.
            case IccColorSpaceType.CieLab when targetParams.PcsType is IccColorSpaceType.CieLab:
            {
                using IMemoryOwner<CieLab> pcsFromToBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieLab>(pcs.Length);
                Span<CieLab> pcsFromTo = pcsFromToBuffer.GetSpan();

                // if both source and target LUT use same v2 LAB encoding, no need to correct them
                if (sourceParams.Is16BitLutEntry && targetParams.Is16BitLutEntry)
                {
                    CieLab.FromScaledVector4(pcs, pcsFromTo);
                    pcsConverter.Convert<CieLab, CieLab>(pcsFromTo, pcsFromTo);
                    CieLab.ToScaledVector4(pcsFromTo, pcs);
                }
                else
                {
                    if (sourceParams.Is16BitLutEntry)
                    {
                        LabV2ToLab(pcs, pcs);
                    }

                    CieLab.FromScaledVector4(pcs, pcsFromTo);
                    pcsConverter.Convert<CieLab, CieLab>(pcsFromTo, pcsFromTo);
                    CieLab.ToScaledVector4(pcsFromTo, pcs);

                    if (targetParams.Is16BitLutEntry)
                    {
                        LabToLabV2(pcs, pcs);
                    }
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException($"Source PCS {sourceParams.PcsType} to target PCS {targetParams.PcsType} is not supported");
        }
    }

    /// <summary>
    /// Effectively this is <see cref="GetTargetPcsWithoutAdjustment(Vector4, ConversionParams, ConversionParams, ColorProfileConverter)"/> with an extra step in the middle.
    /// It adjusts PCS by compensating for the black point used for perceptual intent in v2 profiles.
    /// The adjustment needs to be performed in XYZ space, potentially an overhead of 2 more conversions.
    /// Not required if both spaces need V2 correction, since they both have the same understanding of the PCS.
    /// Not compatible with PCS adjustment for absolute intent.
    /// </summary>
    /// <param name="sourcePcs">The source PCS values.</param>
    /// <param name="sourceParams">The source profile parameters.</param>
    /// <param name="targetParams">The target profile parameters.</param>
    /// <param name="pcsConverter">The converter to use for the PCS adjustments.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source or target PCS is not supported.</exception>
    private static Vector4 GetTargetPcsWithPerceptualAdjustment(
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
                break;
            case IccColorSpaceType.CieXyz:
                xyz = CieXyz.FromScaledVector4(sourcePcs);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Source PCS {sourceParams.PcsType} is not supported");
        }

        bool oneProfileHasV2PerceptualAdjustment = sourceParams.HasV2PerceptualHandling ^ targetParams.HasV2PerceptualHandling;

        // when converting from device to PCS with v2 perceptual intent
        // the black point needs to be adjusted to v4 after converting the PCS values
        if (sourceParams.HasNoPerceptualHandling ||
            (oneProfileHasV2PerceptualAdjustment && sourceParams.HasV2PerceptualHandling))
        {
            Vector3 vector = xyz.ToVector3();

            // when using LAB PCS, negative values are clipped before PCS adjustment (in DemoIccMAX)
            if (sourceParams.PcsType == IccColorSpaceType.CieLab)
            {
                vector = Vector3.Max(vector, Vector3.Zero);
            }

            xyz = new CieXyz(AdjustPcsFromV2BlackPoint(vector));
        }

        // when converting from PCS to device with v2 perceptual intent
        // the black point needs to be adjusted to v2 before converting the PCS values
        if (targetParams.HasNoPerceptualHandling ||
            (oneProfileHasV2PerceptualAdjustment && targetParams.HasV2PerceptualHandling))
        {
            Vector3 vector = AdjustPcsToV2BlackPoint(xyz.ToVector3());

            // when using XYZ PCS, negative values are clipped after PCS adjustment (in DemoIccMAX)
            if (targetParams.PcsType == IccColorSpaceType.CieXyz)
            {
                vector = Vector3.Max(vector, Vector3.Zero);
            }

            xyz = new CieXyz(vector);
        }

        switch (targetParams.PcsType)
        {
            // 16-bit Lab encodings changed from v2 to v4, but 16-bit LUTs always use the legacy encoding regardless of version
            // so convert Lab back to legacy encoding before using in a 16-bit LUT
            case IccColorSpaceType.CieLab:
                CieLab lab = pcsConverter.Convert<CieXyz, CieLab>(in xyz);
                Vector4 targetPcs = lab.ToScaledVector4();
                return targetParams.Is16BitLutEntry ? LabToLabV2(targetPcs) : targetPcs;
            case IccColorSpaceType.CieXyz:
                return xyz.ToScaledVector4();
            default:
                throw new ArgumentOutOfRangeException($"Target PCS {targetParams.PcsType} is not supported");
        }
    }

    /// <summary>
    /// Effectively this is <see cref="GetTargetPcsWithoutAdjustment(Span{Vector4}, ConversionParams, ConversionParams, ColorProfileConverter)"/> with an extra step in the middle.
    /// It adjusts PCS by compensating for the black point used for perceptual intent in v2 profiles.
    /// The adjustment needs to be performed in XYZ space, potentially an overhead of 2 more conversions.
    /// Not required if both spaces need V2 correction, since they both have the same understanding of the PCS.
    /// Not compatible with PCS adjustment for absolute intent.
    /// </summary>
    /// <param name="pcs">The PCS values from the source.</param>
    /// <param name="sourceParams">The source profile parameters.</param>
    /// <param name="targetParams">The target profile parameters.</param>
    /// <param name="pcsConverter">The converter to use for the PCS adjustments.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source or target PCS is not supported.</exception>
    private static void GetTargetPcsWithPerceptualAdjustment(
        Span<Vector4> pcs,
        ConversionParams sourceParams,
        ConversionParams targetParams,
        ColorProfileConverter pcsConverter)
    {
        // All conversions are funneled through XYZ in case PCS adjustments need to be made
        using IMemoryOwner<CieXyz> xyzBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieXyz>(pcs.Length);
        Span<CieXyz> xyz = xyzBuffer.GetSpan();

        switch (sourceParams.PcsType)
        {
            // 16-bit Lab encodings changed from v2 to v4, but 16-bit LUTs always use the legacy encoding regardless of version
            // so convert Lab to modern v4 encoding when returned from a 16-bit LUT
            case IccColorSpaceType.CieLab:
            {
                if (sourceParams.Is16BitLutEntry)
                {
                    LabV2ToLab(pcs, pcs);
                }

                using IMemoryOwner<CieLab> pcsFromBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieLab>(pcs.Length);
                Span<CieLab> pcsFrom = pcsFromBuffer.GetSpan();
                CieLab.FromScaledVector4(pcs, pcsFrom);
                pcsConverter.Convert<CieLab, CieXyz>(pcsFrom, xyz);
                break;
            }

            case IccColorSpaceType.CieXyz:
                CieXyz.FromScaledVector4(pcs, xyz);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Source PCS {sourceParams.PcsType} is not supported");
        }

        bool oneProfileHasV2PerceptualAdjustment = sourceParams.HasV2PerceptualHandling ^ targetParams.HasV2PerceptualHandling;

        using IMemoryOwner<Vector4> vectorBuffer = pcsConverter.Options.MemoryAllocator.Allocate<Vector4>(pcs.Length);
        Span<Vector4> vector = vectorBuffer.GetSpan();

        // When converting from device to PCS with v2 perceptual intent
        // the black point needs to be adjusted to v4 after converting the PCS values
        if (sourceParams.HasNoPerceptualHandling ||
            (oneProfileHasV2PerceptualAdjustment && sourceParams.HasV2PerceptualHandling))
        {
            CieXyz.ToVector4(xyz, vector);

            // When using LAB PCS, negative values are clipped before PCS adjustment (in DemoIccMAX)
            if (sourceParams.PcsType == IccColorSpaceType.CieLab)
            {
                ClipNegative(vector);
            }

            AdjustPcsFromV2BlackPoint(vector, vector);
            CieXyz.FromVector4(vector, xyz);
        }

        // When converting from PCS to device with v2 perceptual intent
        // the black point needs to be adjusted to v2 before converting the PCS values
        if (targetParams.HasNoPerceptualHandling ||
            (oneProfileHasV2PerceptualAdjustment && targetParams.HasV2PerceptualHandling))
        {
            CieXyz.ToVector4(xyz, vector);
            AdjustPcsToV2BlackPoint(vector, vector);

            // When using XYZ PCS, negative values are clipped after PCS adjustment (in DemoIccMAX)
            if (targetParams.PcsType == IccColorSpaceType.CieXyz)
            {
                ClipNegative(vector);
            }

            CieXyz.FromVector4(vector, xyz);
        }

        switch (targetParams.PcsType)
        {
            // 16-bit Lab encodings changed from v2 to v4, but 16-bit LUTs always use the legacy encoding regardless of version
            // so convert Lab back to legacy encoding before using in a 16-bit LUT
            case IccColorSpaceType.CieLab:
            {
                using IMemoryOwner<CieLab> pcsToBuffer = pcsConverter.Options.MemoryAllocator.Allocate<CieLab>(pcs.Length);
                Span<CieLab> pcsTo = pcsToBuffer.GetSpan();
                pcsConverter.Convert<CieXyz, CieLab>(xyz, pcsTo);

                CieLab.ToScaledVector4(pcsTo, pcs);

                if (targetParams.Is16BitLutEntry)
                {
                    LabToLabV2(pcs, pcs);
                }

                break;
            }

            case IccColorSpaceType.CieXyz:
                CieXyz.ToScaledVector4(xyz, pcs);
                break;
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

    private static void AdjustPcsFromV2BlackPoint(Span<Vector4> source, Span<Vector4> destination)
    {
        if (Vector.IsHardwareAccelerated && Vector<float>.IsSupported &&
            Vector<float>.Count <= Vector512<float>.Count &&
            source.Length * 4 >= Vector<float>.Count)
        {
            // TODO: Check our constants. They may require scaling.
            Vector<float> vScale = new(PcsV2FromBlackPointScale.AsSpan()[..Vector<float>.Count]);
            Vector<float> vAdd = new(PcsV2FromBlackPointAdd.AsSpan()[..Vector<float>.Count]);

            // SIMD loop
            int i = 0;
            int simdBatchSize = Vector<float>.Count / 4; // Number of Vector4 elements per SIMD batch
            for (; i <= source.Length - simdBatchSize; i += simdBatchSize)
            {
                // Load the vector from source span
                Vector<float> v = Unsafe.ReadUnaligned<Vector<float>>(ref Unsafe.As<Vector4, byte>(ref source[i]));

                // Scale and add the vector
                v *= vScale;
                v += vAdd;

                // Write the vector to the destination span
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector4, byte>(ref destination[i]), v);
            }

            // Scalar fallback for remaining elements
            for (; i < source.Length; i++)
            {
                Vector4 s = source[i];
                s *= new Vector4(0.9965153F, 0.9965269F, 0.9965208F, 1F);
                s += new Vector4(0.00336F, 0.0034731F, 0.00287F, 0F);
                destination[i] = s;
            }
        }
        else
        {
            // Scalar fallback if SIMD is not supported
            for (int i = 0; i < source.Length; i++)
            {
                Vector4 s = source[i];
                s *= new Vector4(0.9965153F, 0.9965269F, 0.9965208F, 1F);
                s += new Vector4(0.00336F, 0.0034731F, 0.00287F, 0F);
                destination[i] = s;
            }
        }
    }

    private static void AdjustPcsToV2BlackPoint(Span<Vector4> source, Span<Vector4> destination)
    {
        if (Vector.IsHardwareAccelerated && Vector<float>.IsSupported &&
            Vector<float>.Count <= Vector512<float>.Count &&
            source.Length * 4 >= Vector<float>.Count)
        {
            // TODO: Check our constants. They may require scaling.
            Vector<float> vScale = new(PcsV2ToBlackPointScale.AsSpan()[..Vector<float>.Count]);
            Vector<float> vAdd = new(PcsV2ToBlackPointAdd.AsSpan()[..Vector<float>.Count]);

            // SIMD loop
            int i = 0;
            int simdBatchSize = Vector<float>.Count / 4; // Number of Vector4 elements per SIMD batch
            for (; i <= source.Length - simdBatchSize; i += simdBatchSize)
            {
                // Load the vector from source span
                Vector<float> v = Unsafe.ReadUnaligned<Vector<float>>(ref Unsafe.As<Vector4, byte>(ref source[i]));

                // Scale and add the vector
                v *= vScale;
                v += vAdd;

                // Write the vector to the destination span
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector4, byte>(ref destination[i]), v);
            }

            // Scalar fallback for remaining elements
            for (; i < source.Length; i++)
            {
                Vector4 s = source[i];
                s *= new Vector4(1.0034969F, 1.0034852F, 1.0034913F, 1F);
                s += new Vector4(0.0033717495F, 0.0034852044F, 0.0028800198F, 0F);
                destination[i] = s;
            }
        }
        else
        {
            // Scalar fallback if SIMD is not supported
            for (int i = 0; i < source.Length; i++)
            {
                Vector4 s = source[i];
                s *= new Vector4(1.0034969F, 1.0034852F, 1.0034913F, 1F);
                s += new Vector4(0.0033717495F, 0.0034852044F, 0.0028800198F, 0F);
                destination[i] = s;
            }
        }
    }

    private static void ClipNegative(Span<Vector4> source)
    {
        if (Vector.IsHardwareAccelerated && Vector<float>.IsSupported && Vector<float>.Count >= source.Length * 4)
        {
            // SIMD loop
            int i = 0;
            int simdBatchSize = Vector<float>.Count / 4; // Number of Vector4 elements per SIMD batch
            for (; i <= source.Length - simdBatchSize; i += simdBatchSize)
            {
                // Load the vector from source span
                Vector<float> v = Unsafe.ReadUnaligned<Vector<float>>(ref Unsafe.As<Vector4, byte>(ref source[i]));

                v = Vector.Max(v, Vector<float>.Zero);

                // Write the vector to the destination span
                Unsafe.WriteUnaligned(ref Unsafe.As<Vector4, byte>(ref source[i]), v);
            }

            // Scalar fallback for remaining elements
            for (; i < source.Length; i++)
            {
                ref Vector4 s = ref source[i];
                s = Vector4.Max(s, Vector4.Zero);
            }
        }
        else
        {
            // Scalar fallback if SIMD is not supported
            for (int i = 0; i < source.Length; i++)
            {
                ref Vector4 s = ref source[i];
                s = Vector4.Max(s, Vector4.Zero);
            }
        }
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
        if (Vector.IsHardwareAccelerated && Vector<float>.IsSupported)
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

        internal bool HasV2PerceptualHandling => this.Intent == IccRenderingIntent.Perceptual && this.Version.Major == 2;

        internal bool HasNoPerceptualHandling => this.Intent == IccRenderingIntent.Perceptual && this.Converter.IsTrc;

        internal bool Is16BitLutEntry => this.Converter.Is16BitLutEntry;
    }
}

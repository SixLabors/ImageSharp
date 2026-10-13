// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// Applies selected AV1 grain blocks to restored luma and chroma samples.
/// </summary>
/// <remarks>
/// SIMD lanes follow consecutive samples within one plane row. Grain values and native samples widen to signed 32-bit
/// lanes before the scaling-table lookup and fixed-point addition. Then the result is clipped and narrowed only once. AVX2
/// uses indexed gathers for the 256-entry scaling table. The portable 128-bit path handles high-bit-depth interpolation
/// with scalar table reads. All remaining columns use the same scalar equation.
/// </remarks>
internal static class Av1FilmGrainNoise
{
    /// <summary>
    /// The lower restricted-range luma value at eight-bit precision.
    /// </summary>
    private const int RestrictedLumaMinimum = 16;

    /// <summary>
    /// The upper restricted-range luma value at eight-bit precision.
    /// </summary>
    private const int RestrictedLumaMaximum = 235;

    /// <summary>
    /// The lower restricted-range chroma value at eight-bit precision.
    /// </summary>
    private const int RestrictedChromaMinimum = 16;

    /// <summary>
    /// The upper restricted-range chroma value at eight-bit precision.
    /// </summary>
    private const int RestrictedChromaMaximum = 240;

    /// <summary>
    /// Adds a selected grain rectangle to its corresponding restored samples.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="parameters">The complete frame grain parameters.</param>
    /// <param name="scalingY">The luma scaling lookup table.</param>
    /// <param name="scalingCb">The first chroma scaling lookup table.</param>
    /// <param name="scalingCr">The second chroma scaling lookup table.</param>
    /// <param name="luma">The restored luma rectangle.</param>
    /// <param name="cb">The restored first chroma rectangle.</param>
    /// <param name="cr">The restored second chroma rectangle.</param>
    /// <param name="lumaStride">The luma row stride in samples.</param>
    /// <param name="chromaStride">The chroma row stride in samples.</param>
    /// <param name="lumaGrain">The selected luma grain rectangle.</param>
    /// <param name="cbGrain">The selected first chroma grain rectangle.</param>
    /// <param name="crGrain">The selected second chroma grain rectangle.</param>
    /// <param name="lumaGrainStride">The luma grain row stride.</param>
    /// <param name="chromaGrainStride">The chroma grain row stride.</param>
    /// <param name="halfLumaHeight">Half the luma rectangle height.</param>
    /// <param name="halfLumaWidth">Half the luma rectangle width.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="isMonochrome">Whether the frame has no chroma planes.</param>
    /// <param name="isIdentityMatrix">Whether every plane uses the luma restricted range.</param>
    public static void Apply<TSample>(
        ObuFilmGrainParameters parameters,
        ReadOnlySpan<int> scalingY,
        ReadOnlySpan<int> scalingCb,
        ReadOnlySpan<int> scalingCr,
        Span<TSample> luma,
        Span<TSample> cb,
        Span<TSample> cr,
        int lumaStride,
        int chromaStride,
        ReadOnlySpan<int> lumaGrain,
        ReadOnlySpan<int> cbGrain,
        ReadOnlySpan<int> crGrain,
        int lumaGrainStride,
        int chromaGrainStride,
        int halfLumaHeight,
        int halfLumaWidth,
        int bitDepth,
        int subsamplingX,
        int subsamplingY,
        bool isMonochrome,
        bool isIdentityMatrix)
        where TSample : unmanaged
    {
        int scalingShift = (int)parameters.GrainScalingMinus8 + 8;
        int roundingOffset = 1 << (scalingShift - 1);
        int depthScale = 1 << (bitDepth - 8);
        int sampleMaximum = (256 * depthScale) - 1;
        int lumaMinimum = 0;
        int lumaMaximum = sampleMaximum;
        int chromaMinimum = 0;
        int chromaMaximum = sampleMaximum;
        if (parameters.ClipToRestrictedRange)
        {
            // The restricted-range limits are eight-bit values. A multiply by the depth scale gives the exact limits at higher depths.
            lumaMinimum = RestrictedLumaMinimum * depthScale;
            lumaMaximum = RestrictedLumaMaximum * depthScale;
            chromaMinimum = (isIdentityMatrix ? RestrictedLumaMinimum : RestrictedChromaMinimum) * depthScale;
            chromaMaximum = (isIdentityMatrix ? RestrictedLumaMaximum : RestrictedChromaMaximum) * depthScale;
        }

        if (!isMonochrome)
        {
            int cbMultiplier = (int)parameters.CbMult - 128;
            int cbLumaMultiplier = (int)parameters.CbLumaMult - 128;
            int cbOffset = ((int)parameters.CbOffset * depthScale) - (256 * depthScale);
            int crMultiplier = (int)parameters.CrMult - 128;
            int crLumaMultiplier = (int)parameters.CrLumaMult - 128;
            int crOffset = ((int)parameters.CrOffset * depthScale) - (256 * depthScale);
            if (parameters.ChromaScalingFromLuma)
            {
                // A luma multiplier of 64 is 1.0 in Q6. Thus the scaling index is the luma value, and the chroma sample has no effect.
                cbMultiplier = 0;
                cbLumaMultiplier = 64;
                cbOffset = 0;
                crMultiplier = 0;
                crLumaMultiplier = 64;
                crOffset = 0;
            }

            ApplyChroma(
                scalingCb,
                scalingCr,
                luma,
                cb,
                cr,
                lumaStride,
                chromaStride,
                cbGrain,
                crGrain,
                chromaGrainStride,
                halfLumaHeight << (1 - subsamplingY),
                halfLumaWidth << (1 - subsamplingX),
                bitDepth,
                subsamplingX,
                subsamplingY,
                parameters.NumCbPoints != 0 || parameters.ChromaScalingFromLuma,
                parameters.NumCrPoints != 0 || parameters.ChromaScalingFromLuma,
                cbMultiplier,
                cbLumaMultiplier,
                cbOffset,
                crMultiplier,
                crLumaMultiplier,
                crOffset,
                roundingOffset,
                scalingShift,
                sampleMaximum,
                chromaMinimum,
                chromaMaximum);
        }

        if (parameters.NumYPoints != 0)
        {
            ApplyLuma(
                scalingY,
                luma,
                lumaStride,
                lumaGrain,
                lumaGrainStride,
                halfLumaHeight << 1,
                halfLumaWidth << 1,
                bitDepth,
                roundingOffset,
                scalingShift,
                lumaMinimum,
                lumaMaximum);
        }
    }

    /// <summary>
    /// Selects the widest available luma traversal.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scaling">The luma scaling lookup table.</param>
    /// <param name="samples">The restored luma samples.</param>
    /// <param name="sampleStride">The sample row stride.</param>
    /// <param name="grain">The selected luma grain rectangle.</param>
    /// <param name="grainStride">The grain row stride.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    private static void ApplyLuma<TSample>(
        ReadOnlySpan<int> scaling,
        Span<TSample> samples,
        int sampleStride,
        ReadOnlySpan<int> grain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum)
        where TSample : unmanaged
    {
        // Scaling is an indexed lookup, so the dispatch follows gather support and not the preferred vector width. The 256-bit
        // path uses the AVX2 gather where it exists. The 128-bit path has no gather, so `CanVectorizeWithoutGather` limits it.
        if (Vector256.IsHardwareAccelerated)
        {
            ApplyLuma(
                scaling,
                samples,
                sampleStride,
                grain,
                grainStride,
                height,
                width,
                bitDepth,
                roundingOffset,
                scalingShift,
                minimum,
                maximum,
                Vector256<int>.Zero);

            return;
        }

        if (CanVectorizeWithoutGather(bitDepth))
        {
            ApplyLuma(
                scaling,
                samples,
                sampleStride,
                grain,
                grainStride,
                height,
                width,
                bitDepth,
                roundingOffset,
                scalingShift,
                minimum,
                maximum,
                Vector128<int>.Zero);

            return;
        }

        ApplyLumaScalar(scaling, samples, sampleStride, grain, grainStride, height, width, bitDepth, roundingOffset, scalingShift, minimum, maximum);
    }

    /// <summary>
    /// Applies luma grain eight samples at a time.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scaling">The luma scaling lookup table.</param>
    /// <param name="samples">The restored luma samples.</param>
    /// <param name="sampleStride">The sample row stride.</param>
    /// <param name="grain">The selected luma grain rectangle.</param>
    /// <param name="grainStride">The grain row stride.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void ApplyLuma<TSample>(
        ReadOnlySpan<int> scaling,
        Span<TSample> samples,
        int sampleStride,
        ReadOnlySpan<int> grain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum,
        Vector256<int> vector)
        where TSample : unmanaged
    {
        ref TSample sampleBase = ref MemoryMarshal.GetReference(samples);
        ref int grainBase = ref MemoryMarshal.GetReference(grain);
        for (int row = 0; row < height; row++)
        {
            int sampleRowOffset = row * sampleStride;
            int grainRowOffset = row * grainStride;
            int column = 0;
            int vectorEnd = (int)(Numerics.Vector256Count<int>(width) * (nuint)Vector256<int>.Count);

            // Samples, grain, and scale indices share the same lane coordinate. No permutation is required between
            // the lookup, fixed-point multiply, clipping, and native-sample store.
            for (; column < vectorEnd; column += Vector256<int>.Count)
            {
                ref TSample destination = ref Unsafe.Add(ref sampleBase, sampleRowOffset + column);
                Vector256<int> source = Av1FilmGrainSampleOperations<TSample>.Load8(ref destination);
                Vector256<int> grainValues = Vector256.LoadUnsafe(ref grainBase, (nuint)(grainRowOffset + column));
                Vector256<int> result = AddNoise(source, grainValues, scaling, bitDepth, roundingOffset, scalingShift, minimum, maximum);
                Av1FilmGrainSampleOperations<TSample>.Store8(ref destination, result);
            }

            ApplyLumaScalar(
                scaling,
                samples.Slice(sampleRowOffset + column),
                sampleStride,
                grain.Slice(grainRowOffset + column),
                grainStride,
                1,
                width - column,
                bitDepth,
                roundingOffset,
                scalingShift,
                minimum,
                maximum);
        }
    }

    /// <summary>
    /// Applies luma grain four samples at a time.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scaling">The luma scaling lookup table.</param>
    /// <param name="samples">The restored luma samples.</param>
    /// <param name="sampleStride">The sample row stride.</param>
    /// <param name="grain">The selected luma grain rectangle.</param>
    /// <param name="grainStride">The grain row stride.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void ApplyLuma<TSample>(
        ReadOnlySpan<int> scaling,
        Span<TSample> samples,
        int sampleStride,
        ReadOnlySpan<int> grain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum,
        Vector128<int> vector)
        where TSample : unmanaged
    {
        ref TSample sampleBase = ref MemoryMarshal.GetReference(samples);
        ref int grainBase = ref MemoryMarshal.GetReference(grain);
        for (int row = 0; row < height; row++)
        {
            int sampleRowOffset = row * sampleStride;
            int grainRowOffset = row * grainStride;
            int column = 0;
            int vectorEnd = (int)(Numerics.Vector128Count<int>(width) * (nuint)Vector128<int>.Count);

            // Four scalar table reads build the scale vector, including the interpolation for 10-bit and 12-bit coordinates.
            // The rest of the normative grain equation stays lane-wise.
            for (; column < vectorEnd; column += Vector128<int>.Count)
            {
                ref TSample destination = ref Unsafe.Add(ref sampleBase, sampleRowOffset + column);
                Vector128<int> source = Av1FilmGrainSampleOperations<TSample>.Load4(ref destination);
                Vector128<int> grainValues = Vector128.LoadUnsafe(ref grainBase, (nuint)(grainRowOffset + column));
                Vector128<int> result = AddNoise(source, grainValues, scaling, bitDepth, roundingOffset, scalingShift, minimum, maximum);
                Av1FilmGrainSampleOperations<TSample>.Store4(ref destination, result);
            }

            ApplyLumaScalar(
                scaling,
                samples.Slice(sampleRowOffset + column),
                sampleStride,
                grain.Slice(grainRowOffset + column),
                grainStride,
                1,
                width - column,
                bitDepth,
                roundingOffset,
                scalingShift,
                minimum,
                maximum);
        }
    }

    /// <summary>
    /// Applies luma grain to the scalar remainder of a row, or to the whole rectangle when no vector path is available.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scaling">The luma scaling lookup table.</param>
    /// <param name="samples">The restored luma samples.</param>
    /// <param name="sampleStride">The sample row stride.</param>
    /// <param name="grain">The selected luma grain rectangle.</param>
    /// <param name="grainStride">The grain row stride.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="width">The number of samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    private static void ApplyLumaScalar<TSample>(
        ReadOnlySpan<int> scaling,
        Span<TSample> samples,
        int sampleStride,
        ReadOnlySpan<int> grain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum)
        where TSample : unmanaged
    {
        ref TSample sampleBase = ref MemoryMarshal.GetReference(samples);
        ref int grainBase = ref MemoryMarshal.GetReference(grain);
        for (int row = 0; row < height; row++)
        {
            int sampleRowOffset = row * sampleStride;
            int grainRowOffset = row * grainStride;
            for (int column = 0; column < width; column++)
            {
                ref TSample destination = ref Unsafe.Add(ref sampleBase, sampleRowOffset + column);
                int source = Av1FilmGrainSampleOperations<TSample>.Load(ref destination);
                int scale = ScaleLookup(scaling, source, bitDepth);
                int value = source + (((scale * Unsafe.Add(ref grainBase, grainRowOffset + column)) + roundingOffset) >> scalingShift);
                Av1FilmGrainSampleOperations<TSample>.Store(ref destination, Av1Math.Clamp(value, minimum, maximum));
            }
        }
    }

    /// <summary>
    /// Selects the widest available chroma traversal.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scalingCb">The first chroma scaling lookup table.</param>
    /// <param name="scalingCr">The second chroma scaling lookup table.</param>
    /// <param name="luma">The restored luma samples that select the chroma scaling.</param>
    /// <param name="cb">The restored first chroma samples.</param>
    /// <param name="cr">The restored second chroma samples.</param>
    /// <param name="lumaStride">The luma row stride.</param>
    /// <param name="chromaStride">The chroma row stride.</param>
    /// <param name="cbGrain">The selected first chroma grain rectangle.</param>
    /// <param name="crGrain">The selected second chroma grain rectangle.</param>
    /// <param name="grainStride">The chroma grain row stride.</param>
    /// <param name="height">The number of chroma rows.</param>
    /// <param name="width">The number of chroma samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="applyCb">Whether to add grain to the first chroma plane.</param>
    /// <param name="applyCr">Whether to add grain to the second chroma plane.</param>
    /// <param name="cbMultiplier">The signed Q6 weight of the first chroma sample in its scaling index.</param>
    /// <param name="cbLumaMultiplier">The signed Q6 weight of the luma value in the first chroma scaling index.</param>
    /// <param name="cbOffset">The offset added to the first chroma scaling index.</param>
    /// <param name="crMultiplier">The signed Q6 weight of the second chroma sample in its scaling index.</param>
    /// <param name="crLumaMultiplier">The signed Q6 weight of the luma value in the second chroma scaling index.</param>
    /// <param name="crOffset">The offset added to the second chroma scaling index.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="sampleMaximum">The largest sample value, which is also the largest scaling index.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    private static void ApplyChroma<TSample>(
        ReadOnlySpan<int> scalingCb,
        ReadOnlySpan<int> scalingCr,
        Span<TSample> luma,
        Span<TSample> cb,
        Span<TSample> cr,
        int lumaStride,
        int chromaStride,
        ReadOnlySpan<int> cbGrain,
        ReadOnlySpan<int> crGrain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int subsamplingX,
        int subsamplingY,
        bool applyCb,
        bool applyCr,
        int cbMultiplier,
        int cbLumaMultiplier,
        int cbOffset,
        int crMultiplier,
        int crLumaMultiplier,
        int crOffset,
        int roundingOffset,
        int scalingShift,
        int sampleMaximum,
        int minimum,
        int maximum)
        where TSample : unmanaged
    {
        // Chroma uses the same dispatch as luma. AVX2 gathers the scaling values. The portable vector path runs only for high
        // bit depths, where it also interpolates. This dispatch choice does not show which path is faster.
        if (Vector256.IsHardwareAccelerated)
        {
            ApplyChroma(
                scalingCb,
                scalingCr,
                luma,
                cb,
                cr,
                lumaStride,
                chromaStride,
                cbGrain,
                crGrain,
                grainStride,
                height,
                width,
                bitDepth,
                subsamplingX,
                subsamplingY,
                applyCb,
                applyCr,
                cbMultiplier,
                cbLumaMultiplier,
                cbOffset,
                crMultiplier,
                crLumaMultiplier,
                crOffset,
                roundingOffset,
                scalingShift,
                sampleMaximum,
                minimum,
                maximum,
                Vector256<int>.Zero);

            return;
        }

        if (CanVectorizeWithoutGather(bitDepth))
        {
            ApplyChroma(
                scalingCb,
                scalingCr,
                luma,
                cb,
                cr,
                lumaStride,
                chromaStride,
                cbGrain,
                crGrain,
                grainStride,
                height,
                width,
                bitDepth,
                subsamplingX,
                subsamplingY,
                applyCb,
                applyCr,
                cbMultiplier,
                cbLumaMultiplier,
                cbOffset,
                crMultiplier,
                crLumaMultiplier,
                crOffset,
                roundingOffset,
                scalingShift,
                sampleMaximum,
                minimum,
                maximum,
                Vector128<int>.Zero);

            return;
        }

        ApplyChromaScalar(
            scalingCb,
            scalingCr,
            luma,
            cb,
            cr,
            lumaStride,
            chromaStride,
            cbGrain,
            crGrain,
            grainStride,
            height,
            width,
            bitDepth,
            subsamplingX,
            subsamplingY,
            applyCb,
            applyCr,
            cbMultiplier,
            cbLumaMultiplier,
            cbOffset,
            crMultiplier,
            crLumaMultiplier,
            crOffset,
            roundingOffset,
            scalingShift,
            sampleMaximum,
            minimum,
            maximum);
    }

    /// <summary>
    /// Applies chroma grain eight samples at a time.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scalingCb">The first chroma scaling lookup table.</param>
    /// <param name="scalingCr">The second chroma scaling lookup table.</param>
    /// <param name="luma">The restored luma samples that select the chroma scaling.</param>
    /// <param name="cb">The restored first chroma samples.</param>
    /// <param name="cr">The restored second chroma samples.</param>
    /// <param name="lumaStride">The luma row stride.</param>
    /// <param name="chromaStride">The chroma row stride.</param>
    /// <param name="cbGrain">The selected first chroma grain rectangle.</param>
    /// <param name="crGrain">The selected second chroma grain rectangle.</param>
    /// <param name="grainStride">The chroma grain row stride.</param>
    /// <param name="height">The number of chroma rows.</param>
    /// <param name="width">The number of chroma samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="applyCb">Whether to add grain to the first chroma plane.</param>
    /// <param name="applyCr">Whether to add grain to the second chroma plane.</param>
    /// <param name="cbMultiplier">The signed Q6 weight of the first chroma sample in its scaling index.</param>
    /// <param name="cbLumaMultiplier">The signed Q6 weight of the luma value in the first chroma scaling index.</param>
    /// <param name="cbOffset">The offset added to the first chroma scaling index.</param>
    /// <param name="crMultiplier">The signed Q6 weight of the second chroma sample in its scaling index.</param>
    /// <param name="crLumaMultiplier">The signed Q6 weight of the luma value in the second chroma scaling index.</param>
    /// <param name="crOffset">The offset added to the second chroma scaling index.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="sampleMaximum">The largest sample value, which is also the largest scaling index.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void ApplyChroma<TSample>(
        ReadOnlySpan<int> scalingCb,
        ReadOnlySpan<int> scalingCr,
        Span<TSample> luma,
        Span<TSample> cb,
        Span<TSample> cr,
        int lumaStride,
        int chromaStride,
        ReadOnlySpan<int> cbGrain,
        ReadOnlySpan<int> crGrain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int subsamplingX,
        int subsamplingY,
        bool applyCb,
        bool applyCr,
        int cbMultiplier,
        int cbLumaMultiplier,
        int cbOffset,
        int crMultiplier,
        int crLumaMultiplier,
        int crOffset,
        int roundingOffset,
        int scalingShift,
        int sampleMaximum,
        int minimum,
        int maximum,
        Vector256<int> vector)
        where TSample : unmanaged
    {
        ref TSample lumaBase = ref MemoryMarshal.GetReference(luma);
        ref TSample cbBase = ref MemoryMarshal.GetReference(cb);
        ref TSample crBase = ref MemoryMarshal.GetReference(cr);
        ref int cbGrainBase = ref MemoryMarshal.GetReference(cbGrain);
        ref int crGrainBase = ref MemoryMarshal.GetReference(crGrain);
        Vector256<int> zero = Vector256<int>.Zero;
        Vector256<int> maximumIndex = Vector256.Create(sampleMaximum);
        for (int row = 0; row < height; row++)
        {
            ref TSample lumaRow = ref Unsafe.Add(ref lumaBase, (row << subsamplingY) * lumaStride);
            int chromaRowOffset = row * chromaStride;
            int grainRowOffset = row * grainStride;
            int column = 0;
            int vectorEnd = (int)(Numerics.Vector256Count<int>(width) * (nuint)Vector256<int>.Count);

            // Each lane represents one chroma coordinate and its corresponding reconstructed-luma coordinate. The Q6
            // luma/chroma blend is clamped to a legal sample code before it becomes a scaling-table index.
            for (; column < vectorEnd; column += Vector256<int>.Count)
            {
                ref TSample lumaSource = ref Unsafe.Add(ref lumaRow, column << subsamplingX);
                Vector256<int> averageLuma = Av1FilmGrainSampleOperations<TSample>.LoadChromaLuma8(ref lumaSource, subsamplingX);
                if (applyCb)
                {
                    ref TSample destination = ref Unsafe.Add(ref cbBase, chromaRowOffset + column);
                    Vector256<int> source = Av1FilmGrainSampleOperations<TSample>.Load8(ref destination);
                    Vector256<int> scalingIndex = ((averageLuma * cbLumaMultiplier) + (source * cbMultiplier)) >> 6;
                    scalingIndex = Vector256.Min(Vector256.Max(scalingIndex + Vector256.Create(cbOffset), zero), maximumIndex);
                    Vector256<int> grainValues = Vector256.LoadUnsafe(ref cbGrainBase, (nuint)(grainRowOffset + column));
                    Vector256<int> result = AddNoise(
                        source,
                        grainValues,
                        scalingCb,
                        scalingIndex,
                        bitDepth,
                        roundingOffset,
                        scalingShift,
                        minimum,
                        maximum);

                    Av1FilmGrainSampleOperations<TSample>.Store8(ref destination, result);
                }

                if (applyCr)
                {
                    ref TSample destination = ref Unsafe.Add(ref crBase, chromaRowOffset + column);
                    Vector256<int> source = Av1FilmGrainSampleOperations<TSample>.Load8(ref destination);
                    Vector256<int> scalingIndex = ((averageLuma * crLumaMultiplier) + (source * crMultiplier)) >> 6;
                    scalingIndex = Vector256.Min(Vector256.Max(scalingIndex + Vector256.Create(crOffset), zero), maximumIndex);
                    Vector256<int> grainValues = Vector256.LoadUnsafe(ref crGrainBase, (nuint)(grainRowOffset + column));
                    Vector256<int> result = AddNoise(
                        source,
                        grainValues,
                        scalingCr,
                        scalingIndex,
                        bitDepth,
                        roundingOffset,
                        scalingShift,
                        minimum,
                        maximum);

                    Av1FilmGrainSampleOperations<TSample>.Store8(ref destination, result);
                }
            }

            ApplyChromaScalar(
                scalingCb,
                scalingCr,
                luma.Slice(((row << subsamplingY) * lumaStride) + (column << subsamplingX)),
                cb.Slice(chromaRowOffset + column),
                cr.Slice(chromaRowOffset + column),
                lumaStride,
                chromaStride,
                cbGrain.Slice(grainRowOffset + column),
                crGrain.Slice(grainRowOffset + column),
                grainStride,
                1,
                width - column,
                bitDepth,
                subsamplingX,
                subsamplingY,
                applyCb,
                applyCr,
                cbMultiplier,
                cbLumaMultiplier,
                cbOffset,
                crMultiplier,
                crLumaMultiplier,
                crOffset,
                roundingOffset,
                scalingShift,
                sampleMaximum,
                minimum,
                maximum);
        }
    }

    /// <summary>
    /// Applies chroma grain four samples at a time.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scalingCb">The first chroma scaling lookup table.</param>
    /// <param name="scalingCr">The second chroma scaling lookup table.</param>
    /// <param name="luma">The restored luma samples that select the chroma scaling.</param>
    /// <param name="cb">The restored first chroma samples.</param>
    /// <param name="cr">The restored second chroma samples.</param>
    /// <param name="lumaStride">The luma row stride.</param>
    /// <param name="chromaStride">The chroma row stride.</param>
    /// <param name="cbGrain">The selected first chroma grain rectangle.</param>
    /// <param name="crGrain">The selected second chroma grain rectangle.</param>
    /// <param name="grainStride">The chroma grain row stride.</param>
    /// <param name="height">The number of chroma rows.</param>
    /// <param name="width">The number of chroma samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="applyCb">Whether to add grain to the first chroma plane.</param>
    /// <param name="applyCr">Whether to add grain to the second chroma plane.</param>
    /// <param name="cbMultiplier">The signed Q6 weight of the first chroma sample in its scaling index.</param>
    /// <param name="cbLumaMultiplier">The signed Q6 weight of the luma value in the first chroma scaling index.</param>
    /// <param name="cbOffset">The offset added to the first chroma scaling index.</param>
    /// <param name="crMultiplier">The signed Q6 weight of the second chroma sample in its scaling index.</param>
    /// <param name="crLumaMultiplier">The signed Q6 weight of the luma value in the second chroma scaling index.</param>
    /// <param name="crOffset">The offset added to the second chroma scaling index.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="sampleMaximum">The largest sample value, which is also the largest scaling index.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <param name="vector">The overload-selection value.</param>
    private static void ApplyChroma<TSample>(
        ReadOnlySpan<int> scalingCb,
        ReadOnlySpan<int> scalingCr,
        Span<TSample> luma,
        Span<TSample> cb,
        Span<TSample> cr,
        int lumaStride,
        int chromaStride,
        ReadOnlySpan<int> cbGrain,
        ReadOnlySpan<int> crGrain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int subsamplingX,
        int subsamplingY,
        bool applyCb,
        bool applyCr,
        int cbMultiplier,
        int cbLumaMultiplier,
        int cbOffset,
        int crMultiplier,
        int crLumaMultiplier,
        int crOffset,
        int roundingOffset,
        int scalingShift,
        int sampleMaximum,
        int minimum,
        int maximum,
        Vector128<int> vector)
        where TSample : unmanaged
    {
        ref TSample lumaBase = ref MemoryMarshal.GetReference(luma);
        ref TSample cbBase = ref MemoryMarshal.GetReference(cb);
        ref TSample crBase = ref MemoryMarshal.GetReference(cr);
        ref int cbGrainBase = ref MemoryMarshal.GetReference(cbGrain);
        ref int crGrainBase = ref MemoryMarshal.GetReference(crGrain);
        Vector128<int> zero = Vector128<int>.Zero;
        Vector128<int> maximumIndex = Vector128.Create(sampleMaximum);
        for (int row = 0; row < height; row++)
        {
            ref TSample lumaRow = ref Unsafe.Add(ref lumaBase, (row << subsamplingY) * lumaStride);
            int chromaRowOffset = row * chromaStride;
            int grainRowOffset = row * grainStride;
            int column = 0;
            int vectorEnd = (int)(Numerics.Vector128Count<int>(width) * (nuint)Vector128<int>.Count);

            // The four-lane path keeps the same coordinate alignment and Q6 scaling-index arithmetic. Only the table read
            // changes from a hardware gather to four scalar reads that build one vector.
            for (; column < vectorEnd; column += Vector128<int>.Count)
            {
                ref TSample lumaSource = ref Unsafe.Add(ref lumaRow, column << subsamplingX);
                Vector128<int> averageLuma = Av1FilmGrainSampleOperations<TSample>.LoadChromaLuma4(ref lumaSource, subsamplingX);
                if (applyCb)
                {
                    ref TSample destination = ref Unsafe.Add(ref cbBase, chromaRowOffset + column);
                    Vector128<int> source = Av1FilmGrainSampleOperations<TSample>.Load4(ref destination);
                    Vector128<int> scalingIndex = ((averageLuma * cbLumaMultiplier) + (source * cbMultiplier)) >> 6;
                    scalingIndex = Vector128.Min(Vector128.Max(scalingIndex + Vector128.Create(cbOffset), zero), maximumIndex);
                    Vector128<int> grainValues = Vector128.LoadUnsafe(ref cbGrainBase, (nuint)(grainRowOffset + column));
                    Vector128<int> result = AddNoise(
                        source,
                        grainValues,
                        scalingCb,
                        scalingIndex,
                        bitDepth,
                        roundingOffset,
                        scalingShift,
                        minimum,
                        maximum);

                    Av1FilmGrainSampleOperations<TSample>.Store4(ref destination, result);
                }

                if (applyCr)
                {
                    ref TSample destination = ref Unsafe.Add(ref crBase, chromaRowOffset + column);
                    Vector128<int> source = Av1FilmGrainSampleOperations<TSample>.Load4(ref destination);
                    Vector128<int> scalingIndex = ((averageLuma * crLumaMultiplier) + (source * crMultiplier)) >> 6;
                    scalingIndex = Vector128.Min(Vector128.Max(scalingIndex + Vector128.Create(crOffset), zero), maximumIndex);
                    Vector128<int> grainValues = Vector128.LoadUnsafe(ref crGrainBase, (nuint)(grainRowOffset + column));
                    Vector128<int> result = AddNoise(
                        source,
                        grainValues,
                        scalingCr,
                        scalingIndex,
                        bitDepth,
                        roundingOffset,
                        scalingShift,
                        minimum,
                        maximum);

                    Av1FilmGrainSampleOperations<TSample>.Store4(ref destination, result);
                }
            }

            ApplyChromaScalar(
                scalingCb,
                scalingCr,
                luma.Slice(((row << subsamplingY) * lumaStride) + (column << subsamplingX)),
                cb.Slice(chromaRowOffset + column),
                cr.Slice(chromaRowOffset + column),
                lumaStride,
                chromaStride,
                cbGrain.Slice(grainRowOffset + column),
                crGrain.Slice(grainRowOffset + column),
                grainStride,
                1,
                width - column,
                bitDepth,
                subsamplingX,
                subsamplingY,
                applyCb,
                applyCr,
                cbMultiplier,
                cbLumaMultiplier,
                cbOffset,
                crMultiplier,
                crLumaMultiplier,
                crOffset,
                roundingOffset,
                scalingShift,
                sampleMaximum,
                minimum,
                maximum);
        }
    }

    /// <summary>
    /// Applies chroma grain to the scalar remainder of a row, or to the whole rectangle when no vector path is available.
    /// </summary>
    /// <typeparam name="TSample">The native sample type.</typeparam>
    /// <param name="scalingCb">The first chroma scaling lookup table.</param>
    /// <param name="scalingCr">The second chroma scaling lookup table.</param>
    /// <param name="luma">The restored luma samples that select the chroma scaling.</param>
    /// <param name="cb">The restored first chroma samples.</param>
    /// <param name="cr">The restored second chroma samples.</param>
    /// <param name="lumaStride">The luma row stride.</param>
    /// <param name="chromaStride">The chroma row stride.</param>
    /// <param name="cbGrain">The selected first chroma grain rectangle.</param>
    /// <param name="crGrain">The selected second chroma grain rectangle.</param>
    /// <param name="grainStride">The chroma grain row stride.</param>
    /// <param name="height">The number of chroma rows.</param>
    /// <param name="width">The number of chroma samples in each row.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="applyCb">Whether to add grain to the first chroma plane.</param>
    /// <param name="applyCr">Whether to add grain to the second chroma plane.</param>
    /// <param name="cbMultiplier">The signed Q6 weight of the first chroma sample in its scaling index.</param>
    /// <param name="cbLumaMultiplier">The signed Q6 weight of the luma value in the first chroma scaling index.</param>
    /// <param name="cbOffset">The offset added to the first chroma scaling index.</param>
    /// <param name="crMultiplier">The signed Q6 weight of the second chroma sample in its scaling index.</param>
    /// <param name="crLumaMultiplier">The signed Q6 weight of the luma value in the second chroma scaling index.</param>
    /// <param name="crOffset">The offset added to the second chroma scaling index.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="sampleMaximum">The largest sample value, which is also the largest scaling index.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    private static void ApplyChromaScalar<TSample>(
        ReadOnlySpan<int> scalingCb,
        ReadOnlySpan<int> scalingCr,
        Span<TSample> luma,
        Span<TSample> cb,
        Span<TSample> cr,
        int lumaStride,
        int chromaStride,
        ReadOnlySpan<int> cbGrain,
        ReadOnlySpan<int> crGrain,
        int grainStride,
        int height,
        int width,
        int bitDepth,
        int subsamplingX,
        int subsamplingY,
        bool applyCb,
        bool applyCr,
        int cbMultiplier,
        int cbLumaMultiplier,
        int cbOffset,
        int crMultiplier,
        int crLumaMultiplier,
        int crOffset,
        int roundingOffset,
        int scalingShift,
        int sampleMaximum,
        int minimum,
        int maximum)
        where TSample : unmanaged
    {
        ref TSample lumaBase = ref MemoryMarshal.GetReference(luma);
        ref TSample cbBase = ref MemoryMarshal.GetReference(cb);
        ref TSample crBase = ref MemoryMarshal.GetReference(cr);
        ref int cbGrainBase = ref MemoryMarshal.GetReference(cbGrain);
        ref int crGrainBase = ref MemoryMarshal.GetReference(crGrain);
        for (int row = 0; row < height; row++)
        {
            int lumaRowOffset = (row << subsamplingY) * lumaStride;
            int chromaRowOffset = row * chromaStride;
            int grainRowOffset = row * grainStride;
            for (int column = 0; column < width; column++)
            {
                int lumaOffset = lumaRowOffset + (column << subsamplingX);
                int averageLuma = Av1FilmGrainSampleOperations<TSample>.Load(ref Unsafe.Add(ref lumaBase, lumaOffset));
                if (subsamplingX != 0)
                {
                    averageLuma = (averageLuma + Av1FilmGrainSampleOperations<TSample>.Load(ref Unsafe.Add(ref lumaBase, lumaOffset + 1)) + 1) >> 1;
                }

                int chromaOffset = chromaRowOffset + column;
                int grainOffset = grainRowOffset + column;
                if (applyCb)
                {
                    ref TSample destination = ref Unsafe.Add(ref cbBase, chromaOffset);
                    int source = Av1FilmGrainSampleOperations<TSample>.Load(ref destination);
                    int scalingIndex = Av1Math.Clamp(
                        (((averageLuma * cbLumaMultiplier) + (source * cbMultiplier)) >> 6) + cbOffset,
                        0,
                        sampleMaximum);

                    int scale = ScaleLookup(scalingCb, scalingIndex, bitDepth);
                    int value = source + (((scale * Unsafe.Add(ref cbGrainBase, grainOffset)) + roundingOffset) >> scalingShift);
                    Av1FilmGrainSampleOperations<TSample>.Store(ref destination, Av1Math.Clamp(value, minimum, maximum));
                }

                if (applyCr)
                {
                    ref TSample destination = ref Unsafe.Add(ref crBase, chromaOffset);
                    int source = Av1FilmGrainSampleOperations<TSample>.Load(ref destination);
                    int scalingIndex = Av1Math.Clamp(
                        (((averageLuma * crLumaMultiplier) + (source * crMultiplier)) >> 6) + crOffset,
                        0,
                        sampleMaximum);

                    int scale = ScaleLookup(scalingCr, scalingIndex, bitDepth);
                    int value = source + (((scale * Unsafe.Add(ref crGrainBase, grainOffset)) + roundingOffset) >> scalingShift);
                    Av1FilmGrainSampleOperations<TSample>.Store(ref destination, Av1Math.Clamp(value, minimum, maximum));
                }
            }
        }
    }

    /// <summary>
    /// Adds scaled grain to eight source samples and clips the result. The source samples are also the scaling indices.
    /// </summary>
    /// <param name="source">The widened source samples.</param>
    /// <param name="grain">The grain values for the same lanes.</param>
    /// <param name="scaling">The scaling lookup table.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <returns>The clipped output samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> AddNoise(
        Vector256<int> source,
        Vector256<int> grain,
        ReadOnlySpan<int> scaling,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum)
        => AddNoise(source, grain, scaling, source, bitDepth, roundingOffset, scalingShift, minimum, maximum);

    /// <summary>
    /// Adds scaled grain to eight source samples with separate scaling indices, and clips the result.
    /// </summary>
    /// <param name="source">The widened source samples.</param>
    /// <param name="grain">The grain values for the same lanes.</param>
    /// <param name="scaling">The scaling lookup table.</param>
    /// <param name="scalingIndex">The scaling-table coordinate of each lane, at the decoded bit depth.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <returns>The clipped output samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> AddNoise(
        Vector256<int> source,
        Vector256<int> grain,
        ReadOnlySpan<int> scaling,
        Vector256<int> scalingIndex,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum)
    {
        // Each lane computes source + round(scale * grain / 2^scalingShift). The arithmetic right shift rounds negative
        // products the same way as the scalar `>>`, so every lane equals the scalar result.
        Vector256<int> scale = ScaleLookup(scaling, scalingIndex, bitDepth);
        Vector256<int> result = source + (((scale * grain) + Vector256.Create(roundingOffset)) >> scalingShift);
        return Vector256.Min(Vector256.Max(result, Vector256.Create(minimum)), Vector256.Create(maximum));
    }

    /// <summary>
    /// Adds scaled grain to four source samples and clips the result. The source samples are also the scaling indices.
    /// </summary>
    /// <param name="source">The widened source samples.</param>
    /// <param name="grain">The grain values for the same lanes.</param>
    /// <param name="scaling">The scaling lookup table.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <returns>The clipped output samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> AddNoise(
        Vector128<int> source,
        Vector128<int> grain,
        ReadOnlySpan<int> scaling,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum)
        => AddNoise(source, grain, scaling, source, bitDepth, roundingOffset, scalingShift, minimum, maximum);

    /// <summary>
    /// Adds scaled grain to four source samples with separate scaling indices, and clips the result.
    /// </summary>
    /// <param name="source">The widened source samples.</param>
    /// <param name="grain">The grain values for the same lanes.</param>
    /// <param name="scaling">The scaling lookup table.</param>
    /// <param name="scalingIndex">The scaling-table coordinate of each lane, at the decoded bit depth.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <param name="roundingOffset">The rounding offset added before the scaling shift.</param>
    /// <param name="scalingShift">The right shift that removes the scaling precision.</param>
    /// <param name="minimum">The minimum output sample.</param>
    /// <param name="maximum">The maximum output sample.</param>
    /// <returns>The clipped output samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> AddNoise(
        Vector128<int> source,
        Vector128<int> grain,
        ReadOnlySpan<int> scaling,
        Vector128<int> scalingIndex,
        int bitDepth,
        int roundingOffset,
        int scalingShift,
        int minimum,
        int maximum)
    {
        // The lane formula and rounding are the same as in the eight-lane overload.
        Vector128<int> scale = ScaleLookup(scaling, scalingIndex, bitDepth);
        Vector128<int> result = source + (((scale * grain) + Vector128.Create(roundingOffset)) >> scalingShift);
        return Vector128.Min(Vector128.Max(result, Vector128.Create(minimum)), Vector128.Create(maximum));
    }

    /// <summary>
    /// Determines whether the current dispatch enables the portable vector traversal.
    /// </summary>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <returns>Whether to use the portable vector traversal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanVectorizeWithoutGather(int bitDepth)
    {
        // This path builds four scale values from scalar lookups. High-depth samples also interpolate between table entries.
        // The bit-depth gate is a performance choice without end-to-end measurement. Film-grain synthesis has no numerical need for it.
        return bitDepth > 8 && Vector128.IsHardwareAccelerated;
    }

    /// <summary>
    /// Gathers eight scaling values and interpolates high-bit-depth coordinates.
    /// </summary>
    /// <param name="scaling">The 256-entry scaling lookup table.</param>
    /// <param name="index">The scaling coordinate of each lane, at the decoded bit depth.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <returns>The scaling value of each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> ScaleLookup(ReadOnlySpan<int> scaling, Vector256<int> index, int bitDepth)
    {
        // The high bits of a coordinate select the table entry. At high bit depths the low bits are the interpolation fraction.
        int depthShift = bitDepth - 8;
        Vector256<int> tableIndex = index >> depthShift;
        ref int table = ref MemoryMarshal.GetReference(scaling);
        Vector256<int> current = Vector256_.Gather(ref table, tableIndex);
        if (depthShift == 0)
        {
            return current;
        }

        // The clamp makes the next entry of index 255 equal to entry 255. Thus the difference is zero, and the lane returns
        // entry 255 the same as the scalar early return. The rounded linear interpolation is the same as the scalar overload.
        Vector256<int> nextIndex = Vector256.Min(tableIndex + Vector256<int>.One, Vector256.Create(255));
        Vector256<int> next = Vector256_.Gather(ref table, nextIndex);
        Vector256<int> fraction = index & Vector256.Create((1 << depthShift) - 1);
        return current + ((((next - current) * fraction) + Vector256.Create(1 << (depthShift - 1))) >> depthShift);
    }

    /// <summary>
    /// Reads four scaling values and interpolates high-bit-depth coordinates.
    /// </summary>
    /// <param name="scaling">The 256-entry scaling lookup table.</param>
    /// <param name="index">The scaling coordinate of each lane, at the decoded bit depth.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <returns>The scaling value of each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> ScaleLookup(ReadOnlySpan<int> scaling, Vector128<int> index, int bitDepth)
        => Vector128.Create(
            ScaleLookup(scaling, index.GetElement(0), bitDepth),
            ScaleLookup(scaling, index.GetElement(1), bitDepth),
            ScaleLookup(scaling, index.GetElement(2), bitDepth),
            ScaleLookup(scaling, index.GetElement(3), bitDepth));

    /// <summary>
    /// Reads one scaling value. At high bit depths, it interpolates between two adjacent table entries.
    /// </summary>
    /// <param name="scaling">The 256-entry scaling lookup table.</param>
    /// <param name="index">The scaling coordinate at the decoded bit depth.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <returns>The scaling value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ScaleLookup(ReadOnlySpan<int> scaling, int index, int bitDepth)
    {
        int depthShift = bitDepth - 8;
        int tableIndex = index >> depthShift;
        if (depthShift == 0 || tableIndex == 255)
        {
            return scaling[tableIndex];
        }

        int fraction = index & ((1 << depthShift) - 1);
        return scaling[tableIndex] + ((((scaling[tableIndex + 1] - scaling[tableIndex]) * fraction) + (1 << (depthShift - 1))) >> depthShift);
    }
}

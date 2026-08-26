// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// Applies selected AV1 grain blocks to restored luma and chroma samples.
/// </summary>
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
            // Restricted-range endpoints are signaled at eight-bit precision and scale exactly at higher depths.
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
                // Unity in the Q6 luma-multiplier domain selects luma directly and removes the chroma contribution.
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
        if (Avx2.IsSupported)
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
            int vectorEnd = width - Vector256<int>.Count;
            for (; column <= vectorEnd; column += Vector256<int>.Count)
            {
                ref TSample destination = ref Unsafe.Add(ref sampleBase, sampleRowOffset + column);
                Vector256<int> source = Av1FilmGrainSampleOperator<TSample>.Load8(ref destination);
                Vector256<int> grainValues = Vector256.LoadUnsafe(ref grainBase, (nuint)(grainRowOffset + column));
                Vector256<int> result = AddNoise(source, grainValues, scaling, bitDepth, roundingOffset, scalingShift, minimum, maximum);
                Av1FilmGrainSampleOperator<TSample>.Store8(ref destination, result);
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
            int vectorEnd = width - Vector128<int>.Count;
            for (; column <= vectorEnd; column += Vector128<int>.Count)
            {
                ref TSample destination = ref Unsafe.Add(ref sampleBase, sampleRowOffset + column);
                Vector128<int> source = Av1FilmGrainSampleOperator<TSample>.Load4(ref destination);
                Vector128<int> grainValues = Vector128.LoadUnsafe(ref grainBase, (nuint)(grainRowOffset + column));
                Vector128<int> result = AddNoise(source, grainValues, scaling, bitDepth, roundingOffset, scalingShift, minimum, maximum);
                Av1FilmGrainSampleOperator<TSample>.Store4(ref destination, result);
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
    /// Applies the luma scalar remainder or complete scalar fallback.
    /// </summary>
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
                int source = Av1FilmGrainSampleOperator<TSample>.Load(ref destination);
                int scale = ScaleLookup(scaling, source, bitDepth);
                int value = source + (((scale * Unsafe.Add(ref grainBase, grainRowOffset + column)) + roundingOffset) >> scalingShift);
                Av1FilmGrainSampleOperator<TSample>.Store(ref destination, Av1Math.Clamp(value, minimum, maximum));
            }
        }
    }

    /// <summary>
    /// Selects the widest available chroma traversal.
    /// </summary>
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
        if (Avx2.IsSupported)
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
            int vectorEnd = width - Vector256<int>.Count;
            for (; column <= vectorEnd; column += Vector256<int>.Count)
            {
                ref TSample lumaSource = ref Unsafe.Add(ref lumaRow, column << subsamplingX);
                Vector256<int> averageLuma = Av1FilmGrainSampleOperator<TSample>.LoadChromaLuma8(ref lumaSource, subsamplingX);
                if (applyCb)
                {
                    ref TSample destination = ref Unsafe.Add(ref cbBase, chromaRowOffset + column);
                    Vector256<int> source = Av1FilmGrainSampleOperator<TSample>.Load8(ref destination);
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

                    Av1FilmGrainSampleOperator<TSample>.Store8(ref destination, result);
                }

                if (applyCr)
                {
                    ref TSample destination = ref Unsafe.Add(ref crBase, chromaRowOffset + column);
                    Vector256<int> source = Av1FilmGrainSampleOperator<TSample>.Load8(ref destination);
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

                    Av1FilmGrainSampleOperator<TSample>.Store8(ref destination, result);
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
            int vectorEnd = width - Vector128<int>.Count;
            for (; column <= vectorEnd; column += Vector128<int>.Count)
            {
                ref TSample lumaSource = ref Unsafe.Add(ref lumaRow, column << subsamplingX);
                Vector128<int> averageLuma = Av1FilmGrainSampleOperator<TSample>.LoadChromaLuma4(ref lumaSource, subsamplingX);
                if (applyCb)
                {
                    ref TSample destination = ref Unsafe.Add(ref cbBase, chromaRowOffset + column);
                    Vector128<int> source = Av1FilmGrainSampleOperator<TSample>.Load4(ref destination);
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

                    Av1FilmGrainSampleOperator<TSample>.Store4(ref destination, result);
                }

                if (applyCr)
                {
                    ref TSample destination = ref Unsafe.Add(ref crBase, chromaRowOffset + column);
                    Vector128<int> source = Av1FilmGrainSampleOperator<TSample>.Load4(ref destination);
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

                    Av1FilmGrainSampleOperator<TSample>.Store4(ref destination, result);
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
    /// Applies the chroma scalar remainder or complete scalar fallback.
    /// </summary>
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
                int averageLuma = Av1FilmGrainSampleOperator<TSample>.Load(ref Unsafe.Add(ref lumaBase, lumaOffset));
                if (subsamplingX != 0)
                {
                    averageLuma = (averageLuma + Av1FilmGrainSampleOperator<TSample>.Load(ref Unsafe.Add(ref lumaBase, lumaOffset + 1)) + 1) >> 1;
                }

                int chromaOffset = chromaRowOffset + column;
                int grainOffset = grainRowOffset + column;
                if (applyCb)
                {
                    ref TSample destination = ref Unsafe.Add(ref cbBase, chromaOffset);
                    int source = Av1FilmGrainSampleOperator<TSample>.Load(ref destination);
                    int scalingIndex = Av1Math.Clamp(
                        (((averageLuma * cbLumaMultiplier) + (source * cbMultiplier)) >> 6) + cbOffset,
                        0,
                        sampleMaximum);

                    int scale = ScaleLookup(scalingCb, scalingIndex, bitDepth);
                    int value = source + (((scale * Unsafe.Add(ref cbGrainBase, grainOffset)) + roundingOffset) >> scalingShift);
                    Av1FilmGrainSampleOperator<TSample>.Store(ref destination, Av1Math.Clamp(value, minimum, maximum));
                }

                if (applyCr)
                {
                    ref TSample destination = ref Unsafe.Add(ref crBase, chromaOffset);
                    int source = Av1FilmGrainSampleOperator<TSample>.Load(ref destination);
                    int scalingIndex = Av1Math.Clamp(
                        (((averageLuma * crLumaMultiplier) + (source * crMultiplier)) >> 6) + crOffset,
                        0,
                        sampleMaximum);

                    int scale = ScaleLookup(scalingCr, scalingIndex, bitDepth);
                    int value = source + (((scale * Unsafe.Add(ref crGrainBase, grainOffset)) + roundingOffset) >> scalingShift);
                    Av1FilmGrainSampleOperator<TSample>.Store(ref destination, Av1Math.Clamp(value, minimum, maximum));
                }
            }
        }
    }

    /// <summary>
    /// Adds scaled grain to eight source samples and clips the result.
    /// </summary>
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
    /// Adds scaled grain to eight source samples using independent scaling coordinates.
    /// </summary>
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
        Vector256<int> scale = ScaleLookup(scaling, scalingIndex, bitDepth);
        Vector256<int> result = source + (((scale * grain) + Vector256.Create(roundingOffset)) >> scalingShift);
        return Vector256.Min(Vector256.Max(result, Vector256.Create(minimum)), Vector256.Create(maximum));
    }

    /// <summary>
    /// Adds scaled grain to four source samples and clips the result.
    /// </summary>
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
    /// Adds scaled grain to four source samples using independent scaling coordinates.
    /// </summary>
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
        Vector128<int> scale = ScaleLookup(scaling, scalingIndex, bitDepth);
        Vector128<int> result = source + (((scale * grain) + Vector128.Create(roundingOffset)) >> scalingShift);
        return Vector128.Min(Vector128.Max(result, Vector128.Create(minimum)), Vector128.Create(maximum));
    }

    /// <summary>
    /// Determines whether portable vector arithmetic repays the cost of scalar scaling-table reads.
    /// </summary>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    /// <returns>Whether to use the portable vector traversal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanVectorizeWithoutGather(int bitDepth)
    {
        // Portable Vector128 has no indexed table load. At eight bits, assembling each scaling vector from four
        // scalar reads is slower than the complete scalar loop; high-depth interpolation contains enough arithmetic
        // to amortize those reads. The AVX2 path uses native gather and remains the primary traversal at every depth.
        return bitDepth > 8 && Vector128.IsHardwareAccelerated;
    }

    /// <summary>
    /// Gathers eight scaling values and interpolates high-bit-depth coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector256<int> ScaleLookup(ReadOnlySpan<int> scaling, Vector256<int> index, int bitDepth)
    {
        int depthShift = bitDepth - 8;
        Vector256<int> tableIndex = index >> depthShift;
        fixed (int* table = scaling)
        {
            Vector256<int> current = Avx2.GatherVector256(table, tableIndex, sizeof(int));
            if (depthShift == 0)
            {
                return current;
            }

            // Clamping the following index extends entry 255 across the final interpolation interval.
            Vector256<int> nextIndex = Vector256.Min(tableIndex + Vector256<int>.One, Vector256.Create(255));
            Vector256<int> next = Avx2.GatherVector256(table, nextIndex, sizeof(int));
            Vector256<int> fraction = index & Vector256.Create((1 << depthShift) - 1);
            return current + ((((next - current) * fraction) + Vector256.Create(1 << (depthShift - 1))) >> depthShift);
        }
    }

    /// <summary>
    /// Reads four scaling values and interpolates high-bit-depth coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> ScaleLookup(ReadOnlySpan<int> scaling, Vector128<int> index, int bitDepth)
        => Vector128.Create(
            ScaleLookup(scaling, index.GetElement(0), bitDepth),
            ScaleLookup(scaling, index.GetElement(1), bitDepth),
            ScaleLookup(scaling, index.GetElement(2), bitDepth),
            ScaleLookup(scaling, index.GetElement(3), bitDepth));

    /// <summary>
    /// Reads one scaling value, interpolating between eight-bit entries when required.
    /// </summary>
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

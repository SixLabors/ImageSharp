// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <summary>
/// Provides SIMD sample widening, chroma reconstruction, planar storage, and packed output for HEIF color conversion.
/// </summary>
internal static class HeifSampleConversion
{
    /// <summary>
    /// The largest value represented by a 16-bit packed RGB component.
    /// </summary>
    private const float UShortMaximum = ushort.MaxValue;

    /// <summary>
    /// Widens and normalizes reconstructed integer samples into a pooled float component row.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <typeparam name="TLoader">The widening operations for the sample type.</typeparam>
    /// <param name="source">The reconstructed samples.</param>
    /// <param name="destination">The destination component row.</param>
    /// <param name="bias">The encoded value corresponding to normalized zero.</param>
    /// <param name="scale">The encoded range corresponding to a normalized interval of one.</param>
    public static void ConvertSamplesToFloat<TSample, TLoader>(
        ReadOnlySpan<TSample> source,
        Span<float> destination,
        float bias,
        float scale)
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleConverter<TSample>
    {
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        // Descending vector widths use the widest supported lanes first. A wide-capable CPU processes
        // complete wide batches first while short and irregular rows continue through narrower SIMD tails.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> biasVector = Vector512.Create(bias);
            Vector512<float> scaleVector = Vector512.Create(scale);
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<float> samples = TLoader.LoadVector512(ref Unsafe.Add(ref sourceBase, i));
                Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref destinationBase, i)) = (samples - biasVector) / scaleVector;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> biasVector = Vector256.Create(bias);
            Vector256<float> scaleVector = Vector256.Create(scale);
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<float> samples = TLoader.LoadVector256(ref Unsafe.Add(ref sourceBase, i));
                Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref destinationBase, i)) = (samples - biasVector) / scaleVector;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> biasVector = Vector128.Create(bias);
            Vector128<float> scaleVector = Vector128.Create(scale);
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                Vector128<float> samples = TLoader.LoadVector128(ref Unsafe.Add(ref sourceBase, i));
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref destinationBase, i)) = (samples - biasVector) / scaleVector;
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = (GetSample(source, i) - bias) / scale;
        }
    }

    /// <summary>
    /// Expands horizontally subsampled chroma by repeating each sample at two luma positions.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <typeparam name="TLoader">The widening operations for the sample type.</typeparam>
    /// <param name="source">The complete subsampled chroma row.</param>
    /// <param name="sourceX">The first luma coordinate of the output window.</param>
    /// <param name="destination">The exact output component row.</param>
    /// <param name="bias">The encoded chroma value corresponding to normalized zero.</param>
    /// <param name="scale">The encoded chroma range.</param>
    public static void ReconstructChromaRow<TSample, TLoader>(
        ReadOnlySpan<TSample> source,
        int sourceX,
        Span<float> destination,
        float bias,
        float scale)
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleConverter<TSample>
    {
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourceIndex = sourceX >> 1;
        int i = 0;

        // An odd crop origin starts at the second pixel of a replicated pair. Consume that pixel
        // before vectorizing complete pairs; the final scalar tail handles an odd right edge.
        if ((sourceX & 1) != 0)
        {
            destination[i++] = (GetSample(source, sourceIndex++) - bias) / scale;
        }

        if (Vector512.IsHardwareAccelerated)
        {
            int lastBatch = destination.Length - 32;
            for (; i <= lastBatch; i += 32, sourceIndex += 16)
            {
                Vector512<float> samples = TLoader.LoadVector512(ref Unsafe.Add(ref sourceBase, sourceIndex));
                samples = (samples - Vector512.Create(bias)) / Vector512.Create(scale);

                // Each four-lane group [a,b,c,d] becomes [a,a,b,b,c,c,d,d]. Keeping groups
                // in source order avoids lane-local shuffles interleaving separate pixel groups.
                StoreInterleavedChroma(samples.GetLower().GetLower(), samples.GetLower().GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreInterleavedChroma(samples.GetLower().GetUpper(), samples.GetLower().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 8));
                StoreInterleavedChroma(samples.GetUpper().GetLower(), samples.GetUpper().GetLower(), ref Unsafe.Add(ref destinationBase, i + 16));
                StoreInterleavedChroma(samples.GetUpper().GetUpper(), samples.GetUpper().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 24));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int lastBatch = destination.Length - 16;
            for (; i <= lastBatch; i += 16, sourceIndex += 8)
            {
                Vector256<float> samples = TLoader.LoadVector256(ref Unsafe.Add(ref sourceBase, sourceIndex));
                samples = (samples - Vector256.Create(bias)) / Vector256.Create(scale);
                StoreInterleavedChroma(samples.GetLower(), samples.GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreInterleavedChroma(samples.GetUpper(), samples.GetUpper(), ref Unsafe.Add(ref destinationBase, i + 8));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int lastBatch = destination.Length - 8;
            for (; i <= lastBatch; i += 8, sourceIndex += 4)
            {
                Vector128<float> samples = TLoader.LoadVector128(ref Unsafe.Add(ref sourceBase, sourceIndex));
                samples = (samples - Vector128.Create(bias)) / Vector128.Create(scale);
                StoreInterleavedChroma(samples, samples, ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < destination.Length; i++)
        {
            destination[i] = (GetSample(source, (sourceX + i) >> 1) - bias) / scale;
        }
    }

    /// <summary>
    /// Reconstructs one normalized chroma row using the signaled sample positions.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <typeparam name="TLoader">The widening operations for the sample type.</typeparam>
    /// <param name="row0">The upper chroma row.</param>
    /// <param name="row1">The lower chroma row.</param>
    /// <param name="y1Weight">The lower-row weight with a denominator of four.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="isCenteredX">Whether horizontally subsampled chroma is centered between luma samples.</param>
    /// <param name="destination">The reconstructed full-width chroma row.</param>
    /// <param name="scratch0">The first pooled chroma scratch row.</param>
    /// <param name="scratch1">The second pooled chroma scratch row.</param>
    /// <param name="bias">The encoded chroma value corresponding to normalized zero.</param>
    /// <param name="scale">The encoded chroma range.</param>
    public static void ReconstructChromaRowBilinear<TSample, TLoader>(
        ReadOnlySpan<TSample> row0,
        ReadOnlySpan<TSample> row1,
        int y1Weight,
        int subX,
        bool isCenteredX,
        Span<float> destination,
        Span<float> scratch0,
        Span<float> scratch1,
        float bias,
        float scale)
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleConverter<TSample>
    {
        int sourceLength = subX == 0 ? destination.Length : (destination.Length + 1) >> 1;
        Span<float> top = scratch0[..sourceLength];
        ConvertSamplesToFloat<TSample, TLoader>(row0, top, bias, scale);

        if (subX == 0)
        {
            top.CopyTo(destination);
            return;
        }

        ReadOnlySpan<float> bottom = top;
        if (y1Weight != 0)
        {
            Span<float> lower = scratch1[..sourceLength];
            ConvertSamplesToFloat<TSample, TLoader>(row1, lower, bias, scale);
            bottom = lower;
        }

        // Normalize each sample before interpolation. Preserve the four products and their
        // addition order instead of combining duplicate rows or performing two separable blends.
        // Collapsed vertical boundaries reuse the top row with the same centered weights.
        float closestWeight = y1Weight == 0 ? 0.75F : Math.Max(y1Weight, 4 - y1Weight) * 0.25F;
        ReadOnlySpan<float> closest = y1Weight > 2 ? bottom : top;
        ReadOnlySpan<float> adjacent = y1Weight > 2 ? top : bottom;
        UpsampleChromaHorizontal(closest, adjacent, destination, closestWeight, isCenteredX);
    }

    /// <summary>
    /// Blends four normalized chroma samples in closest, horizontal, vertical, diagonal order.
    /// </summary>
    /// <param name="closest">The vertically closest normalized row.</param>
    /// <param name="adjacent">The vertically adjacent normalized row, or the same row at a boundary.</param>
    /// <param name="destination">The full-width chroma values.</param>
    /// <param name="closestWeight">The weight of the closest row.</param>
    /// <param name="isCentered">Whether chroma lies between neighboring luma samples.</param>
    private static void UpsampleChromaHorizontal(
        ReadOnlySpan<float> closest,
        ReadOnlySpan<float> adjacent,
        Span<float> destination,
        float closestWeight,
        bool isCentered)
    {
        ref float closestBase = ref MemoryMarshal.GetReference(closest);
        ref float adjacentBase = ref MemoryMarshal.GetReference(adjacent);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourceLength = closest.Length;
        float adjacentWeight = 1F - closestWeight;
        float evenWeight = isCentered ? 0.75F : 1F;
        float oddWeight = isCentered ? 0.75F : 0.5F;
        float e0 = evenWeight * closestWeight;
        float e1 = (1F - evenWeight) * closestWeight;
        float e2 = evenWeight * adjacentWeight;
        float e3 = (1F - evenWeight) * adjacentWeight;
        float o0 = oddWeight * closestWeight;
        float o1 = (1F - oddWeight) * closestWeight;
        float o2 = oddWeight * adjacentWeight;
        float o3 = (1F - oddWeight) * adjacentWeight;
        int i = 0;

        if (isCentered)
        {
            // Extend the left edge before SIMD loads can address a previous sample.
            // Retain duplicate terms: collapsing their weights changes floating-point rounding.
            int next = Math.Min(1, sourceLength - 1);
            float even = (((closest[0] * e0) + (closest[0] * e1)) + (adjacent[0] * e2)) + (adjacent[0] * e3);
            float odd = (((closest[0] * o0) + (closest[next] * o1)) + (adjacent[0] * o2)) + (adjacent[next] * o3);
            StoreChromaPair(ref destinationBase, 0, even, odd, destination.Length);
            i = 1;
        }

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorBeforeEnd = sourceLength - Vector512<float>.Count - 1;
            Vector512<float> e0Vector = Vector512.Create(e0);
            Vector512<float> e1Vector = Vector512.Create(e1);
            Vector512<float> e2Vector = Vector512.Create(e2);
            Vector512<float> e3Vector = Vector512.Create(e3);
            Vector512<float> o0Vector = Vector512.Create(o0);
            Vector512<float> o1Vector = Vector512.Create(o1);
            Vector512<float> o2Vector = Vector512.Create(o2);
            Vector512<float> o3Vector = Vector512.Create(o3);
            for (; i <= oneVectorBeforeEnd; i += Vector512<float>.Count)
            {
                // Each lane represents one chroma column. Even lanes use the previous column
                // for centered samples; odd lanes use the next. The two rows remain separate
                // until the four weighted terms are added, without fused multiply-add rounding.
                int previous = isCentered ? i - 1 : i;
                Vector512<float> center0 = Vector512.LoadUnsafe(ref closestBase, (nuint)i);
                Vector512<float> center1 = Vector512.LoadUnsafe(ref adjacentBase, (nuint)i);
                Vector512<float> previous0 = Vector512.LoadUnsafe(ref closestBase, (nuint)previous);
                Vector512<float> previous1 = Vector512.LoadUnsafe(ref adjacentBase, (nuint)previous);
                Vector512<float> next0 = Vector512.LoadUnsafe(ref closestBase, (nuint)(i + 1));
                Vector512<float> next1 = Vector512.LoadUnsafe(ref adjacentBase, (nuint)(i + 1));
                Vector512<float> even = (((center0 * e0Vector) + (previous0 * e1Vector)) + (center1 * e2Vector)) + (previous1 * e3Vector);
                Vector512<float> odd = (((center0 * o0Vector) + (next0 * o1Vector)) + (center1 * o2Vector)) + (next1 * o3Vector);

                StoreInterleavedChroma(even.GetLower().GetLower(), odd.GetLower().GetLower(), ref Unsafe.Add(ref destinationBase, (i * 2) + 0));
                StoreInterleavedChroma(even.GetLower().GetUpper(), odd.GetLower().GetUpper(), ref Unsafe.Add(ref destinationBase, (i * 2) + 8));
                StoreInterleavedChroma(even.GetUpper().GetLower(), odd.GetUpper().GetLower(), ref Unsafe.Add(ref destinationBase, (i * 2) + 16));
                StoreInterleavedChroma(even.GetUpper().GetUpper(), odd.GetUpper().GetUpper(), ref Unsafe.Add(ref destinationBase, (i * 2) + 24));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorBeforeEnd = sourceLength - Vector256<float>.Count - 1;
            Vector256<float> e0Vector = Vector256.Create(e0);
            Vector256<float> e1Vector = Vector256.Create(e1);
            Vector256<float> e2Vector = Vector256.Create(e2);
            Vector256<float> e3Vector = Vector256.Create(e3);
            Vector256<float> o0Vector = Vector256.Create(o0);
            Vector256<float> o1Vector = Vector256.Create(o1);
            Vector256<float> o2Vector = Vector256.Create(o2);
            Vector256<float> o3Vector = Vector256.Create(o3);
            for (; i <= oneVectorBeforeEnd; i += Vector256<float>.Count)
            {
                // Each lane represents one chroma column. Even lanes use the previous column
                // for centered samples; odd lanes use the next. The two rows remain separate
                // until the four weighted terms are added, without fused multiply-add rounding.
                int previous = isCentered ? i - 1 : i;
                Vector256<float> center0 = Vector256.LoadUnsafe(ref closestBase, (nuint)i);
                Vector256<float> center1 = Vector256.LoadUnsafe(ref adjacentBase, (nuint)i);
                Vector256<float> previous0 = Vector256.LoadUnsafe(ref closestBase, (nuint)previous);
                Vector256<float> previous1 = Vector256.LoadUnsafe(ref adjacentBase, (nuint)previous);
                Vector256<float> next0 = Vector256.LoadUnsafe(ref closestBase, (nuint)(i + 1));
                Vector256<float> next1 = Vector256.LoadUnsafe(ref adjacentBase, (nuint)(i + 1));
                Vector256<float> even = (((center0 * e0Vector) + (previous0 * e1Vector)) + (center1 * e2Vector)) + (previous1 * e3Vector);
                Vector256<float> odd = (((center0 * o0Vector) + (next0 * o1Vector)) + (center1 * o2Vector)) + (next1 * o3Vector);

                StoreInterleavedChroma(even.GetLower(), odd.GetLower(), ref Unsafe.Add(ref destinationBase, (i * 2) + 0));
                StoreInterleavedChroma(even.GetUpper(), odd.GetUpper(), ref Unsafe.Add(ref destinationBase, (i * 2) + 8));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorBeforeEnd = sourceLength - Vector128<float>.Count - 1;
            Vector128<float> e0Vector = Vector128.Create(e0);
            Vector128<float> e1Vector = Vector128.Create(e1);
            Vector128<float> e2Vector = Vector128.Create(e2);
            Vector128<float> e3Vector = Vector128.Create(e3);
            Vector128<float> o0Vector = Vector128.Create(o0);
            Vector128<float> o1Vector = Vector128.Create(o1);
            Vector128<float> o2Vector = Vector128.Create(o2);
            Vector128<float> o3Vector = Vector128.Create(o3);
            for (; i <= oneVectorBeforeEnd; i += Vector128<float>.Count)
            {
                // Each lane represents one chroma column. Even lanes use the previous column
                // for centered samples; odd lanes use the next. The two rows remain separate
                // until the four weighted terms are added, without fused multiply-add rounding.
                int previous = isCentered ? i - 1 : i;
                Vector128<float> center0 = Vector128.LoadUnsafe(ref closestBase, (nuint)i);
                Vector128<float> center1 = Vector128.LoadUnsafe(ref adjacentBase, (nuint)i);
                Vector128<float> previous0 = Vector128.LoadUnsafe(ref closestBase, (nuint)previous);
                Vector128<float> previous1 = Vector128.LoadUnsafe(ref adjacentBase, (nuint)previous);
                Vector128<float> next0 = Vector128.LoadUnsafe(ref closestBase, (nuint)(i + 1));
                Vector128<float> next1 = Vector128.LoadUnsafe(ref adjacentBase, (nuint)(i + 1));
                Vector128<float> even = (((center0 * e0Vector) + (previous0 * e1Vector)) + (center1 * e2Vector)) + (previous1 * e3Vector);
                Vector128<float> odd = (((center0 * o0Vector) + (next0 * o1Vector)) + (center1 * o2Vector)) + (next1 * o3Vector);

                StoreInterleavedChroma(even, odd, ref Unsafe.Add(ref destinationBase, (i * 2) + 0));
            }
        }

        for (; i < sourceLength; i++)
        {
            int previous = isCentered ? Math.Max(i - 1, 0) : i;
            int next = Math.Min(i + 1, sourceLength - 1);
            float even = (((closest[i] * e0) + (closest[previous] * e1)) + (adjacent[i] * e2)) + (adjacent[previous] * e3);
            float odd = (((closest[i] * o0) + (closest[next] * o1)) + (adjacent[i] * o2)) + (adjacent[next] * o3);
            StoreChromaPair(ref destinationBase, i * 2, even, odd, destination.Length);
        }
    }

    /// <summary>
    /// Stores one reconstructed chroma pair without writing beyond an odd-width destination row.
    /// </summary>
    /// <param name="destination">The first destination value.</param>
    /// <param name="index">The even destination index.</param>
    /// <param name="even">The even luma-coordinate value.</param>
    /// <param name="odd">The odd luma-coordinate value.</param>
    /// <param name="length">The destination length.</param>
    private static void StoreChromaPair(ref float destination, int index, float even, float odd, int length)
    {
        Unsafe.Add(ref destination, index) = even;
        if (index + 1 < length)
        {
            Unsafe.Add(ref destination, index + 1) = odd;
        }
    }

    /// <summary>
    /// Stores four even chroma lanes interleaved with their four odd lanes.
    /// </summary>
    /// <param name="even">The even luma-coordinate values.</param>
    /// <param name="odd">The odd luma-coordinate values.</param>
    /// <param name="destination">The first destination value.</param>
    private static void StoreInterleavedChroma(Vector128<float> even, Vector128<float> odd, ref float destination)
    {
        Vector128<float> lower = Vector128_.UnpackLow(even.AsInt32(), odd.AsInt32()).AsSingle();
        Vector128<float> upper = Vector128_.UnpackHigh(even.AsInt32(), odd.AsInt32()).AsSingle();
        Unsafe.As<float, Vector128<float>>(ref destination) = lower;
        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref destination, Vector128<float>.Count)) = upper;
    }

    /// <summary>
    /// Deinterleaves high-bit-depth RGB pixels into planar component rows.
    /// </summary>
    /// <param name="source">The packed RGBA pixels.</param>
    /// <param name="red">The destination red components.</param>
    /// <param name="green">The destination green components.</param>
    /// <param name="blue">The destination blue components.</param>
    public static void DeinterleaveRgb48(ReadOnlySpan<Rgb48> source, Span<float> red, Span<float> green, Span<float> blue)
    {
        ref float redBase = ref MemoryMarshal.GetReference(red);
        ref float greenBase = ref MemoryMarshal.GetReference(green);
        ref float blueBase = ref MemoryMarshal.GetReference(blue);
        ref Rgb48 sourceBase = ref MemoryMarshal.GetReference(source);
        int length = source.Length;
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                ref Rgb48 pixel0 = ref Unsafe.Add(ref sourceBase, i);
                ref Rgb48 pixel1 = ref Unsafe.Add(ref sourceBase, i + 1);
                ref Rgb48 pixel2 = ref Unsafe.Add(ref sourceBase, i + 2);
                ref Rgb48 pixel3 = ref Unsafe.Add(ref sourceBase, i + 3);

                // Widen each UInt16 channel to a UInt32 lane before converting to Single. This avoids
                // reinterpreting adjacent 16-bit samples as one unrelated 32-bit integer.
                Vector128<float> redVector = Vector128.ConvertToSingle(
                    Vector128.Create((uint)pixel0.R, pixel1.R, pixel2.R, pixel3.R));

                Vector128<float> greenVector = Vector128.ConvertToSingle(
                    Vector128.Create((uint)pixel0.G, pixel1.G, pixel2.G, pixel3.G));

                Vector128<float> blueVector = Vector128.ConvertToSingle(
                    Vector128.Create((uint)pixel0.B, pixel1.B, pixel2.B, pixel3.B));

                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref redBase, i)) = redVector;
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref greenBase, i)) = greenVector;
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref blueBase, i)) = blueVector;
            }
        }

        for (; i < length; i++)
        {
            Rgb48 pixel = Unsafe.Add(ref sourceBase, i);
            Unsafe.Add(ref redBase, i) = pixel.R;
            Unsafe.Add(ref greenBase, i) = pixel.G;
            Unsafe.Add(ref blueBase, i) = pixel.B;
        }
    }

    /// <summary>
    /// Scales, quantizes, and stores one planar row using the widest available SIMD width.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <typeparam name="TStorer">The narrowing and storage operations for the sample type.</typeparam>
    /// <param name="source">The normalized source values.</param>
    /// <param name="destination">The encoded destination samples.</param>
    /// <param name="scale">The encoded range scale.</param>
    /// <param name="bias">The encoded range bias.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    public static void WriteSamples<TSample, TStorer>(ReadOnlySpan<float> source, Span<TSample> destination, float scale, float bias, float maximum)
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref TSample destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<float> values = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref sourceBase, i));
                TStorer.Store(ScaleBiasRoundAndClampToInt32(values, scale, bias, maximum), ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<float> values = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref sourceBase, i));
                TStorer.Store(ScaleBiasRoundAndClampToInt32(values, scale, bias, maximum), ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                Vector128<float> values = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref sourceBase, i));
                TStorer.Store(ScaleBiasRoundAndClampToInt32(values, scale, bias, maximum), ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = ToSample<TSample>((Unsafe.Add(ref sourceBase, i) * scale) + bias, maximum);
        }
    }

    /// <summary>
    /// Filters one or two planar rows into horizontally subsampled encoded samples.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <typeparam name="TStorer">The narrowing and storage operations for the sample type.</typeparam>
    /// <param name="row0">The first normalized source row.</param>
    /// <param name="row1">The optional second normalized source row.</param>
    /// <param name="destination">The encoded subsampled destination row.</param>
    /// <param name="isCenteredX">Whether each output sample is centered between two horizontal source samples.</param>
    /// <param name="row1Weight">The contribution of the second row, in the inclusive range zero through one.</param>
    /// <param name="scale">The encoded range scale.</param>
    /// <param name="bias">The encoded range bias.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    public static void WriteSubsampledSamples<TSample, TStorer>(
        ReadOnlySpan<float> row0,
        ReadOnlySpan<float> row1,
        Span<TSample> destination,
        bool isCenteredX,
        float row1Weight,
        float scale,
        float bias,
        float maximum)
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        ref float row0Base = ref MemoryMarshal.GetReference(row0);
        ref float row1Base = ref MemoryMarshal.GetReference(row1);
        ref TSample destinationBase = ref MemoryMarshal.GetReference(destination);
        bool hasSecondRow = !row1.IsEmpty;
        float effectiveRow1Weight = hasSecondRow ? row1Weight : 0F;
        float row0Weight = 1F - effectiveRow1Weight;
        int length = row0.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> evenOdd = Vector512.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15);
            Vector512<float> row0WeightVector = Vector512.Create(row0Weight);
            Vector512<float> row1WeightVector = Vector512.Create(effectiveRow1Weight);
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<float> filtered = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref row0Base, i)) * row0WeightVector;
                if (hasSecondRow)
                {
                    filtered = Vector512.MultiplyAddEstimate(
                        Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref row1Base, i)),
                        row1WeightVector,
                        filtered);
                }

                Vector512<float> shuffled = Vector512.Shuffle(filtered, evenOdd);
                Vector256<float> samples = isCenteredX
                    ? (shuffled.GetLower() + shuffled.GetUpper()) * Vector256.Create(0.5F)
                    : shuffled.GetLower();

                TStorer.Store(ScaleBiasRoundAndClampToInt32(samples, scale, bias, maximum), ref Unsafe.Add(ref destinationBase, i >> 1));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> evenOdd = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
            Vector256<float> row0WeightVector = Vector256.Create(row0Weight);
            Vector256<float> row1WeightVector = Vector256.Create(effectiveRow1Weight);
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<float> filtered = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref row0Base, i)) * row0WeightVector;
                if (hasSecondRow)
                {
                    filtered = Vector256.MultiplyAddEstimate(
                        Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref row1Base, i)),
                        row1WeightVector,
                        filtered);
                }

                Vector256<float> shuffled = Vector256.Shuffle(filtered, evenOdd);
                Vector128<float> samples = isCenteredX
                    ? (shuffled.GetLower() + shuffled.GetUpper()) * Vector128.Create(0.5F)
                    : shuffled.GetLower();

                TStorer.Store(ScaleBiasRoundAndClampToInt32(samples, scale, bias, maximum), ref Unsafe.Add(ref destinationBase, i >> 1));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> evenOdd = Vector128.Create(0, 2, 1, 3);
            Vector128<float> row0WeightVector = Vector128.Create(row0Weight);
            Vector128<float> row1WeightVector = Vector128.Create(effectiveRow1Weight);
            int twoVectorsFromEnd = length - (Vector128<float>.Count * 2);
            for (; i <= twoVectorsFromEnd; i += Vector128<float>.Count * 2)
            {
                Vector128<float> filtered0 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref row0Base, i)) * row0WeightVector;
                Vector128<float> filtered1 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref row0Base, i + Vector128<float>.Count)) * row0WeightVector;
                if (hasSecondRow)
                {
                    filtered0 = Vector128.MultiplyAddEstimate(
                        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref row1Base, i)),
                        row1WeightVector,
                        filtered0);

                    filtered1 = Vector128.MultiplyAddEstimate(
                        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref row1Base, i + Vector128<float>.Count)),
                        row1WeightVector,
                        filtered1);
                }

                Vector128<float> shuffled0 = Vector128.Shuffle(filtered0, evenOdd);
                Vector128<float> shuffled1 = Vector128.Shuffle(filtered1, evenOdd);
                Vector64<float> samples0 = isCenteredX
                    ? (shuffled0.GetLower() + shuffled0.GetUpper()) * Vector64.Create(0.5F)
                    : shuffled0.GetLower();

                Vector64<float> samples1 = isCenteredX
                    ? (shuffled1.GetLower() + shuffled1.GetUpper()) * Vector64.Create(0.5F)
                    : shuffled1.GetLower();

                TStorer.Store(
                    ScaleBiasRoundAndClampToInt32(Vector128.Create(samples0, samples1), scale, bias, maximum),
                    ref Unsafe.Add(ref destinationBase, i >> 1));
            }
        }

        for (; i < length; i += 2)
        {
            float row0Sample = Unsafe.Add(ref row0Base, i);
            if (isCenteredX && i + 1 < length)
            {
                row0Sample = (row0Sample + Unsafe.Add(ref row0Base, i + 1)) * 0.5F;
            }

            float sample = row0Sample * row0Weight;
            if (hasSecondRow)
            {
                float row1Sample = Unsafe.Add(ref row1Base, i);
                if (isCenteredX && i + 1 < length)
                {
                    row1Sample = (row1Sample + Unsafe.Add(ref row1Base, i + 1)) * 0.5F;
                }

                sample += row1Sample * effectiveRow1Weight;
            }

            Unsafe.Add(ref destinationBase, i >> 1) = ToSample<TSample>((sample * scale) + bias, maximum);
        }
    }

    /// <summary>
    /// Packs 16-bit RGB component rows into opaque 16-bit RGBA pixels.
    /// </summary>
    /// <param name="red">The red components.</param>
    /// <param name="green">The green components.</param>
    /// <param name="blue">The blue components.</param>
    /// <param name="destination">The destination pixels.</param>
    public static void PackRgba64(ReadOnlySpan<ushort> red, ReadOnlySpan<ushort> green, ReadOnlySpan<ushort> blue, Span<Rgba64> destination)
    {
        ref ushort redBase = ref MemoryMarshal.GetReference(red);
        ref ushort greenBase = ref MemoryMarshal.GetReference(green);
        ref ushort blueBase = ref MemoryMarshal.GetReference(blue);
        ref Rgba64 destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector512<int>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<int>.Count)
            {
                (Vector256<uint> redLower, Vector256<uint> redUpper) = Vector256.Widen(Vector256.LoadUnsafe(ref Unsafe.Add(ref redBase, i)));
                (Vector256<uint> greenLower, Vector256<uint> greenUpper) = Vector256.Widen(Vector256.LoadUnsafe(ref Unsafe.Add(ref greenBase, i)));
                (Vector256<uint> blueLower, Vector256<uint> blueUpper) = Vector256.Widen(Vector256.LoadUnsafe(ref Unsafe.Add(ref blueBase, i)));
                Vector512<int> r = Vector512.Create(redLower, redUpper).AsInt32();
                Vector512<int> g = Vector512.Create(greenLower, greenUpper).AsInt32();
                Vector512<int> b = Vector512.Create(blueLower, blueUpper).AsInt32();
                StoreRgba64Batch(r.GetLower().GetLower(), g.GetLower().GetLower(), b.GetLower().GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreRgba64Batch(r.GetLower().GetUpper(), g.GetLower().GetUpper(), b.GetLower().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 4));
                StoreRgba64Batch(r.GetUpper().GetLower(), g.GetUpper().GetLower(), b.GetUpper().GetLower(), ref Unsafe.Add(ref destinationBase, i + 8));
                StoreRgba64Batch(r.GetUpper().GetUpper(), g.GetUpper().GetUpper(), b.GetUpper().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 12));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector256<int>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<int>.Count)
            {
                Vector128<ushort> red16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref redBase, i));
                Vector128<ushort> green16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref greenBase, i));
                Vector128<ushort> blue16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref blueBase, i));
                Vector256<int> r = Vector256.Create(Vector128.WidenLower(red16), Vector128.WidenUpper(red16)).AsInt32();
                Vector256<int> g = Vector256.Create(Vector128.WidenLower(green16), Vector128.WidenUpper(green16)).AsInt32();
                Vector256<int> b = Vector256.Create(Vector128.WidenLower(blue16), Vector128.WidenUpper(blue16)).AsInt32();
                StoreRgba64Batch(r.GetLower(), g.GetLower(), b.GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreRgba64Batch(r.GetUpper(), g.GetUpper(), b.GetUpper(), ref Unsafe.Add(ref destinationBase, i + 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector128<int>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<int>.Count)
            {
                ulong packedRed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref redBase, i)));
                ulong packedGreen = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref greenBase, i)));
                ulong packedBlue = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref blueBase, i)));
                Vector128<int> r = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedRed).AsUInt16()).AsInt32();
                Vector128<int> g = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedGreen).AsUInt16()).AsInt32();
                Vector128<int> b = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedBlue).AsUInt16()).AsInt32();
                StoreRgba64Batch(r, g, b, ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = new Rgba64(
                Unsafe.Add(ref redBase, i),
                Unsafe.Add(ref greenBase, i),
                Unsafe.Add(ref blueBase, i),
                ushort.MaxValue);
        }
    }

    /// <summary>
    /// Packs normalized RGB component rows into opaque 16-bit RGBA pixels.
    /// </summary>
    /// <param name="red">The normalized red components.</param>
    /// <param name="green">The normalized green components.</param>
    /// <param name="blue">The normalized blue components.</param>
    /// <param name="destination">The destination pixels.</param>
    public static void PackRgba64(ReadOnlySpan<float> red, ReadOnlySpan<float> green, ReadOnlySpan<float> blue, Span<Rgba64> destination)
    {
        ref float redBase = ref MemoryMarshal.GetReference(red);
        ref float greenBase = ref MemoryMarshal.GetReference(green);
        ref float blueBase = ref MemoryMarshal.GetReference(blue);
        ref Rgba64 destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<int> r = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref redBase, i)), UShortMaximum);
                Vector512<int> g = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref greenBase, i)), UShortMaximum);
                Vector512<int> b = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref blueBase, i)), UShortMaximum);
                StoreRgba64Batch(r.GetLower().GetLower(), g.GetLower().GetLower(), b.GetLower().GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreRgba64Batch(r.GetLower().GetUpper(), g.GetLower().GetUpper(), b.GetLower().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 4));
                StoreRgba64Batch(r.GetUpper().GetLower(), g.GetUpper().GetLower(), b.GetUpper().GetLower(), ref Unsafe.Add(ref destinationBase, i + 8));
                StoreRgba64Batch(r.GetUpper().GetUpper(), g.GetUpper().GetUpper(), b.GetUpper().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 12));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<int> r = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref redBase, i)), UShortMaximum);
                Vector256<int> g = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref greenBase, i)), UShortMaximum);
                Vector256<int> b = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref blueBase, i)), UShortMaximum);
                StoreRgba64Batch(r.GetLower(), g.GetLower(), b.GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreRgba64Batch(r.GetUpper(), g.GetUpper(), b.GetUpper(), ref Unsafe.Add(ref destinationBase, i + 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                Vector128<int> r = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref redBase, i)), UShortMaximum);
                Vector128<int> g = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref greenBase, i)), UShortMaximum);
                Vector128<int> b = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref blueBase, i)), UShortMaximum);
                StoreRgba64Batch(r, g, b, ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = new Rgba64(
                ToSample<ushort>(Unsafe.Add(ref redBase, i) * UShortMaximum, UShortMaximum),
                ToSample<ushort>(Unsafe.Add(ref greenBase, i) * UShortMaximum, UShortMaximum),
                ToSample<ushort>(Unsafe.Add(ref blueBase, i) * UShortMaximum, UShortMaximum),
                ushort.MaxValue);
        }
    }

    /// <summary>
    /// Packs normalized monochrome samples into 16-bit luminance pixels.
    /// </summary>
    /// <param name="source">The normalized monochrome samples.</param>
    /// <param name="destination">The destination luminance pixels.</param>
    public static void PackL16(ReadOnlySpan<float> source, Span<L16> destination)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref L16 destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> maximum = Vector512.Create(UShortMaximum);
            Vector512<float> redWeight = Vector512.Create(0.2126F);
            Vector512<float> greenWeight = Vector512.Create(0.7152F);
            Vector512<float> blueWeight = Vector512.Create(0.0722F);
            Vector512<float> roundingOffset = Vector512.Create(0.5F);
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<float> value = Vector512.Clamp(
                    Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref sourceBase, i)),
                    Vector512<float>.Zero,
                    Vector512<float>.One) * maximum;

                // L16 uses its BT.709 luminance expression even when all three source components are equal. Preserve
                // that exact arithmetic order so the SIMD path remains byte-identical to L16.FromScaledVector4.
                Vector512<float> luminance = ((value * redWeight) + (value * greenWeight)) + (value * blueWeight);
                Vector512<int> samples = Vector512.ConvertToInt32(luminance + roundingOffset);
                Vector256<ushort> packed = Vector256.Narrow(samples.GetLower().AsUInt32(), samples.GetUpper().AsUInt32());
                packed.StoreUnsafe(ref Unsafe.As<L16, ushort>(ref Unsafe.Add(ref destinationBase, i)));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> maximum = Vector256.Create(UShortMaximum);
            Vector256<float> redWeight = Vector256.Create(0.2126F);
            Vector256<float> greenWeight = Vector256.Create(0.7152F);
            Vector256<float> blueWeight = Vector256.Create(0.0722F);
            Vector256<float> roundingOffset = Vector256.Create(0.5F);
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<float> value = Vector256.Clamp(
                    Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref sourceBase, i)),
                    Vector256<float>.Zero,
                    Vector256<float>.One) * maximum;

                Vector256<float> luminance = ((value * redWeight) + (value * greenWeight)) + (value * blueWeight);
                Vector256<int> samples = Vector256.ConvertToInt32(luminance + roundingOffset);
                Vector128<ushort> packed = Vector128.Narrow(samples.GetLower().AsUInt32(), samples.GetUpper().AsUInt32());
                packed.StoreUnsafe(ref Unsafe.As<L16, ushort>(ref Unsafe.Add(ref destinationBase, i)));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> maximum = Vector128.Create(UShortMaximum);
            Vector128<float> redWeight = Vector128.Create(0.2126F);
            Vector128<float> greenWeight = Vector128.Create(0.7152F);
            Vector128<float> blueWeight = Vector128.Create(0.0722F);
            Vector128<float> roundingOffset = Vector128.Create(0.5F);
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                Vector128<float> value = Vector128.Clamp(
                    Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref sourceBase, i)),
                    Vector128<float>.Zero,
                    Vector128<float>.One) * maximum;

                Vector128<float> luminance = ((value * redWeight) + (value * greenWeight)) + (value * blueWeight);
                Vector128<int> samples = Vector128.ConvertToInt32(luminance + roundingOffset);
                Vector64<ushort> packed = Vector128.Narrow(samples.AsUInt32(), Vector128<uint>.Zero).GetLower();
                packed.StoreUnsafe(ref Unsafe.As<L16, ushort>(ref Unsafe.Add(ref destinationBase, i)));
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = L16.FromScaledVector4(new Vector4(Unsafe.Add(ref sourceBase, i)));
        }
    }

    /// <summary>
    /// Reads an eight-bit or 16-bit unsigned sample without an intermediate conversion buffer.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <param name="source">The source samples.</param>
    /// <param name="index">The zero-based sample index.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetSample<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged
    {
        ref TSample sample = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), index);
        return typeof(TSample) == typeof(byte)
            ? Unsafe.As<TSample, byte>(ref sample)
            : Unsafe.As<TSample, ushort>(ref sample);
    }

    /// <summary>
    /// Rounds and clamps a conversion result to the encoded sample range.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <param name="value">The conversion result.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    /// <returns>The bounded sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSample ToSample<TSample>(float value, float maximum)
        where TSample : unmanaged
    {
        int sample = Numerics.Clamp((int)MathF.Round(value, MidpointRounding.AwayFromZero), 0, (int)maximum);
        if (typeof(TSample) == typeof(byte))
        {
            byte result = (byte)sample;
            return Unsafe.As<byte, TSample>(ref result);
        }

        ushort highBitDepthResult = (ushort)sample;
        return Unsafe.As<ushort, TSample>(ref highBitDepthResult);
    }

    /// <summary>
    /// Applies the encoded component range and converts four lanes to bounded integer samples.
    /// </summary>
    /// <param name="value">The normalized component values.</param>
    /// <param name="scale">The encoded range scale.</param>
    /// <param name="bias">The encoded range bias.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    /// <returns>The bounded integer samples.</returns>
    private static Vector128<int> ScaleBiasRoundAndClampToInt32(Vector128<float> value, float scale, float bias, float maximum)
    {
        Vector128<float> encoded = (value * Vector128.Create(scale)) + Vector128.Create(bias);
        Vector128<float> bounded = Vector128.Clamp(encoded, Vector128<float>.Zero, Vector128.Create(maximum));
        return Vector128.ConvertToInt32(Vector128.Round(bounded, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Applies the encoded component range and converts eight lanes to bounded integer samples.
    /// </summary>
    /// <param name="value">The normalized component values.</param>
    /// <param name="scale">The encoded range scale.</param>
    /// <param name="bias">The encoded range bias.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    /// <returns>The bounded integer samples.</returns>
    private static Vector256<int> ScaleBiasRoundAndClampToInt32(Vector256<float> value, float scale, float bias, float maximum)
    {
        Vector256<float> encoded = (value * Vector256.Create(scale)) + Vector256.Create(bias);
        Vector256<float> bounded = Vector256.Clamp(encoded, Vector256<float>.Zero, Vector256.Create(maximum));
        return Vector256.ConvertToInt32(Vector256.Round(bounded, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Applies the encoded component range and converts sixteen lanes to bounded integer samples.
    /// </summary>
    /// <param name="value">The normalized component values.</param>
    /// <param name="scale">The encoded range scale.</param>
    /// <param name="bias">The encoded range bias.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    /// <returns>The bounded integer samples.</returns>
    private static Vector512<int> ScaleBiasRoundAndClampToInt32(Vector512<float> value, float scale, float bias, float maximum)
    {
        Vector512<float> encoded = (value * Vector512.Create(scale)) + Vector512.Create(bias);
        Vector512<float> bounded = Vector512.Clamp(encoded, Vector512<float>.Zero, Vector512.Create(maximum));
        return Vector512.ConvertToInt32(Vector512.Round(bounded, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Scales, rounds, and clamps four normalized components to integer storage values.
    /// </summary>
    /// <param name="value">The normalized component values.</param>
    /// <param name="maximum">The largest storage value.</param>
    /// <returns>The bounded integer values.</returns>
    private static Vector128<int> ScaleRoundAndClampToInt32(Vector128<float> value, float maximum)
    {
        Vector128<float> scaled = value * Vector128.Create(maximum);
        Vector128<float> bounded = Vector128.Min(Vector128.Max(scaled, Vector128<float>.Zero), Vector128.Create(maximum));
        return Vector128.ConvertToInt32(Vector128.Round(bounded, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Scales, rounds, and clamps eight normalized components to integer storage values.
    /// </summary>
    /// <param name="value">The normalized component values.</param>
    /// <param name="maximum">The largest storage value.</param>
    /// <returns>The bounded integer values.</returns>
    private static Vector256<int> ScaleRoundAndClampToInt32(Vector256<float> value, float maximum)
    {
        Vector256<float> scaled = value * Vector256.Create(maximum);
        Vector256<float> bounded = Vector256.Min(Vector256.Max(scaled, Vector256<float>.Zero), Vector256.Create(maximum));
        return Vector256.ConvertToInt32(Vector256.Round(bounded, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Scales, rounds, and clamps sixteen normalized components to integer storage values.
    /// </summary>
    /// <param name="value">The normalized component values.</param>
    /// <param name="maximum">The largest storage value.</param>
    /// <returns>The bounded integer values.</returns>
    private static Vector512<int> ScaleRoundAndClampToInt32(Vector512<float> value, float maximum)
    {
        Vector512<float> scaled = value * Vector512.Create(maximum);
        Vector512<float> bounded = Vector512.Min(Vector512.Max(scaled, Vector512<float>.Zero), Vector512.Create(maximum));
        return Vector512.ConvertToInt32(Vector512.Round(bounded, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Interleaves four red, green, and blue integer lanes into four opaque 16-bit RGBA pixels.
    /// </summary>
    /// <param name="red">The red component values.</param>
    /// <param name="green">The green component values.</param>
    /// <param name="blue">The blue component values.</param>
    /// <param name="destination">The first destination pixel.</param>
    private static void StoreRgba64Batch(Vector128<int> red, Vector128<int> green, Vector128<int> blue, ref Rgba64 destination)
    {
        Vector128<ushort> red16 = Vector128.Narrow(red.AsUInt32(), Vector128<uint>.Zero);
        Vector128<ushort> green16 = Vector128.Narrow(green.AsUInt32(), Vector128<uint>.Zero);
        Vector128<ushort> blue16 = Vector128.Narrow(blue.AsUInt32(), Vector128<uint>.Zero);
        Vector128<ushort> alpha16 = Vector128.Create(ushort.MaxValue);
        Vector128<ushort> redGreen = Vector128_.UnpackLow(red16.AsInt16(), green16.AsInt16()).AsUInt16();
        Vector128<ushort> blueAlpha = Vector128_.UnpackLow(blue16.AsInt16(), alpha16.AsInt16()).AsUInt16();
        Vector128<uint> lower = Vector128_.UnpackLow(redGreen.AsInt32(), blueAlpha.AsInt32()).AsUInt32();
        Vector128<uint> upper = Vector128_.UnpackHigh(redGreen.AsInt32(), blueAlpha.AsInt32()).AsUInt32();
        Unsafe.As<Rgba64, Vector128<uint>>(ref destination) = lower;
        Unsafe.As<Rgba64, Vector128<uint>>(ref Unsafe.Add(ref destination, 2)) = upper;
    }
}

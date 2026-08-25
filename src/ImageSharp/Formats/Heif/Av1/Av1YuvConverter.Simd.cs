// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <content>
/// Provides SIMD sample widening, chroma reconstruction, color traversal, and packed output for AV1 decoding.
/// </content>
internal static partial class Av1YuvConverter
{
    /// <summary>
    /// Defines the SIMD widening operations for one reconstructed AV1 sample type.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    private interface ISampleLoader<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Loads and widens four samples to single-precision lanes.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <returns>The widened samples.</returns>
        public static abstract Vector128<float> LoadVector128(ref TSample source);

        /// <summary>
        /// Loads and widens eight samples to single-precision lanes.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <returns>The widened samples.</returns>
        public static abstract Vector256<float> LoadVector256(ref TSample source);

        /// <summary>
        /// Loads and widens sixteen samples to single-precision lanes.
        /// </summary>
        /// <param name="source">The first source sample.</param>
        /// <returns>The widened samples.</returns>
        public static abstract Vector512<float> LoadVector512(ref TSample source);
    }

    /// <summary>
    /// Widens reconstructed integer samples into a pooled float component row.
    /// </summary>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <typeparam name="TLoader">The widening operations for the sample type.</typeparam>
    /// <param name="source">The reconstructed samples.</param>
    /// <param name="destination">The destination component row.</param>
    private static void ConvertSamplesToFloat<TSample, TLoader>(ReadOnlySpan<TSample> source, Span<float> destination)
        where TSample : unmanaged
        where TLoader : struct, ISampleLoader<TSample>
    {
        ref TSample sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        // Descending vector widths match the JPEG color-converter traversal. A wide-capable CPU processes
        // complete wide batches first while short and irregular rows continue through narrower SIMD tails.
        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<float> samples = TLoader.LoadVector512(ref Unsafe.Add(ref sourceBase, i));
                Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref destinationBase, i)) = samples;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<float> samples = TLoader.LoadVector256(ref Unsafe.Add(ref sourceBase, i));
                Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref destinationBase, i)) = samples;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                Vector128<float> samples = TLoader.LoadVector128(ref Unsafe.Add(ref sourceBase, i));
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref destinationBase, i)) = samples;
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = GetSample(source, i);
        }
    }

    /// <summary>
    /// Reconstructs one full-width chroma row using the signaled vertical and horizontal sample positions.
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
    private static void ReconstructChromaRow<TSample, TLoader>(
        ReadOnlySpan<TSample> row0,
        ReadOnlySpan<TSample> row1,
        int y1Weight,
        int subX,
        bool isCenteredX,
        Span<float> destination,
        Span<float> scratch0,
        Span<float> scratch1)
        where TSample : unmanaged
        where TLoader : struct, ISampleLoader<TSample>
    {
        int sourceLength = subX == 0 ? destination.Length : (destination.Length + 1) >> 1;
        Span<float> top = scratch0[..sourceLength];
        ConvertSamplesToFloat<TSample, TLoader>(row0, top);

        if (y1Weight != 0)
        {
            Span<float> bottom = scratch1[..sourceLength];
            ConvertSamplesToFloat<TSample, TLoader>(row1, bottom);
            InterpolateChromaRows(top, bottom, y1Weight);
        }

        if (subX == 0)
        {
            top.CopyTo(destination);
            return;
        }

        UpsampleChromaHorizontal(top, destination, isCenteredX);
    }

    /// <summary>
    /// Interpolates two chroma rows in place using AV1 quarter-sample weights.
    /// </summary>
    /// <param name="top">The upper row, replaced by the interpolated values.</param>
    /// <param name="bottom">The lower row.</param>
    /// <param name="bottomWeight">The lower-row weight with a denominator of four.</param>
    private static void InterpolateChromaRows(Span<float> top, ReadOnlySpan<float> bottom, int bottomWeight)
    {
        ref float topBase = ref MemoryMarshal.GetReference(top);
        ref float bottomBase = ref MemoryMarshal.GetReference(bottom);
        int length = top.Length;
        int i = 0;
        float topWeight = 4 - bottomWeight;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> topWeightVector = Vector512.Create(topWeight);
            Vector512<float> bottomWeightVector = Vector512.Create((float)bottomWeight);
            Vector512<float> scale = Vector512.Create(0.25F);
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                ref Vector512<float> topVector = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref topBase, i));
                Vector512<float> bottomVector = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref bottomBase, i));
                topVector = Vector512.MultiplyAddEstimate(bottomWeightVector, bottomVector, topWeightVector * topVector) * scale;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> topWeightVector = Vector256.Create(topWeight);
            Vector256<float> bottomWeightVector = Vector256.Create((float)bottomWeight);
            Vector256<float> scale = Vector256.Create(0.25F);
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                ref Vector256<float> topVector = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref topBase, i));
                Vector256<float> bottomVector = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref bottomBase, i));
                topVector = Vector256.MultiplyAddEstimate(bottomWeightVector, bottomVector, topWeightVector * topVector) * scale;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> topWeightVector = Vector128.Create(topWeight);
            Vector128<float> bottomWeightVector = Vector128.Create((float)bottomWeight);
            Vector128<float> scale = Vector128.Create(0.25F);
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                ref Vector128<float> topVector = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref topBase, i));
                Vector128<float> bottomVector = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref bottomBase, i));
                topVector = Vector128.MultiplyAddEstimate(bottomWeightVector, bottomVector, topWeightVector * topVector) * scale;
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref topBase, i) = ((Unsafe.Add(ref topBase, i) * topWeight) + (Unsafe.Add(ref bottomBase, i) * bottomWeight)) * 0.25F;
        }
    }

    /// <summary>
    /// Expands horizontally subsampled chroma to luma width using the AV1 sample-position rules.
    /// </summary>
    /// <param name="source">The subsampled chroma values.</param>
    /// <param name="destination">The full-width chroma values.</param>
    /// <param name="isCentered">Whether chroma lies between neighboring luma samples.</param>
    private static void UpsampleChromaHorizontal(ReadOnlySpan<float> source, Span<float> destination, bool isCentered)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourceLength = source.Length;
        int i = 0;

        if (isCentered)
        {
            // The first centered pair extends the left edge. Interior vectors can then read one real neighbor
            // on each side and use the exact [1,3]/4 and [3,1]/4 AV1 interpolation weights.
            StoreChromaPair(ref destinationBase, 0, source[0], ((3F * source[0]) + source[Math.Min(1, sourceLength - 1)]) * 0.25F, destination.Length);
            i = 1;
        }

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorBeforeEnd = sourceLength - Vector512<float>.Count - 1;
            Vector512<float> quarter = Vector512.Create(0.25F);
            Vector512<float> half = Vector512.Create(0.5F);
            Vector512<float> three = Vector512.Create(3F);
            for (; i <= oneVectorBeforeEnd; i += Vector512<float>.Count)
            {
                Vector512<float> center = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref sourceBase, i));
                Vector512<float> next = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref sourceBase, i + 1));
                Vector512<float> even;
                Vector512<float> odd;
                if (isCentered)
                {
                    Vector512<float> previous = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref sourceBase, i - 1));
                    even = Vector512.MultiplyAddEstimate(three, center, previous) * quarter;
                    odd = Vector512.MultiplyAddEstimate(three, center, next) * quarter;
                }
                else
                {
                    even = center;
                    odd = (center + next) * half;
                }

                // Vector512 has no cross-platform unpack helper. The interpolation remains 512-bit; four
                // established Vector128 unpack operations only transpose the final even/odd lanes for storage.
                StoreInterleavedChroma(even.GetLower().GetLower(), odd.GetLower().GetLower(), ref Unsafe.Add(ref destinationBase, i * 2));
                StoreInterleavedChroma(even.GetLower().GetUpper(), odd.GetLower().GetUpper(), ref Unsafe.Add(ref destinationBase, (i * 2) + 8));
                StoreInterleavedChroma(even.GetUpper().GetLower(), odd.GetUpper().GetLower(), ref Unsafe.Add(ref destinationBase, (i * 2) + 16));
                StoreInterleavedChroma(even.GetUpper().GetUpper(), odd.GetUpper().GetUpper(), ref Unsafe.Add(ref destinationBase, (i * 2) + 24));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorBeforeEnd = sourceLength - Vector256<float>.Count - 1;
            Vector256<float> quarter = Vector256.Create(0.25F);
            Vector256<float> half = Vector256.Create(0.5F);
            Vector256<float> three = Vector256.Create(3F);
            for (; i <= oneVectorBeforeEnd; i += Vector256<float>.Count)
            {
                Vector256<float> center = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref sourceBase, i));
                Vector256<float> next = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref sourceBase, i + 1));
                Vector256<float> even;
                Vector256<float> odd;
                if (isCentered)
                {
                    Vector256<float> previous = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref sourceBase, i - 1));
                    even = Vector256.MultiplyAddEstimate(three, center, previous) * quarter;
                    odd = Vector256.MultiplyAddEstimate(three, center, next) * quarter;
                }
                else
                {
                    even = center;
                    odd = (center + next) * half;
                }

                StoreInterleavedChroma(even.GetLower(), odd.GetLower(), ref Unsafe.Add(ref destinationBase, i * 2));
                StoreInterleavedChroma(even.GetUpper(), odd.GetUpper(), ref Unsafe.Add(ref destinationBase, (i * 2) + 8));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorBeforeEnd = sourceLength - Vector128<float>.Count - 1;
            Vector128<float> quarter = Vector128.Create(0.25F);
            Vector128<float> half = Vector128.Create(0.5F);
            Vector128<float> three = Vector128.Create(3F);
            for (; i <= oneVectorBeforeEnd; i += Vector128<float>.Count)
            {
                Vector128<float> center = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref sourceBase, i));
                Vector128<float> next = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref sourceBase, i + 1));
                Vector128<float> even;
                Vector128<float> odd;
                if (isCentered)
                {
                    Vector128<float> previous = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref sourceBase, i - 1));
                    even = Vector128.MultiplyAddEstimate(three, center, previous) * quarter;
                    odd = Vector128.MultiplyAddEstimate(three, center, next) * quarter;
                }
                else
                {
                    even = center;
                    odd = (center + next) * half;
                }

                StoreInterleavedChroma(even, odd, ref Unsafe.Add(ref destinationBase, i * 2));
            }
        }

        for (; i < sourceLength; i++)
        {
            float center = source[i];
            float next = source[Math.Min(i + 1, sourceLength - 1)];
            float even = isCentered ? (source[Math.Max(i - 1, 0)] + (3F * center)) * 0.25F : center;
            float odd = isCentered ? ((3F * center) + next) * 0.25F : (center + next) * 0.5F;
            StoreChromaPair(ref destinationBase, i * 2, even, odd, destination.Length);
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
    /// Dispatches one normalized component row to its matrix-specific scalar and SIMD operator.
    /// </summary>
    /// <param name="red">The luma row, replaced by red.</param>
    /// <param name="green">The blue-difference row, replaced by green.</param>
    /// <param name="blue">The red-difference row, replaced by blue.</param>
    /// <param name="isMonochrome">Whether the frame contains only luma samples.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    private static void ConvertYuvToRgbRow(
        Span<float> red,
        Span<float> green,
        Span<float> blue,
        bool isMonochrome,
        ConversionMode mode,
        in YuvToRgbParameters parameters)
    {
        if (isMonochrome)
        {
            ConvertYuvToRgbRow<MonochromeOperator>(red, green, blue, in parameters);
            return;
        }

        switch (mode)
        {
            case ConversionMode.Identity:
                ConvertYuvToRgbRow<IdentityOperator>(red, green, blue, in parameters);
                break;
            case ConversionMode.YCgCo:
                ConvertYuvToRgbRow<YCgCoOperator>(red, green, blue, in parameters);
                break;
            case ConversionMode.Smpte2085:
                ConvertYuvToRgbRow<Smpte2085Operator>(red, green, blue, in parameters);
                break;
            case ConversionMode.ConstantLuminance:
                ConvertYuvToRgbRow<ConstantLuminanceOperator>(red, green, blue, in parameters);
                break;
            case ConversionMode.ICtCp when parameters.TransferCharacteristics == OpenBitstreamUnit.ObuTransferCharacteristics.Hlg:
                ConvertYuvToRgbRow<ICtCpHlgOperator>(red, green, blue, in parameters);
                break;
            case ConversionMode.ICtCp:
                ConvertYuvToRgbRow<ICtCpOperator>(red, green, blue, in parameters);
                break;
            default:
                ConvertYuvToRgbRow<CoefficientsOperator>(red, green, blue, in parameters);
                break;
        }
    }

    /// <summary>
    /// Converts one component row using the selected static scalar and SIMD operator.
    /// </summary>
    /// <typeparam name="TOperator">The matrix-specific conversion operator.</typeparam>
    /// <param name="red">The luma row, replaced by red.</param>
    /// <param name="green">The blue-difference row, replaced by green.</param>
    /// <param name="blue">The red-difference row, replaced by blue.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    private static void ConvertYuvToRgbRow<TOperator>(Span<float> red, Span<float> green, Span<float> blue, in YuvToRgbParameters parameters)
        where TOperator : struct, IYuvToRgbOperator
    {
        ref float redBase = ref MemoryMarshal.GetReference(red);
        ref float greenBase = ref MemoryMarshal.GetReference(green);
        ref float blueBase = ref MemoryMarshal.GetReference(blue);
        int length = red.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> lumaBias = Vector512.Create(parameters.LumaBias);
            Vector512<float> inverseLumaScale = Vector512.Create(1F / parameters.LumaScale);
            Vector512<float> chromaBias = Vector512.Create(parameters.ChromaBias);
            Vector512<float> inverseChromaScale = Vector512.Create(1F / parameters.ChromaScale);
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                ref Vector512<float> redVector = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref redBase, i));
                Vector512<float> y = (redVector - lumaBias) * inverseLumaScale;
                Vector512<float> cb = default;
                Vector512<float> cr = default;
                if (TOperator.UsesChroma)
                {
                    cb = (Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref greenBase, i)) - chromaBias) * inverseChromaScale;
                    cr = (Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref blueBase, i)) - chromaBias) * inverseChromaScale;
                }

                TOperator.Convert(ref y, ref cb, ref cr, in parameters);
                redVector = y;
                Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref greenBase, i)) = cb;
                Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref blueBase, i)) = cr;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> lumaBias = Vector256.Create(parameters.LumaBias);
            Vector256<float> inverseLumaScale = Vector256.Create(1F / parameters.LumaScale);
            Vector256<float> chromaBias = Vector256.Create(parameters.ChromaBias);
            Vector256<float> inverseChromaScale = Vector256.Create(1F / parameters.ChromaScale);
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                ref Vector256<float> redVector = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref redBase, i));
                Vector256<float> y = (redVector - lumaBias) * inverseLumaScale;
                Vector256<float> cb = default;
                Vector256<float> cr = default;
                if (TOperator.UsesChroma)
                {
                    cb = (Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref greenBase, i)) - chromaBias) * inverseChromaScale;
                    cr = (Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref blueBase, i)) - chromaBias) * inverseChromaScale;
                }

                TOperator.Convert(ref y, ref cb, ref cr, in parameters);
                redVector = y;
                Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref greenBase, i)) = cb;
                Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref blueBase, i)) = cr;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> lumaBias = Vector128.Create(parameters.LumaBias);
            Vector128<float> inverseLumaScale = Vector128.Create(1F / parameters.LumaScale);
            Vector128<float> chromaBias = Vector128.Create(parameters.ChromaBias);
            Vector128<float> inverseChromaScale = Vector128.Create(1F / parameters.ChromaScale);
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                ref Vector128<float> redVector = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref redBase, i));
                Vector128<float> y = (redVector - lumaBias) * inverseLumaScale;
                Vector128<float> cb = default;
                Vector128<float> cr = default;
                if (TOperator.UsesChroma)
                {
                    cb = (Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref greenBase, i)) - chromaBias) * inverseChromaScale;
                    cr = (Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref blueBase, i)) - chromaBias) * inverseChromaScale;
                }

                TOperator.Convert(ref y, ref cb, ref cr, in parameters);
                redVector = y;
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref greenBase, i)) = cb;
                Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref blueBase, i)) = cr;
            }
        }

        for (; i < length; i++)
        {
            float y = (Unsafe.Add(ref redBase, i) - parameters.LumaBias) / parameters.LumaScale;
            float cb = TOperator.UsesChroma ? (Unsafe.Add(ref greenBase, i) - parameters.ChromaBias) / parameters.ChromaScale : 0F;
            float cr = TOperator.UsesChroma ? (Unsafe.Add(ref blueBase, i) - parameters.ChromaBias) / parameters.ChromaScale : 0F;
            TOperator.Convert(ref y, ref cb, ref cr, in parameters);
            Unsafe.Add(ref redBase, i) = y;
            Unsafe.Add(ref greenBase, i) = cb;
            Unsafe.Add(ref blueBase, i) = cr;
        }
    }

    /// <summary>
    /// Packs normalized RGB component rows into opaque eight-bit RGBA pixels.
    /// </summary>
    /// <param name="red">The normalized red components.</param>
    /// <param name="green">The normalized green components.</param>
    /// <param name="blue">The normalized blue components.</param>
    /// <param name="destination">The destination pixels.</param>
    private static void PackRgba32(ReadOnlySpan<float> red, ReadOnlySpan<float> green, ReadOnlySpan<float> blue, Span<Rgba32> destination)
    {
        ref float redBase = ref MemoryMarshal.GetReference(red);
        ref float greenBase = ref MemoryMarshal.GetReference(green);
        ref float blueBase = ref MemoryMarshal.GetReference(blue);
        ref Rgba32 destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = destination.Length;
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector512<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
            {
                Vector512<int> r = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref redBase, i)), ByteMaximum);
                Vector512<int> g = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref greenBase, i)), ByteMaximum);
                Vector512<int> b = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref blueBase, i)), ByteMaximum);
                StoreRgba32Batch(r.GetLower().GetLower(), g.GetLower().GetLower(), b.GetLower().GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreRgba32Batch(r.GetLower().GetUpper(), g.GetLower().GetUpper(), b.GetLower().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 4));
                StoreRgba32Batch(r.GetUpper().GetLower(), g.GetUpper().GetLower(), b.GetUpper().GetLower(), ref Unsafe.Add(ref destinationBase, i + 8));
                StoreRgba32Batch(r.GetUpper().GetUpper(), g.GetUpper().GetUpper(), b.GetUpper().GetUpper(), ref Unsafe.Add(ref destinationBase, i + 12));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector256<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
            {
                Vector256<int> r = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref redBase, i)), ByteMaximum);
                Vector256<int> g = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref greenBase, i)), ByteMaximum);
                Vector256<int> b = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref blueBase, i)), ByteMaximum);
                StoreRgba32Batch(r.GetLower(), g.GetLower(), b.GetLower(), ref Unsafe.Add(ref destinationBase, i));
                StoreRgba32Batch(r.GetUpper(), g.GetUpper(), b.GetUpper(), ref Unsafe.Add(ref destinationBase, i + 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = length - Vector128<float>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                Vector128<int> r = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref redBase, i)), ByteMaximum);
                Vector128<int> g = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref greenBase, i)), ByteMaximum);
                Vector128<int> b = ScaleRoundAndClampToInt32(Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref blueBase, i)), ByteMaximum);
                StoreRgba32Batch(r, g, b, ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = new Rgba32(
                ToSample<byte>(Unsafe.Add(ref redBase, i) * ByteMaximum, ByteMaximum),
                ToSample<byte>(Unsafe.Add(ref greenBase, i) * ByteMaximum, ByteMaximum),
                ToSample<byte>(Unsafe.Add(ref blueBase, i) * ByteMaximum, ByteMaximum));
        }
    }

    /// <summary>
    /// Packs normalized RGB component rows into opaque 16-bit RGBA pixels.
    /// </summary>
    /// <param name="red">The normalized red components.</param>
    /// <param name="green">The normalized green components.</param>
    /// <param name="blue">The normalized blue components.</param>
    /// <param name="destination">The destination pixels.</param>
    private static void PackRgba64(ReadOnlySpan<float> red, ReadOnlySpan<float> green, ReadOnlySpan<float> blue, Span<Rgba64> destination)
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
    /// Interleaves four red, green, and blue integer lanes into four opaque eight-bit RGBA pixels.
    /// </summary>
    /// <param name="red">The red component values.</param>
    /// <param name="green">The green component values.</param>
    /// <param name="blue">The blue component values.</param>
    /// <param name="destination">The first destination pixel.</param>
    private static void StoreRgba32Batch(Vector128<int> red, Vector128<int> green, Vector128<int> blue, ref Rgba32 destination)
    {
        Vector128<byte> red8 = Vector128.Narrow(Vector128.Narrow(red.AsUInt32(), Vector128<uint>.Zero), Vector128<ushort>.Zero);
        Vector128<byte> green8 = Vector128.Narrow(Vector128.Narrow(green.AsUInt32(), Vector128<uint>.Zero), Vector128<ushort>.Zero);
        Vector128<byte> blue8 = Vector128.Narrow(Vector128.Narrow(blue.AsUInt32(), Vector128<uint>.Zero), Vector128<ushort>.Zero);
        Vector128<byte> alpha8 = Vector128.Create(byte.MaxValue);
        Vector128<byte> redGreen = Vector128_.UnpackLow(red8, green8);
        Vector128<byte> blueAlpha = Vector128_.UnpackLow(blue8, alpha8);

        // Interleaving the byte pairs as 16-bit lanes produces RGBA memory order on every supported
        // architecture without relying on the host integer endianness of Rgba32.PackedValue.
        Vector128<byte> rgba = Vector128_.UnpackLow(redGreen.AsInt16(), blueAlpha.AsInt16()).AsByte();
        Unsafe.As<Rgba32, Vector128<byte>>(ref destination) = rgba;
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

    /// <summary>
    /// Widens reconstructed eight-bit samples using exact unsigned conversions.
    /// </summary>
    private readonly struct ByteSampleLoader : ISampleLoader<byte>
    {
        /// <inheritdoc/>
        public static Vector128<float> LoadVector128(ref byte source)
        {
            uint packed = Unsafe.ReadUnaligned<uint>(ref source);
            Vector128<ushort> samples16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            return Vector128.ConvertToSingle(Vector128.WidenLower(samples16));
        }

        /// <inheritdoc/>
        public static Vector256<float> LoadVector256(ref byte source)
        {
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref source);
            Vector128<ushort> samples16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            Vector256<uint> samples32 = Vector256.Create(Vector128.WidenLower(samples16), Vector128.WidenUpper(samples16));
            return Vector256.ConvertToSingle(samples32);
        }

        /// <inheritdoc/>
        public static Vector512<float> LoadVector512(ref byte source)
        {
            Vector128<byte> packed = Unsafe.ReadUnaligned<Vector128<byte>>(ref source);
            (Vector128<ushort> lower16, Vector128<ushort> upper16) = Vector128.Widen(packed);
            Vector256<uint> lower32 = Vector256.Create(Vector128.WidenLower(lower16), Vector128.WidenUpper(lower16));
            Vector256<uint> upper32 = Vector256.Create(Vector128.WidenLower(upper16), Vector128.WidenUpper(upper16));
            return Vector512.ConvertToSingle(Vector512.Create(lower32, upper32));
        }
    }

    /// <summary>
    /// Widens reconstructed high-bit-depth samples using exact unsigned conversions.
    /// </summary>
    private readonly struct UShortSampleLoader : ISampleLoader<ushort>
    {
        /// <inheritdoc/>
        public static Vector128<float> LoadVector128(ref ushort source)
        {
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source));
            Vector128<ushort> samples16 = Vector128.CreateScalarUnsafe(packed).AsUInt16();
            return Vector128.ConvertToSingle(Vector128.WidenLower(samples16));
        }

        /// <inheritdoc/>
        public static Vector256<float> LoadVector256(ref ushort source)
        {
            Vector128<ushort> samples16 = Unsafe.ReadUnaligned<Vector128<ushort>>(ref Unsafe.As<ushort, byte>(ref source));
            Vector256<uint> samples32 = Vector256.Create(Vector128.WidenLower(samples16), Vector128.WidenUpper(samples16));
            return Vector256.ConvertToSingle(samples32);
        }

        /// <inheritdoc/>
        public static Vector512<float> LoadVector512(ref ushort source)
        {
            Vector256<ushort> samples16 = Unsafe.ReadUnaligned<Vector256<ushort>>(ref Unsafe.As<ushort, byte>(ref source));
            (Vector256<uint> lower32, Vector256<uint> upper32) = Vector256.Widen(samples16);
            return Vector512.ConvertToSingle(Vector512.Create(lower32, upper32));
        }
    }
}

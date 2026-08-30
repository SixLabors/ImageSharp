// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Defines closed high-bit-depth color operators and nearest-sample row traversal.
/// </content>
internal static partial class HeifYuvToRgb16Converter
{
    /// <summary>
    /// Defines source-precision RGB arithmetic for scalar and SIMD lanes.
    /// </summary>
    private interface IHeifYuvToRgb16Operator
    {
        /// <summary>
        /// Converts sixteen YCbCr samples to source-precision RGB lanes.
        /// </summary>
        /// <param name="y">The luma lanes.</param>
        /// <param name="cb">The blue-difference lanes.</param>
        /// <param name="cr">The red-difference lanes.</param>
        /// <param name="parameters">The image conversion parameters.</param>
        /// <param name="r">The converted red lanes.</param>
        /// <param name="g">The converted green lanes.</param>
        /// <param name="b">The converted blue lanes.</param>
        public static abstract void Convert(
            Vector512<int> y,
            Vector512<int> cb,
            Vector512<int> cr,
            in ConversionParameters parameters,
            out Vector512<int> r,
            out Vector512<int> g,
            out Vector512<int> b);

        /// <summary>
        /// Converts eight YCbCr samples to source-precision RGB lanes.
        /// </summary>
        /// <param name="y">The luma lanes.</param>
        /// <param name="cb">The blue-difference lanes.</param>
        /// <param name="cr">The red-difference lanes.</param>
        /// <param name="parameters">The image conversion parameters.</param>
        /// <param name="r">The converted red lanes.</param>
        /// <param name="g">The converted green lanes.</param>
        /// <param name="b">The converted blue lanes.</param>
        public static abstract void Convert(
            Vector256<int> y,
            Vector256<int> cb,
            Vector256<int> cr,
            in ConversionParameters parameters,
            out Vector256<int> r,
            out Vector256<int> g,
            out Vector256<int> b);

        /// <summary>
        /// Converts four YCbCr samples to source-precision RGB lanes.
        /// </summary>
        /// <param name="y">The luma lanes.</param>
        /// <param name="cb">The blue-difference lanes.</param>
        /// <param name="cr">The red-difference lanes.</param>
        /// <param name="parameters">The image conversion parameters.</param>
        /// <param name="r">The converted red lanes.</param>
        /// <param name="g">The converted green lanes.</param>
        /// <param name="b">The converted blue lanes.</param>
        public static abstract void Convert(
            Vector128<int> y,
            Vector128<int> cb,
            Vector128<int> cr,
            in ConversionParameters parameters,
            out Vector128<int> r,
            out Vector128<int> g,
            out Vector128<int> b);

        /// <summary>
        /// Converts one YCbCr sample to source-precision RGB.
        /// </summary>
        /// <param name="y">The luma sample.</param>
        /// <param name="cb">The blue-difference sample.</param>
        /// <param name="cr">The red-difference sample.</param>
        /// <param name="parameters">The image conversion parameters.</param>
        /// <param name="r">The converted red sample.</param>
        /// <param name="g">The converted green sample.</param>
        /// <param name="b">The converted blue sample.</param>
        public static abstract void Convert(
            ushort y,
            ushort cb,
            ushort cr,
            in ConversionParameters parameters,
            out int r,
            out int g,
            out int b);
    }

    /// <summary>
    /// Converts one luma row and its nearest native chroma row to planar 16-bit RGB storage.
    /// </summary>
    /// <typeparam name="TOperator">The source-precision color arithmetic selected for the row.</typeparam>
    /// <param name="luma">The full-resolution luma samples.</param>
    /// <param name="chromaBlue">The native blue-difference samples.</param>
    /// <param name="chromaRed">The native red-difference samples.</param>
    /// <param name="red">The destination red samples.</param>
    /// <param name="green">The destination green samples.</param>
    /// <param name="blue">The destination blue samples.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="parameters">The image conversion parameters.</param>
    private static void ConvertRow<TOperator>(
        ReadOnlySpan<ushort> luma,
        ReadOnlySpan<ushort> chromaBlue,
        ReadOnlySpan<ushort> chromaRed,
        Span<ushort> red,
        Span<ushort> green,
        Span<ushort> blue,
        int subsamplingX,
        in ConversionParameters parameters)
        where TOperator : struct, IHeifYuvToRgb16Operator
    {
        ref ushort lumaBase = ref MemoryMarshal.GetReference(luma);
        ref ushort chromaBlueBase = ref MemoryMarshal.GetReference(chromaBlue);
        ref ushort chromaRedBase = ref MemoryMarshal.GetReference(chromaRed);
        ref ushort redBase = ref MemoryMarshal.GetReference(red);
        ref ushort greenBase = ref MemoryMarshal.GetReference(green);
        ref ushort blueBase = ref MemoryMarshal.GetReference(blue);
        int outputLeftShift = parameters.Scalar.OutputLeftShift;
        int x = 0;

        // Each operator produces code values at the source precision. The traversal then left-aligns those values in
        // UInt16 storage, matching libheif's high-bit-depth RGB output without discarding low source bits.
        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = luma.Length - Vector512<int>.Count;

            for (; x <= oneVectorFromEnd; x += Vector512<int>.Count)
            {
                Vector512<int> y = LoadVector512(ref Unsafe.Add(ref lumaBase, x));
                Vector512<int> cb = subsamplingX == 0
                    ? LoadVector512(ref Unsafe.Add(ref chromaBlueBase, x))
                    : LoadRepeatedVector512(ref Unsafe.Add(ref chromaBlueBase, x >> 1));

                Vector512<int> cr = subsamplingX == 0
                    ? LoadVector512(ref Unsafe.Add(ref chromaRedBase, x))
                    : LoadRepeatedVector512(ref Unsafe.Add(ref chromaRedBase, x >> 1));

                TOperator.Convert(y, cb, cr, in parameters, out Vector512<int> r, out Vector512<int> g, out Vector512<int> b);
                HeifUShortSampleConverter.Store(r << outputLeftShift, ref Unsafe.Add(ref redBase, x));
                HeifUShortSampleConverter.Store(g << outputLeftShift, ref Unsafe.Add(ref greenBase, x));
                HeifUShortSampleConverter.Store(b << outputLeftShift, ref Unsafe.Add(ref blueBase, x));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = luma.Length - Vector256<int>.Count;

            for (; x <= oneVectorFromEnd; x += Vector256<int>.Count)
            {
                Vector256<int> y = LoadVector256(ref Unsafe.Add(ref lumaBase, x));
                Vector256<int> cb = subsamplingX == 0
                    ? LoadVector256(ref Unsafe.Add(ref chromaBlueBase, x))
                    : LoadRepeatedVector256(ref Unsafe.Add(ref chromaBlueBase, x >> 1));

                Vector256<int> cr = subsamplingX == 0
                    ? LoadVector256(ref Unsafe.Add(ref chromaRedBase, x))
                    : LoadRepeatedVector256(ref Unsafe.Add(ref chromaRedBase, x >> 1));

                TOperator.Convert(y, cb, cr, in parameters, out Vector256<int> r, out Vector256<int> g, out Vector256<int> b);
                HeifUShortSampleConverter.Store(r << outputLeftShift, ref Unsafe.Add(ref redBase, x));
                HeifUShortSampleConverter.Store(g << outputLeftShift, ref Unsafe.Add(ref greenBase, x));
                HeifUShortSampleConverter.Store(b << outputLeftShift, ref Unsafe.Add(ref blueBase, x));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = luma.Length - Vector128<int>.Count;

            for (; x <= oneVectorFromEnd; x += Vector128<int>.Count)
            {
                Vector128<int> y = LoadVector128(ref Unsafe.Add(ref lumaBase, x));
                Vector128<int> cb = subsamplingX == 0
                    ? LoadVector128(ref Unsafe.Add(ref chromaBlueBase, x))
                    : LoadRepeatedVector128(ref Unsafe.Add(ref chromaBlueBase, x >> 1));

                Vector128<int> cr = subsamplingX == 0
                    ? LoadVector128(ref Unsafe.Add(ref chromaRedBase, x))
                    : LoadRepeatedVector128(ref Unsafe.Add(ref chromaRedBase, x >> 1));

                TOperator.Convert(y, cb, cr, in parameters, out Vector128<int> r, out Vector128<int> g, out Vector128<int> b);
                HeifUShortSampleConverter.Store(r << outputLeftShift, ref Unsafe.Add(ref redBase, x));
                HeifUShortSampleConverter.Store(g << outputLeftShift, ref Unsafe.Add(ref greenBase, x));
                HeifUShortSampleConverter.Store(b << outputLeftShift, ref Unsafe.Add(ref blueBase, x));
            }
        }

        for (; x < luma.Length; x++)
        {
            TOperator.Convert(
                Unsafe.Add(ref lumaBase, x),
                Unsafe.Add(ref chromaBlueBase, x >> subsamplingX),
                Unsafe.Add(ref chromaRedBase, x >> subsamplingX),
                in parameters,
                out int r,
                out int g,
                out int b);

            Unsafe.Add(ref redBase, x) = (ushort)(r << outputLeftShift);
            Unsafe.Add(ref greenBase, x) = (ushort)(g << outputLeftShift);
            Unsafe.Add(ref blueBase, x) = (ushort)(b << outputLeftShift);
        }
    }

    /// <summary>
    /// Loads sixteen native samples as signed 32-bit SIMD lanes.
    /// </summary>
    /// <param name="source">The first native sample.</param>
    /// <returns>The widened sample lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> LoadVector512(ref ushort source)
    {
        (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref source));
        return Vector512.Create(lower, upper).AsInt32();
    }

    /// <summary>
    /// Loads eight native samples as signed 32-bit SIMD lanes.
    /// </summary>
    /// <param name="source">The first native sample.</param>
    /// <returns>The widened sample lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> LoadVector256(ref ushort source)
    {
        Vector128<ushort> samples = Vector128.LoadUnsafe(ref source);
        return Vector256.Create(Vector128.WidenLower(samples), Vector128.WidenUpper(samples)).AsInt32();
    }

    /// <summary>
    /// Loads four native samples as signed 32-bit SIMD lanes.
    /// </summary>
    /// <param name="source">The first native sample.</param>
    /// <returns>The widened sample lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> LoadVector128(ref ushort source)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source));
        return Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsUInt16()).AsInt32();
    }

    /// <summary>
    /// Loads eight chroma samples and repeats each sample into two of sixteen 32-bit SIMD lanes.
    /// </summary>
    /// <param name="source">The first native chroma sample.</param>
    /// <returns>The horizontally replicated chroma lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> LoadRepeatedVector512(ref ushort source)
    {
        Vector128<ushort> samples = Vector128.LoadUnsafe(ref source);
        Vector128<ushort> lower = Vector128_.UnpackLow(samples.AsInt16(), samples.AsInt16()).AsUInt16();
        Vector128<ushort> upper = Vector128_.UnpackHigh(samples.AsInt16(), samples.AsInt16()).AsUInt16();
        (Vector256<uint> widenedLower, Vector256<uint> widenedUpper) = Vector256.Widen(Vector256.Create(lower, upper));
        return Vector512.Create(widenedLower, widenedUpper).AsInt32();
    }

    /// <summary>
    /// Loads four chroma samples and repeats each sample into two of eight 32-bit SIMD lanes.
    /// </summary>
    /// <param name="source">The first native chroma sample.</param>
    /// <returns>The horizontally replicated chroma lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> LoadRepeatedVector256(ref ushort source)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source));
        Vector128<ushort> samples = Vector128.CreateScalarUnsafe(packed).AsUInt16();
        Vector128<ushort> repeated = Vector128_.UnpackLow(samples.AsInt16(), samples.AsInt16()).AsUInt16();
        return Vector256.Create(Vector128.WidenLower(repeated), Vector128.WidenUpper(repeated)).AsInt32();
    }

    /// <summary>
    /// Loads two chroma samples and repeats each sample into two of four 32-bit SIMD lanes.
    /// </summary>
    /// <param name="source">The first native chroma sample.</param>
    /// <returns>The horizontally replicated chroma lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> LoadRepeatedVector128(ref ushort source)
    {
        uint packed = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<ushort, byte>(ref source));
        Vector128<ushort> samples = Vector128.CreateScalarUnsafe(packed).AsUInt16();
        Vector128<ushort> repeated = Vector128_.UnpackLow(samples.AsInt16(), samples.AsInt16()).AsUInt16();
        return Vector128.WidenLower(repeated).AsInt32();
    }
}

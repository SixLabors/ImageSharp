// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    public static partial class HwIntrinsics
    {
        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        public static ushort LeadingZeroCount(ushort value)
            => (ushort)(BitOperations.LeadingZeroCount(value) - 16);

        /// <summary>
        /// Count the number of trailing zero bits in an integer value.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        public static ushort TrailingZeroCount(ushort value)
            => (ushort)(BitOperations.TrailingZeroCount(value << 16) - 16);

        /// <summary>
        /// <see cref="ByteToNormalizedFloat"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ByteToNormalizedFloatReduce(
            ref ReadOnlySpan<byte> source,
            ref Span<float> destination)
            => ByteToNormalizedFloatReduce<PixelFormats.PixelOperations.NoAlphaOperator>(ref source, ref destination);

        /// <summary>
        /// <see cref="ByteToNormalizedFloat{TAlpha}"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        /// <typeparam name="TAlpha">The alpha step that the conversion applies before each store.</typeparam>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        internal static void ByteToNormalizedFloatReduce<TAlpha>(
            ref ReadOnlySpan<byte> source,
            ref Span<float> destination)
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

            if (Vector128.IsHardwareAccelerated)
            {
                int remainder;
                if (Vector512.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector512<byte>.Count);
                }
                else if (Vector256.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector256<byte>.Count);
                }
                else
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector128<byte>.Count);
                }

                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    ByteToNormalizedFloat<TAlpha>(source[..adjustedCount], destination[..adjustedCount]);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Implementation <see cref="SimdUtils.ByteToNormalizedFloat"/>, which is faster on new RyuJIT runtime.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <remarks>
        /// Implementation is based on MagicScaler code:
        /// https://github.com/saucecontrol/PhotoSauce/blob/b5811908041200488aa18fdfd17df5fc457415dc/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L80-L182
        /// </remarks>
        internal static void ByteToNormalizedFloat(
            ReadOnlySpan<byte> source,
            Span<float> destination)
            => ByteToNormalizedFloat<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination);

        /// <summary>
        /// Converts bytes to floats in [0, 1], and applies <typeparamref name="TAlpha"/> to the floats before each store.
        /// </summary>
        /// <remarks>
        /// Each float vector holds whole RGBA groups, because the vector widths are multiples of 4 floats and each step starts at
        /// a multiple of 64, 32, or 16 bytes. Thus, an alpha step that reads RGBA groups is valid for RGBA pixel data.
        /// </remarks>
        /// <typeparam name="TAlpha">The alpha step that the conversion applies before each store.</typeparam>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        internal static void ByteToNormalizedFloat<TAlpha>(
            ReadOnlySpan<byte> source,
            Span<float> destination)
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            const double reciprocal = 1D / byte.MaxValue;
            const float reciprocalHigh = (float)reciprocal;
            const float reciprocalLow = (float)(reciprocal - reciprocalHigh);

            if (Vector512.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector512<byte>.Count);

                nuint n = destination.Vector512Count<byte>();

                ref byte sourceBase = ref MemoryMarshal.GetReference(source);
                ref Vector512<float> destinationBase = ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(destination));
                Vector512<float> high = Vector512.Create(reciprocalHigh);
                Vector512<float> low = Vector512.Create(reciprocalLow);

                for (nuint i = 0; i < n; i++)
                {
                    nuint si = (uint)Vector512<byte>.Count * i;

                    // Each 16-byte group widens to sixteen 32-bit lanes.
                    Vector512<int> i0 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si));
                    Vector512<int> i1 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si + (nuint)Vector512<int>.Count));
                    Vector512<int> i2 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si + (nuint)(Vector512<int>.Count * 2)));
                    Vector512<int> i3 = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, si + (nuint)(Vector512<int>.Count * 3)));

                    Vector512<float> f0 = Vector512.ConvertToSingle(i0);
                    Vector512<float> f1 = Vector512.ConvertToSingle(i1);
                    Vector512<float> f2 = Vector512.ConvertToSingle(i2);
                    Vector512<float> f3 = Vector512.ConvertToSingle(i3);

                    // The residual term restores the correctly rounded byte / 255F result without paying for vector division.
                    f0 = Vector512.FusedMultiplyAdd(f0, high, f0 * low);
                    f1 = Vector512.FusedMultiplyAdd(f1, high, f1 * low);
                    f2 = Vector512.FusedMultiplyAdd(f2, high, f2 * low);
                    f3 = Vector512.FusedMultiplyAdd(f3, high, f3 * low);

                    ref Vector512<float> d = ref Unsafe.Add(ref destinationBase, i * 4);

                    // The alpha step runs in registers, so an associated conversion needs no second pass.
                    d = TAlpha.Apply(f0);
                    Unsafe.Add(ref d, 1) = TAlpha.Apply(f1);
                    Unsafe.Add(ref d, 2) = TAlpha.Apply(f2);
                    Unsafe.Add(ref d, 3) = TAlpha.Apply(f3);
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector256<byte>.Count);

                nuint n = destination.Vector256Count<byte>();

                ref byte sourceBase = ref MemoryMarshal.GetReference(source);
                ref Vector256<float> destinationBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(destination));
                Vector256<float> high = Vector256.Create(reciprocalHigh);
                Vector256<float> low = Vector256.Create(reciprocalLow);

                for (nuint i = 0; i < n; i++)
                {
                    nuint si = (uint)Vector256<byte>.Count * i;

                    // Each 8-byte group widens to eight 32-bit lanes. The 8-byte loads do not read past the source.
                    Vector256<int> i0 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si));
                    Vector256<int> i1 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si + (nuint)Vector256<int>.Count));
                    Vector256<int> i2 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si + (nuint)(Vector256<int>.Count * 2)));
                    Vector256<int> i3 = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, si + (nuint)(Vector256<int>.Count * 3)));

                    Vector256<float> f0 = Vector256.ConvertToSingle(i0);
                    Vector256<float> f1 = Vector256.ConvertToSingle(i1);
                    Vector256<float> f2 = Vector256.ConvertToSingle(i2);
                    Vector256<float> f3 = Vector256.ConvertToSingle(i3);

                    f0 = Vector256.FusedMultiplyAdd(f0, high, f0 * low);
                    f1 = Vector256.FusedMultiplyAdd(f1, high, f1 * low);
                    f2 = Vector256.FusedMultiplyAdd(f2, high, f2 * low);
                    f3 = Vector256.FusedMultiplyAdd(f3, high, f3 * low);

                    ref Vector256<float> d = ref Unsafe.Add(ref destinationBase, i * 4);

                    // The alpha step runs in registers, so an associated conversion needs no second pass.
                    d = TAlpha.Apply(f0);
                    Unsafe.Add(ref d, 1) = TAlpha.Apply(f1);
                    Unsafe.Add(ref d, 2) = TAlpha.Apply(f2);
                    Unsafe.Add(ref d, 3) = TAlpha.Apply(f3);
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector128<byte>.Count);

                nuint n = destination.Vector128Count<byte>();

                ref byte sourceBase = ref MemoryMarshal.GetReference(source);
                ref Vector128<float> destinationBase = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(destination));

                Vector128<float> high = Vector128.Create(reciprocalHigh);
                Vector128<float> low = Vector128.Create(reciprocalLow);

                for (nuint i = 0; i < n; i++)
                {
                    nuint si = (uint)Vector128<byte>.Count * i;

                    // The 16 bytes widen to four vectors of four 32-bit lanes.
                    Vector128_.Widen(
                        Vector128.LoadUnsafe(ref sourceBase, si),
                        out Vector128<int> i0,
                        out Vector128<int> i1,
                        out Vector128<int> i2,
                        out Vector128<int> i3);

                    Vector128<float> f0 = Vector128.ConvertToSingle(i0);
                    Vector128<float> f1 = Vector128.ConvertToSingle(i1);
                    Vector128<float> f2 = Vector128.ConvertToSingle(i2);
                    Vector128<float> f3 = Vector128.ConvertToSingle(i3);

                    f0 = Vector128.FusedMultiplyAdd(f0, high, f0 * low);
                    f1 = Vector128.FusedMultiplyAdd(f1, high, f1 * low);
                    f2 = Vector128.FusedMultiplyAdd(f2, high, f2 * low);
                    f3 = Vector128.FusedMultiplyAdd(f3, high, f3 * low);

                    ref Vector128<float> d = ref Unsafe.Add(ref destinationBase, i * 4);

                    // The alpha step runs in registers, so an associated conversion needs no second pass.
                    d = TAlpha.Apply(f0);
                    Unsafe.Add(ref d, 1) = TAlpha.Apply(f1);
                    Unsafe.Add(ref d, 2) = TAlpha.Apply(f2);
                    Unsafe.Add(ref d, 3) = TAlpha.Apply(f3);
                }
            }
        }

        /// <summary>
        /// <see cref="NormalizedFloatToByteSaturate"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void NormalizedFloatToByteSaturateReduce(
            ref ReadOnlySpan<float> source,
            ref Span<byte> destination)
            => FloatToByteSaturateReduce(ref source, ref destination, byte.MaxValue);

        /// <summary>
        /// Converts as many scaled floating-point values as possible to bytes and retains the unconverted remainder.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="scaleFactor">The factor applied before conversion.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void FloatToByteSaturateReduce(
            ref ReadOnlySpan<float> source,
            ref Span<byte> destination,
            float scaleFactor)
            => FloatToByteSaturateReduce<PixelFormats.PixelOperations.NoAlphaOperator>(ref source, ref destination, scaleFactor);

        /// <summary>
        /// Converts as many scaled floating-point values as possible to bytes with <see cref="FloatToByteSaturate{TAlpha}"/>,
        /// and retains the unconverted remainder.
        /// </summary>
        /// <typeparam name="TAlpha">The alpha step that the conversion applies before the scale.</typeparam>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="scaleFactor">The factor applied before conversion.</param>
        internal static void FloatToByteSaturateReduce<TAlpha>(
            ref ReadOnlySpan<float> source,
            ref Span<byte> destination,
            float scaleFactor)
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");

            if (Vector128.IsHardwareAccelerated)
            {
                // One step of FloatToByteSaturate makes one byte vector of the widest accelerated width.
                int remainder;
                if (Vector512.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector512<byte>.Count);
                }
                else if (Vector256.IsHardwareAccelerated)
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector256<byte>.Count);
                }
                else
                {
                    remainder = Numerics.ModuloP2(source.Length, Vector128<byte>.Count);
                }

                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    FloatToByteSaturate<TAlpha>(
                        source[..adjustedCount],
                        destination[..adjustedCount],
                        scaleFactor);

                    source = source[adjustedCount..];
                    destination = destination[adjustedCount..];
                }
            }
        }

        /// <summary>
        /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/>, which is faster on new .NET runtime.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <remarks>
        /// Implementation is based on MagicScaler code:
        /// https://github.com/saucecontrol/PhotoSauce/blob/b5811908041200488aa18fdfd17df5fc457415dc/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L541-L622
        /// </remarks>
        internal static void NormalizedFloatToByteSaturate(
            ReadOnlySpan<float> source,
            Span<byte> destination)
            => FloatToByteSaturate(source, destination, byte.MaxValue);

        /// <summary>
        /// Converts scaled floating-point values to bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <param name="source">
        /// The source buffer. Its length is a multiple of the byte vector length of the widest accelerated width.
        /// </param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="scaleFactor">The factor applied before conversion.</param>
        internal static void FloatToByteSaturate(
            ReadOnlySpan<float> source,
            Span<byte> destination,
            float scaleFactor)
            => FloatToByteSaturate<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination, scaleFactor);

        /// <summary>
        /// Converts scaled floating-point values to bytes, and applies <typeparamref name="TAlpha"/> to the floats before the
        /// scale. Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each float vector holds whole RGBA groups, because the vector widths are multiples of 4 floats and each step starts at
        /// a multiple of 64, 32, or 16 floats. Thus, an alpha step that reads RGBA groups is valid for RGBA pixel data.
        /// </remarks>
        /// <typeparam name="TAlpha">The alpha step that the conversion applies before the scale.</typeparam>
        /// <param name="source">
        /// The source buffer. Its length is a multiple of the byte vector length of the widest accelerated width.
        /// </param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="scaleFactor">The factor applied before conversion.</param>
        internal static void FloatToByteSaturate<TAlpha>(
            ReadOnlySpan<float> source,
            Span<byte> destination,
            float scaleFactor)
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            // The source can hold infinity, NaN, or values outside the int range. The clamp maps NaN to 0 and the
            // other values into [0, 255 / scaleFactor]. Then all values are finite. The vector conversion requires finite values.
            if (Vector512.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector512<byte>.Count);

                nuint n = destination.Vector512Count<byte>();

                ref Vector512<float> sourceBase = ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector512<byte> destinationBase = ref Unsafe.As<byte, Vector512<byte>>(ref MemoryMarshal.GetReference(destination));

                Vector512<float> scale = Vector512.Create(scaleFactor);
                Vector512<float> upperBound = Vector512.Create(byte.MaxValue / scaleFactor);

                for (nuint i = 0; i < n; i++)
                {
                    // Four float vectors give the 64 bytes of one destination vector.
                    ref Vector512<float> s = ref Unsafe.Add(ref sourceBase, i * 4);
                    Unsafe.Add(ref destinationBase, i) = FloatToByteSaturate(
                        ColorNumerics.ClampToFinite(TAlpha.Apply(s), Vector512<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 1)), Vector512<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 2)), Vector512<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 3)), Vector512<float>.Zero, upperBound),
                        scale);
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector256<byte>.Count);

                nuint n = destination.Vector256Count<byte>();

                ref Vector256<float> sourceBase = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector256<byte> destinationBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(destination));

                Vector256<float> scale = Vector256.Create(scaleFactor);
                Vector256<float> upperBound = Vector256.Create(byte.MaxValue / scaleFactor);

                for (nuint i = 0; i < n; i++)
                {
                    // Four float vectors give the 32 bytes of one destination vector.
                    ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);
                    Unsafe.Add(ref destinationBase, i) = FloatToByteSaturate(
                        ColorNumerics.ClampToFinite(TAlpha.Apply(s), Vector256<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 1)), Vector256<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 2)), Vector256<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 3)), Vector256<float>.Zero, upperBound),
                        scale);
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                DebugVerifySpanInput(source, destination, Vector128<byte>.Count);

                nuint n = destination.Vector128Count<byte>();

                ref Vector128<float> sourceBase = ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector128<byte> destinationBase = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(destination));

                Vector128<float> scale = Vector128.Create(scaleFactor);
                Vector128<float> upperBound = Vector128.Create(byte.MaxValue / scaleFactor);

                for (nuint i = 0; i < n; i++)
                {
                    // Four float vectors give the 16 bytes of one destination vector.
                    ref Vector128<float> s = ref Unsafe.Add(ref sourceBase, i * 4);
                    Unsafe.Add(ref destinationBase, i) = FloatToByteSaturate(
                        ColorNumerics.ClampToFinite(TAlpha.Apply(s), Vector128<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 1)), Vector128<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 2)), Vector128<float>.Zero, upperBound),
                        ColorNumerics.ClampToFinite(TAlpha.Apply(Unsafe.Add(ref s, 3)), Vector128<float>.Zero, upperBound),
                        scale);
                }
            }
        }

        /// <summary>
        /// Converts four vectors of finite scaled floating-point values to one vector of bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each value must be finite, and each scaled value plus one half must be in the <see cref="int"/> range.
        /// The span overload and the JPEG color converters meet this condition. A scaled value outside [0, 255] saturates to 0 or 255.
        /// </remarks>
        /// <param name="f0">The values for bytes 0 to 15.</param>
        /// <param name="f1">The values for bytes 16 to 31.</param>
        /// <param name="f2">The values for bytes 32 to 47.</param>
        /// <param name="f3">The values for bytes 48 to 63.</param>
        /// <param name="scale">The factor applied before conversion.</param>
        /// <returns>The 64 bytes in source order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> FloatToByteSaturate(
            Vector512<float> f0,
            Vector512<float> f1,
            Vector512<float> f2,
            Vector512<float> f3,
            Vector512<float> scale)
        {
            // Each lane is truncate(x * scale + 0.5), the rule of the scalar remainder in SimdUtils. The native
            // conversion truncates toward zero on every platform, so all widths and platforms give the same bytes.
            // The narrowing saturates negative results to 0 and results above 255 to 255.
            Vector512<float> half = Vector512.Create(.5F);
            return Vector512_.NarrowToByteWithSaturation(
                Vector512.ConvertToInt32Native((f0 * scale) + half),
                Vector512.ConvertToInt32Native((f1 * scale) + half),
                Vector512.ConvertToInt32Native((f2 * scale) + half),
                Vector512.ConvertToInt32Native((f3 * scale) + half));
        }

        /// <summary>
        /// Converts four vectors of finite scaled floating-point values to one vector of bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each value must be finite, and each scaled value plus one half must be in the <see cref="int"/> range.
        /// The span overload and the JPEG color converters meet this condition. A scaled value outside [0, 255] saturates to 0 or 255.
        /// </remarks>
        /// <param name="f0">The values for bytes 0 to 7.</param>
        /// <param name="f1">The values for bytes 8 to 15.</param>
        /// <param name="f2">The values for bytes 16 to 23.</param>
        /// <param name="f3">The values for bytes 24 to 31.</param>
        /// <param name="scale">The factor applied before conversion.</param>
        /// <returns>The 32 bytes in source order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> FloatToByteSaturate(
            Vector256<float> f0,
            Vector256<float> f1,
            Vector256<float> f2,
            Vector256<float> f3,
            Vector256<float> scale)
        {
            // See the Vector512 overload.
            Vector256<float> half = Vector256.Create(.5F);
            return Vector256_.NarrowToByteWithSaturation(
                Vector256.ConvertToInt32Native((f0 * scale) + half),
                Vector256.ConvertToInt32Native((f1 * scale) + half),
                Vector256.ConvertToInt32Native((f2 * scale) + half),
                Vector256.ConvertToInt32Native((f3 * scale) + half));
        }

        /// <summary>
        /// Converts four vectors of finite scaled floating-point values to one vector of bytes.
        /// Each byte is the scaled value plus one half, truncated toward zero and saturated to [0, 255].
        /// </summary>
        /// <remarks>
        /// Each value must be finite, and each scaled value plus one half must be in the <see cref="int"/> range.
        /// The span overload and the JPEG color converters meet this condition. A scaled value outside [0, 255] saturates to 0 or 255.
        /// </remarks>
        /// <param name="f0">The values for bytes 0 to 3.</param>
        /// <param name="f1">The values for bytes 4 to 7.</param>
        /// <param name="f2">The values for bytes 8 to 11.</param>
        /// <param name="f3">The values for bytes 12 to 15.</param>
        /// <param name="scale">The factor applied before conversion.</param>
        /// <returns>The 16 bytes in source order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> FloatToByteSaturate(
            Vector128<float> f0,
            Vector128<float> f1,
            Vector128<float> f2,
            Vector128<float> f3,
            Vector128<float> scale)
        {
            // See the Vector512 overload.
            Vector128<float> half = Vector128.Create(.5F);
            return Vector128_.NarrowToByteWithSaturation(
                Vector128.ConvertToInt32Native((f0 * scale) + half),
                Vector128.ConvertToInt32Native((f1 * scale) + half),
                Vector128.ConvertToInt32Native((f2 * scale) + half),
                Vector128.ConvertToInt32Native((f3 * scale) + half));
        }
    }
}

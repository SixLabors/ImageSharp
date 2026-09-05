// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static partial class Av1IntraEdgeUpsampler
{
    /// <summary>
    /// Traverses a bounded edge using a closed interpolation operator.
    /// </summary>
    /// <typeparam name="TOperator">The four-tap interpolation arithmetic.</typeparam>
    private static class Upsampler<TOperator>
        where TOperator : struct, IEdgeUpsamplingOperator
    {
        /// <summary>
        /// Inserts half samples using the original edge values and repeated endpoints.
        /// </summary>
        /// <param name="edge">The edge with prefix and doubled output capacity.</param>
        /// <param name="count">The original sample count.</param>
        /// <param name="scratch">The reusable original-sample workspace.</param>
        public static void Apply(Span<byte> edge, int count, Span<byte> scratch)
        {
            ref byte destination = ref MemoryMarshal.GetReference(edge);
            ref byte source = ref MemoryMarshal.GetReference(scratch);

            // Preserve the corner twice and the final sample once. Every SIMD load below covers exactly its
            // input lanes, so the native 16+3-sample workspace also suffices for the widest interpolation.
            source = Unsafe.Subtract(ref destination, 1);
            Unsafe.Add(ref source, 1) = source;
            edge[..count].CopyTo(scratch[2..]);
            Unsafe.Add(ref source, count + 2) = edge[count - 1];
            Unsafe.Subtract(ref destination, 2) = source;
            ref byte firstOutput = ref Unsafe.Subtract(ref destination, 1);
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector512<int>.Count;
                for (; i <= vectorEnd; i += Vector512<int>.Count)
                {
                    Vector256<ushort> w0 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 0)), Vector128<byte>.Zero));

                    Vector512<int> s0 = Vector512.WidenLower(Vector512.Create(w0, Vector256<ushort>.Zero)).AsInt32();

                    Vector256<ushort> w1 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 1)), Vector128<byte>.Zero));

                    Vector512<int> s1 = Vector512.WidenLower(Vector512.Create(w1, Vector256<ushort>.Zero)).AsInt32();

                    Vector256<ushort> w2 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 2)), Vector128<byte>.Zero));

                    Vector512<int> s2 = Vector512.WidenLower(Vector512.Create(w2, Vector256<ushort>.Zero)).AsInt32();

                    Vector256<ushort> w3 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 3)), Vector128<byte>.Zero));

                    Vector512<int> s3 = Vector512.WidenLower(Vector512.Create(w3, Vector256<ushort>.Zero)).AsInt32();

                    Vector512<int> values = TOperator.Interpolate(s0, s1, s2, s3, 255);
                    Vector256<ushort> halfWords = Vector512.Narrow(values, Vector512<int>.Zero).GetLower().AsUInt16();
                    Vector128<byte> halfSamples = Vector256.Narrow(halfWords, Vector256<ushort>.Zero).GetLower();
                    Vector128<byte> originals = Vector128.LoadUnsafe(ref source, (nuint)(i + 2));

                    // Unpack the lower and upper eight pairs independently to retain linear sample order.
                    Vector128_.UnpackLow(halfSamples, originals).StoreUnsafe(ref firstOutput, (nuint)(2 * i));
                    Vector128_.UnpackHigh(halfSamples, originals).StoreUnsafe(ref firstOutput, (nuint)((2 * i) + 16));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector256<int>.Count;
                for (; i <= vectorEnd; i += Vector256<int>.Count)
                {
                    Vector128<ushort> w0 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 0)), Vector64<byte>.Zero));

                    Vector256<int> s0 = Vector256.WidenLower(Vector256.Create(w0, Vector128<ushort>.Zero)).AsInt32();

                    Vector128<ushort> w1 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 1)), Vector64<byte>.Zero));

                    Vector256<int> s1 = Vector256.WidenLower(Vector256.Create(w1, Vector128<ushort>.Zero)).AsInt32();

                    Vector128<ushort> w2 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 2)), Vector64<byte>.Zero));

                    Vector256<int> s2 = Vector256.WidenLower(Vector256.Create(w2, Vector128<ushort>.Zero)).AsInt32();

                    Vector128<ushort> w3 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 3)), Vector64<byte>.Zero));

                    Vector256<int> s3 = Vector256.WidenLower(Vector256.Create(w3, Vector128<ushort>.Zero)).AsInt32();

                    Vector256<int> values = TOperator.Interpolate(s0, s1, s2, s3, 255);
                    Vector128<ushort> halfWords = Vector256.Narrow(values, Vector256<int>.Zero).GetLower().AsUInt16();
                    Vector128<byte> halfSamples = Vector128.Narrow(halfWords, Vector128<ushort>.Zero);
                    Vector128<byte> originals = Vector128.Create(Vector64.LoadUnsafe(ref source, (nuint)(i + 2)), Vector64<byte>.Zero);
                    Vector128_.UnpackLow(halfSamples, originals).StoreUnsafe(ref firstOutput, (nuint)(2 * i));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector128<int>.Count;
                for (; i <= vectorEnd; i += Vector128<int>.Count)
                {
                    Vector128<byte> b0 = Vector128.CreateScalar(Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i + 0))).AsByte();
                    Vector128<ushort> w0 = Vector128.WidenLower(b0);
                    Vector128<int> s0 = Vector128.WidenLower(w0).AsInt32();

                    Vector128<byte> b1 = Vector128.CreateScalar(Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i + 1))).AsByte();
                    Vector128<ushort> w1 = Vector128.WidenLower(b1);
                    Vector128<int> s1 = Vector128.WidenLower(w1).AsInt32();

                    Vector128<byte> b2 = Vector128.CreateScalar(Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i + 2))).AsByte();
                    Vector128<ushort> w2 = Vector128.WidenLower(b2);
                    Vector128<int> s2 = Vector128.WidenLower(w2).AsInt32();

                    Vector128<byte> b3 = Vector128.CreateScalar(Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i + 3))).AsByte();
                    Vector128<ushort> w3 = Vector128.WidenLower(b3);
                    Vector128<int> s3 = Vector128.WidenLower(w3).AsInt32();

                    Vector128<int> values = TOperator.Interpolate(s0, s1, s2, s3, 255);
                    Vector128<byte> halfSamples = Vector128.Narrow(
                        Vector128.Narrow(values, Vector128<int>.Zero).AsUInt16(), Vector128<ushort>.Zero);

                    Vector128<byte> originals = Vector128.CreateScalar(Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i + 2))).AsByte();
                    Vector128_.UnpackLow(halfSamples, originals).GetLower().StoreUnsafe(ref firstOutput, (nuint)(2 * i));
                }
            }

            for (; i < count; i++)
            {
                int value = TOperator.Interpolate(
                    Unsafe.Add(ref source, i),
                    Unsafe.Add(ref source, i + 1),
                    Unsafe.Add(ref source, i + 2),
                    Unsafe.Add(ref source, i + 3),
                    255);

                Unsafe.Add(ref destination, (2 * i) - 1) = (byte)value;
                Unsafe.Add(ref destination, 2 * i) = Unsafe.Add(ref source, i + 2);
            }
        }

        /// <summary>
        /// Inserts half samples using the original edge values and repeated endpoints.
        /// </summary>
        /// <param name="edge">The edge with prefix and doubled output capacity.</param>
        /// <param name="count">The original sample count.</param>
        /// <param name="maximum">The maximum coded sample value.</param>
        /// <param name="scratch">The reusable original-sample workspace.</param>
        public static void Apply(Span<short> edge, int count, int maximum, Span<short> scratch)
        {
            ref short destination = ref MemoryMarshal.GetReference(edge);
            ref short source = ref MemoryMarshal.GetReference(scratch);

            // Preserve the corner twice and the final sample once. Every SIMD load below covers exactly its
            // input lanes, so the native 16+3-sample workspace also suffices for the widest interpolation.
            source = Unsafe.Subtract(ref destination, 1);
            Unsafe.Add(ref source, 1) = source;
            edge[..count].CopyTo(scratch[2..]);
            Unsafe.Add(ref source, count + 2) = edge[count - 1];
            Unsafe.Subtract(ref destination, 2) = source;
            ref short firstOutput = ref Unsafe.Subtract(ref destination, 1);
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector512<int>.Count;
                for (; i <= vectorEnd; i += Vector512<int>.Count)
                {
                    Vector512<int> s0 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 0)), Vector256<short>.Zero));

                    Vector512<int> s1 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 1)), Vector256<short>.Zero));

                    Vector512<int> s2 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 2)), Vector256<short>.Zero));

                    Vector512<int> s3 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 3)), Vector256<short>.Zero));

                    Vector512<int> values = TOperator.Interpolate(s0, s1, s2, s3, maximum);
                    Vector256<short> halfSamples = Vector512.Narrow(values, Vector512<int>.Zero).GetLower();
                    Vector256<short> originals = Vector256.LoadUnsafe(ref source, (nuint)(i + 2));

                    // Four contiguous groups of four pairs avoid treating lane-local unpack order as one
                    // linear 256-bit edge. Each store writes only prepared half samples and their originals.
                    Vector128_.UnpackLow(halfSamples.GetLower(), originals.GetLower())
                        .StoreUnsafe(ref firstOutput, (nuint)(2 * i));

                    Vector128_.UnpackHigh(halfSamples.GetLower(), originals.GetLower())
                        .StoreUnsafe(ref firstOutput, (nuint)((2 * i) + 8));

                    Vector128_.UnpackLow(halfSamples.GetUpper(), originals.GetUpper())
                        .StoreUnsafe(ref firstOutput, (nuint)((2 * i) + 16));

                    Vector128_.UnpackHigh(halfSamples.GetUpper(), originals.GetUpper())
                        .StoreUnsafe(ref firstOutput, (nuint)((2 * i) + 24));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector256<int>.Count;
                for (; i <= vectorEnd; i += Vector256<int>.Count)
                {
                    Vector256<int> s0 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 0)), Vector128<short>.Zero));

                    Vector256<int> s1 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 1)), Vector128<short>.Zero));

                    Vector256<int> s2 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 2)), Vector128<short>.Zero));

                    Vector256<int> s3 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 3)), Vector128<short>.Zero));

                    Vector256<int> values = TOperator.Interpolate(s0, s1, s2, s3, maximum);
                    Vector128<short> halfSamples = Vector256.Narrow(values, Vector256<int>.Zero).GetLower();
                    Vector128<short> originals = Vector128.LoadUnsafe(ref source, (nuint)(i + 2));
                    Vector128_.UnpackLow(halfSamples, originals).StoreUnsafe(ref firstOutput, (nuint)(2 * i));
                    Vector128_.UnpackHigh(halfSamples, originals).StoreUnsafe(ref firstOutput, (nuint)((2 * i) + 8));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = count - Vector128<int>.Count;
                for (; i <= vectorEnd; i += Vector128<int>.Count)
                {
                    Vector128<int> s0 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 0)), Vector64<short>.Zero));

                    Vector128<int> s1 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 1)), Vector64<short>.Zero));

                    Vector128<int> s2 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 2)), Vector64<short>.Zero));

                    Vector128<int> s3 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 3)), Vector64<short>.Zero));

                    Vector128<int> values = TOperator.Interpolate(s0, s1, s2, s3, maximum);
                    Vector128<short> halfSamples = Vector128.Narrow(values, Vector128<int>.Zero);
                    Vector128<short> originals = Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 2)), Vector64<short>.Zero);

                    Vector128_.UnpackLow(halfSamples, originals).StoreUnsafe(ref firstOutput, (nuint)(2 * i));
                }
            }

            for (; i < count; i++)
            {
                int value = TOperator.Interpolate(
                    Unsafe.Add(ref source, i),
                    Unsafe.Add(ref source, i + 1),
                    Unsafe.Add(ref source, i + 2),
                    Unsafe.Add(ref source, i + 3),
                    maximum);

                Unsafe.Add(ref destination, (2 * i) - 1) = (short)value;
                Unsafe.Add(ref destination, 2 * i) = Unsafe.Add(ref source, i + 2);
            }
        }
    }
}

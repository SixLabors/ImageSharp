// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static partial class Av1IntraEdgeFilter
{
    /// <summary>
    /// Traverses one edge using the arithmetic of a closed smoothing operator.
    /// </summary>
    /// <typeparam name="TOperator">The filter-strength arithmetic.</typeparam>
    private static class Filter<TOperator>
        where TOperator : struct, IEdgeFilterOperator
    {
        /// <summary>
        /// Filters all samples following the preserved first sample.
        /// </summary>
        /// <param name="edge">The first edge sample.</param>
        /// <param name="count">The number of samples including the preserved sample.</param>
        /// <param name="scratch">The reusable source workspace.</param>
        public static void Apply(ref byte edge, int count, Span<byte> scratch)
        {
            // Each convolution reads the original edge. Duplicate its first sample once and its last sample
            // twice so the five-tap windows implement endpoint clamping without per-lane boundary branches.
            scratch[0] = edge;
            MemoryMarshal.CreateReadOnlySpan(ref edge, count).CopyTo(scratch[1..]);
            scratch.Slice(count + 1, 2).Fill(Unsafe.Add(ref edge, count - 1));

            ref byte source = ref MemoryMarshal.GetReference(scratch);
            int outputCount = count - 1;
            int i = 0;

            // The same offset advances through descending SIMD widths. Adjacent lanes represent adjacent
            // output samples, and only complete windows are loaded; the final incomplete window is scalar.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = outputCount - Vector512<ushort>.Count;
                for (; i <= vectorEnd; i += Vector512<ushort>.Count)
                {
                    Vector512<ushort> s0 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 0)), Vector256<byte>.Zero));

                    Vector512<ushort> s1 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 1)), Vector256<byte>.Zero));

                    Vector512<ushort> s2 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 2)), Vector256<byte>.Zero));

                    Vector512<ushort> s3 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 3)), Vector256<byte>.Zero));

                    Vector512<ushort> s4 = Vector512.WidenLower(Vector512.Create(
                        Vector256.LoadUnsafe(ref source, (nuint)(i + 4)), Vector256<byte>.Zero));

                    Vector512<ushort> result = TOperator.Apply(s0, s1, s2, s3, s4);
                    Vector512.Narrow(result, Vector512<ushort>.Zero).GetLower().StoreUnsafe(ref edge, (nuint)(i + 1));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = outputCount - Vector256<ushort>.Count;
                for (; i <= vectorEnd; i += Vector256<ushort>.Count)
                {
                    Vector256<ushort> s0 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 0)), Vector128<byte>.Zero));

                    Vector256<ushort> s1 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 1)), Vector128<byte>.Zero));

                    Vector256<ushort> s2 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 2)), Vector128<byte>.Zero));

                    Vector256<ushort> s3 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 3)), Vector128<byte>.Zero));

                    Vector256<ushort> s4 = Vector256.WidenLower(Vector256.Create(
                        Vector128.LoadUnsafe(ref source, (nuint)(i + 4)), Vector128<byte>.Zero));

                    Vector256<ushort> result = TOperator.Apply(s0, s1, s2, s3, s4);
                    Vector256.Narrow(result, Vector256<ushort>.Zero).GetLower().StoreUnsafe(ref edge, (nuint)(i + 1));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = outputCount - Vector128<ushort>.Count;
                for (; i <= vectorEnd; i += Vector128<ushort>.Count)
                {
                    Vector128<ushort> s0 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 0)), Vector64<byte>.Zero));

                    Vector128<ushort> s1 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 1)), Vector64<byte>.Zero));

                    Vector128<ushort> s2 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 2)), Vector64<byte>.Zero));

                    Vector128<ushort> s3 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 3)), Vector64<byte>.Zero));

                    Vector128<ushort> s4 = Vector128.WidenLower(Vector128.Create(
                        Vector64.LoadUnsafe(ref source, (nuint)(i + 4)), Vector64<byte>.Zero));

                    Vector128<ushort> result = TOperator.Apply(s0, s1, s2, s3, s4);
                    Vector128.Narrow(result, Vector128<ushort>.Zero).GetLower().StoreUnsafe(ref edge, (nuint)(i + 1));
                }
            }

            for (; i < outputCount; i++)
            {
                int value = TOperator.Apply(
                    Unsafe.Add(ref source, i),
                    Unsafe.Add(ref source, i + 1),
                    Unsafe.Add(ref source, i + 2),
                    Unsafe.Add(ref source, i + 3),
                    Unsafe.Add(ref source, i + 4));

                Unsafe.Add(ref edge, i + 1) = (byte)value;
            }
        }

        /// <summary>
        /// Filters all samples following the preserved first sample.
        /// </summary>
        /// <param name="edge">The first edge sample.</param>
        /// <param name="count">The number of samples including the preserved sample.</param>
        /// <param name="scratch">The reusable source workspace.</param>
        public static void Apply(ref short edge, int count, Span<short> scratch)
        {
            // Each convolution reads the original edge. Duplicate its first sample once and its last sample
            // twice so the five-tap windows implement endpoint clamping without per-lane boundary branches.
            scratch[0] = edge;
            MemoryMarshal.CreateReadOnlySpan(ref edge, count).CopyTo(scratch[1..]);
            scratch.Slice(count + 1, 2).Fill(Unsafe.Add(ref edge, count - 1));

            ref short source = ref MemoryMarshal.GetReference(scratch);
            int outputCount = count - 1;
            int i = 0;

            // The same offset advances through descending SIMD widths. Adjacent lanes represent adjacent
            // output samples, and only complete windows are loaded; the final incomplete window is scalar.
            // Nonnegative 12-bit samples have a maximum weighted sum of 65520. The rounding bias keeps
            // that below 65536, so unsigned 16-bit lanes preserve the normative result at all strengths.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorEnd = outputCount - Vector512<ushort>.Count;
                for (; i <= vectorEnd; i += Vector512<ushort>.Count)
                {
                    Vector512<ushort> s0 = Vector512.LoadUnsafe(ref source, (nuint)(i + 0)).AsUInt16();
                    Vector512<ushort> s1 = Vector512.LoadUnsafe(ref source, (nuint)(i + 1)).AsUInt16();
                    Vector512<ushort> s2 = Vector512.LoadUnsafe(ref source, (nuint)(i + 2)).AsUInt16();
                    Vector512<ushort> s3 = Vector512.LoadUnsafe(ref source, (nuint)(i + 3)).AsUInt16();
                    Vector512<ushort> s4 = Vector512.LoadUnsafe(ref source, (nuint)(i + 4)).AsUInt16();

                    Vector512<ushort> result = TOperator.Apply(s0, s1, s2, s3, s4);
                    result.AsInt16().StoreUnsafe(ref edge, (nuint)(i + 1));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorEnd = outputCount - Vector256<ushort>.Count;
                for (; i <= vectorEnd; i += Vector256<ushort>.Count)
                {
                    Vector256<ushort> s0 = Vector256.LoadUnsafe(ref source, (nuint)(i + 0)).AsUInt16();
                    Vector256<ushort> s1 = Vector256.LoadUnsafe(ref source, (nuint)(i + 1)).AsUInt16();
                    Vector256<ushort> s2 = Vector256.LoadUnsafe(ref source, (nuint)(i + 2)).AsUInt16();
                    Vector256<ushort> s3 = Vector256.LoadUnsafe(ref source, (nuint)(i + 3)).AsUInt16();
                    Vector256<ushort> s4 = Vector256.LoadUnsafe(ref source, (nuint)(i + 4)).AsUInt16();

                    Vector256<ushort> result = TOperator.Apply(s0, s1, s2, s3, s4);
                    result.AsInt16().StoreUnsafe(ref edge, (nuint)(i + 1));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int vectorEnd = outputCount - Vector128<ushort>.Count;
                for (; i <= vectorEnd; i += Vector128<ushort>.Count)
                {
                    Vector128<ushort> s0 = Vector128.LoadUnsafe(ref source, (nuint)(i + 0)).AsUInt16();
                    Vector128<ushort> s1 = Vector128.LoadUnsafe(ref source, (nuint)(i + 1)).AsUInt16();
                    Vector128<ushort> s2 = Vector128.LoadUnsafe(ref source, (nuint)(i + 2)).AsUInt16();
                    Vector128<ushort> s3 = Vector128.LoadUnsafe(ref source, (nuint)(i + 3)).AsUInt16();
                    Vector128<ushort> s4 = Vector128.LoadUnsafe(ref source, (nuint)(i + 4)).AsUInt16();

                    Vector128<ushort> result = TOperator.Apply(s0, s1, s2, s3, s4);
                    result.AsInt16().StoreUnsafe(ref edge, (nuint)(i + 1));
                }
            }

            for (; i < outputCount; i++)
            {
                int value = TOperator.Apply(
                    Unsafe.Add(ref source, i),
                    Unsafe.Add(ref source, i + 1),
                    Unsafe.Add(ref source, i + 2),
                    Unsafe.Add(ref source, i + 3),
                    Unsafe.Add(ref source, i + 4));

                Unsafe.Add(ref edge, i + 1) = (short)value;
            }
        }
    }
}

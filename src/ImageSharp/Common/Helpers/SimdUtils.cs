// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tuples;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Various extension and utility methods for <see cref="Vector4"/> and <see cref="Vector{T}"/> utilizing SIMD capabilities
    /// </summary>
    internal static partial class SimdUtils
    {
        /// <summary>
        /// Gets a value indicating whether the code is being executed on AVX2 CPU where both float and integer registers are of size 256 byte.
        /// </summary>
        public static bool IsAvx2CompatibleArchitecture { get; } =
            Vector.IsHardwareAccelerated && Vector<float>.Count == 8 && Vector<int>.Count == 8;

        internal static void GuardAvx2(string operation)
        {
            if (!IsAvx2CompatibleArchitecture)
            {
                throw new NotSupportedException($"{operation} is supported only on AVX2 CPU!");
            }
        }

        /// <summary>
        /// Transform all scalars in 'v' in a way that converting them to <see cref="int"/> would have rounding semantics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector4 PseudoRound(this Vector4 v)
        {
            var sign = Vector4.Clamp(v, new Vector4(-1), new Vector4(1));

            return v + (sign * 0.5f);
        }

        /// <summary>
        /// Rounds all values in 'v' to the nearest integer following <see cref="MidpointRounding.ToEven"/> semantics.
        /// Source:
        /// <see>
        ///     <cref>https://github.com/g-truc/glm/blob/master/glm/simd/common.h#L110</cref>
        /// </see>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector<float> FastRound(this Vector<float> x)
        {
            Vector<int> magic0 = new Vector<int>(int.MinValue); // 0x80000000
            Vector<float> sgn0 = Vector.AsVectorSingle(magic0);
            Vector<float> and0 = Vector.BitwiseAnd(sgn0, x);
            Vector<float> or0 = Vector.BitwiseOr(and0, new Vector<float>(8388608.0f));
            Vector<float> add0 = Vector.Add(x, or0);
            Vector<float> sub0 = Vector.Subtract(add0, or0);
            return sub0;
        }

        /// <summary>
        /// Converts `dest.Length` <see cref="byte"/>-s to <see cref="float"/>-s normalized into [0..1].
        /// <paramref name="source"/> should be the of the same size as <paramref name="dest"/>,
        /// but there are no restrictions on the span's length.
        /// </summary>
        internal static void BulkConvertByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same size!");

            ExtendedIntrinsics.BulkConvertByteToNormalizedFloatReduce(ref source, ref dest);
            BasicIntrinsics256.BulkConvertByteToNormalizedFloatReduce(ref source, ref dest);

            // Deal with the remainder:
            int count = source.Length;
            if (count > 0)
            {
                // TODO: Do we need to optimize anything on this? (There are at most 7 remainders)
                ref byte sBase = ref MemoryMarshal.GetReference(source);
                ref float dBase = ref MemoryMarshal.GetReference(dest);
                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, i) / 255f;
                }
            }
        }

        /// <summary>
        /// Convert 'source.Length' <see cref="float"/> values normalized into [0..1] from 'source' into 'dest' buffer of <see cref="byte"/>.
        /// The values are scaled up into [0-255] and rounded, overflows are clamped.
        /// <paramref name="source"/> should be the of the same size as <paramref name="dest"/>,
        /// but there are no restrictions on the span's length.
        /// </summary>
        internal static void BulkConvertNormalizedFloatToByteClampOverflows(ReadOnlySpan<float> source, Span<byte> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same size!");

            ExtendedIntrinsics.BulkConvertNormalizedFloatToByteClampOverflowsReduce(ref source, ref dest);
            BasicIntrinsics256.BulkConvertNormalizedFloatToByteClampOverflowsReduce(ref source, ref dest);

            // Deal with the remainder:
            int count = source.Length;
            if (count > 0)
            {
                ref float sBase = ref MemoryMarshal.GetReference(source);
                ref byte dBase = ref MemoryMarshal.GetReference(dest);

                for (int i = 0; i < count; i++)
                {
                    // TODO: Do we need to optimize anything on this? (There are at most 7 remainders)
                    float f = Unsafe.Add(ref sBase, i);
                    f *= 255f;
                    f += 0.5f;
                    f = MathF.Max(0, f);
                    f = MathF.Min(255f, f);

                    Unsafe.Add(ref dBase, i) = (byte)f;
                }
            }
        }
    }
}
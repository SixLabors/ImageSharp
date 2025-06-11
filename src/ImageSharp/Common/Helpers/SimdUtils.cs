﻿// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp;

/// <summary>
/// Various extension and utility methods for <see cref="Vector4"/> and <see cref="Vector{T}"/> utilizing SIMD capabilities
/// </summary>
internal static partial class SimdUtils
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Vector{T}"/> code is being JIT-ed to AVX2 instructions
    /// where both float and integer registers are of size 256 byte.
    /// </summary>
    public static bool HasVector8 { get; } =
        Vector.IsHardwareAccelerated && Vector<float>.Count == 8 && Vector<int>.Count == 8;

    /// <summary>
    /// Transform all scalars in 'v' in a way that converting them to <see cref="int"/> would have rounding semantics.
    /// </summary>
    /// <param name="v">The vector</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 PseudoRound(this Vector4 v)
    {
        Vector4 sign = Numerics.Clamp(v, new Vector4(-1), new Vector4(1));

        return v + (sign * 0.5f);
    }

    /// <summary>
    /// Rounds all values in 'v' to the nearest integer following <see cref="MidpointRounding.ToEven"/> semantics.
    /// </summary>
    /// <param name="v">The vector</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector<float> FastRound(this Vector<float> v)
    {
        // .NET9+ has a built-in method for this Vector.Round
        if (Avx2.IsSupported && Vector<float>.Count == Vector256<float>.Count)
        {
            ref Vector256<float> v256 = ref Unsafe.As<Vector<float>, Vector256<float>>(ref v);
            Vector256<float> vRound = Avx.RoundToNearestInteger(v256);
            return Unsafe.As<Vector256<float>, Vector<float>>(ref vRound);
        }

        if (Sse41.IsSupported && Vector<float>.Count == Vector128<float>.Count)
        {
            ref Vector128<float> v128 = ref Unsafe.As<Vector<float>, Vector128<float>>(ref v);
            Vector128<float> vRound = Sse41.RoundToNearestInteger(v128);
            return Unsafe.As<Vector128<float>, Vector<float>>(ref vRound);
        }

        if (AdvSimd.IsSupported && Vector<float>.Count == Vector128<float>.Count)
        {
            ref Vector128<float> v128 = ref Unsafe.As<Vector<float>, Vector128<float>>(ref v);
            Vector128<float> vRound = AdvSimd.RoundToNearest(v128);
            return Unsafe.As<Vector128<float>, Vector<float>>(ref vRound);
        }

        // https://github.com/g-truc/glm/blob/master/glm/simd/common.h#L11
        Vector<float> sign = v & new Vector<float>(-0F);
        Vector<float> val_2p23_f32 = sign | new Vector<float>(8388608F);

        val_2p23_f32 = (v + val_2p23_f32) - val_2p23_f32;
        return val_2p23_f32 | sign;
    }

    [Conditional("DEBUG")]
    private static void DebugVerifySpanInput(ReadOnlySpan<byte> source, Span<float> dest, int shouldBeDivisibleBy)
    {
        DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");
        DebugGuard.IsTrue(
            Numerics.ModuloP2(dest.Length, shouldBeDivisibleBy) == 0,
            nameof(source),
            $"length should be divisible by {shouldBeDivisibleBy}!");
    }

    [Conditional("DEBUG")]
    private static void DebugVerifySpanInput(ReadOnlySpan<float> source, Span<byte> destination, int shouldBeDivisibleBy)
    {
        DebugGuard.IsTrue(source.Length == destination.Length, nameof(source), "Input spans must be of same length!");
        DebugGuard.IsTrue(
            Numerics.ModuloP2(destination.Length, shouldBeDivisibleBy) == 0,
            nameof(source),
            $"length should be divisible by {shouldBeDivisibleBy}!");
    }

    private struct ByteTuple4
    {
        public byte V0;
        public byte V1;
        public byte V2;
        public byte V3;
    }
}

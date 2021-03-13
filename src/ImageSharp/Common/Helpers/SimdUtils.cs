// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp
{
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
        /// Gets a value indicating whether <see cref="Vector{T}"/> code is being JIT-ed to SSE instructions
        /// where float and integer registers are of size 128 byte.
        /// </summary>
        public static bool HasVector4 { get; } =
            Vector.IsHardwareAccelerated && Vector<float>.Count == 4;

        public static bool HasAvx2
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Avx2.IsSupported;
#else
                return false;
#endif
            }
        }

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
        /// Source:
        /// <see>
        ///     <cref>https://github.com/g-truc/glm/blob/master/glm/simd/common.h#L110</cref>
        /// </see>
        /// </summary>
        /// <param name="v">The vector</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector<float> FastRound(this Vector<float> v)
        {
#if SUPPORTS_RUNTIME_INTRINSICS

            if (Avx2.IsSupported)
            {
                ref Vector256<float> v256 = ref Unsafe.As<Vector<float>, Vector256<float>>(ref v);
                Vector256<float> vRound = Avx.RoundToNearestInteger(v256);
                return Unsafe.As<Vector256<float>, Vector<float>>(ref vRound);
            }
            else
#endif
            {
                var magic0 = new Vector<int>(int.MinValue); // 0x80000000
                var sgn0 = Vector.AsVectorSingle(magic0);
                var and0 = Vector.BitwiseAnd(sgn0, v);
                var or0 = Vector.BitwiseOr(and0, new Vector<float>(8388608.0f));
                var add0 = Vector.Add(v, or0);
                return Vector.Subtract(add0, or0);
            }
        }

        /// <summary>
        /// Converts all input <see cref="byte"/>-s to <see cref="float"/>-s normalized into [0..1].
        /// <paramref name="source"/> should be the of the same size as <paramref name="dest"/>,
        /// but there are no restrictions on the span's length.
        /// </summary>
        /// <param name="source">The source span of bytes</param>
        /// <param name="dest">The destination span of floats</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");
#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.ByteToNormalizedFloatReduce(ref source, ref dest);
#elif SUPPORTS_EXTENDED_INTRINSICS
            ExtendedIntrinsics.ByteToNormalizedFloatReduce(ref source, ref dest);
#else
            BasicIntrinsics256.ByteToNormalizedFloatReduce(ref source, ref dest);
#endif

            // Also deals with the remainder from previous conversions:
            FallbackIntrinsics128.ByteToNormalizedFloatReduce(ref source, ref dest);

            // Deal with the remainder:
            if (source.Length > 0)
            {
                ConvertByteToNormalizedFloatRemainder(source, dest);
            }
        }

        /// <summary>
        /// Convert all <see cref="float"/> values normalized into [0..1] from 'source' into 'dest' buffer of <see cref="byte"/>.
        /// The values are scaled up into [0-255] and rounded, overflows are clamped.
        /// <paramref name="source"/> should be the of the same size as <paramref name="dest"/>,
        /// but there are no restrictions on the span's length.
        /// </summary>
        /// <param name="source">The source span of floats</param>
        /// <param name="dest">The destination span of bytes</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void NormalizedFloatToByteSaturate(ReadOnlySpan<float> source, Span<byte> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

#if SUPPORTS_RUNTIME_INTRINSICS
            HwIntrinsics.NormalizedFloatToByteSaturateReduce(ref source, ref dest);
#elif SUPPORTS_EXTENDED_INTRINSICS
            ExtendedIntrinsics.NormalizedFloatToByteSaturateReduce(ref source, ref dest);
#else
            BasicIntrinsics256.NormalizedFloatToByteSaturateReduce(ref source, ref dest);
#endif

            // Also deals with the remainder from previous conversions:
            FallbackIntrinsics128.NormalizedFloatToByteSaturateReduce(ref source, ref dest);

            // Deal with the remainder:
            if (source.Length > 0)
            {
                ConvertNormalizedFloatToByteRemainder(source, dest);
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ConvertByteToNormalizedFloatRemainder(ReadOnlySpan<byte> source, Span<float> dest)
        {
            ref byte sBase = ref MemoryMarshal.GetReference(source);
            ref float dBase = ref MemoryMarshal.GetReference(dest);

            // There are at most 3 elements at this point, having a for loop is overkill.
            // Let's minimize the no. of instructions!
            switch (source.Length)
            {
                case 3:
                    Unsafe.Add(ref dBase, 2) = Unsafe.Add(ref sBase, 2) / 255f;
                    goto case 2;
                case 2:
                    Unsafe.Add(ref dBase, 1) = Unsafe.Add(ref sBase, 1) / 255f;
                    goto case 1;
                case 1:
                    dBase = sBase / 255f;
                    break;
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ConvertNormalizedFloatToByteRemainder(ReadOnlySpan<float> source, Span<byte> dest)
        {
            ref float sBase = ref MemoryMarshal.GetReference(source);
            ref byte dBase = ref MemoryMarshal.GetReference(dest);

            switch (source.Length)
            {
                case 3:
                    Unsafe.Add(ref dBase, 2) = ConvertToByte(Unsafe.Add(ref sBase, 2));
                    goto case 2;
                case 2:
                    Unsafe.Add(ref dBase, 1) = ConvertToByte(Unsafe.Add(ref sBase, 1));
                    goto case 1;
                case 1:
                    dBase = ConvertToByte(sBase);
                    break;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static byte ConvertToByte(float f) => (byte)Numerics.Clamp((f * 255F) + 0.5F, 0, 255F);

        [Conditional("DEBUG")]
        private static void VerifyHasVector8(string operation)
        {
            if (!HasVector8)
            {
                throw new NotSupportedException($"{operation} is supported only on AVX2 CPU!");
            }
        }

        [Conditional("DEBUG")]
        private static void VerifySpanInput(ReadOnlySpan<byte> source, Span<float> dest, int shouldBeDivisibleBy)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");
            DebugGuard.IsTrue(
                Numerics.ModuloP2(dest.Length, shouldBeDivisibleBy) == 0,
                nameof(source),
                $"length should be divisible by {shouldBeDivisibleBy}!");
        }

        [Conditional("DEBUG")]
        private static void VerifySpanInput(ReadOnlySpan<float> source, Span<byte> dest, int shouldBeDivisibleBy)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");
            DebugGuard.IsTrue(
                Numerics.ModuloP2(dest.Length, shouldBeDivisibleBy) == 0,
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
}

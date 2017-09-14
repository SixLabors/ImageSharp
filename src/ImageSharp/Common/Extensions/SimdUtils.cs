// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    using System.Diagnostics;

    /// <summary>
    /// Various extension and utility methods for <see cref="Vector4"/> and <see cref="Vector{T}"/> utilizing SIMD capabilities
    /// </summary>
    internal static class SimdUtils
    {
        /// <summary>
        /// Indicates AVX2 architecture where both float and integer registers are of size 256 byte.
        /// </summary>
        public static readonly bool IsAvx2 = Vector<float>.Count == 8 && Vector<int>.Count == 8;

        [Conditional("DEBUG")]
        internal static void GuardAvx2(string operation)
        {
            if (!IsAvx2)
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
        /// Convert 'source.Length' <see cref="float"/> values normalized into [0..1] from 'source' into 'dest' buffer of <see cref="byte"/> values.
        /// The values gonna be scaled up into [0-255] and rounded.
        /// Based on:
        /// <see>
        ///     <cref>http://lolengine.net/blog/2011/3/20/understanding-fast-float-integer-conversions</cref>
        /// </see>
        /// </summary>
        internal static void BulkConvertNormalizedFloatToByte(ReadOnlySpan<float> source, Span<byte> dest)
        {
            GuardAvx2(nameof(BulkConvertNormalizedFloatToByte));

            DebugGuard.IsTrue((source.Length % Vector<float>.Count) == 0, nameof(source), "source.Length should be divisable by Vector<float>.Count!");

            if (source.Length == 0)
            {
                return;
            }

            ref Vector<float> srcBase = ref Unsafe.As<float, Vector<float>>(ref source.DangerousGetPinnableReference());
            ref Octet.OfByte destBase = ref Unsafe.As<byte, Octet.OfByte>(ref dest.DangerousGetPinnableReference());

            Vector<float> magick = new Vector<float>(32768.0f);
            Vector<float> scale = new Vector<float>(255f) / new Vector<float>(256f);

            int n = source.Length;

            for (int i = 0; i < n; i++)
            {
                // union { float f; uint32_t i; } u;
                // u.f = 32768.0f + x * (255.0f / 256.0f);
                // return (uint8_t)u.i;
                Vector<float> x = Unsafe.Add(ref srcBase, i);
                x = (x * scale) + magick;

                Vector<uint> u = Vector.AsVectorUInt32(x);

                Octet.OfUInt32 ii = Unsafe.As<Vector<uint>, Octet.OfUInt32>(ref u);

                ref Octet.OfByte d = ref Unsafe.Add(ref destBase, i);
                d.LoadFrom(ref ii);
            }
        }

        /// <summary>
        /// Same as <see cref="BulkConvertNormalizedFloatToByte"/> but clamps overflown values before conversion.
        /// </summary>
        internal static void BulkConvertNormalizedFloatToByteClampOverflows(ReadOnlySpan<float> source, Span<byte> dest)
        {
            GuardAvx2(nameof(BulkConvertNormalizedFloatToByte));

            DebugGuard.IsTrue((source.Length % Vector<float>.Count) == 0, nameof(source), "source.Length should be divisable by Vector<float>.Count!");

            if (source.Length == 0)
            {
                return;
            }

            ref Vector<float> srcBase = ref Unsafe.As<float, Vector<float>>(ref source.DangerousGetPinnableReference());
            ref Octet.OfByte destBase = ref Unsafe.As<byte, Octet.OfByte>(ref dest.DangerousGetPinnableReference());

            Vector<float> magick = new Vector<float>(32768.0f);
            Vector<float> scale = new Vector<float>(255f) / new Vector<float>(256f);

            int n = source.Length;

            for (int i = 0; i < n; i++)
            {
                // union { float f; uint32_t i; } u;
                // u.f = 32768.0f + x * (255.0f / 256.0f);
                // return (uint8_t)u.i;
                Vector<float> x = Unsafe.Add(ref srcBase, i);
                x = Vector.Max(x, Vector<float>.Zero);
                x = Vector.Min(x, Vector<float>.One);

                x = (x * scale) + magick;

                Vector<uint> u = Vector.AsVectorUInt32(x);

                Octet.OfUInt32 ii = Unsafe.As<Vector<uint>, Octet.OfUInt32>(ref u);

                ref Octet.OfByte d = ref Unsafe.Add(ref destBase, i);
                d.LoadFrom(ref ii);
            }
        }

#pragma warning disable SA1132 // Do not combine fields
        private static class Octet
        {
            public struct OfUInt32
            {
                public uint V0, V1, V2, V3, V4, V5, V6, V7;
            }

            public struct OfByte
            {
                public byte V0, V1, V2, V3, V4, V5, V6, V7;

                public void LoadFrom(ref OfUInt32 i)
                {
                    this.V0 = (byte)i.V0;
                    this.V1 = (byte)i.V1;
                    this.V2 = (byte)i.V2;
                    this.V3 = (byte)i.V3;
                    this.V4 = (byte)i.V4;
                    this.V5 = (byte)i.V5;
                    this.V6 = (byte)i.V6;
                    this.V7 = (byte)i.V7;
                }
            }
        }
    }
}
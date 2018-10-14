// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Various extension and utility methods for <see cref="Vector4"/> and <see cref="Vector{T}"/> utilizing SIMD capabilities
    /// </summary>
    internal static class SimdUtils
    {
        /// <summary>
        /// Gets a value indicating whether the code is being executed on AVX2 CPU where both float and integer registers are of size 256 byte.
        /// </summary>
        public static bool IsAvx2CompatibleArchitecture => Vector<float>.Count == 8 && Vector<int>.Count == 8;

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

            ref Vector<float> srcBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
            ref Octet.OfByte destBase = ref Unsafe.As<byte, Octet.OfByte>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 8;

            Vector<float> magick = new Vector<float>(32768.0f);
            Vector<float> scale = new Vector<float>(255f) / new Vector<float>(256f);

            // need to copy to a temporary struct, because
            // SimdUtils.Octet.OfUInt32 temp = Unsafe.As<Vector<float>, SimdUtils.Octet.OfUInt32>(ref x)
            // does not work. TODO: This might be a CoreClr bug, need to ask/report
            var temp = default(Octet.OfUInt32);
            ref Vector<float> tempRef = ref Unsafe.As<Octet.OfUInt32, Vector<float>>(ref temp);

            for (int i = 0; i < n; i++)
            {
                // union { float f; uint32_t i; } u;
                // u.f = 32768.0f + x * (255.0f / 256.0f);
                // return (uint8_t)u.i;
                Vector<float> x = Unsafe.Add(ref srcBase, i);
                x = (x * scale) + magick;
                tempRef = x;

                ref Octet.OfByte d = ref Unsafe.Add(ref destBase, i);
                d.LoadFrom(ref temp);
            }
        }

        /// <summary>
        /// Fast <see cref="byte"/> -> <see cref="float"/> conversion for RyuJIT runtimes having dotnet/coreclr#10662 merged.
        /// <see>
        ///     <cref>https://github.com/dotnet/coreclr/pull/10662</cref>
        /// </see>
        /// </summary>
        internal static void BulkConvertByteToNormalizedFloatFast(ReadOnlySpan<byte> source, Span<float> dest)
        {
            Guard.IsTrue(
                source.Length % Vector<byte>.Count == 0,
                nameof(source),
                "dest.Length should be divisable by Vector<byte>.Count!");

            int n = source.Length / Vector<byte>.Count;

            ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference(source));
            ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dest));

            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                Vector<float> f0 = Vector.ConvertToSingle(w0) * scale;
                Vector<float> f1 = Vector.ConvertToSingle(w1) * scale;
                Vector<float> f2 = Vector.ConvertToSingle(w2) * scale;
                Vector<float> f3 = Vector.ConvertToSingle(w3) * scale;

                ref Vector<float> d = ref Unsafe.Add(ref destBase, i * 4);
                d = f0;
                Unsafe.Add(ref d, 1) = f1;
                Unsafe.Add(ref d, 2) = f2;
                Unsafe.Add(ref d, 3) = f3;
            }
        }

        internal static void BulkConvertByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
        {
            GuardAvx2(nameof(BulkConvertByteToNormalizedFloat));

            DebugGuard.IsTrue((dest.Length % Vector<float>.Count) == 0, nameof(source), "dest.Length should be divisable by Vector<float>.Count!");

            var bVec = new Vector<float>(256.0f / 255.0f);
            var magicFloat = new Vector<float>(32768.0f);
            var magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
            var mask = new Vector<uint>(255);

            ref Octet.OfByte sourceBase = ref Unsafe.As<byte, Octet.OfByte>(ref MemoryMarshal.GetReference(source));
            ref Octet.OfUInt32 destBaseAsWideOctet = ref Unsafe.As<float, Octet.OfUInt32>(ref MemoryMarshal.GetReference(dest));

            ref Vector<float> destBaseAsFloat = ref Unsafe.As<Octet.OfUInt32, Vector<float>>(ref destBaseAsWideOctet);

            int n = dest.Length / 8;
            Octet.OfUInt32 temp = default;

            for (int i = 0; i < n; i++)
            {
                Octet.OfByte sVal = Unsafe.Add(ref sourceBase, i);

                // This call is the bottleneck now:
                temp.LoadFrom(ref sVal);

                Vector<uint> vi = Unsafe.As<Octet.OfUInt32, Vector<uint>>(ref temp);
                vi &= mask;
                vi |= magicInt;

                var vf = Vector.AsVectorSingle(vi);
                vf = (vf - magicFloat) * bVec;

                Unsafe.Add(ref destBaseAsFloat, i) = vf;
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

            ref Vector<float> srcBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
            ref Octet.OfByte destBase = ref Unsafe.As<byte, Octet.OfByte>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 8;

            Vector<float> magick = new Vector<float>(32768.0f);
            Vector<float> scale = new Vector<float>(255f) / new Vector<float>(256f);

            // need to copy to a temporary struct, because
            // SimdUtils.Octet.OfUInt32 temp = Unsafe.As<Vector<float>, SimdUtils.Octet.OfUInt32>(ref x)
            // does not work. TODO: This might be a CoreClr bug, need to ask/report
            var temp = default(Octet.OfUInt32);
            ref Vector<float> tempRef = ref Unsafe.As<Octet.OfUInt32, Vector<float>>(ref temp);

            for (int i = 0; i < n; i++)
            {
                // union { float f; uint32_t i; } u;
                // u.f = 32768.0f + x * (255.0f / 256.0f);
                // return (uint8_t)u.i;
                Vector<float> x = Unsafe.Add(ref srcBase, i);
                x = Vector.Max(x, Vector<float>.Zero);
                x = Vector.Min(x, Vector<float>.One);

                x = (x * scale) + magick;
                tempRef = x;

                ref Octet.OfByte d = ref Unsafe.Add(ref destBase, i);
                d.LoadFrom(ref temp);
            }
        }

        // TODO: Replace these with T4-d library level tuples!
        internal static class Octet
        {
            [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(uint))]
            public struct OfUInt32
            {
                [FieldOffset(0 * sizeof(uint))]
                public uint V0;

                [FieldOffset(1 * sizeof(uint))]
                public uint V1;

                [FieldOffset(2 * sizeof(uint))]
                public uint V2;

                [FieldOffset(3 * sizeof(uint))]
                public uint V3;

                [FieldOffset(4 * sizeof(uint))]
                public uint V4;

                [FieldOffset(5 * sizeof(uint))]
                public uint V5;

                [FieldOffset(6 * sizeof(uint))]
                public uint V6;

                [FieldOffset(7 * sizeof(uint))]
                public uint V7;

                public override string ToString()
                {
                    return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
                }

                [MethodImpl(InliningOptions.ShortMethod)]
                public void LoadFrom(ref OfByte src)
                {
                    this.V0 = src.V0;
                    this.V1 = src.V1;
                    this.V2 = src.V2;
                    this.V3 = src.V3;
                    this.V4 = src.V4;
                    this.V5 = src.V5;
                    this.V6 = src.V6;
                    this.V7 = src.V7;
                }
            }

            [StructLayout(LayoutKind.Explicit, Size = 8)]
            public struct OfByte
            {
                [FieldOffset(0)]
                public byte V0;

                [FieldOffset(1)]
                public byte V1;

                [FieldOffset(2)]
                public byte V2;

                [FieldOffset(3)]
                public byte V3;

                [FieldOffset(4)]
                public byte V4;

                [FieldOffset(5)]
                public byte V5;

                [FieldOffset(6)]
                public byte V6;

                [FieldOffset(7)]
                public byte V7;

                public override string ToString()
                {
                    return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
                }

                [MethodImpl(InliningOptions.ShortMethod)]
                public void LoadFrom(ref OfUInt32 src)
                {
                    this.V0 = (byte)src.V0;
                    this.V1 = (byte)src.V1;
                    this.V2 = (byte)src.V2;
                    this.V3 = (byte)src.V3;
                    this.V4 = (byte)src.V4;
                    this.V5 = (byte)src.V5;
                    this.V6 = (byte)src.V6;
                    this.V7 = (byte)src.V7;
                }
            }
        }
    }
}
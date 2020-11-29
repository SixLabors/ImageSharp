// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Tuples;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        /// <summary>
        /// Implementation with 256bit / AVX2 intrinsics NOT depending on newer API-s (Vector.Widen etc.)
        /// </summary>
        public static class BasicIntrinsics256
        {
            public static bool IsAvailable { get; } = HasVector8;

#if !SUPPORTS_EXTENDED_INTRINSICS
            /// <summary>
            /// <see cref="ByteToNormalizedFloat"/> as many elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ByteToNormalizedFloatReduce(
                ref ReadOnlySpan<byte> source,
                ref Span<float> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

                if (!IsAvailable)
                {
                    return;
                }

                int remainder = Numerics.Modulo8(source.Length);
                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    ByteToNormalizedFloat(
                        source.Slice(0, adjustedCount),
                        dest.Slice(0, adjustedCount));

                    source = source.Slice(adjustedCount);
                    dest = dest.Slice(adjustedCount);
                }
            }

            /// <summary>
            /// <see cref="NormalizedFloatToByteSaturate"/> as many elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void NormalizedFloatToByteSaturateReduce(
                ref ReadOnlySpan<float> source,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

                if (!IsAvailable)
                {
                    return;
                }

                int remainder = Numerics.Modulo8(source.Length);
                int adjustedCount = source.Length - remainder;

                if (adjustedCount > 0)
                {
                    NormalizedFloatToByteSaturate(source.Slice(0, adjustedCount), dest.Slice(0, adjustedCount));

                    source = source.Slice(adjustedCount);
                    dest = dest.Slice(adjustedCount);
                }
            }
#endif

            /// <summary>
            /// SIMD optimized implementation for <see cref="SimdUtils.ByteToNormalizedFloat"/>.
            /// Works only with span Length divisible by 8.
            /// Implementation adapted from:
            /// http://lolengine.net/blog/2011/3/20/understanding-fast-float-integer-conversions
            /// http://stackoverflow.com/a/536278
            /// </summary>
            internal static void ByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
            {
                VerifyHasVector8(nameof(ByteToNormalizedFloat));
                VerifySpanInput(source, dest, 8);

                var bVec = new Vector<float>(256.0f / 255.0f);
                var magicFloat = new Vector<float>(32768.0f);
                var magicInt = new Vector<uint>(1191182336); // reinterpreted value of 32768.0f
                var mask = new Vector<uint>(255);

                ref Octet<byte> sourceBase = ref Unsafe.As<byte, Octet<byte>>(ref MemoryMarshal.GetReference(source));
                ref Octet<uint> destBaseAsWideOctet = ref Unsafe.As<float, Octet<uint>>(ref MemoryMarshal.GetReference(dest));

                ref Vector<float> destBaseAsFloat = ref Unsafe.As<Octet<uint>, Vector<float>>(ref destBaseAsWideOctet);

                int n = dest.Length / 8;

                for (int i = 0; i < n; i++)
                {
                    ref Octet<byte> s = ref Unsafe.Add(ref sourceBase, i);
                    ref Octet<uint> d = ref Unsafe.Add(ref destBaseAsWideOctet, i);
                    d.LoadFrom(ref s);
                }

                for (int i = 0; i < n; i++)
                {
                    ref Vector<float> df = ref Unsafe.Add(ref destBaseAsFloat, i);

                    var vi = Vector.AsVectorUInt32(df);
                    vi &= mask;
                    vi |= magicInt;

                    var vf = Vector.AsVectorSingle(vi);
                    vf = (vf - magicFloat) * bVec;

                    df = vf;
                }
            }

            /// <summary>
            /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/> which is faster on older runtimes.
            /// </summary>
            internal static void NormalizedFloatToByteSaturate(ReadOnlySpan<float> source, Span<byte> dest)
            {
                VerifyHasVector8(nameof(NormalizedFloatToByteSaturate));
                VerifySpanInput(source, dest, 8);

                if (source.Length == 0)
                {
                    return;
                }

                ref Vector<float> srcBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
                ref Octet<byte> destBase = ref Unsafe.As<byte, Octet<byte>>(ref MemoryMarshal.GetReference(dest));
                int n = source.Length / 8;

                var magick = new Vector<float>(32768.0f);
                var scale = new Vector<float>(255f) / new Vector<float>(256f);

                // need to copy to a temporary struct, because
                // SimdUtils.Octet<uint> temp = Unsafe.As<Vector<float>, SimdUtils.Octet<uint>>(ref x)
                // does not work. TODO: This might be a CoreClr bug, need to ask/report
                var temp = default(Octet<uint>);
                ref Vector<float> tempRef = ref Unsafe.As<Octet<uint>, Vector<float>>(ref temp);

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

                    ref Octet<byte> d = ref Unsafe.Add(ref destBase, i);
                    d.LoadFrom(ref temp);
                }
            }

            /// <summary>
            /// Convert all <see cref="float"/> values normalized into [0..1] from 'source'
            /// into 'dest' buffer of <see cref="byte"/>. The values are scaled up into [0-255] and rounded.
            /// This implementation is SIMD optimized and works only when span Length is divisible by 8.
            /// Based on:
            /// <see>
            ///     <cref>http://lolengine.net/blog/2011/3/20/understanding-fast-float-integer-conversions</cref>
            /// </see>
            /// </summary>
            internal static void BulkConvertNormalizedFloatToByte(ReadOnlySpan<float> source, Span<byte> dest)
            {
                VerifyHasVector8(nameof(BulkConvertNormalizedFloatToByte));
                VerifySpanInput(source, dest, 8);

                if (source.Length == 0)
                {
                    return;
                }

                ref Vector<float> srcBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
                ref Octet<byte> destBase = ref Unsafe.As<byte, Octet<byte>>(ref MemoryMarshal.GetReference(dest));
                int n = source.Length / 8;

                var magick = new Vector<float>(32768.0f);
                var scale = new Vector<float>(255f) / new Vector<float>(256f);

                // need to copy to a temporary struct, because
                // SimdUtils.Octet<uint> temp = Unsafe.As<Vector<float>, SimdUtils.Octet<uint>>(ref x)
                // does not work. TODO: This might be a CoreClr bug, need to ask/report
                var temp = default(Octet<uint>);
                ref Vector<float> tempRef = ref Unsafe.As<Octet<uint>, Vector<float>>(ref temp);

                for (int i = 0; i < n; i++)
                {
                    // union { float f; uint32_t i; } u;
                    // u.f = 32768.0f + x * (255.0f / 256.0f);
                    // return (uint8_t)u.i;
                    Vector<float> x = Unsafe.Add(ref srcBase, i);
                    x = (x * scale) + magick;
                    tempRef = x;

                    ref Octet<byte> d = ref Unsafe.Add(ref destBase, i);
                    d.LoadFrom(ref temp);
                }
            }
        }
    }
}

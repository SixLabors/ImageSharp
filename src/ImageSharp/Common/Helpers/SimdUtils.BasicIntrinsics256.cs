// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
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
            public static bool IsAvailable { get; } = IsAvx2CompatibleArchitecture;

            /// <summary>
            /// <see cref="BulkConvertByteToNormalizedFloat"/> as much elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            internal static void BulkConvertByteToNormalizedFloatReduce(
                ref ReadOnlySpan<byte> source,
                ref Span<float> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same size!");

                if (IsAvailable)
                {
                    int remainder = source.Length % 8;
                    int alignedCount = source.Length - remainder;

                    if (alignedCount > 0)
                    {
                        BulkConvertByteToNormalizedFloat(
                            source.Slice(0, alignedCount),
                            dest.Slice(0, alignedCount));

                        source = source.Slice(alignedCount);
                        dest = dest.Slice(alignedCount);
                    }
                }
            }

            /// <summary>
            /// SIMD optimized implementation for <see cref="SimdUtils.BulkConvertByteToNormalizedFloat"/>.
            /// Works only with `dest.Length` divisible by 8.
            /// Implementation adapted from:
            /// http://lolengine.net/blog/2011/3/20/understanding-fast-float-integer-conversions
            /// http://stackoverflow.com/a/536278
            /// </summary>
            internal static void BulkConvertByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
            {
                GuardAvx2(nameof(BulkConvertByteToNormalizedFloat));

                DebugGuard.IsTrue((dest.Length % 8) == 0, nameof(source), "dest.Length should be divisable by 8!");

                var bVec = new Vector<float>(256.0f / 255.0f);
                var magicFloat = new Vector<float>(32768.0f);
                var magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
                var mask = new Vector<uint>(255);

                ref Octet.OfByte sourceBase = ref Unsafe.As<byte, Octet.OfByte>(ref MemoryMarshal.GetReference(source));
                ref Octet.OfUInt32 destBaseAsWideOctet = ref Unsafe.As<float, Octet.OfUInt32>(ref MemoryMarshal.GetReference(dest));

                ref Vector<float> destBaseAsFloat = ref Unsafe.As<Octet.OfUInt32, Vector<float>>(ref destBaseAsWideOctet);

                int n = dest.Length / 8;

                for (int i = 0; i < n; i++)
                {
                    ref Octet.OfByte s = ref Unsafe.Add(ref sourceBase, i);
                    ref Octet.OfUInt32 d = ref Unsafe.Add(ref destBaseAsWideOctet, i);
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
            /// <see cref="BulkConvertNormalizedFloatToByteClampOverflows"/> as much elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            internal static void BulkConvertNormalizedFloatToByteClampOverflowsReduce(
                ref ReadOnlySpan<float> source,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same size!");

                if (IsAvailable)
                {
                    int remainder = source.Length % Vector<byte>.Count;
                    int alignedCount = source.Length - remainder;

                    if (alignedCount > 0)
                    {
                        BulkConvertNormalizedFloatToByteClampOverflows(source.Slice(0, alignedCount), dest.Slice(0, alignedCount));

                        source = source.Slice(alignedCount);
                        dest = dest.Slice(alignedCount);
                    }
                }
            }

            /// <summary>
            /// Implementation of <see cref="SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows"/> which is faster on older runtimes.
            /// </summary>
            internal static void BulkConvertNormalizedFloatToByteClampOverflows(ReadOnlySpan<float> source, Span<byte> dest)
            {
                GuardAvx2(nameof(BulkConvertNormalizedFloatToByteClampOverflows));

                DebugGuard.IsTrue((source.Length % 8) == 0, nameof(source), "source.Length should be divisible by 8!");

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

            /// <summary>
            /// Convert 'source.Length' <see cref="float"/> values normalized into [0..1] from 'source'
            /// into 'dest' buffer of <see cref="byte"/>. The values are scaled up into [0-255] and rounded.
            /// The implementation is SIMD optimized and works only with `source.Length` divisible by 8.
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
        }
    }
}
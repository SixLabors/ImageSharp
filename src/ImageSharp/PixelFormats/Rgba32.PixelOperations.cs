// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rgba32
    {
        /// <summary>
        /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="Rgba32"/>.
        /// </summary>
        internal partial class PixelOperations : PixelOperations<Rgba32>
        {
            /// <summary>
            /// SIMD optimized bulk implementation of <see cref="IPixel.PackFromVector4(Vector4)"/>
            /// that works only with `count` divisible by <see cref="Vector{UInt32}.Count"/>.
            /// </summary>
            /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
            /// <param name="destVectors">The <see cref="Span{T}"/> to the dstination vectors.</param>
            /// <param name="count">The number of pixels to convert.</param>
            /// <remarks>
            /// Implementation adapted from:
            /// <see>
            ///     <cref>http://stackoverflow.com/a/5362789</cref>
            /// </see>
            /// TODO: We can replace this implementation in the future using new Vector API-s:
            /// <see>
            ///     <cref>https://github.com/dotnet/corefx/issues/15957</cref>
            /// </see>
            /// </remarks>
            internal static void ToVector4SimdAligned(Span<Rgba32> sourceColors, Span<Vector4> destVectors, int count)
            {
                if (!Vector.IsHardwareAccelerated)
                {
                    throw new InvalidOperationException(
                        "Rgba32.PixelOperations.ToVector4SimdAligned() should not be called when Vector.IsHardwareAccelerated == false!");
                }

                DebugGuard.IsTrue(
                    count % Vector<uint>.Count == 0,
                    nameof(count),
                    "Argument 'count' should divisible by Vector<uint>.Count!");

                var bVec = new Vector<float>(256.0f / 255.0f);
                var magicFloat = new Vector<float>(32768.0f);
                var magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
                var mask = new Vector<uint>(255);

                int unpackedRawCount = count * 4;

                ref uint sourceBase = ref Unsafe.As<Rgba32, uint>(ref sourceColors.DangerousGetPinnableReference());
                ref UnpackedRGBA destBaseAsUnpacked = ref Unsafe.As<Vector4, UnpackedRGBA>(ref destVectors.DangerousGetPinnableReference());
                ref Vector<uint> destBaseAsUInt = ref Unsafe.As<UnpackedRGBA, Vector<uint>>(ref destBaseAsUnpacked);
                ref Vector<float> destBaseAsFloat = ref Unsafe.As<UnpackedRGBA, Vector<float>>(ref destBaseAsUnpacked);

                for (int i = 0; i < count; i++)
                {
                    uint sVal = Unsafe.Add(ref sourceBase, i);
                    ref UnpackedRGBA dst = ref Unsafe.Add(ref destBaseAsUnpacked, i);

                    // This call is the bottleneck now:
                    dst.Load(sVal);
                }

                int numOfVectors = unpackedRawCount / Vector<uint>.Count;

                for (int i = 0; i < numOfVectors; i++)
                {
                    Vector<uint> vi = Unsafe.Add(ref destBaseAsUInt, i);

                    vi &= mask;
                    vi |= magicInt;

                    var vf = Vector.AsVectorSingle(vi);
                    vf = (vf - magicFloat) * bVec;

                    Unsafe.Add(ref destBaseAsFloat, i) = vf;
                }
            }

            /// <inheritdoc />
            internal override void ToVector4(Span<Rgba32> sourceColors, Span<Vector4> destVectors, int count)
            {
                Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
                Guard.MustBeSizedAtLeast(destVectors, count, nameof(destVectors));

                if (count < 256 || !Vector.IsHardwareAccelerated)
                {
                    // Doesn't worth to bother with SIMD:
                    base.ToVector4(sourceColors, destVectors, count);
                    return;
                }

                int remainder = count % Vector<uint>.Count;
                int alignedCount = count - remainder;

                if (alignedCount > 0)
                {
                    ToVector4SimdAligned(sourceColors, destVectors, alignedCount);
                }

                if (remainder > 0)
                {
                    sourceColors = sourceColors.Slice(alignedCount);
                    destVectors = destVectors.Slice(alignedCount);
                    base.ToVector4(sourceColors, destVectors, remainder);
                }
            }

            internal override void PackFromVector4(Span<Vector4> sourceVectors, Span<Rgba32> destColors, int count)
            {
                GuardSpans(sourceVectors, nameof(sourceVectors), destColors, nameof(destColors), count);

                if (!SimdUtils.IsAvx2CompatibleArchitecture)
                {
                    base.PackFromVector4(sourceVectors, destColors, count);
                    return;
                }

                int remainder = count % 2;
                int alignedCount = count - remainder;

                if (alignedCount > 0)
                {
                    Span<float> flatSrc = sourceVectors.Slice(0, alignedCount).NonPortableCast<Vector4, float>();
                    Span<byte> flatDest = destColors.NonPortableCast<Rgba32, byte>();

                    SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(flatSrc, flatDest);
                }

                if (remainder > 0)
                {
                    // actually: remainder == 1
                    int lastIdx = count - 1;
                    destColors[lastIdx].PackFromVector4(sourceVectors[lastIdx]);
                }
            }

            /// <inheritdoc />
            internal override void PackFromRgba32(Span<Rgba32> source, Span<Rgba32> destPixels, int count)
            {
                GuardSpans(source, nameof(source), destPixels, nameof(destPixels), count);

                SpanHelper.Copy(source, destPixels, count);
            }

            /// <inheritdoc />
            internal override void ToRgba32(Span<Rgba32> sourcePixels, Span<Rgba32> dest, int count)
            {
                GuardSpans(sourcePixels, nameof(sourcePixels), dest, nameof(dest), count);

                SpanHelper.Copy(sourcePixels, dest, count);
            }

            /// <summary>
            /// Value type to store <see cref="Rgba32"/>-s unpacked into multiple <see cref="uint"/>-s.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            private struct UnpackedRGBA
            {
                private uint r;

                private uint g;

                private uint b;

                private uint a;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Load(uint p)
                {
                    this.r = p;
                    this.g = p >> GreenShift;
                    this.b = p >> BlueShift;
                    this.a = p >> AlphaShift;
                }
            }
        }
    }
}
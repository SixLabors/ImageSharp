// <copyright file="Rgba32.PixelOperations.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rgba32
    {
        /// <summary>
        /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="Rgba32"/>.
        /// </summary>
        internal class PixelOperations : PixelOperations<Rgba32>
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

                Vector<float> bVec = new Vector<float>(256.0f / 255.0f);
                Vector<float> magicFloat = new Vector<float>(32768.0f);
                Vector<uint> magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
                Vector<uint> mask = new Vector<uint>(255);

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

                    Vector<float> vf = Vector.AsVectorSingle(vi);
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

            internal override void PackFromRgb24(Span<Rgb24> source, Span<Rgba32> destPixels, int count)
            {
                Guard.MustBeSizedAtLeast(source, count, nameof(source));
                Guard.MustBeSizedAtLeast(destPixels, count, nameof(destPixels));

                ref Rgb24 sourceRef = ref source.DangerousGetPinnableReference();
                ref Rgba32 destRef = ref destPixels.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref Rgb24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba32 dp = ref Unsafe.Add(ref destRef, i);

                    Unsafe.As<Rgba32, Rgb24>(ref dp) = sp;
                    dp.A = 255;
                }
            }

            internal override void ToRgb24(Span<Rgba32> sourcePixels, Span<Rgb24> dest, int count)
            {
                Guard.MustBeSizedAtLeast(sourcePixels, count, nameof(sourcePixels));
                Guard.MustBeSizedAtLeast(dest, count, nameof(dest));

                ref Rgba32 sourceRef = ref sourcePixels.DangerousGetPinnableReference();
                ref Rgb24 destRef = ref dest.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref Rgba32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgb24 dp = ref Unsafe.Add(ref destRef, i);

                    dp = Unsafe.As<Rgba32, Rgb24>(ref sp);
                }
            }

            /// <inheritdoc />
            internal override unsafe void PackFromRgba32Bytes(Span<byte> sourceBytes, Span<Rgba32> destColors, int count)
            {
                Guard.MustBeSizedAtLeast(sourceBytes, count * 4, nameof(sourceBytes));
                Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

                SpanHelper.Copy(sourceBytes, destColors.AsBytes(), count * sizeof(Rgba32));
            }

            /// <inheritdoc />
            internal override unsafe void ToXyzwBytes(Span<Rgba32> sourceColors, Span<byte> destBytes, int count)
            {
                Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
                Guard.MustBeSizedAtLeast(destBytes, count * 4, nameof(destBytes));

                SpanHelper.Copy(sourceColors.AsBytes(), destBytes, count * sizeof(Rgba32));
            }

            /// <inheritdoc />
            internal override void PackFromZyxBytes(Span<byte> sourceBytes, Span<Rgba32> destColors, int count)
            {
                Guard.MustBeSizedAtLeast(sourceBytes, count * 3, nameof(sourceBytes));
                Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

                ref RGB24 sourceRef = ref Unsafe.As<byte, RGB24>(ref sourceBytes.DangerousGetPinnableReference());
                ref Rgba32 destRef = ref destColors.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref RGB24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba32 dp = ref Unsafe.Add(ref destRef, i);

                    Unsafe.As<Rgba32, RGB24>(ref dp) = sp.ToZyx();
                    dp.A = 255;
                }
            }

            /// <inheritdoc />
            internal override void ToZyxBytes(Span<Rgba32> sourceColors, Span<byte> destBytes, int count)
            {
                Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
                Guard.MustBeSizedAtLeast(destBytes, count * 3, nameof(destBytes));

                ref Rgba32 sourceRef = ref sourceColors.DangerousGetPinnableReference();
                ref RGB24 destRef = ref Unsafe.As<byte, RGB24>(ref destBytes.DangerousGetPinnableReference());

                for (int i = 0; i < count; i++)
                {
                    ref Rgba32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref RGB24 dp = ref Unsafe.Add(ref destRef, i);

                    dp = Unsafe.As<Rgba32, RGB24>(ref sp).ToZyx();
                }
            }

            /// <inheritdoc />
            internal override void PackFromZyxwBytes(Span<byte> sourceBytes, Span<Rgba32> destColors, int count)
            {
                Guard.MustBeSizedAtLeast(sourceBytes, count * 4, nameof(sourceBytes));
                Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

                ref RGBA32 sourceRef = ref Unsafe.As<byte, RGBA32>(ref sourceBytes.DangerousGetPinnableReference());
                ref Rgba32 destRef = ref destColors.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref RGBA32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Rgba32 dp = ref Unsafe.Add(ref destRef, i);
                    RGBA32 zyxw = sp.ToZyxw();
                    dp = Unsafe.As<RGBA32, Rgba32>(ref zyxw);
                }
            }

            /// <inheritdoc />
            internal override void ToZyxwBytes(Span<Rgba32> sourceColors, Span<byte> destBytes, int count)
            {
                Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
                Guard.MustBeSizedAtLeast(destBytes, count * 4, nameof(destBytes));

                ref Rgba32 sourceRef = ref sourceColors.DangerousGetPinnableReference();
                ref RGBA32 destRef = ref Unsafe.As<byte, RGBA32>(ref destBytes.DangerousGetPinnableReference());

                for (int i = 0; i < count; i++)
                {
                    ref RGBA32 sp = ref Unsafe.As<Rgba32, RGBA32>(ref Unsafe.Add(ref sourceRef, i));
                    ref RGBA32 dp = ref Unsafe.Add(ref destRef, i);
                    dp = sp.ToZyxw();
                }
            }

            /// <summary>
            /// Helper struct to manipulate 3-byte RGB data.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            private struct RGB24
            {
                private byte x;

                private byte y;

                private byte z;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public RGB24 ToZyx() => new RGB24 { x = this.z, y = this.y, z = this.x };
            }

            /// <summary>
            /// Helper struct to manipulate 4-byte RGBA data.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            private struct RGBA32
            {
                private byte x;

                private byte y;

                private byte z;

                private byte w;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public RGBA32 ToZyxw() => new RGBA32 { x = this.z, y = this.y, z = this.x, w = this.w };
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
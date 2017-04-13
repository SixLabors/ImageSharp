// <copyright file="Color.BulkOperations.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <content>
    /// Conains the definition of <see cref="BulkOperations"/>
    /// </content>
    public partial struct Color
    {
        /// <summary>
        /// <see cref="BulkPixelOperations{TColor}"/> implementation optimized for <see cref="Color"/>.
        /// </summary>
        internal class BulkOperations : BulkPixelOperations<Color>
        {
            /// <summary>
            /// SIMD optimized bulk implementation of <see cref="IPixel.PackFromVector4(Vector4)"/>
            /// that works only with `count` divisible by <see cref="Vector{UInt32}.Count"/>.
            /// </summary>
            /// <param name="sourceColors">The <see cref="BufferSpan{T}"/> to the source colors.</param>
            /// <param name="destVectors">The <see cref="BufferSpan{T}"/> to the dstination vectors.</param>
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
            internal static unsafe void ToVector4SimdAligned(
                BufferSpan<Color> sourceColors,
                BufferSpan<Vector4> destVectors,
                int count)
            {
                if (!Vector.IsHardwareAccelerated)
                {
                    throw new InvalidOperationException(
                        "Color.BulkOperations.ToVector4SimdAligned() should not be called when Vector.IsHardwareAccelerated == false!");
                }

                int vecSize = Vector<uint>.Count;

                DebugGuard.IsTrue(
                    count % vecSize == 0,
                    nameof(count),
                    "Argument 'count' should divisible by Vector<uint>.Count!");

                Vector<float> bVec = new Vector<float>(256.0f / 255.0f);
                Vector<float> magicFloat = new Vector<float>(32768.0f);
                Vector<uint> magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
                Vector<uint> mask = new Vector<uint>(255);

                int unpackedRawCount = count * 4;

                ref uint src = ref Unsafe.As<Color, uint>(ref sourceColors.DangerousGetPinnableReference());

                using (PinnedBuffer<uint> tempBuf = new PinnedBuffer<uint>(unpackedRawCount + Vector<uint>.Count))
                {
                    uint* tPtr = (uint*)tempBuf.Pointer;
                    uint[] temp = tempBuf.Array;
                    float[] fTemp = Unsafe.As<float[]>(temp);
                    UnpackedRGBA* dst = (UnpackedRGBA*)tPtr;

                    for (int i = 0; i < count; i++)
                    {
                        // This call is the bottleneck now:
                        ref uint sp = ref Unsafe.Add(ref src, i);
                        dst->Load(sp);
                    }

                    for (int i = 0; i < unpackedRawCount; i += vecSize)
                    {
                        Vector<uint> vi = new Vector<uint>(temp, i);

                        vi &= mask;
                        vi |= magicInt;

                        Vector<float> vf = Vector.AsVectorSingle(vi);
                        vf = (vf - magicFloat) * bVec;
                        vf.CopyTo(fTemp, i);
                    }

                    BufferSpan.Copy(tempBuf.Span.AsBytes(), destVectors.AsBytes(), unpackedRawCount);
                }
            }

            /// <inheritdoc />
            internal override void ToVector4(BufferSpan<Color> sourceColors, BufferSpan<Vector4> destVectors, int count)
            {
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

            /// <inheritdoc />
            internal override void PackFromXyzBytes(
                BufferSpan<byte> sourceBytes,
                BufferSpan<Color> destColors,
                int count)
            {
                ref RGB24 sourceRef = ref Unsafe.As<byte, RGB24>(ref sourceBytes.DangerousGetPinnableReference());
                ref Color destRef = ref destColors.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref RGB24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Color dp = ref Unsafe.Add(ref destRef, i);

                    Unsafe.As<Color, RGB24>(ref dp) = sp;
                    dp.A = 255;
                }
            }

            /// <inheritdoc />
            internal override void ToXyzBytes(BufferSpan<Color> sourceColors, BufferSpan<byte> destBytes, int count)
            {
                ref Color sourceRef = ref sourceColors.DangerousGetPinnableReference();
                ref RGB24 destRef = ref Unsafe.As<byte, RGB24>(ref destBytes.DangerousGetPinnableReference());

                for (int i = 0; i < count; i++)
                {
                    ref Color sp = ref Unsafe.Add(ref sourceRef, i);
                    ref RGB24 dp = ref Unsafe.Add(ref destRef, i);

                    dp = Unsafe.As<Color, RGB24>(ref sp);
                }
            }

            /// <inheritdoc />
            internal override unsafe void PackFromXyzwBytes(
                BufferSpan<byte> sourceBytes,
                BufferSpan<Color> destColors,
                int count)
            {
                BufferSpan.Copy(sourceBytes, destColors.AsBytes(), count * sizeof(Color));
            }

            /// <inheritdoc />
            internal override unsafe void ToXyzwBytes(BufferSpan<Color> sourceColors, BufferSpan<byte> destBytes, int count)
            {
                BufferSpan.Copy(sourceColors.AsBytes(), destBytes, count * sizeof(Color));
            }

            /// <inheritdoc />
            internal override void PackFromZyxBytes(
                BufferSpan<byte> sourceBytes,
                BufferSpan<Color> destColors,
                int count)
            {
                ref RGB24 sourceRef = ref Unsafe.As<byte, RGB24>(ref sourceBytes.DangerousGetPinnableReference());
                ref Color destRef = ref destColors.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref RGB24 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Color dp = ref Unsafe.Add(ref destRef, i);

                    Unsafe.As<Color, RGB24>(ref dp) = sp.ToZyx();
                    dp.A = 255;
                }
            }

            /// <inheritdoc />
            internal override void ToZyxBytes(
                BufferSpan<Color> sourceColors,
                BufferSpan<byte> destBytes,
                int count)
            {
                ref Color sourceRef = ref sourceColors.DangerousGetPinnableReference();
                ref RGB24 destRef = ref Unsafe.As<byte, RGB24>(ref destBytes.DangerousGetPinnableReference());

                for (int i = 0; i < count; i++)
                {
                    ref Color sp = ref Unsafe.Add(ref sourceRef, i);
                    ref RGB24 dp = ref Unsafe.Add(ref destRef, i);

                    dp = Unsafe.As<Color, RGB24>(ref sp).ToZyx();
                }
            }

            /// <inheritdoc />
            internal override void PackFromZyxwBytes(
                BufferSpan<byte> sourceBytes,
                BufferSpan<Color> destColors,
                int count)
            {
                ref RGBA32 sourceRef = ref Unsafe.As<byte, RGBA32>(ref sourceBytes.DangerousGetPinnableReference());
                ref Color destRef = ref destColors.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref RGBA32 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Color dp = ref Unsafe.Add(ref destRef, i);
                    RGBA32 zyxw = sp.ToZyxw();
                    dp = Unsafe.As<RGBA32, Color>(ref zyxw);
                }
            }

            /// <inheritdoc />
            internal override void ToZyxwBytes(
                BufferSpan<Color> sourceColors,
                BufferSpan<byte> destBytes,
                int count)
            {
                ref Color sourceRef = ref sourceColors.DangerousGetPinnableReference();
                ref RGBA32 destRef = ref Unsafe.As<byte, RGBA32>(ref destBytes.DangerousGetPinnableReference());

                for (int i = 0; i < count; i++)
                {
                    ref RGBA32 sp = ref Unsafe.As<Color, RGBA32>(ref Unsafe.Add(ref sourceRef, i));
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
            /// Value type to store <see cref="Color"/>-s unpacked into multiple <see cref="uint"/>-s.
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
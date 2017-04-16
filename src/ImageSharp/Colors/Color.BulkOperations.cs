// <copyright file="Color.BulkOperations.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <content>
    /// Conains the definition of <see cref="BulkOperations"/>
    /// </content>
    public partial struct Color32
    {
        /// <summary>
        /// <see cref="BulkPixelOperations{TColor}"/> implementation optimized for <see cref="Color32"/>.
        /// </summary>
        internal class BulkOperations : BulkPixelOperations<Color32>
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
                BufferSpan<Color32> sourceColors,
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

                uint* src = (uint*)sourceColors.PointerAtOffset;
                uint* srcEnd = src + count;

                using (PinnedBuffer<uint> tempBuf = new PinnedBuffer<uint>(
                        unpackedRawCount + Vector<uint>.Count))
                {
                    uint* tPtr = (uint*)tempBuf.Pointer;
                    uint[] temp = tempBuf.Array;
                    float[] fTemp = Unsafe.As<float[]>(temp);
                    UnpackedRGBA* dst = (UnpackedRGBA*)tPtr;

                    for (; src < srcEnd; src++, dst++)
                    {
                        // This call is the bottleneck now:
                        dst->Load(*src);
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

                    BufferSpan.Copy<uint>(tempBuf, (BufferSpan<byte>)destVectors, unpackedRawCount);
                }
            }

            /// <inheritdoc />
            internal override void ToVector4(BufferSpan<Color32> sourceColors, BufferSpan<Vector4> destVectors, int count)
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
            internal override unsafe void PackFromXyzBytes(BufferSpan<byte> sourceBytes, BufferSpan<Color32> destColors, int count)
            {
                byte* source = (byte*)sourceBytes;
                byte* destination = (byte*)destColors;

                for (int x = 0; x < count; x++)
                {
                    Unsafe.Write(destination, (uint)(*source << 0 | *(source + 1) << 8 | *(source + 2) << 16 | 255 << 24));

                    source += 3;
                    destination += 4;
                }
            }

            /// <inheritdoc />
            internal override unsafe void ToXyzBytes(BufferSpan<Color32> sourceColors, BufferSpan<byte> destBytes, int count)
            {
                byte* source = (byte*)sourceColors;
                byte* destination = (byte*)destBytes;

                for (int x = 0; x < count; x++)
                {
                    *destination = *(source + 0);
                    *(destination + 1) = *(source + 1);
                    *(destination + 2) = *(source + 2);

                    source += 4;
                    destination += 3;
                }
            }

            /// <inheritdoc />
            internal override void PackFromXyzwBytes(BufferSpan<byte> sourceBytes, BufferSpan<Color32> destColors, int count)
            {
                BufferSpan.Copy(sourceBytes, destColors, count);
            }

            /// <inheritdoc />
            internal override void ToXyzwBytes(BufferSpan<Color32> sourceColors, BufferSpan<byte> destBytes, int count)
            {
                BufferSpan.Copy(sourceColors, destBytes, count);
            }

            /// <inheritdoc />
            internal override unsafe void PackFromZyxBytes(BufferSpan<byte> sourceBytes, BufferSpan<Color32> destColors, int count)
            {
                byte* source = (byte*)sourceBytes;
                byte* destination = (byte*)destColors;

                for (int x = 0; x < count; x++)
                {
                    Unsafe.Write(destination, (uint)(*(source + 2) << 0 | *(source + 1) << 8 | *source << 16 | 255 << 24));

                    source += 3;
                    destination += 4;
                }
            }

            /// <inheritdoc />
            internal override unsafe void ToZyxBytes(BufferSpan<Color32> sourceColors, BufferSpan<byte> destBytes, int count)
            {
                byte* source = (byte*)sourceColors;
                byte* destination = (byte*)destBytes;

                for (int x = 0; x < count; x++)
                {
                    *destination = *(source + 2);
                    *(destination + 1) = *(source + 1);
                    *(destination + 2) = *(source + 0);

                    source += 4;
                    destination += 3;
                }
            }

            /// <inheritdoc />
            internal override unsafe void PackFromZyxwBytes(BufferSpan<byte> sourceBytes, BufferSpan<Color32> destColors, int count)
            {
                byte* source = (byte*)sourceBytes;
                byte* destination = (byte*)destColors;

                for (int x = 0; x < count; x++)
                {
                    Unsafe.Write(destination, (uint)(*(source + 2) << 0 | *(source + 1) << 8 | *source << 16 | *(source + 3) << 24));

                    source += 4;
                    destination += 4;
                }
            }

            /// <inheritdoc />
            internal override unsafe void ToZyxwBytes(BufferSpan<Color32> sourceColors, BufferSpan<byte> destBytes, int count)
            {
                byte* source = (byte*)sourceColors;
                byte* destination = (byte*)destBytes;

                for (int x = 0; x < count; x++)
                {
                    *destination = *(source + 2);
                    *(destination + 1) = *(source + 1);
                    *(destination + 2) = *(source + 0);
                    *(destination + 3) = *(source + 3);

                    source += 4;
                    destination += 4;
                }
            }

            /// <summary>
            /// Value type to store <see cref="Color32"/>-s unpacked into multiple <see cref="uint"/>-s.
            /// </summary>
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
                    this.g = p >> Color32.GreenShift;
                    this.b = p >> Color32.BlueShift;
                    this.a = p >> Color32.AlphaShift;
                }
            }
        }
    }
}
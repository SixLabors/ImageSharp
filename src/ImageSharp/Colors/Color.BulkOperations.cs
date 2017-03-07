namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public partial struct Color
    {
        /// <summary>
        /// <see cref="BulkPixelOperations{TColor}"/> implementation optimized for <see cref="Color"/>.
        /// </summary>
        internal class BulkOperations : BulkPixelOperations<Color>
        {
            /// <summary>
            /// Value type to store <see cref="Color"/>-s unpacked into multiple <see cref="uint"/>-s.
            /// </summary>
            private struct RGBAUint
            {
                private uint r;
                private uint g;
                private uint b;
                private uint a;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Load(uint p)
                {
                    this.r = p;
                    this.g = p >> Color.GreenShift;
                    this.b = p >> Color.BlueShift;
                    this.a = p >> Color.AlphaShift;
                }
            }

            /// <summary>
            /// SIMD optimized bulk implementation of <see cref="IPixel.PackFromVector4(Vector4)"/> 
            /// that works only with `count` divisible by <see cref="Vector{UInt32}.Count"/>.
            /// </summary>
            /// <param name="sourceColors">The <see cref="BufferPointer{T}"/> to the source colors.</param>
            /// <param name="destVectors">The <see cref="BufferPointer{T}"/> to the dstination vectors.</param>
            /// <param name="count">The number of pixels to convert.</param>
            /// <remarks>
            /// Implementation adapted from:
            /// <see>
            ///     <cref>http://stackoverflow.com/a/5362789</cref>
            /// </see>
            /// </remarks>
            internal static unsafe void ToVector4SimdAligned(
                BufferPointer<Color> sourceColors,
                BufferPointer<Vector4> destVectors,
                int count)
            {
                int vecSize = Vector<uint>.Count;

                DebugGuard.IsTrue(
                    count % vecSize == 0,
                    nameof(count),
                    "Argument 'count' should divisible by Vector<uint>.Count!"
                    );

                Vector<float> bVec = new Vector<float>(256.0f / 255.0f);
                Vector<float> magicFloat = new Vector<float>(32768.0f);
                Vector<uint> magicInt = new Vector<uint>(1191182336); // reinterpreded value of 32768.0f
                Vector<uint> mask = new Vector<uint>(255);

                int rawInputSize = count * 4;
                
                uint* src = (uint*)sourceColors.PointerAtOffset;
                uint* srcEnd = src + count;

                using (PinnedBuffer<uint> tempBuf = new PinnedBuffer<uint>(rawInputSize + Vector<uint>.Count))
                {
                    uint* tPtr = (uint*)tempBuf.Pointer;
                    uint[] temp = tempBuf.Array;
                    float[] fTemp = Unsafe.As<float[]>(temp);
                    RGBAUint* dst = (RGBAUint*)tPtr;

                    for (; src < srcEnd; src++, dst++)
                    {
                        dst->Load(*src);
                    }

                    for (int i = 0; i < rawInputSize; i += vecSize)
                    {
                        Vector<uint> vi = new Vector<uint>(temp, i);

                        vi &= mask;
                        vi |= magicInt;

                        Vector<float> vf = Vector.AsVectorSingle(vi);
                        vf = (vf - magicFloat) * bVec;
                        vf.CopyTo(fTemp, i);
                    }

                    BufferPointer.Copy<uint>(tempBuf, (BufferPointer<byte>) destVectors, rawInputSize);
                }
            }

            /// <inheritdoc />
            internal override void ToVector4(BufferPointer<Color> sourceColors, BufferPointer<Vector4> destVectors, int count)
            {
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
            internal override unsafe void PackFromXyzBytes(BufferPointer<byte> sourceBytes, BufferPointer<Color> destColors, int count)
            {
                byte* source = (byte*)sourceBytes;
                byte* destination =  (byte*)destColors;

                for (int x = 0; x < count; x++)
                {
                    Unsafe.Write(destination, (uint)(*source << 0 | *(source + 1) << 8 | *(source + 2) << 16 | 255 << 24));

                    source += 3;
                    destination += 4;
                }
            }

            /// <inheritdoc />
            internal override unsafe void ToXyzBytes(BufferPointer<Color> sourceColors, BufferPointer<byte> destBytes, int count)
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
            internal override void PackFromXyzwBytes(BufferPointer<byte> sourceBytes, BufferPointer<Color> destColors, int count)
            {
                BufferPointer.Copy(sourceBytes, destColors, count);
            }

            /// <inheritdoc />
            internal override void ToXyzwBytes(BufferPointer<Color> sourceColors, BufferPointer<byte> destBytes, int count)
            {
                BufferPointer.Copy(sourceColors, destBytes, count);
            }

            /// <inheritdoc />
            internal override unsafe void PackFromZyxBytes(BufferPointer<byte> sourceBytes, BufferPointer<Color> destColors, int count)
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
            internal override unsafe void ToZyxBytes(BufferPointer<Color> sourceColors, BufferPointer<byte> destBytes, int count)
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
            internal override unsafe void PackFromZyxwBytes(BufferPointer<byte> sourceBytes, BufferPointer<Color> destColors, int count)
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
            internal override unsafe void ToZyxwBytes(BufferPointer<Color> sourceColors, BufferPointer<byte> destBytes, int count)
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
        }
    }
}
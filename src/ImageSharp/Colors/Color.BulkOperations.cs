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
                Vector<uint> magicInt = new Vector<uint>(1191182336);
                Vector<float> magicFloat = new Vector<float>(32768.0f);
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

                    // TODO: Replace this with an optimized ArrayPointer.Copy() implementation:
                    uint byteCount = (uint)rawInputSize * sizeof(float);
                    
                    if (byteCount > 1024u)
                    {
                        Marshal.Copy(fTemp, 0, destVectors.PointerAtOffset, rawInputSize);
                    }
                    else
                    {
                        Unsafe.CopyBlock((void*)destVectors, tPtr, byteCount);
                    }
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
        }
    }
}
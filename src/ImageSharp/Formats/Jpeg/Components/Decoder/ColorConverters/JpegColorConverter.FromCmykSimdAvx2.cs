// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static SixLabors.ImageSharp.SimdUtils;
#else
using SixLabors.ImageSharp.Tuples;
#endif

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromCmykVector8 : JpegColorConverter
        {
            public FromCmykVector8(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            public static bool IsAvailable => Vector.IsHardwareAccelerated && SimdUtils.HasVector8;

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                int remainder = result.Length % 8;
                int simdCount = result.Length - remainder;
                if (simdCount > 0)
                {
                    ConvertCore(values.Slice(0, simdCount), result.Slice(0, simdCount), this.MaximumValue);
                }

                FromCmykBasic.ConvertCore(values.Slice(simdCount, remainder), result.Slice(simdCount, remainder), this.MaximumValue);
            }

            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result, float maxValue)
            {
                // This implementation is actually AVX specific.
                // An AVX register is capable of storing 8 float-s.
                if (!IsAvailable)
                {
                    throw new InvalidOperationException(
                        "JpegColorConverter.FromGrayscaleVector8 can be used only on architecture having 256 byte floating point SIMD registers!");
                }

#if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> cBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> mBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> yBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector256<float> kBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector256<float> resultBase =
                    ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(result));

                // Used for the color conversion
                var scale = Vector256.Create(1 / maxValue);
                var one = Vector256.Create(1F);

                // Used for packing
                ref byte control = ref MemoryMarshal.GetReference(HwIntrinsics.PermuteMaskEvenOdd8x32);
                Vector256<int> vcontrol = Unsafe.As<byte, Vector256<int>>(ref control);

                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector256<float> k = Avx2.PermuteVar8x32(Unsafe.Add(ref kBase, i), vcontrol);
                    Vector256<float> c = Avx2.PermuteVar8x32(Unsafe.Add(ref cBase, i), vcontrol);
                    Vector256<float> m = Avx2.PermuteVar8x32(Unsafe.Add(ref mBase, i), vcontrol);
                    Vector256<float> y = Avx2.PermuteVar8x32(Unsafe.Add(ref yBase, i), vcontrol);

                    k = Avx.Multiply(k, scale);

                    c = Avx.Multiply(Avx.Multiply(c, k), scale);
                    m = Avx.Multiply(Avx.Multiply(m, k), scale);
                    y = Avx.Multiply(Avx.Multiply(y, k), scale);

                    Vector256<float> cmLo = Avx.UnpackLow(c, m);
                    Vector256<float> yoLo = Avx.UnpackLow(y, one);
                    Vector256<float> cmHi = Avx.UnpackHigh(c, m);
                    Vector256<float> yoHi = Avx.UnpackHigh(y, one);

                    ref Vector256<float> destination = ref Unsafe.Add(ref resultBase, i * 4);

                    destination = Avx.Shuffle(cmLo, yoLo, 0b01_00_01_00);
                    Unsafe.Add(ref destination, 1) = Avx.Shuffle(cmLo, yoLo, 0b11_10_11_10);
                    Unsafe.Add(ref destination, 2) = Avx.Shuffle(cmHi, yoHi, 0b01_00_01_00);
                    Unsafe.Add(ref destination, 3) = Avx.Shuffle(cmHi, yoHi, 0b11_10_11_10);
                }
#else
                ref Vector<float> cBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> mBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> yBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> kBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                Vector4Pair cc = default;
                Vector4Pair mm = default;
                Vector4Pair yy = default;
                ref Vector<float> ccRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref cc);
                ref Vector<float> mmRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref mm);
                ref Vector<float> yyRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref yy);

                var scale = new Vector<float>(1 / maxValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector<float> c = Unsafe.Add(ref cBase, i);
                    Vector<float> m = Unsafe.Add(ref mBase, i);
                    Vector<float> y = Unsafe.Add(ref yBase, i);
                    Vector<float> k = Unsafe.Add(ref kBase, i) * scale;

                    c = (c * k) * scale;
                    m = (m * k) * scale;
                    y = (y * k) * scale;

                    ccRefAsVector = c;
                    mmRefAsVector = m;
                    yyRefAsVector = y;

                    // Collect (c0,c1...c8) (m0,m1...m8) (y0,y1...y8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref cc, ref mm, ref yy);
                }
#endif
            }
        }
    }
}

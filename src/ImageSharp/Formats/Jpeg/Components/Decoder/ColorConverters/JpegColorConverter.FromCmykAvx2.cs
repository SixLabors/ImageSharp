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
#endif

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromCmykAvx2 : Avx2JpegColorConverter
        {
            public FromCmykAvx2(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
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
                var scale = Vector256.Create(1 / this.MaximumValue);
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
#endif
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromCmykBasic.ConvertCore(values, result, this.MaximumValue);
        }
    }
}

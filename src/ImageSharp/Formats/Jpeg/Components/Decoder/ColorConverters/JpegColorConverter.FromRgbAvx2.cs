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
        internal sealed class FromRgbAvx2 : Avx2JpegColorConverter
        {
            public FromRgbAvx2(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> rBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> gBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> bBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

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
                    Vector256<float> r = Avx.Multiply(Avx2.PermuteVar8x32(Unsafe.Add(ref rBase, i), vcontrol), scale);
                    Vector256<float> g = Avx.Multiply(Avx2.PermuteVar8x32(Unsafe.Add(ref gBase, i), vcontrol), scale);
                    Vector256<float> b = Avx.Multiply(Avx2.PermuteVar8x32(Unsafe.Add(ref bBase, i), vcontrol), scale);

                    Vector256<float> rgLo = Avx.UnpackLow(r, g);
                    Vector256<float> boLo = Avx.UnpackLow(b, one);
                    Vector256<float> rgHi = Avx.UnpackHigh(r, g);
                    Vector256<float> boHi = Avx.UnpackHigh(b, one);

                    ref Vector256<float> destination = ref Unsafe.Add(ref resultBase, i * 4);

                    destination = Avx.Shuffle(rgLo, boLo, 0b01_00_01_00);
                    Unsafe.Add(ref destination, 1) = Avx.Shuffle(rgLo, boLo, 0b11_10_11_10);
                    Unsafe.Add(ref destination, 2) = Avx.Shuffle(rgHi, boHi, 0b01_00_01_00);
                    Unsafe.Add(ref destination, 3) = Avx.Shuffle(rgHi, boHi, 0b11_10_11_10);
                }
#endif
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromRgbBasic.ConvertCore(values, result, this.MaximumValue);
        }
    }
}

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
        internal sealed class FromGrayscaleAvx2 : Avx2JpegColorConverter
        {
            public FromGrayscaleAvx2(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> gBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));

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
                    Vector256<float> g = Avx.Multiply(Unsafe.Add(ref gBase, i), scale);

                    g = Avx2.PermuteVar8x32(g, vcontrol);

                    ref Vector256<float> destination = ref Unsafe.Add(ref resultBase, i * 4);

                    destination = Avx.Blend(Avx.Permute(g, 0b00_00_00_00), one, 0b1000_1000);
                    Unsafe.Add(ref destination, 1) = Avx.Blend(Avx.Shuffle(g, g, 0b01_01_01_01), one, 0b1000_1000);
                    Unsafe.Add(ref destination, 2) = Avx.Blend(Avx.Shuffle(g, g, 0b10_10_10_10), one, 0b1000_1000);
                    Unsafe.Add(ref destination, 3) = Avx.Blend(Avx.Shuffle(g, g, 0b11_11_11_11), one, 0b1000_1000);
                }
#endif
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromGrayscaleBasic.ConvertCore(values, result, this.MaximumValue);
        }
    }
}

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
        internal sealed class FromGrayscaleVector8 : JpegColorConverter
        {
            public FromGrayscaleVector8(int precision)
                : base(JpegColorSpace.Grayscale, precision)
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

                FromGrayscaleBasic.ConvertCore(values.Slice(simdCount, remainder), result.Slice(simdCount, remainder), this.MaximumValue);
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
                ref Vector256<float> gBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));

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
                    Vector256<float> g = Avx2.PermuteVar8x32(Unsafe.Add(ref gBase, i), vcontrol);

                    g = Avx.Multiply(g, scale);

                    ref Vector256<float> destination = ref Unsafe.Add(ref resultBase, i * 4);

                    destination = Avx.Blend(Avx.Permute(g, 0b00_00_00_00), one, 0b1000_1000);
                    Unsafe.Add(ref destination, 1) = Avx.Blend(Avx.Permute(g, 0b01_01_01_01), one, 0b1000_1000);
                    Unsafe.Add(ref destination, 2) = Avx.Blend(Avx.Permute(g, 0b10_10_10_10), one, 0b1000_1000);
                    Unsafe.Add(ref destination, 3) = Avx.Blend(Avx.Permute(g, 0b11_11_11_11), one, 0b1000_1000);
                }
#else
                ref Vector<float> gBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                Vector4Pair gg = default;
                ref Vector<float> ggRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref gg);

                var scale = new Vector<float>(1 / maxValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector<float> g = Unsafe.Add(ref gBase, i);
                    g *= scale;

                    ggRefAsVector = g;

                    // Collect (g0,g1...g7) vector values in the expected (g0,g0,g0,1), (g1,g1,g1,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref gg);
                }
#endif
            }
        }
    }
}

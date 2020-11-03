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
        internal sealed class FromRgbVector8 : JpegColorConverter
        {
            public FromRgbVector8(int precision)
                : base(JpegColorSpace.RGB, precision)
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

                FromRgbBasic.ConvertCore(values.Slice(simdCount, remainder), result.Slice(simdCount, remainder), this.MaximumValue);
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
                ref Vector256<float> rBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> gBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> bBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

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
#else
                ref Vector<float> rBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> gBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> bBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                Vector4Pair rr = default;
                Vector4Pair gg = default;
                Vector4Pair bb = default;
                ref Vector<float> rrRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref rr);
                ref Vector<float> ggRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref gg);
                ref Vector<float> bbRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref bb);

                var scale = new Vector<float>(1 / maxValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector<float> r = Unsafe.Add(ref rBase, i);
                    Vector<float> g = Unsafe.Add(ref gBase, i);
                    Vector<float> b = Unsafe.Add(ref bBase, i);
                    r *= scale;
                    g *= scale;
                    b *= scale;

                    rrRefAsVector = r;
                    ggRefAsVector = g;
                    bbRefAsVector = b;

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref rr, ref gg, ref bb);
                }
#endif
            }
        }
    }
}

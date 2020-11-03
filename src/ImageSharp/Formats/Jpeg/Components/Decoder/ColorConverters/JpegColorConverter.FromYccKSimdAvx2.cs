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
        internal sealed class FromYccKVector8 : JpegColorConverter
        {
            public FromYccKVector8(int precision)
                : base(JpegColorSpace.Ycck, precision)
            {
            }

            public static bool IsAvailable => Vector.IsHardwareAccelerated && SimdUtils.HasVector8;

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                int remainder = result.Length % 8;
                int simdCount = result.Length - remainder;
                if (simdCount > 0)
                {
                    ConvertCore(values.Slice(0, simdCount), result.Slice(0, simdCount), this.MaximumValue, this.HalfValue);
                }

                FromYccKBasic.ConvertCore(values.Slice(simdCount, remainder), result.Slice(simdCount, remainder), this.MaximumValue, this.HalfValue);
            }

            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result, float maxValue, float halfValue)
            {
                // This implementation is actually AVX specific.
                // An AVX register is capable of storing 8 float-s.
                if (!IsAvailable)
                {
                    throw new InvalidOperationException(
                        "JpegColorConverter.FromYCbCrSimd256 can be used only on architecture having 256 byte floating point SIMD registers!");
                }

#if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> yBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> cbBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> crBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector256<float> kBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector256<float> resultBase =
                    ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(result));

                // Used for the color conversion
                var chromaOffset = Vector256.Create(-halfValue);
                var scale = Vector256.Create(1 / maxValue);
                var max = Vector256.Create(maxValue);
                var rCrMult = Vector256.Create(1.402F);
                var gCbMult = Vector256.Create(-0.344136F);
                var gCrMult = Vector256.Create(-0.714136F);
                var bCbMult = Vector256.Create(1.772F);

                // Used for packing.
                var va = Vector256.Create(1F);
                ref byte control = ref MemoryMarshal.GetReference(HwIntrinsics.PermuteMaskEvenOdd8x32);
                Vector256<int> vcontrol = Unsafe.As<byte, Vector256<int>>(ref control);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    // k = kVals[i] / 256F;
                    Vector256<float> y = Unsafe.Add(ref yBase, i);
                    Vector256<float> cb = Avx.Add(Unsafe.Add(ref cbBase, i), chromaOffset);
                    Vector256<float> cr = Avx.Add(Unsafe.Add(ref crBase, i), chromaOffset);
                    Vector256<float> k = Avx.Divide(Unsafe.Add(ref kBase, i), max);

                    y = Avx2.PermuteVar8x32(y, vcontrol);
                    cb = Avx2.PermuteVar8x32(cb, vcontrol);
                    cr = Avx2.PermuteVar8x32(cr, vcontrol);
                    k = Avx2.PermuteVar8x32(k, vcontrol);

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    // Adding & multiplying 8 elements at one time:
                    Vector256<float> r = HwIntrinsics.MultiplyAdd(y, cr, rCrMult);
                    Vector256<float> g = HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                    Vector256<float> b = HwIntrinsics.MultiplyAdd(y, cb, bCbMult);

                    r = Avx.Subtract(max, Avx.RoundToNearestInteger(r));
                    g = Avx.Subtract(max, Avx.RoundToNearestInteger(g));
                    b = Avx.Subtract(max, Avx.RoundToNearestInteger(b));

                    r = Avx.Multiply(Avx.Multiply(r, k), scale);
                    g = Avx.Multiply(Avx.Multiply(g, k), scale);
                    b = Avx.Multiply(Avx.Multiply(b, k), scale);

                    Vector256<float> vte = Avx.UnpackLow(r, b);
                    Vector256<float> vto = Avx.UnpackLow(g, va);

                    ref Vector256<float> destination = ref Unsafe.Add(ref resultBase, i * 4);

                    destination = Avx.UnpackLow(vte, vto);
                    Unsafe.Add(ref destination, 1) = Avx.UnpackHigh(vte, vto);

                    vte = Avx.UnpackHigh(r, b);
                    vto = Avx.UnpackHigh(g, va);

                    Unsafe.Add(ref destination, 2) = Avx.UnpackLow(vte, vto);
                    Unsafe.Add(ref destination, 3) = Avx.UnpackHigh(vte, vto);
                }
#else
                ref Vector<float> yBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> cbBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> crBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> kBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                var chromaOffset = new Vector<float>(-halfValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;

                Vector4Pair rr = default;
                Vector4Pair gg = default;
                Vector4Pair bb = default;

                ref Vector<float> rrRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref rr);
                ref Vector<float> ggRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref gg);
                ref Vector<float> bbRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref bb);

                var scale = new Vector<float>(1 / maxValue);
                var max = new Vector<float>(maxValue);

                for (int i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    // k = kVals[i] / 256F;
                    Vector<float> y = Unsafe.Add(ref yBase, i);
                    Vector<float> cb = Unsafe.Add(ref cbBase, i) + chromaOffset;
                    Vector<float> cr = Unsafe.Add(ref crBase, i) + chromaOffset;
                    Vector<float> k = Unsafe.Add(ref kBase, i) / max;

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    // Adding & multiplying 8 elements at one time:
                    Vector<float> r = y + (cr * new Vector<float>(1.402F));
                    Vector<float> g = y - (cb * new Vector<float>(0.344136F)) - (cr * new Vector<float>(0.714136F));
                    Vector<float> b = y + (cb * new Vector<float>(1.772F));

                    r = (max - r.FastRound()) * k;
                    g = (max - g.FastRound()) * k;
                    b = (max - b.FastRound()) * k;
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

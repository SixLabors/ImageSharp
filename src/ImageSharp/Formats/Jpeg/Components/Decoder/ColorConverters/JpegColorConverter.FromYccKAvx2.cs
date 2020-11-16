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
        internal sealed class FromYccKAvx2 : Avx2JpegColorConverter
        {
            public FromYccKAvx2(int precision)
                : base(JpegColorSpace.Ycck, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
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
                var chromaOffset = Vector256.Create(-this.HalfValue);
                var scale = Vector256.Create(1 / this.MaximumValue);
                var max = Vector256.Create(this.MaximumValue);
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
                    Vector256<float> g =
                        HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
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
#endif
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromYccKBasic.ConvertCore(values, result, this.MaximumValue, this.HalfValue);
        }
    }
}

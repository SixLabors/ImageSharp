// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static SixLabors.ImageSharp.SimdUtils;

// ReSharper disable ImpureMethodCallOnReadonlyValueField
namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromYCbCrAvx : JpegColorConverterAvx
        {
            public FromYCbCrAvx(int precision)
                : base(JpegColorSpace.YCbCr, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                ref Vector256<float> c0Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> c1Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> c2Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

                // Used for the color conversion
                var chromaOffset = Vector256.Create(-this.HalfValue);
                var scale = Vector256.Create(1 / this.MaximumValue);
                var rCrMult = Vector256.Create(FromYCbCrScalar.RCrMult);
                var gCbMult = Vector256.Create(-FromYCbCrScalar.GCbMult);
                var gCrMult = Vector256.Create(-FromYCbCrScalar.GCrMult);
                var bCbMult = Vector256.Create(FromYCbCrScalar.BCbMult);

                // Walking 8 elements at one step:
                nint n = values.Component0.Length / Vector256<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);

                    Vector256<float> y = c0;
                    Vector256<float> cb = Avx.Add(c1, chromaOffset);
                    Vector256<float> cr = Avx.Add(c2, chromaOffset);

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    Vector256<float> r = HwIntrinsics.MultiplyAdd(y, cr, rCrMult);
                    Vector256<float> g = HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                    Vector256<float> b = HwIntrinsics.MultiplyAdd(y, cb, bCbMult);

                    r = Avx.Multiply(Avx.RoundToNearestInteger(r), scale);
                    g = Avx.Multiply(Avx.RoundToNearestInteger(g), scale);
                    b = Avx.Multiply(Avx.RoundToNearestInteger(b), scale);

                    c0 = r;
                    c1 = g;
                    c2 = b;
                }
            }

            public override void ConvertFromRgbInplace(in ComponentValues values)
            {
                ref Vector256<float> c0Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> c1Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> c2Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

                // Used for the color conversion
                var chromaOffset = Vector256.Create(this.HalfValue);
                var scale = Vector256.Create(this.MaximumValue);
                var f0299 = Vector256.Create(0.299f);
                var f0587 = Vector256.Create(0.587f);
                var f0114 = Vector256.Create(0.114f);
                var fn0168736 = Vector256.Create(-0.168736f);
                var fn0331264 = Vector256.Create(-0.331264f);
                var fn0418688 = Vector256.Create(-0.418688f);
                var fn0081312F = Vector256.Create(-0.081312F);
                var f05 = Vector256.Create(0.5f);

                nint n = values.Component0.Length / Vector256<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);

                    Vector256<float> r = Avx.Multiply(c0, scale);
                    Vector256<float> g = Avx.Multiply(c1, scale);
                    Vector256<float> b = Avx.Multiply(c2, scale);

                    // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                    // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                    // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                    Vector256<float> y = HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);
                    Vector256<float> cb = Avx.Add(chromaOffset, HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));
                    Vector256<float> cr = Avx.Add(chromaOffset, HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));

                    c0 = y;
                    c1 = cb;
                    c2 = cr;
                }
            }
        }
    }
}
#endif

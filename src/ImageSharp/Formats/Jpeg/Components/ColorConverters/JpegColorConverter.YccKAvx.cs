// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if SUPPORTS_RUNTIME_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class YccKAvx : JpegColorConverterAvx
        {
            public YccKAvx(int precision)
                : base(JpegColorSpace.Ycck, precision)
            {
            }

            /// <inheritdoc/>
            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                ref Vector256<float> c0Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> c1Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> c2Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector256<float> kBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

                // Used for the color conversion
                var chromaOffset = Vector256.Create(-this.HalfValue);
                var scale = Vector256.Create(1 / (this.MaximumValue * this.MaximumValue));
                var max = Vector256.Create(this.MaximumValue);
                var rCrMult = Vector256.Create(YCbCrScalar.RCrMult);
                var gCbMult = Vector256.Create(-YCbCrScalar.GCbMult);
                var gCrMult = Vector256.Create(-YCbCrScalar.GCrMult);
                var bCbMult = Vector256.Create(YCbCrScalar.BCbMult);

                // Walking 8 elements at one step:
                nint n = values.Component0.Length / Vector256<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    // k = kVals[i] / 256F;
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);
                    Vector256<float> y = c0;
                    Vector256<float> cb = Avx.Add(c1, chromaOffset);
                    Vector256<float> cr = Avx.Add(c2, chromaOffset);
                    Vector256<float> scaledK = Avx.Multiply(Unsafe.Add(ref kBase, i), scale);

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    Vector256<float> r = HwIntrinsics.MultiplyAdd(y, cr, rCrMult);
                    Vector256<float> g =
                        HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                    Vector256<float> b = HwIntrinsics.MultiplyAdd(y, cb, bCbMult);

                    r = Avx.Subtract(max, Avx.RoundToNearestInteger(r));
                    g = Avx.Subtract(max, Avx.RoundToNearestInteger(g));
                    b = Avx.Subtract(max, Avx.RoundToNearestInteger(b));

                    r = Avx.Multiply(r, scaledK);
                    g = Avx.Multiply(g, scaledK);
                    b = Avx.Multiply(b, scaledK);

                    c0 = r;
                    c1 = g;
                    c2 = b;
                }
            }

            /// <inheritdoc/>
            public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            {
                // rgb -> cmyk
                CmykAvx.ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

                // cmyk -> ycck
                ref Vector256<float> destY =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> destCb =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> destCr =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

                ref Vector256<float> srcR = ref destY;
                ref Vector256<float> srcG = ref destCb;
                ref Vector256<float> srcB = ref destCr;

                // Used for the color conversion
                var maxSampleValue = Vector256.Create(this.MaximumValue);

                var chromaOffset = Vector256.Create(this.HalfValue);

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
                    Vector256<float> r = Avx.Subtract(maxSampleValue, Unsafe.Add(ref srcR, i));
                    Vector256<float> g = Avx.Subtract(maxSampleValue, Unsafe.Add(ref srcG, i));
                    Vector256<float> b = Avx.Subtract(maxSampleValue, Unsafe.Add(ref srcB, i));

                    // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                    // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                    // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                    Vector256<float> y = HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(Avx.Multiply(f0114, b), f0587, g), f0299, r);
                    Vector256<float> cb = Avx.Add(chromaOffset, HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(Avx.Multiply(f05, b), fn0331264, g), fn0168736, r));
                    Vector256<float> cr = Avx.Add(chromaOffset, HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(Avx.Multiply(fn0081312F, b), fn0418688, g), f05, r));

                    Unsafe.Add(ref destY, i) = y;
                    Unsafe.Add(ref destCb, i) = cb;
                    Unsafe.Add(ref destCr, i) = cr;
                }
            }
        }
    }
}
#endif

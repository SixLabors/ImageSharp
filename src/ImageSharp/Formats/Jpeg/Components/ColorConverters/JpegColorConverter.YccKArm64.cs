// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YccKArm64 : JpegColorConverterArm64
    {
        public YccKArm64(int precision)
            : base(JpegColorSpace.Ycck, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInplace(in ComponentValues values)
        {
            ref Vector128<float> c0Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> c1Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> c2Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector128<float> kBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            // Used for the color conversion
            var chromaOffset = Vector128.Create(-this.HalfValue);
            var scale = Vector128.Create(1 / (this.MaximumValue * this.MaximumValue));
            var max = Vector128.Create(this.MaximumValue);
            var rCrMult = Vector128.Create(YCbCrScalar.RCrMult);
            var gCbMult = Vector128.Create(-YCbCrScalar.GCbMult);
            var gCrMult = Vector128.Create(-YCbCrScalar.GCrMult);
            var bCbMult = Vector128.Create(YCbCrScalar.BCbMult);

            // Walking 8 elements at one step:
            nuint n = (uint)values.Component0.Length / (uint)Vector128<float>.Count;
            for (nuint i = 0; i < n; i++)
            {
                // y = yVals[i];
                // cb = cbVals[i] - 128F;
                // cr = crVals[i] - 128F;
                // k = kVals[i] / 256F;
                ref Vector128<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector128<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector128<float> c2 = ref Unsafe.Add(ref c2Base, i);
                Vector128<float> y = c0;
                Vector128<float> cb = AdvSimd.Add(c1, chromaOffset);
                Vector128<float> cr = AdvSimd.Add(c2, chromaOffset);
                Vector128<float> scaledK = AdvSimd.Multiply(Unsafe.Add(ref kBase, i), scale);

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector128<float> r = HwIntrinsics.MultiplyAdd(y, cr, rCrMult);
                Vector128<float> g =
                    HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                Vector128<float> b = HwIntrinsics.MultiplyAdd(y, cb, bCbMult);

                r = AdvSimd.Subtract(max, AdvSimd.RoundToNearest(r));
                g = AdvSimd.Subtract(max, AdvSimd.RoundToNearest(g));
                b = AdvSimd.Subtract(max, AdvSimd.RoundToNearest(b));

                r = AdvSimd.Multiply(r, scaledK);
                g = AdvSimd.Multiply(g, scaledK);
                b = AdvSimd.Multiply(b, scaledK);

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            // rgb -> cmyk
            CmykArm64.ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

            // cmyk -> ycck
            ref Vector128<float> destY =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> destCb =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> destCr =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));

            ref Vector128<float> srcR = ref destY;
            ref Vector128<float> srcG = ref destCb;
            ref Vector128<float> srcB = ref destCr;

            // Used for the color conversion
            var maxSampleValue = Vector128.Create(this.MaximumValue);

            var chromaOffset = Vector128.Create(this.HalfValue);

            var f0299 = Vector128.Create(0.299f);
            var f0587 = Vector128.Create(0.587f);
            var f0114 = Vector128.Create(0.114f);
            var fn0168736 = Vector128.Create(-0.168736f);
            var fn0331264 = Vector128.Create(-0.331264f);
            var fn0418688 = Vector128.Create(-0.418688f);
            var fn0081312F = Vector128.Create(-0.081312F);
            var f05 = Vector128.Create(0.5f);

            nuint n = (uint)values.Component0.Length / (uint)Vector128<float>.Count;
            for (nuint i = 0; i < n; i++)
            {
                Vector128<float> r = AdvSimd.Subtract(maxSampleValue, Unsafe.Add(ref srcR, i));
                Vector128<float> g = AdvSimd.Subtract(maxSampleValue, Unsafe.Add(ref srcG, i));
                Vector128<float> b = AdvSimd.Subtract(maxSampleValue, Unsafe.Add(ref srcB, i));

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Vector128<float> y = HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(AdvSimd.Multiply(f0114, b), f0587, g), f0299, r);
                Vector128<float> cb = AdvSimd.Add(chromaOffset, HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(AdvSimd.Multiply(f05, b), fn0331264, g), fn0168736, r));
                Vector128<float> cr = AdvSimd.Add(chromaOffset, HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(AdvSimd.Multiply(fn0081312F, b), fn0418688, g), f05, r));

                Unsafe.Add(ref destY, i) = y;
                Unsafe.Add(ref destCb, i) = cb;
                Unsafe.Add(ref destCr, i) = cr;
            }
        }
    }
}

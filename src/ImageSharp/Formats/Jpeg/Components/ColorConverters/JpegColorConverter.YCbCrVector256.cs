// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YCbCrVector256 : JpegColorConverterVector256
    {
        public YCbCrVector256(int precision)
            : base(JpegColorSpace.YCbCr, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector256<float> c0Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> c1Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> c2Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

            Vector256<float> chromaOffset = Vector256.Create(-this.HalfValue);
            Vector256<float> scale = Vector256.Create(1 / this.MaximumValue);
            Vector256<float> rCrMult = Vector256.Create(YCbCrScalar.RCrMult);
            Vector256<float> gCbMult = Vector256.Create(-YCbCrScalar.GCbMult);
            Vector256<float> gCrMult = Vector256.Create(-YCbCrScalar.GCrMult);
            Vector256<float> bCbMult = Vector256.Create(YCbCrScalar.BCbMult);

            // Walking 8 elements at one step:
            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                // y = yVals[i];
                // cb = cbVals[i] - 128F;
                // cr = crVals[i] - 128F;
                ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);

                Vector256<float> y = c0;
                Vector256<float> cb = c1 + chromaOffset;
                Vector256<float> cr = c2 + chromaOffset;

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector256<float> r = Vector256_.MultiplyAdd(y, cr, rCrMult);
                Vector256<float> g = Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                Vector256<float> b = Vector256_.MultiplyAdd(y, cb, bCbMult);

                r = Vector256_.RoundToNearestInteger(r) * scale;
                g = Vector256_.RoundToNearestInteger(g) * scale;
                b = Vector256_.RoundToNearestInteger(b) * scale;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector256<float> destY =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> destCb =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> destCr =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

            ref Vector256<float> srcR =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector256<float> srcG =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector256<float> srcB =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector256<float> chromaOffset = Vector256.Create(this.HalfValue);
            Vector256<float> f0299 = Vector256.Create(0.299f);
            Vector256<float> f0587 = Vector256.Create(0.587f);
            Vector256<float> f0114 = Vector256.Create(0.114f);
            Vector256<float> fn0168736 = Vector256.Create(-0.168736f);
            Vector256<float> fn0331264 = Vector256.Create(-0.331264f);
            Vector256<float> fn0418688 = Vector256.Create(-0.418688f);
            Vector256<float> fn0081312F = Vector256.Create(-0.081312F);
            Vector256<float> f05 = Vector256.Create(0.5f);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector256<float> r = Unsafe.Add(ref srcR, i);
                Vector256<float> g = Unsafe.Add(ref srcG, i);
                Vector256<float> b = Unsafe.Add(ref srcB, i);

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Vector256<float> y = Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
                Vector256<float> cb = chromaOffset + Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(f05 * b, fn0331264, g), fn0168736, r);
                Vector256<float> cr = chromaOffset + Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(fn0081312F * b, fn0418688, g), f05, r);

                Unsafe.Add(ref destY, i) = y;
                Unsafe.Add(ref destCb, i) = cb;
                Unsafe.Add(ref destCr, i) = cr;
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ImpureMethodCallOnReadonlyValueField
namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YCbCrVector : JpegColorConverterVector
    {
        public YCbCrVector(int precision)
            : base(JpegColorSpace.YCbCr, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceVectorized(in ComponentValues values)
        {
            ref Vector<float> c0Base =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector<float> c1Base =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector<float> c2Base =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

            var chromaOffset = new Vector<float>(-this.HalfValue);

            var scale = new Vector<float>(1 / this.MaximumValue);
            var rCrMult = new Vector<float>(YCbCrScalar.RCrMult);
            var gCbMult = new Vector<float>(-YCbCrScalar.GCbMult);
            var gCrMult = new Vector<float>(-YCbCrScalar.GCrMult);
            var bCbMult = new Vector<float>(YCbCrScalar.BCbMult);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                // y = yVals[i];
                // cb = cbVals[i] - 128F;
                // cr = crVals[i] - 128F;
                ref Vector<float> c0 = ref Extensions.UnsafeAdd(ref c0Base, i);
                ref Vector<float> c1 = ref Extensions.UnsafeAdd(ref c1Base, i);
                ref Vector<float> c2 = ref Extensions.UnsafeAdd(ref c2Base, i);
                Vector<float> y = Extensions.UnsafeAdd(ref c0Base, i);
                Vector<float> cb = Extensions.UnsafeAdd(ref c1Base, i) + chromaOffset;
                Vector<float> cr = Extensions.UnsafeAdd(ref c2Base, i) + chromaOffset;

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector<float> r = y + (cr * rCrMult);
                Vector<float> g = y + (cb * gCbMult) + (cr * gCrMult);
                Vector<float> b = y + (cb * bCbMult);

                r = r.FastRound();
                g = g.FastRound();
                b = b.FastRound();
                r *= scale;
                g *= scale;
                b *= scale;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceScalarRemainder(in ComponentValues values)
            => YCbCrScalar.ConvertToRgbInplace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector<float> destY =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector<float> destCb =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector<float> destCr =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

            ref Vector<float> srcR =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector<float> srcG =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector<float> srcB =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(bLane));

            var chromaOffset = new Vector<float>(this.HalfValue);

            var rYMult = new Vector<float>(0.299f);
            var gYMult = new Vector<float>(0.587f);
            var bYMult = new Vector<float>(0.114f);

            var rCbMult = new Vector<float>(0.168736f);
            var gCbMult = new Vector<float>(0.331264f);
            var bCbMult = new Vector<float>(0.5f);

            var rCrMult = new Vector<float>(0.5f);
            var gCrMult = new Vector<float>(0.418688f);
            var bCrMult = new Vector<float>(0.081312f);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector<float> r = Extensions.UnsafeAdd(ref srcR, i);
                Vector<float> g = Extensions.UnsafeAdd(ref srcG, i);
                Vector<float> b = Extensions.UnsafeAdd(ref srcB, i);

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Extensions.UnsafeAdd(ref destY, i) = (rYMult * r) + (gYMult * g) + (bYMult * b);
                Extensions.UnsafeAdd(ref destCb, i) = chromaOffset - (rCbMult * r) - (gCbMult * g) + (bCbMult * b);
                Extensions.UnsafeAdd(ref destCr, i) = chromaOffset + (rCrMult * r) - (gCrMult * g) - (bCrMult * b);
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => YCbCrScalar.ConvertFromRgb(values, this.HalfValue, r, g, b);
    }
}

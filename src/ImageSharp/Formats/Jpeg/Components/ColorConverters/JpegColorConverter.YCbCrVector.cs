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

            Vector<float> chromaOffset = new(-this.HalfValue);

            Vector<float> scale = new(1 / this.MaximumValue);
            Vector<float> rCrMult = new(YCbCrScalar.RCrMult);
            Vector<float> gCbMult = new(-YCbCrScalar.GCbMult);
            Vector<float> gCrMult = new(-YCbCrScalar.GCrMult);
            Vector<float> bCbMult = new(YCbCrScalar.BCbMult);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                // y = yVals[i];
                // cb = cbVals[i] - 128F;
                // cr = crVals[i] - 128F;
                ref Vector<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector<float> c2 = ref Unsafe.Add(ref c2Base, i);
                Vector<float> y = Unsafe.Add(ref c0Base, i);
                Vector<float> cb = Unsafe.Add(ref c1Base, i) + chromaOffset;
                Vector<float> cr = Unsafe.Add(ref c2Base, i) + chromaOffset;

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

            Vector<float> chromaOffset = new(this.HalfValue);

            Vector<float> rYMult = new(0.299f);
            Vector<float> gYMult = new(0.587f);
            Vector<float> bYMult = new(0.114f);

            Vector<float> rCbMult = new(0.168736f);
            Vector<float> gCbMult = new(0.331264f);
            Vector<float> bCbMult = new(0.5f);

            Vector<float> rCrMult = new(0.5f);
            Vector<float> gCrMult = new(0.418688f);
            Vector<float> bCrMult = new(0.081312f);

            nuint n = values.Component0.VectorCount<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector<float> r = Unsafe.Add(ref srcR, i);
                Vector<float> g = Unsafe.Add(ref srcG, i);
                Vector<float> b = Unsafe.Add(ref srcB, i);

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Unsafe.Add(ref destY, i) = (rYMult * r) + (gYMult * g) + (bYMult * b);
                Unsafe.Add(ref destCb, i) = chromaOffset - (rCbMult * r) - (gCbMult * g) + (bCbMult * b);
                Unsafe.Add(ref destCr, i) = chromaOffset + (rCrMult * r) - (gCrMult * g) - (bCrMult * b);
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => YCbCrScalar.ConvertFromRgb(values, this.HalfValue, r, g, b);
    }
}

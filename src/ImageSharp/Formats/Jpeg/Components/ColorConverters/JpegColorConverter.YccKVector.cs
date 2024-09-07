// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YccKVector : JpegColorConverterVector
    {
        public YccKVector(int precision)
            : base(JpegColorSpace.Ycck, precision)
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
            ref Vector<float> kBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector<float> chromaOffset = new(-this.HalfValue);
            Vector<float> scale = new(1 / (this.MaximumValue * this.MaximumValue));
            Vector<float> max = new(this.MaximumValue);
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
                // k = kVals[i] / 256F;
                ref Vector<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector<float> c2 = ref Unsafe.Add(ref c2Base, i);

                Vector<float> y = c0;
                Vector<float> cb = c1 + chromaOffset;
                Vector<float> cr = c2 + chromaOffset;
                Vector<float> scaledK = Unsafe.Add(ref kBase, i) * scale;

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector<float> r = y + (cr * rCrMult);
                Vector<float> g = y + (cb * gCbMult) + (cr * gCrMult);
                Vector<float> b = y + (cb * bCbMult);

                r = (max - r.FastRound()) * scaledK;
                g = (max - g.FastRound()) * scaledK;
                b = (max - b.FastRound()) * scaledK;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInplaceScalarRemainder(in ComponentValues values)
            => YccKScalar.ConvertToRgpInplace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            // rgb -> cmyk
            CmykVector.ConvertFromRgbInplaceVectorized(in values, this.MaximumValue, rLane, gLane, bLane);

            // cmyk -> ycck
            ref Vector<float> destY =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector<float> destCb =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector<float> destCr =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

            ref Vector<float> srcR = ref destY;
            ref Vector<float> srcG = ref destCb;
            ref Vector<float> srcB = ref destCr;

            Vector<float> maxSampleValue = new(this.MaximumValue);

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
                Vector<float> r = maxSampleValue - Unsafe.Add(ref srcR, i);
                Vector<float> g = maxSampleValue - Unsafe.Add(ref srcG, i);
                Vector<float> b = maxSampleValue - Unsafe.Add(ref srcB, i);

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
        {
            // rgb -> cmyk
            CmykScalar.ConvertFromRgb(in values, this.MaximumValue, r, g, b);

            // cmyk -> ycck
            YccKScalar.ConvertFromRgb(in values, this.HalfValue, this.MaximumValue, r, g, b);
        }
    }
}

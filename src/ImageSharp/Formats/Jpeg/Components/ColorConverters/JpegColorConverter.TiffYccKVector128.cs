// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class TiffYccKVector128 : JpegColorConverterVector128
    {
        public TiffYccKVector128(int precision)
            : base(JpegColorSpace.TiffYccK, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector128<float> c0Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> c1Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> c2Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector128<float> c3Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector128<float> scale = Vector128.Create(1F / this.MaximumValue);
            Vector128<float> chromaOffset = Vector128.Create(this.HalfValue) * scale;
            Vector128<float> rCrMult = Vector128.Create(YCbCrScalar.RCrMult);
            Vector128<float> gCbMult = Vector128.Create(-YCbCrScalar.GCbMult);
            Vector128<float> gCrMult = Vector128.Create(-YCbCrScalar.GCrMult);
            Vector128<float> bCbMult = Vector128.Create(YCbCrScalar.BCbMult);

            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector128<float> c0 = ref Unsafe.Add(ref c0Base, i);
                ref Vector128<float> c1 = ref Unsafe.Add(ref c1Base, i);
                ref Vector128<float> c2 = ref Unsafe.Add(ref c2Base, i);
                ref Vector128<float> c3 = ref Unsafe.Add(ref c3Base, i);

                Vector128<float> y = c0 * scale;
                Vector128<float> cb = (c1 * scale) - chromaOffset;
                Vector128<float> cr = (c2 * scale) - chromaOffset;
                Vector128<float> scaledK = Vector128<float>.One - (c3 * scale);

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                Vector128<float> r = Vector128_.MultiplyAdd(y, cr, rCrMult) * scaledK;
                Vector128<float> g = Vector128_.MultiplyAdd(Vector128_.MultiplyAdd(y, cb, gCbMult), cr, gCrMult) * scaledK;
                Vector128<float> b = Vector128_.MultiplyAdd(y, cb, bCbMult) * scaledK;

                c0 = r;
                c1 = g;
                c2 = b;
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => TiffYccKScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector128<float> srcR =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector128<float> srcG =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector128<float> srcB =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(bLane));

            ref Vector128<float> destY =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> destCb =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> destCr =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector128<float> destK =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            Vector128<float> maxSourceValue = Vector128.Create(255F);
            Vector128<float> maxSampleValue = Vector128.Create(this.MaximumValue);
            Vector128<float> chromaOffset = Vector128.Create(this.HalfValue);

            Vector128<float> f0299 = Vector128.Create(0.299f);
            Vector128<float> f0587 = Vector128.Create(0.587f);
            Vector128<float> f0114 = Vector128.Create(0.114f);
            Vector128<float> fn0168736 = Vector128.Create(-0.168736f);
            Vector128<float> fn0331264 = Vector128.Create(-0.331264f);
            Vector128<float> fn0418688 = Vector128.Create(-0.418688f);
            Vector128<float> fn0081312F = Vector128.Create(-0.081312F);
            Vector128<float> f05 = Vector128.Create(0.5f);

            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector128<float> r = Unsafe.Add(ref srcR, i) / maxSourceValue;
                Vector128<float> g = Unsafe.Add(ref srcG, i) / maxSourceValue;
                Vector128<float> b = Unsafe.Add(ref srcB, i) / maxSourceValue;
                Vector128<float> ktmp = Vector128<float>.One - Vector128.Max(r, Vector128.Min(g, b));

                Vector128<float> kMask = ~Vector128.Equals(ktmp, Vector128<float>.One);
                Vector128<float> divisor = Vector128<float>.One - ktmp;

                r /= divisor;
                g /= divisor;
                b /= divisor;

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                Vector128<float> y = Vector128_.MultiplyAdd(Vector128_.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
                Vector128<float> cb = chromaOffset + Vector128_.MultiplyAdd(Vector128_.MultiplyAdd(f05 * b, fn0331264, g), fn0168736, r);
                Vector128<float> cr = chromaOffset + Vector128_.MultiplyAdd(Vector128_.MultiplyAdd(fn0081312F * b, fn0418688, g), f05, r);

                Unsafe.Add(ref destY, i) = y * maxSampleValue;
                Unsafe.Add(ref destCb, i) = chromaOffset + (cb * maxSampleValue);
                Unsafe.Add(ref destCr, i) = chromaOffset + (cr * maxSampleValue);
                Unsafe.Add(ref destK, i) = ktmp * maxSampleValue;
            }
        }
    }
}

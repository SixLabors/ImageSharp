// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class CmykAvx : JpegColorConverterAvx
    {
        public CmykAvx(int precision)
            : base(JpegColorSpace.Cmyk, precision)
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
            ref Vector256<float> c3Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

            // Used for the color conversion
            Vector256<float> scale = Vector256.Create(1 / (this.MaximumValue * this.MaximumValue));

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector256<float> c = ref Unsafe.Add(ref c0Base, i);
                ref Vector256<float> m = ref Unsafe.Add(ref c1Base, i);
                ref Vector256<float> y = ref Unsafe.Add(ref c2Base, i);
                Vector256<float> k = Unsafe.Add(ref c3Base, i);

                k = Avx.Multiply(k, scale);
                c = Avx.Multiply(c, k);
                m = Avx.Multiply(m, k);
                y = Avx.Multiply(y, k);
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertFromRgb(in ComponentValues values, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector256<float> destC =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector256<float> destM =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector256<float> destY =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector256<float> destK =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

            ref Vector256<float> srcR =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector256<float> srcG =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector256<float> srcB =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector256<float> scale = Vector256.Create(maxValue);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                Vector256<float> ctmp = Avx.Subtract(scale, Unsafe.Add(ref srcR, i));
                Vector256<float> mtmp = Avx.Subtract(scale, Unsafe.Add(ref srcG, i));
                Vector256<float> ytmp = Avx.Subtract(scale, Unsafe.Add(ref srcB, i));
                Vector256<float> ktmp = Avx.Min(ctmp, Avx.Min(mtmp, ytmp));

                Vector256<float> kMask = Avx.CompareNotEqual(ktmp, scale);

                ctmp = Avx.And(Avx.Divide(Avx.Subtract(ctmp, ktmp), Avx.Subtract(scale, ktmp)), kMask);
                mtmp = Avx.And(Avx.Divide(Avx.Subtract(mtmp, ktmp), Avx.Subtract(scale, ktmp)), kMask);
                ytmp = Avx.And(Avx.Divide(Avx.Subtract(ytmp, ktmp), Avx.Subtract(scale, ktmp)), kMask);

                Unsafe.Add(ref destC, i) = Avx.Subtract(scale, Avx.Multiply(ctmp, scale));
                Unsafe.Add(ref destM, i) = Avx.Subtract(scale, Avx.Multiply(mtmp, scale));
                Unsafe.Add(ref destY, i) = Avx.Subtract(scale, Avx.Multiply(ytmp, scale));
                Unsafe.Add(ref destK, i) = Avx.Subtract(scale, ktmp);
            }
        }
    }
}

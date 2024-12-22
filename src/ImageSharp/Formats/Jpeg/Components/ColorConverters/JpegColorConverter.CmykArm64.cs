// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class CmykArm64 : JpegColorConverterArm64
    {
        public CmykArm64(int precision)
            : base(JpegColorSpace.Cmyk, precision)
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
            ref Vector128<float> c3Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            // Used for the color conversion
            Vector128<float> scale = Vector128.Create(1 / (this.MaximumValue * this.MaximumValue));

            nint n = (nint)(uint)values.Component0.Length / Vector128<float>.Count;
            for (nint i = 0; i < n; i++)
            {
                ref Vector128<float> c = ref Unsafe.Add(ref c0Base, i);
                ref Vector128<float> m = ref Unsafe.Add(ref c1Base, i);
                ref Vector128<float> y = ref Unsafe.Add(ref c2Base, i);
                Vector128<float> k = Unsafe.Add(ref c3Base, i);

                k = AdvSimd.Multiply(k, scale);
                c = AdvSimd.Multiply(c, k);
                m = AdvSimd.Multiply(m, k);
                y = AdvSimd.Multiply(y, k);
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertFromRgb(in ComponentValues values, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector128<float> destC =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> destM =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> destY =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));
            ref Vector128<float> destK =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component3));

            ref Vector128<float> srcR =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector128<float> srcG =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector128<float> srcB =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(bLane));

            Vector128<float> scale = Vector128.Create(maxValue);

            nint n = (nint)(uint)values.Component0.Length / Vector128<float>.Count;
            for (nint i = 0; i < n; i++)
            {
                Vector128<float> ctmp = AdvSimd.Subtract(scale, Unsafe.Add(ref srcR, i));
                Vector128<float> mtmp = AdvSimd.Subtract(scale, Unsafe.Add(ref srcG, i));
                Vector128<float> ytmp = AdvSimd.Subtract(scale, Unsafe.Add(ref srcB, i));
                Vector128<float> ktmp = AdvSimd.Min(ctmp, AdvSimd.Min(mtmp, ytmp));

                Vector128<float> kMask = AdvSimd.Not(AdvSimd.CompareEqual(ktmp, scale));

                ctmp = AdvSimd.And(AdvSimd.Arm64.Divide(AdvSimd.Subtract(ctmp, ktmp), AdvSimd.Subtract(scale, ktmp)), kMask);
                mtmp = AdvSimd.And(AdvSimd.Arm64.Divide(AdvSimd.Subtract(mtmp, ktmp), AdvSimd.Subtract(scale, ktmp)), kMask);
                ytmp = AdvSimd.And(AdvSimd.Arm64.Divide(AdvSimd.Subtract(ytmp, ktmp), AdvSimd.Subtract(scale, ktmp)), kMask);

                Unsafe.Add(ref destC, i) = AdvSimd.Subtract(scale, AdvSimd.Multiply(ctmp, scale));
                Unsafe.Add(ref destM, i) = AdvSimd.Subtract(scale, AdvSimd.Multiply(mtmp, scale));
                Unsafe.Add(ref destY, i) = AdvSimd.Subtract(scale, AdvSimd.Multiply(ytmp, scale));
                Unsafe.Add(ref destK, i) = AdvSimd.Subtract(scale, ktmp);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class GrayscaleArm : JpegColorConverterArm
    {
        public GrayscaleArm(int precision)
            : base(JpegColorSpace.Grayscale, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInplace(in ComponentValues values)
        {
            ref Vector128<float> c0Base =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));

            // Used for the color conversion
            Vector128<float> scale = Vector128.Create(1 / this.MaximumValue);

            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector128<float> c0 = ref Unsafe.Add(ref c0Base, i);
                c0 = AdvSimd.Multiply(c0, scale);
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector128<float> destLuminance =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));

            ref Vector128<float> srcRed =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector128<float> srcGreen =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector128<float> srcBlue =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(bLane));

            // Used for the color conversion
            Vector128<float> f0299 = Vector128.Create(0.299f);
            Vector128<float> f0587 = Vector128.Create(0.587f);
            Vector128<float> f0114 = Vector128.Create(0.114f);

            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector128<float> r = ref Unsafe.Add(ref srcRed, i);
                ref Vector128<float> g = ref Unsafe.Add(ref srcGreen, i);
                ref Vector128<float> b = ref Unsafe.Add(ref srcBlue, i);

                // luminocity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                Unsafe.Add(ref destLuminance, i) = HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(AdvSimd.Multiply(f0114, b), f0587, g), f0299, r);
            }
        }
    }
}

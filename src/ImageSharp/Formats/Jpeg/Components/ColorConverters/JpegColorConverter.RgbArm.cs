// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class RgbArm : JpegColorConverterArm
    {
        public RgbArm(int precision)
            : base(JpegColorSpace.RGB, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInplace(in ComponentValues values)
        {
            ref Vector128<float> rBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> gBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> bBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));

            // Used for the color conversion
            Vector128<float> scale = Vector128.Create(1 / this.MaximumValue);
            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector128<float> r = ref Unsafe.Add(ref rBase, i);
                ref Vector128<float> g = ref Unsafe.Add(ref gBase, i);
                ref Vector128<float> b = ref Unsafe.Add(ref bBase, i);
                r = AdvSimd.Multiply(r, scale);
                g = AdvSimd.Multiply(g, scale);
                b = AdvSimd.Multiply(b, scale);
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            rLane.CopyTo(values.Component0);
            gLane.CopyTo(values.Component1);
            bLane.CopyTo(values.Component2);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromRgbAvx : JpegColorConverterAvx
        {
            public FromRgbAvx(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                ref Vector256<float> rBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> gBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> bBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

                // Used for the color conversion
                var scale = Vector256.Create(1 / this.MaximumValue);
                nint n = values.Component0.Length / Vector256<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector256<float> r = ref Unsafe.Add(ref rBase, i);
                    ref Vector256<float> g = ref Unsafe.Add(ref gBase, i);
                    ref Vector256<float> b = ref Unsafe.Add(ref bBase, i);
                    r = Avx.Multiply(r, scale);
                    g = Avx.Multiply(g, scale);
                    b = Avx.Multiply(b, scale);
                }
            }
        }
    }
}

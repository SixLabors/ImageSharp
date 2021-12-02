// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromCmykAvx : AvxColorConverter
        {
            public FromCmykAvx(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

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
                var scale = Vector256.Create(1 / (this.MaximumValue * this.MaximumValue));

                nint n = values.Component0.Length / Vector256<float>.Count;
                for (nint i = 0; i < n; i++)
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
        }
    }
}
#endif

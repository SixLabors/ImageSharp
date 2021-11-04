// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromGrayscaleAvx : AvxColorConverter
        {
            public FromGrayscaleAvx(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
            {
                ref Vector256<float> c0Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));

                // Used for the color conversion
                var scale = Vector256.Create(1 / this.MaximumValue);

                nint n = values.Component0.Length / 8;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    c0 = Avx.Multiply(c0, scale);
                }
            }
        }
    }
}
#endif

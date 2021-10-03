// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static SixLabors.ImageSharp.SimdUtils;
#endif

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromGrayscaleAvx2 : Avx2JpegColorConverter
        {
            public FromGrayscaleAvx2(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> c0Base =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));

                // Used for the color conversion
                var scale = Vector256.Create(1 / this.MaximumValue);

                int n = values.Component0.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    c0 = Avx.Multiply(c0, scale);
                }
#endif
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromGrayscaleBasic.ScaleValues(values.Component0, this.MaximumValue);
        }
    }
}

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
        internal sealed class FromRgbAvx2 : Avx2JpegColorConverter
        {
            public FromRgbAvx2(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> rBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> gBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> bBase =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

                // Used for the color conversion
                var scale = Vector256.Create(1 / this.MaximumValue);
                int n = values.Component0.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    ref Vector256<float> r = ref Unsafe.Add(ref rBase, i);
                    ref Vector256<float> g = ref Unsafe.Add(ref gBase, i);
                    ref Vector256<float> b = ref Unsafe.Add(ref bBase, i);
                    r = Avx.Multiply(r, scale);
                    g = Avx.Multiply(g, scale);
                    b = Avx.Multiply(b, scale);
                }
#endif
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromRgbBasic.ConvertCoreInplace(values, this.MaximumValue);
        }
    }
}

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
        internal sealed class FromCmykAvx2 : Avx2JpegColorConverter
        {
            public FromCmykAvx2(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
                #if SUPPORTS_RUNTIME_INTRINSICS
                ref Vector256<float> c0Base =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector256<float> c1Base =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector256<float> c2Base =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector256<float> c3Base =
                                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

                // Used for the color conversion
                var scale = Vector256.Create(1 / this.MaximumValue);

                int n = values.Component0.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    ref Vector256<float> c = ref Unsafe.Add(ref c0Base, i);
                    ref Vector256<float> m = ref Unsafe.Add(ref c1Base, i);
                    ref Vector256<float> y = ref Unsafe.Add(ref c2Base, i);
                    Vector256<float> k = Unsafe.Add(ref c3Base, i);

                    k = Avx.Multiply(k, scale);
                    c = Avx.Multiply(Avx.Multiply(c, k), scale);
                    m = Avx.Multiply(Avx.Multiply(m, k), scale);
                    y = Avx.Multiply(Avx.Multiply(y, k), scale);
                }
#endif
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromCmykBasic.ConvertCoreInplace(values, this.MaximumValue);
        }
    }
}

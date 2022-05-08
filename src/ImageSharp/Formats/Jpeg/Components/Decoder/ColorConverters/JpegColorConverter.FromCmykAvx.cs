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
        internal sealed class FromCmykAvx : JpegColorConverterAvx
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

            public override void ConvertFromRgbInplace(in ComponentValues values)
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
                var scale = Vector256.Create(this.MaximumValue);
                var one = Vector256.Create(1f);

                nint n = values.Component0.Length / Vector256<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);
                    ref Vector256<float> c3 = ref Unsafe.Add(ref c3Base, i);

                    Vector256<float> ctmp = Avx.Subtract(one, c0);
                    Vector256<float> mtmp = Avx.Subtract(one, c1);
                    Vector256<float> ytmp = Avx.Subtract(one, c2);
                    Vector256<float> ktmp = Avx.Min(ctmp, Avx.Min(mtmp, ytmp));

                    Vector256<float> kMask = Avx.CompareNotEqual(ktmp, one);

                    ctmp = Avx.And(Avx.Divide(Avx.Subtract(ctmp, ktmp), Avx.Subtract(one, ktmp)), kMask);
                    mtmp = Avx.And(Avx.Divide(Avx.Subtract(mtmp, ktmp), Avx.Subtract(one, ktmp)), kMask);
                    ytmp = Avx.And(Avx.Divide(Avx.Subtract(ytmp, ktmp), Avx.Subtract(one, ktmp)), kMask);

                    c0 = Avx.Subtract(scale, Avx.Multiply(ctmp, scale));
                    c1 = Avx.Subtract(scale, Avx.Multiply(mtmp, scale));
                    c2 = Avx.Subtract(scale, Avx.Multiply(ytmp, scale));
                    c3 = Avx.Subtract(scale, Avx.Multiply(ktmp, scale));
                }
            }
        }
    }
}
#endif

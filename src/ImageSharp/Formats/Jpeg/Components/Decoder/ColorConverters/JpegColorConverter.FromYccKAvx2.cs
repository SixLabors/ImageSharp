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
        internal sealed class FromYccKAvx2 : Avx2JpegColorConverter
        {
            public FromYccKAvx2(int precision)
                : base(JpegColorSpace.Ycck, precision)
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
                ref Vector256<float> kBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component3));

                // Used for the color conversion
                var chromaOffset = Vector256.Create(-this.HalfValue);
                var scale = Vector256.Create(1 / (this.MaximumValue * this.MaximumValue));
                var max = Vector256.Create(this.MaximumValue);
                var rCrMult = Vector256.Create(1.402F);
                var gCbMult = Vector256.Create(-0.344136F);
                var gCrMult = Vector256.Create(-0.714136F);
                var bCbMult = Vector256.Create(1.772F);

                // Walking 8 elements at one step:
                nint n = values.Component0.Length / 8;
                for (nint i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    // k = kVals[i] / 256F;
                    ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector256<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector256<float> c2 = ref Unsafe.Add(ref c2Base, i);
                    Vector256<float> y = c0;
                    Vector256<float> cb = Avx.Add(c1, chromaOffset);
                    Vector256<float> cr = Avx.Add(c2, chromaOffset);
                    Vector256<float> scaledK = Avx.Multiply(Unsafe.Add(ref kBase, i), scale);

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    // Adding & multiplying 8 elements at one time:
                    Vector256<float> r = HwIntrinsics.MultiplyAdd(y, cr, rCrMult);
                    Vector256<float> g =
                        HwIntrinsics.MultiplyAdd(HwIntrinsics.MultiplyAdd(y, cb, gCbMult), cr, gCrMult);
                    Vector256<float> b = HwIntrinsics.MultiplyAdd(y, cb, bCbMult);

                    r = Avx.Subtract(max, Avx.RoundToNearestInteger(r));
                    g = Avx.Subtract(max, Avx.RoundToNearestInteger(g));
                    b = Avx.Subtract(max, Avx.RoundToNearestInteger(b));

                    r = Avx.Multiply(r, scaledK);
                    g = Avx.Multiply(g, scaledK);
                    b = Avx.Multiply(b, scaledK);

                    c0 = r;
                    c1 = g;
                    c2 = b;
                }
#endif
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromYccKBasic.ConvertCoreInplace(values, this.MaximumValue, this.HalfValue);
        }
    }
}

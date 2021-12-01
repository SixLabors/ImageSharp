// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ImpureMethodCallOnReadonlyValueField
namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromYCbCrVector8 : VectorizedJpegColorConverter
        {
            public FromYCbCrVector8(int precision)
                : base(JpegColorSpace.YCbCr, precision)
            {
            }

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
                ref Vector<float> c0Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> c1Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> c2Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

                var chromaOffset = new Vector<float>(-this.HalfValue);

                var scale = new Vector<float>(1 / this.MaximumValue);
                var rCrMult = new Vector<float>(FromYCbCrScalar.RCrMult);
                var gCbMult = new Vector<float>(-FromYCbCrScalar.GCbMult);
                var gCrMult = new Vector<float>(-FromYCbCrScalar.GCrMult);
                var bCbMult = new Vector<float>(FromYCbCrScalar.BCbMult);

                nint n = values.Component0.Length / 8;
                for (nint i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    ref Vector<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector<float> c2 = ref Unsafe.Add(ref c2Base, i);
                    Vector<float> y = Unsafe.Add(ref c0Base, i);
                    Vector<float> cb = Unsafe.Add(ref c1Base, i) + chromaOffset;
                    Vector<float> cr = Unsafe.Add(ref c2Base, i) + chromaOffset;

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    Vector<float> r = y + (cr * rCrMult);
                    Vector<float> g = y + (cb * gCbMult) + (cr * gCrMult);
                    Vector<float> b = y + (cb * bCbMult);

                    r = r.FastRound();
                    g = g.FastRound();
                    b = b.FastRound();
                    r *= scale;
                    g *= scale;
                    b *= scale;

                    c0 = r;
                    c1 = g;
                    c2 = b;
                }
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromYCbCrScalar.ConvertCoreInplace(values, this.MaximumValue, this.HalfValue);
        }
    }
}

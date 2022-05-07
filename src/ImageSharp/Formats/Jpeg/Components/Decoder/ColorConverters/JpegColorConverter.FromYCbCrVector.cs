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
        internal sealed class FromYCbCrVector : JpegColorConverterVector
        {
            public FromYCbCrVector(int precision)
                : base(JpegColorSpace.YCbCr, precision)
            {
            }

            protected override void ConvertCoreVectorizedInplaceToRgb(in ComponentValues values)
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

                nint n = values.Component0.Length / Vector<float>.Count;
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

            protected override void ConvertCoreInplaceToRgb(in ComponentValues values)
                => FromYCbCrScalar.ConvertCoreInplaceToRgb(values, this.MaximumValue, this.HalfValue);

            protected override void ConvertCoreVectorizedInplaceFromRgb(in ComponentValues values)
            {
                ref Vector<float> c0Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> c1Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> c2Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

                var chromaOffset = new Vector<float>(this.HalfValue);

                var scale = new Vector<float>(this.MaximumValue);

                var rYMult = new Vector<float>(0.299f);
                var gYMult = new Vector<float>(0.587f);
                var bYMult = new Vector<float>(0.114f);

                var rCbMult = new Vector<float>(0.168736f);
                var gCbMult = new Vector<float>(0.331264f);
                var bCbMult = new Vector<float>(0.5f);

                var rCrMult = new Vector<float>(0.5f);
                var gCrMult = new Vector<float>(0.418688f);
                var bCrMult = new Vector<float>(0.081312f);

                nint n = values.Component0.Length / Vector<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector<float> c2 = ref Unsafe.Add(ref c2Base, i);

                    Vector<float> r = c0 * scale;
                    Vector<float> g = c1 * scale;
                    Vector<float> b = c2 * scale;

                    // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                    // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                    // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                    c0 = (rYMult * r) + (gYMult * g) + (bYMult * b);
                    c1 = chromaOffset - (rCbMult * r) - (gCbMult * g) + (bCbMult * b);
                    c2 = chromaOffset + (rCrMult * r) - (gCrMult * g) - (bCrMult * b);
                }
            }

            protected override void ConvertCoreInplaceFromRgb(in ComponentValues values)
                => FromYCbCrScalar.ConvertCoreInplaceFromRgb(values, this.MaximumValue, this.HalfValue);
        }
    }
}

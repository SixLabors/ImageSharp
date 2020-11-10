// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Tuples;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromYccKVector8 : Vector8JpegColorConverter
        {
            public FromYccKVector8(int precision)
                : base(JpegColorSpace.Ycck, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
                ref Vector<float> yBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> cbBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> crBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> kBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                var chromaOffset = new Vector<float>(-this.HalfValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;

                Vector4Pair rr = default;
                Vector4Pair gg = default;
                Vector4Pair bb = default;

                ref Vector<float> rrRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref rr);
                ref Vector<float> ggRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref gg);
                ref Vector<float> bbRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref bb);

                var scale = new Vector<float>(1 / this.MaximumValue);
                var max = new Vector<float>(this.MaximumValue);

                for (int i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    // cb = cbVals[i] - 128F;
                    // cr = crVals[i] - 128F;
                    // k = kVals[i] / 256F;
                    Vector<float> y = Unsafe.Add(ref yBase, i);
                    Vector<float> cb = Unsafe.Add(ref cbBase, i) + chromaOffset;
                    Vector<float> cr = Unsafe.Add(ref crBase, i) + chromaOffset;
                    Vector<float> k = Unsafe.Add(ref kBase, i) / max;

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    // Adding & multiplying 8 elements at one time:
                    Vector<float> r = y + (cr * new Vector<float>(1.402F));
                    Vector<float> g = y - (cb * new Vector<float>(0.344136F)) - (cr * new Vector<float>(0.714136F));
                    Vector<float> b = y + (cb * new Vector<float>(1.772F));

                    r = (max - r.FastRound()) * k;
                    g = (max - g.FastRound()) * k;
                    b = (max - b.FastRound()) * k;
                    r *= scale;
                    g *= scale;
                    b *= scale;

                    rrRefAsVector = r;
                    ggRefAsVector = g;
                    bbRefAsVector = b;

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref rr, ref gg, ref bb);
                }
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromYccKBasic.ConvertCore(values, result, this.MaximumValue, this.HalfValue);
        }
    }
}

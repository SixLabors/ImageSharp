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
        internal sealed class FromRgbVector8 : Vector8JpegColorConverter
        {
            public FromRgbVector8(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
                ref Vector<float> rBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> gBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> bBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                Vector4Pair rr = default;
                Vector4Pair gg = default;
                Vector4Pair bb = default;
                ref Vector<float> rrRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref rr);
                ref Vector<float> ggRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref gg);
                ref Vector<float> bbRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref bb);

                var scale = new Vector<float>(1 / this.MaximumValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector<float> r = Unsafe.Add(ref rBase, i);
                    Vector<float> g = Unsafe.Add(ref gBase, i);
                    Vector<float> b = Unsafe.Add(ref bBase, i);
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
                FromRgbBasic.ConvertCore(values, result, this.MaximumValue);
        }
    }
}

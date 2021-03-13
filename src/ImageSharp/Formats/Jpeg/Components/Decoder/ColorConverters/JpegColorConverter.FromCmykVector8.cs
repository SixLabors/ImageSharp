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
        internal sealed class FromCmykVector8 : Vector8JpegColorConverter
        {
            public FromCmykVector8(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
                ref Vector<float> cBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> mBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> yBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> kBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                Vector4Pair cc = default;
                Vector4Pair mm = default;
                Vector4Pair yy = default;
                ref Vector<float> ccRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref cc);
                ref Vector<float> mmRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref mm);
                ref Vector<float> yyRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref yy);

                var scale = new Vector<float>(1 / this.MaximumValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector<float> c = Unsafe.Add(ref cBase, i);
                    Vector<float> m = Unsafe.Add(ref mBase, i);
                    Vector<float> y = Unsafe.Add(ref yBase, i);
                    Vector<float> k = Unsafe.Add(ref kBase, i) * scale;

                    c = (c * k) * scale;
                    m = (m * k) * scale;
                    y = (y * k) * scale;

                    ccRefAsVector = c;
                    mmRefAsVector = m;
                    yyRefAsVector = y;

                    // Collect (c0,c1...c8) (m0,m1...m8) (y0,y1...y8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref cc, ref mm, ref yy);
                }
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromCmykBasic.ConvertCore(values, result, this.MaximumValue);
        }
    }
}

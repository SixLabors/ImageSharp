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
        internal sealed class FromGrayscaleVector8 : Vector8JpegColorConverter
        {
            public FromGrayscaleVector8(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
                ref Vector<float> gBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                Vector4Pair gg = default;
                ref Vector<float> ggRefAsVector = ref Unsafe.As<Vector4Pair, Vector<float>>(ref gg);

                var scale = new Vector<float>(1 / this.MaximumValue);

                // Walking 8 elements at one step:
                int n = result.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    Vector<float> g = Unsafe.Add(ref gBase, i);
                    g *= scale;

                    ggRefAsVector = g;

                    // Collect (g0,g1...g7) vector values in the expected (g0,g0,g0,1), (g1,g1,g1,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref gg);
                }
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromGrayscaleBasic.ConvertCore(values, result, this.MaximumValue);
        }
    }
}

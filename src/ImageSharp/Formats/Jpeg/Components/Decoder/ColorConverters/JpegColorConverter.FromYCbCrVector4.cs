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
        internal sealed class FromYCbCrVector4 : VectorizedJpegColorConverter
        {
            public FromYCbCrVector4(int precision)
                : base(JpegColorSpace.YCbCr, precision, 8)
            {
            }

            protected override bool IsAvailable => SimdUtils.HasVector4;

            protected override void ConvertCoreVectorized(in ComponentValues values, Span<Vector4> result)
            {
                // TODO: Find a way to properly run & test this path on AVX2 PC-s! (Have I already mentioned that Vector<T> is terrible?)
                DebugGuard.IsTrue(result.Length % 8 == 0, nameof(result), "result.Length should be divisible by 8!");

                ref Vector4Pair yBase =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector4Pair cbBase =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector4Pair crBase =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component2));

                ref Vector4Octet resultBase =
                    ref Unsafe.As<Vector4, Vector4Octet>(ref MemoryMarshal.GetReference(result));

                var chromaOffset = new Vector4(-this.HalfValue);
                var maxValue = this.MaximumValue;

                // Walking 8 elements at one step:
                int n = result.Length / 8;

                for (int i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    Vector4Pair y = Unsafe.Add(ref yBase, i);

                    // cb = cbVals[i] - halfValue);
                    Vector4Pair cb = Unsafe.Add(ref cbBase, i);
                    cb.AddInplace(chromaOffset);

                    // cr = crVals[i] - halfValue;
                    Vector4Pair cr = Unsafe.Add(ref crBase, i);
                    cr.AddInplace(chromaOffset);

                    // r = y + (1.402F * cr);
                    Vector4Pair r = y;
                    Vector4Pair tmp = cr;
                    tmp.MultiplyInplace(1.402F);
                    r.AddInplace(ref tmp);

                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    Vector4Pair g = y;
                    tmp = cb;
                    tmp.MultiplyInplace(-0.344136F);
                    g.AddInplace(ref tmp);
                    tmp = cr;
                    tmp.MultiplyInplace(-0.714136F);
                    g.AddInplace(ref tmp);

                    // b = y + (1.772F * cb);
                    Vector4Pair b = y;
                    tmp = cb;
                    tmp.MultiplyInplace(1.772F);
                    b.AddInplace(ref tmp);

                    r.RoundAndDownscalePreVector8(maxValue);
                    g.RoundAndDownscalePreVector8(maxValue);
                    b.RoundAndDownscalePreVector8(maxValue);

                    // Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order:
                    ref Vector4Octet destination = ref Unsafe.Add(ref resultBase, i);
                    destination.Pack(ref r, ref g, ref b);
                }
            }

            protected override void ConvertCore(in ComponentValues values, Span<Vector4> result) =>
                FromYCbCrBasic.ConvertCore(values, result, this.MaximumValue, this.HalfValue);
        }
    }
}

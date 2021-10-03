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

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
                DebugGuard.IsTrue(values.Component0.Length % 8 == 0, nameof(values), "Length should be divisible by 8!");

                ref Vector4Pair c0Base =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector4Pair c1Base =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector4Pair c2Base =
                    ref Unsafe.As<float, Vector4Pair>(ref MemoryMarshal.GetReference(values.Component2));

                var chromaOffset = new Vector4(-this.HalfValue);
                var maxValue = this.MaximumValue;

                // Walking 8 elements at one step:
                nint n = values.Component0.Length / 8;

                for (nint i = 0; i < n; i++)
                {
                    // y = yVals[i];
                    ref Vector4Pair c0 = ref Unsafe.Add(ref c0Base, i);

                    // cb = cbVals[i] - halfValue);
                    ref Vector4Pair c1 = ref Unsafe.Add(ref c1Base, i);
                    c1.AddInplace(chromaOffset);

                    // cr = crVals[i] - halfValue;
                    ref Vector4Pair c2 = ref Unsafe.Add(ref c2Base, i);
                    c2.AddInplace(chromaOffset);

                    // r = y + (1.402F * cr);
                    Vector4Pair r = c0;
                    Vector4Pair tmp = c2;
                    tmp.MultiplyInplace(1.402F);
                    r.AddInplace(ref tmp);

                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    Vector4Pair g = c0;
                    tmp = c1;
                    tmp.MultiplyInplace(-0.344136F);
                    g.AddInplace(ref tmp);
                    tmp = c2;
                    tmp.MultiplyInplace(-0.714136F);
                    g.AddInplace(ref tmp);

                    // b = y + (1.772F * cb);
                    Vector4Pair b = c0;
                    tmp = c1;
                    tmp.MultiplyInplace(1.772F);
                    b.AddInplace(ref tmp);

                    r.RoundAndDownscalePreVector8(maxValue);
                    g.RoundAndDownscalePreVector8(maxValue);
                    b.RoundAndDownscalePreVector8(maxValue);

                    c0 = r;
                    c1 = g;
                    c2 = b;
                }
            }

            protected override void ConvertCoreInplace(in ComponentValues values)
                => FromYCbCrBasic.ConvertCoreInplace(values, this.MaximumValue, this.HalfValue);
        }
    }
}

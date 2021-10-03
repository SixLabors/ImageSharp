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

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
                ref Vector<float> cBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> mBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> yBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> kBase =
                                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                var scale = new Vector<float>(1 / this.MaximumValue);

                // Walking 8 elements at one step:
                int n = values.Component0.Length / 8;
                for (int i = 0; i < n; i++)
                {
                    ref Vector<float> c = ref Unsafe.Add(ref cBase, i);
                    ref Vector<float> m = ref Unsafe.Add(ref mBase, i);
                    ref Vector<float> y = ref Unsafe.Add(ref yBase, i);
                    Vector<float> k = Unsafe.Add(ref kBase, i) * scale;

                    c = (c * k) * scale;
                    m = (m * k) * scale;
                    y = (y * k) * scale;
                }
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromCmykBasic.ConvertCoreInplace(values, this.MaximumValue);
        }
    }
}

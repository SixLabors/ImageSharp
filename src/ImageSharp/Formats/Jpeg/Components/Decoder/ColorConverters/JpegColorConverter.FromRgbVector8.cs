// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

            protected override void ConvertCoreVectorizedInplace(in ComponentValues values)
            {
                ref Vector<float> rBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> gBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> bBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));

                var scale = new Vector<float>(1 / this.MaximumValue);

                nint n = values.Component0.Length / 8;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector<float> r = ref Unsafe.Add(ref rBase, i);
                    ref Vector<float> g = ref Unsafe.Add(ref gBase, i);
                    ref Vector<float> b = ref Unsafe.Add(ref bBase, i);
                    r *= scale;
                    g *= scale;
                    b *= scale;
                }
            }

            protected override void ConvertCoreInplace(in ComponentValues values) =>
                FromRgbScalar.ConvertCoreInplace(values, this.MaximumValue);
        }
    }
}

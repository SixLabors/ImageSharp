// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromGrayscaleScalar : ScalarJpegColorConverter
        {
            public FromGrayscaleScalar(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ConvertCoreInplace(values.Component0, this.MaximumValue);

            internal static void ConvertCoreInplace(Span<float> values, float maxValue)
            {
                ref float valuesRef = ref MemoryMarshal.GetReference(values);
                float scale = 1 / maxValue;

                for (nint i = 0; i < values.Length; i++)
                {
                    Unsafe.Add(ref valuesRef, i) *= scale;
                }
            }
        }
    }
}

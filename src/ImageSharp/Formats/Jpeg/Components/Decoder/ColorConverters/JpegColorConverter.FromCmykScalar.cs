// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromCmykScalar : BasicJpegColorConverter
        {
            public FromCmykScalar(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ConvertCoreInplace(values, this.MaximumValue);

            internal static void ConvertCoreInplace(in ComponentValues values, float maxValue)
            {
                Span<float> c0 = values.Component0;
                Span<float> c1 = values.Component1;
                Span<float> c2 = values.Component2;
                Span<float> c3 = values.Component3;

                float scale = 1 / maxValue;
                for (int i = 0; i < c0.Length; i++)
                {
                    float c = c0[i];
                    float m = c1[i];
                    float y = c2[i];
                    float k = c3[i] / maxValue;

                    c0[i] = c * k * scale;
                    c1[i] = m * k * scale;
                    c2[i] = y * k * scale;
                }
            }
        }
    }
}

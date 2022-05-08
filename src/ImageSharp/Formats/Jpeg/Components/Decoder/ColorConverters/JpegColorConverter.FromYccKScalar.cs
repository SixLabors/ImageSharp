// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromYccKScalar : JpegColorConverterScalar
        {
            public FromYccKScalar(int precision)
                : base(JpegColorSpace.Ycck, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ConvertToRgpInplace(values, this.MaximumValue, this.HalfValue);

            public override void ConvertFromRgbInplace(in ComponentValues values)
                => throw new NotImplementedException();

            public static void ConvertToRgpInplace(in ComponentValues values, float maxValue, float halfValue)
            {
                Span<float> c0 = values.Component0;
                Span<float> c1 = values.Component1;
                Span<float> c2 = values.Component2;
                Span<float> c3 = values.Component3;

                float scale = 1 / (maxValue * maxValue);

                for (int i = 0; i < values.Component0.Length; i++)
                {
                    float y = c0[i];
                    float cb = c1[i] - halfValue;
                    float cr = c2[i] - halfValue;
                    float scaledK = c3[i] * scale;

                    c0[i] = (maxValue - MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero)) * scaledK;
                    c1[i] = (maxValue - MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero)) * scaledK;
                    c2[i] = (maxValue - MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero)) * scaledK;
                }
            }
        }
    }
}

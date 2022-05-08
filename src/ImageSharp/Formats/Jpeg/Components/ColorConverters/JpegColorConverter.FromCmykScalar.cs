// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromCmykScalar : JpegColorConverterScalar
        {
            public FromCmykScalar(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ConvertToRgbInplace(values, this.MaximumValue);

            public override void ConvertFromRgbInplace(in ComponentValues values)
                => ConvertFromRgbInplace(values, this.MaximumValue);

            public static void ConvertToRgbInplace(in ComponentValues values, float maxValue)
            {
                Span<float> c0 = values.Component0;
                Span<float> c1 = values.Component1;
                Span<float> c2 = values.Component2;
                Span<float> c3 = values.Component3;

                float scale = 1 / (maxValue * maxValue);
                for (int i = 0; i < c0.Length; i++)
                {
                    float c = c0[i];
                    float m = c1[i];
                    float y = c2[i];
                    float k = c3[i];

                    k *= scale;
                    c0[i] = c * k;
                    c1[i] = m * k;
                    c2[i] = y * k;
                }
            }

            public static void ConvertFromRgbInplace(in ComponentValues values, float maxValue)
            {
                Span<float> c0 = values.Component0;
                Span<float> c1 = values.Component1;
                Span<float> c2 = values.Component2;
                Span<float> c3 = values.Component3;

                for (int i = 0; i < c0.Length; i++)
                {
                    float ctmp = 1f - c0[i];
                    float mtmp = 1f - c1[i];
                    float ytmp = 1f - c2[i];
                    float ktmp = MathF.Min(MathF.Min(ctmp, mtmp), ytmp);

                    if (1f - ktmp <= float.Epsilon)
                    {
                        ctmp = 0f;
                        mtmp = 0f;
                        ytmp = 0f;
                    }
                    else
                    {
                        ctmp = (ctmp - ktmp) / (1f - ktmp);
                        mtmp = (mtmp - ktmp) / (1f - ktmp);
                        ytmp = (ytmp - ktmp) / (1f - ktmp);
                    }

                    c0[i] = maxValue - (ctmp * maxValue);
                    c1[i] = maxValue - (mtmp * maxValue);
                    c2[i] = maxValue - (ytmp * maxValue);
                    c3[i] = maxValue - (ktmp * maxValue);
                }
            }
        }
    }
}

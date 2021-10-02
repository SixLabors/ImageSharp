// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromCmykBasic : BasicJpegColorConverter
        {
            public FromCmykBasic(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                ConvertCore(values, result, this.MaximumValue);
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ConvertCoreInplace(values, this.MaximumValue);

            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result, float maxValue)
            {
                ReadOnlySpan<float> cVals = values.Component0;
                ReadOnlySpan<float> mVals = values.Component1;
                ReadOnlySpan<float> yVals = values.Component2;
                ReadOnlySpan<float> kVals = values.Component3;

                var v = new Vector4(0, 0, 0, 1F);

                var maximum = 1 / maxValue;
                var scale = new Vector4(maximum, maximum, maximum, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float c = cVals[i];
                    float m = mVals[i];
                    float y = yVals[i];
                    float k = kVals[i] / maxValue;

                    v.X = c * k;
                    v.Y = m * k;
                    v.Z = y * k;
                    v.W = 1F;

                    v *= scale;

                    result[i] = v;
                }
            }

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

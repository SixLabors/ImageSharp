// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromYccKBasic : BasicJpegColorConverter
        {
            public FromYccKBasic(int precision)
                : base(JpegColorSpace.Ycck, precision)
            {
            }

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                ConvertCore(values, result, this.MaximumValue, this.HalfValue);
            }

            internal static void ConvertCore(in ComponentValues values, Span<Vector4> result, float maxValue, float halfValue)
            {
                // TODO: We can optimize a lot here with Vector<float> and SRCS.Unsafe()!
                ReadOnlySpan<float> yVals = values.Component0;
                ReadOnlySpan<float> cbVals = values.Component1;
                ReadOnlySpan<float> crVals = values.Component2;
                ReadOnlySpan<float> kVals = values.Component3;

                var v = new Vector4(0, 0, 0, 1F);

                var maximum = 1 / maxValue;
                var scale = new Vector4(maximum, maximum, maximum, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float y = yVals[i];
                    float cb = cbVals[i] - halfValue;
                    float cr = crVals[i] - halfValue;
                    float k = kVals[i] / maxValue;

                    v.X = (maxValue - MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero)) * k;
                    v.Y = (maxValue - MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero)) * k;
                    v.Z = (maxValue - MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero)) * k;
                    v.W = 1F;

                    v *= scale;

                    result[i] = v;
                }
            }
        }
    }
}

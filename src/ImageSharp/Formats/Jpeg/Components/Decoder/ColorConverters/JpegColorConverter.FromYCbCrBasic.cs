// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromYCbCrBasic : BasicJpegColorConverter
        {
            public FromYCbCrBasic(int precision)
                : base(JpegColorSpace.YCbCr, precision)
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

                var v = new Vector4(0, 0, 0, 1);

                var scale = new Vector4(1 / maxValue, 1 / maxValue, 1 / maxValue, 1F);

                for (int i = 0; i < result.Length; i++)
                {
                    float y = yVals[i];
                    float cb = cbVals[i] - halfValue;
                    float cr = crVals[i] - halfValue;

                    v.X = MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero);
                    v.Y = MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero);
                    v.Z = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);

                    v *= scale;

                    result[i] = v;
                }
            }
        }
    }
}

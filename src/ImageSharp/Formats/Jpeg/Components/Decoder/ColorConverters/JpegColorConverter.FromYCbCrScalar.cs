// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal sealed class FromYCbCrScalar : BasicJpegColorConverter
        {
            // TODO: comments, derived from ITU-T Rec. T.871
            internal const float RCrMult = 1.402f;
            internal const float GCbMult = (float)(0.114 * 1.772 / 0.587);
            internal const float GCrMult = (float)(0.299 * 1.402 / 0.587);
            internal const float BCbMult = 1.772f;

            public FromYCbCrScalar(int precision)
                : base(JpegColorSpace.YCbCr, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
                => ConvertCoreInplace(values, this.MaximumValue, this.HalfValue);

            internal static void ConvertCoreInplace(in ComponentValues values, float maxValue, float halfValue)
            {
                Span<float> c0 = values.Component0;
                Span<float> c1 = values.Component1;
                Span<float> c2 = values.Component2;

                float scale = 1 / maxValue;

                for (int i = 0; i < c0.Length; i++)
                {
                    float y = c0[i];
                    float cb = c1[i] - halfValue;
                    float cr = c2[i] - halfValue;

                    // r = y + (1.402F * cr);
                    // g = y - (0.344136F * cb) - (0.714136F * cr);
                    // b = y + (1.772F * cb);
                    c0[i] = MathF.Round(y + (RCrMult * cr), MidpointRounding.AwayFromZero) * scale;
                    c1[i] = MathF.Round(y - (GCbMult * cb) - (GCrMult * cr), MidpointRounding.AwayFromZero) * scale;
                    c2[i] = MathF.Round(y + (BCbMult * cb), MidpointRounding.AwayFromZero) * scale;
                }
            }
        }
    }
}

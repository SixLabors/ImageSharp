// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromGrayscaleScalar : JpegColorConverterScalar
        {
            public FromGrayscaleScalar(int precision)
                : base(JpegColorSpace.Grayscale, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
                => ConvertCoreInplaceToRgb(values.Component0, this.MaximumValue);

            public override void ConvertFromRgbInplace(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
                => ConvertCoreInplaceFromRgb(values, r, g, b);

            internal static void ConvertCoreInplaceToRgb(Span<float> values, float maxValue)
            {
            }

            internal static void ConvertCoreInplaceFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            {
                Span<float> c0 = values.Component0;

                for (int i = 0; i < c0.Length; i++)
                {
                    float r = rLane[i];
                    float g = gLane[i];
                    float b = bLane[i];

                    // luminocity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                    c0[i] = (0.299f * r) + (0.587f * g) + (0.114f * b);
                }
            }
        }
    }
}

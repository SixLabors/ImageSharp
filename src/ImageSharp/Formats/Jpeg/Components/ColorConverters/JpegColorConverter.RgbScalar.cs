// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class RgbScalar : JpegColorConverterScalar
        {
            public RgbScalar(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
                => ConvertCoreInplaceToRgb(values, this.MaximumValue);

            public override void ConvertFromRgbInplace(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
                => ConvertCoreInplaceFromRgb(values, r, g, b);

            internal static void ConvertCoreInplaceToRgb(ComponentValues values, float maxValue)
            {
                GrayscaleScalar.ConvertCoreInplaceToRgb(values.Component0, maxValue);
                GrayscaleScalar.ConvertCoreInplaceToRgb(values.Component1, maxValue);
                GrayscaleScalar.ConvertCoreInplaceToRgb(values.Component2, maxValue);
            }

            internal static void ConvertCoreInplaceFromRgb(ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            {
                r.CopyTo(values.Component0);
                g.CopyTo(values.Component1);
                b.CopyTo(values.Component2);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

            public override void ConvertFromRgbInplace(in ComponentValues values)
                => ConvertCoreInplaceFromRgb(values.Component0, this.MaximumValue);

            internal static void ConvertCoreInplaceToRgb(Span<float> values, float maxValue)
            {
            }

            internal static void ConvertCoreInplaceFromRgb(Span<float> values, float maxValue)
            {
            }
        }
    }
}

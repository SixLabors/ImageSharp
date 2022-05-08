// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromRgbScalar : JpegColorConverterScalar
        {
            public FromRgbScalar(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values)
                => ConvertCoreInplaceFromRgb(values, this.MaximumValue);

            public override void ConvertFromRgbInplace(in ComponentValues values)
                => ConvertCoreInplaceFromRgb(values, this.MaximumValue);

            internal static void ConvertCoreInplaceToRgb(ComponentValues values, float maxValue)
            {
                FromGrayscaleScalar.ConvertCoreInplaceToRgb(values.Component0, maxValue);
                FromGrayscaleScalar.ConvertCoreInplaceToRgb(values.Component1, maxValue);
                FromGrayscaleScalar.ConvertCoreInplaceToRgb(values.Component2, maxValue);
            }

            internal static void ConvertCoreInplaceFromRgb(ComponentValues values, float maxValue)
            {
                FromGrayscaleScalar.ConvertCoreInplaceFromRgb(values.Component0, maxValue);
                FromGrayscaleScalar.ConvertCoreInplaceFromRgb(values.Component1, maxValue);
                FromGrayscaleScalar.ConvertCoreInplaceFromRgb(values.Component2, maxValue);
            }
        }
    }
}

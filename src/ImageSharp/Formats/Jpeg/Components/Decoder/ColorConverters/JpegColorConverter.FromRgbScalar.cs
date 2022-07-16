// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromRgbScalar : JpegColorConverterScalar
        {
            public FromRgbScalar(int precision)
                : base(JpegColorSpace.RGB, precision)
            {
            }

            public override void ConvertToRgbInplace(in ComponentValues values) =>
                ConvertCoreInplace(values, this.MaximumValue);

            internal static void ConvertCoreInplace(ComponentValues values, float maxValue)
            {
                FromGrayscaleScalar.ConvertCoreInplace(values.Component0, maxValue);
                FromGrayscaleScalar.ConvertCoreInplace(values.Component1, maxValue);
                FromGrayscaleScalar.ConvertCoreInplace(values.Component2, maxValue);
            }
        }
    }
}

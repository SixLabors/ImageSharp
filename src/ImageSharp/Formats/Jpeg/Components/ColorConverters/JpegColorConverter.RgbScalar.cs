// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class RgbScalar : JpegColorConverterScalar
    {
        public RgbScalar(int precision)
            : base(JpegColorSpace.RGB, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgbInplace(values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => ConvertFromRgb(values, r, g, b);

        internal static void ConvertToRgbInplace(ComponentValues values, float maxValue)
        {
            GrayscaleScalar.ConvertToRgbInplace(values.Component0, maxValue);
            GrayscaleScalar.ConvertToRgbInplace(values.Component1, maxValue);
            GrayscaleScalar.ConvertToRgbInplace(values.Component2, maxValue);
        }

        internal static void ConvertFromRgb(ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
        {
            r.CopyTo(values.Component0);
            g.CopyTo(values.Component1);
            b.CopyTo(values.Component2);
        }
    }
}

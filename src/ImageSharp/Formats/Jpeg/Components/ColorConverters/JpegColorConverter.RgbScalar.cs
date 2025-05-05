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
            => ConvertToRgbInPlace(values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(values, rLane, gLane, bLane);

        internal static void ConvertToRgbInPlace(ComponentValues values, float maxValue)
        {
            GrayScaleScalar.ConvertToRgbInPlace(values.Component0, maxValue);
            GrayScaleScalar.ConvertToRgbInPlace(values.Component1, maxValue);
            GrayScaleScalar.ConvertToRgbInPlace(values.Component2, maxValue);
        }

        internal static void ConvertFromRgb(ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            rLane.CopyTo(values.Component0);
            gLane.CopyTo(values.Component1);
            bLane.CopyTo(values.Component2);
        }
    }
}

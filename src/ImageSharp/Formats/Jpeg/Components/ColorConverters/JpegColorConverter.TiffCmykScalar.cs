// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// Color converter for tiff images, which use the jpeg compression and CMYK colorspace.
    /// </summary>
    internal sealed class TiffCmykScalar : JpegColorConverterScalar
    {
        public TiffCmykScalar(int precision)
            : base(JpegColorSpace.TiffCmyk, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInplace(in ComponentValues values)
            => ConvertToRgbInplace(values, this.MaximumValue);

        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane) => throw new NotImplementedException();

        internal static void ConvertToRgbInplace(ComponentValues values, float maxValue)
        {
            ColorProfileConverter converter = new();
            float invMax = 1 / maxValue;
            for (int i = 0; i < values.Component0.Length; i++)
            {
                Cmyk cmyk = new(values.Component0[i] * invMax, values.Component1[i] * invMax, values.Component2[i] * invMax, values.Component3[i] * invMax);
                Rgb rgb = converter.Convert<Cmyk, Rgb>(cmyk);
                values.Component0[i] = rgb.R;
                values.Component1[i] = rgb.G;
                values.Component2[i] = rgb.B;
            }
        }
    }
}

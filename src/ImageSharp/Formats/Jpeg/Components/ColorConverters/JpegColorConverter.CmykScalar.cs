// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class CmykScalar : JpegColorConverterScalar
    {
        public CmykScalar(int precision)
            : base(JpegColorSpace.Cmyk, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values) =>
            ConvertToRgbInplace(values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(values, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertToRgbInplace(in ComponentValues values, float maxValue)
        {
            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;
            Span<float> c3 = values.Component3;

            float scale = 1 / (maxValue * maxValue);
            for (int i = 0; i < c0.Length; i++)
            {
                float c = c0[i];
                float m = c1[i];
                float y = c2[i];
                float k = c3[i];

                k *= scale;
                c0[i] = c * k;
                c1[i] = m * k;
                c2[i] = y * k;
            }
        }

        public static void ConvertFromRgb(in ComponentValues values, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            Span<float> c = values.Component0;
            Span<float> m = values.Component1;
            Span<float> y = values.Component2;
            Span<float> k = values.Component3;

            for (int i = 0; i < c.Length; i++)
            {
                float ctmp = 255f - rLane[i];
                float mtmp = 255f - gLane[i];
                float ytmp = 255f - bLane[i];
                float ktmp = MathF.Min(MathF.Min(ctmp, mtmp), ytmp);

                if (ktmp >= 255f)
                {
                    ctmp = 0f;
                    mtmp = 0f;
                    ytmp = 0f;
                }
                else
                {
                    ctmp = (ctmp - ktmp) / (255f - ktmp);
                    mtmp = (mtmp - ktmp) / (255f - ktmp);
                    ytmp = (ytmp - ktmp) / (255f - ktmp);
                }

                c[i] = maxValue - (ctmp * maxValue);
                m[i] = maxValue - (mtmp * maxValue);
                y[i] = maxValue - (ytmp * maxValue);
                k[i] = maxValue - ktmp;
            }
        }
    }
}

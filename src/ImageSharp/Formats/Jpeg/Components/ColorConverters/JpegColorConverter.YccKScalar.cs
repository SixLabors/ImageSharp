// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YccKScalar : JpegColorConverterScalar
    {
        // derived from ITU-T Rec. T.871
        internal const float RCrMult = 1.402f;
        internal const float GCbMult = (float)(0.114 * 1.772 / 0.587);
        internal const float GCrMult = (float)(0.299 * 1.402 / 0.587);
        internal const float BCbMult = 1.772f;

        public YccKScalar(int precision)
            : base(JpegColorSpace.Ycck, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgpInPlace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(values, this.HalfValue, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertToRgpInPlace(in ComponentValues values, float maxValue, float halfValue)
        {
            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;
            Span<float> c3 = values.Component3;

            float scale = 1 / (maxValue * maxValue);

            for (int i = 0; i < values.Component0.Length; i++)
            {
                float y = c0[i];
                float cb = c1[i] - halfValue;
                float cr = c2[i] - halfValue;
                float scaledK = c3[i] * scale;

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                c0[i] = (maxValue - MathF.Round(y + (RCrMult * cr), MidpointRounding.AwayFromZero)) * scaledK;
                c1[i] = (maxValue - MathF.Round(y - (GCbMult * cb) - (GCrMult * cr), MidpointRounding.AwayFromZero)) * scaledK;
                c2[i] = (maxValue - MathF.Round(y + (BCbMult * cb), MidpointRounding.AwayFromZero)) * scaledK;
            }
        }

        public static void ConvertFromRgb(in ComponentValues values, float halfValue, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            // rgb -> cmyk
            CmykScalar.ConvertFromRgb(in values, maxValue, rLane, gLane, bLane);

            // cmyk -> ycck
            Span<float> c = values.Component0;
            Span<float> m = values.Component1;
            Span<float> y = values.Component2;

            for (int i = 0; i < y.Length; i++)
            {
                float r = maxValue - c[i];
                float g = maxValue - m[i];
                float b = maxValue - y[i];

                // k value is passed untouched from rgb -> cmyk conversion
                c[i] = (0.299f * r) + (0.587f * g) + (0.114f * b);
                m[i] = halfValue - (0.168736f * r) - (0.331264f * g) + (0.5f * b);
                y[i] = halfValue + (0.5f * r) - (0.418688f * g) - (0.081312f * b);
            }
        }
    }
}

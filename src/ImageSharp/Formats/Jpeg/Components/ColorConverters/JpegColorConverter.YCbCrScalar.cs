// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class YCbCrScalar : JpegColorConverterScalar
    {
        // derived from ITU-T Rec. T.871
        internal const float RCrMult = 1.402f;
        internal const float GCbMult = (float)(0.114 * 1.772 / 0.587);
        internal const float GCrMult = (float)(0.299 * 1.402 / 0.587);
        internal const float BCbMult = 1.772f;

        public YCbCrScalar(int precision)
            : base(JpegColorSpace.YCbCr, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgbInplace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => ConvertFromRgb(values, this.HalfValue, r, g, b);

        public static void ConvertToRgbInplace(in ComponentValues values, float maxValue, float halfValue)
        {
            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;

            float scale = 1 / maxValue;

            for (int i = 0; i < c0.Length; i++)
            {
                float y = c0[i];
                float cb = c1[i] - halfValue;
                float cr = c2[i] - halfValue;

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                c0[i] = MathF.Round(y + (RCrMult * cr), MidpointRounding.AwayFromZero) * scale;
                c1[i] = MathF.Round(y - (GCbMult * cb) - (GCrMult * cr), MidpointRounding.AwayFromZero) * scale;
                c2[i] = MathF.Round(y + (BCbMult * cb), MidpointRounding.AwayFromZero) * scale;
            }
        }

        public static void ConvertFromRgb(in ComponentValues values, float halfValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            Span<float> y = values.Component0;
            Span<float> cb = values.Component1;
            Span<float> cr = values.Component2;

            for (int i = 0; i < y.Length; i++)
            {
                float r = rLane[i];
                float g = gLane[i];
                float b = bLane[i];

                // y  =   0 + (0.299 * r) + (0.587 * g) + (0.114 * b)
                // cb = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b)
                // cr = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b)
                y[i] = (0.299f * r) + (0.587f * g) + (0.114f * b);
                cb[i] = halfValue - (0.168736f * r) - (0.331264f * g) + (0.5f * b);
                cr[i] = halfValue + (0.5f * r) - (0.418688f * g) - (0.081312f * b);
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

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
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

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

        public static void ConvertToRgbInPlaceWithIcc(Configuration configuration, IccProfile profile, in ComponentValues values, float maxValue)
        {
            using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(values.Component0.Length * 4);
            Span<float> packed = memoryOwner.Memory.Span;

            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;
            Span<float> c3 = values.Component3;

            PackedInvertNormalizeInterleave4(c0, c1, c2, c3, packed, maxValue);

            ColorProfileConverter converter = new();
            Span<Cmyk> source = MemoryMarshal.Cast<float, Cmyk>(packed);

            // YccK is not a defined ICC color space — it's a JPEG-specific encoding used in Adobe-style CMYK JPEGs.
            // ICC profiles expect colorimetric CMYK values, so we must first convert YccK to CMYK using a hardcoded inverse transform.
            // This transform assumes Rec.601 YCbCr coefficients and an inverted K channel.
            //
            // The YccK => Cmyk conversion is independent of any embedded ICC profile.
            // Since the same RGB working space is used during conversion to and from XYZ,
            // colorimetric accuracy is preserved.
            converter.Convert<YccK, Cmyk>(MemoryMarshal.Cast<Cmyk, YccK>(source), source);

            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed)[..source.Length];

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };
            converter = new(options);
            converter.Convert<Cmyk, Rgb>(source, destination);

            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }
    }
}

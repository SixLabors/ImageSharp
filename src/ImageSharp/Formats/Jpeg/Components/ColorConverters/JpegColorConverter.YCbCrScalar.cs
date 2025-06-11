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
            => ConvertToRgbInPlace(values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(values, this.HalfValue, rLane, gLane, bLane);

        public static void ConvertToRgbInPlace(in ComponentValues values, float maxValue, float halfValue)
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

        public static void ConvertToRgbInPlaceWithIcc(Configuration configuration, IccProfile profile, in ComponentValues values, float maxValue)
        {
            using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(values.Component0.Length * 3);
            Span<float> packed = memoryOwner.Memory.Span;

            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;

            // Although YCbCr is a defined ICC color space, in practice ICC profiles
            // do not implement transforms from it.
            // Therefore, we first convert JPEG YCbCr to RGB manually, then perform
            // color-managed conversion to the target profile.
            //
            // The YCbCr => RGB conversion is based on BT.601 and is independent of any embedded ICC profile.
            // Since the same RGB working space is used during conversion to and from XYZ,
            // colorimetric accuracy is preserved.
            ColorProfileConverter converter = new();

            PackedNormalizeInterleave3(c0, c1, c2, packed, 1F / maxValue);

            Span<YCbCr> source = MemoryMarshal.Cast<float, YCbCr>(packed);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed);

            converter.Convert<YCbCr, Rgb>(source, destination);

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };
            converter = new(options);
            converter.Convert<Rgb, Rgb>(destination, destination);

            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
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

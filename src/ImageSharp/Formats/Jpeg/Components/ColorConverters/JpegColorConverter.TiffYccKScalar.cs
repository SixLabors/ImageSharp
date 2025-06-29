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
    /// <summary>
    /// Color converter for tiff images, which use the jpeg compression and CMYK colorspace.
    /// </summary>
    internal sealed class TiffYccKScalar : JpegColorConverterScalar
    {
        // Derived from ITU-T Rec. T.871
        internal const float RCrMult = 1.402f;
        internal const float GCbMult = (float)(0.114 * 1.772 / 0.587);
        internal const float GCrMult = (float)(0.299 * 1.402 / 0.587);
        internal const float BCbMult = 1.772f;

        public TiffYccKScalar(int precision)
            : base(JpegColorSpace.TiffYccK, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgbInPlace(in values, this.MaximumValue, this.HalfValue);

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(values, this.HalfValue, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertToRgbInPlace(in ComponentValues values, float maxValue, float halfValue)
        {
            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;
            Span<float> c3 = values.Component3;

            float scale = 1F / maxValue;
            halfValue *= scale;

            for (int i = 0; i < values.Component0.Length; i++)
            {
                float y = c0[i] * scale;
                float cb = (c1[i] * scale) - halfValue;
                float cr = (c2[i] * scale) - halfValue;
                float scaledK = 1 - (c3[i] * scale);

                // r = y + (1.402F * cr);
                // g = y - (0.344136F * cb) - (0.714136F * cr);
                // b = y + (1.772F * cb);
                c0[i] = (y + (RCrMult * cr)) * scaledK;
                c1[i] = (y - (GCbMult * cb) - (GCrMult * cr)) * scaledK;
                c2[i] = (y + (BCbMult * cb)) * scaledK;
            }
        }

        public static void ConvertFromRgb(in ComponentValues values, float halfValue, float maxValue, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            Span<float> y = values.Component0;
            Span<float> cb = values.Component1;
            Span<float> cr = values.Component2;
            Span<float> k = values.Component3;

            for (int i = 0; i < cr.Length; i++)
            {
                // Scale down to [0-1]
                const float divisor = 1F / 255F;
                float r = rLane[i] * divisor;
                float g = gLane[i] * divisor;
                float b = bLane[i] * divisor;

                float ytmp;
                float cbtmp;
                float crtmp;
                float ktmp = 1F - MathF.Max(r, MathF.Max(g, b));

                if (ktmp >= 1F)
                {
                    ytmp = 0F;
                    cbtmp = 0.5F;
                    crtmp = 0.5F;
                    ktmp = maxValue;
                }
                else
                {
                    float kmask = 1F / (1F - ktmp);
                    r *= kmask;
                    g *= kmask;
                    b *= kmask;

                    // Scale to [0-maxValue]
                    ytmp = ((0.299f * r) + (0.587f * g) + (0.114f * b)) * maxValue;
                    cbtmp = halfValue - (((0.168736f * r) - (0.331264f * g) + (0.5f * b)) * maxValue);
                    crtmp = halfValue + (((0.5f * r) - (0.418688f * g) - (0.081312f * b)) * maxValue);
                    ktmp *= maxValue;
                }

                y[i] = ytmp;
                cb[i] = cbtmp;
                cr[i] = crtmp;
                k[i] = ktmp;
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

            PackedNormalizeInterleave4(c0, c1, c2, c3, packed, maxValue);

            ColorProfileConverter converter = new();
            Span<Cmyk> source = MemoryMarshal.Cast<float, Cmyk>(packed);

            // YccK is not a defined ICC color space � it's a JPEG-specific encoding used in Adobe-style CMYK JPEGs.
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
            converter = new ColorProfileConverter(options);
            converter.Convert<Cmyk, Rgb>(source, destination);

            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }
    }
}

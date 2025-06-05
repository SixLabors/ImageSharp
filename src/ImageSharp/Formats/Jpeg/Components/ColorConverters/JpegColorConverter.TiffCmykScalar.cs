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
    internal sealed class TiffCmykScalar : JpegColorConverterScalar
    {
        public TiffCmykScalar(int precision)
            : base(JpegColorSpace.TiffCmyk, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgbInPlace(in values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(in values, this.MaximumValue, rLane, gLane, bLane);

        public static void ConvertToRgbInPlace(in ComponentValues values, float maxValue)
        {
            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;
            Span<float> c3 = values.Component3;

            float scale = 1 / maxValue;
            for (int i = 0; i < c0.Length; i++)
            {
                float c = c0[i] * scale;
                float m = c1[i] * scale;
                float y = c2[i] * scale;
                float k = 1 - (c3[i] * scale);

                c0[i] = (1 - c) * k;
                c1[i] = (1 - m) * k;
                c2[i] = (1 - y) * k;
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
                float ctmp = 255F - rLane[i];
                float mtmp = 255F - gLane[i];
                float ytmp = 255F - bLane[i];
                float ktmp = MathF.Min(MathF.Min(ctmp, mtmp), ytmp);

                if (ktmp >= 255F)
                {
                    ctmp = 0F;
                    mtmp = 0F;
                    ytmp = 0F;
                }
                else
                {
                    ctmp = (ctmp - ktmp) / (255F - ktmp);
                    mtmp = (mtmp - ktmp) / (255F - ktmp);
                    ytmp = (ytmp - ktmp) / (255F - ktmp);
                }

                c[i] = ctmp * maxValue;
                m[i] = mtmp * maxValue;
                y[i] = ytmp * maxValue;
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

            Span<Cmyk> source = MemoryMarshal.Cast<float, Cmyk>(packed);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed)[..source.Length];

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };
            ColorProfileConverter converter = new(options);
            converter.Convert<Cmyk, Rgb>(source, destination);

            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }
    }
}

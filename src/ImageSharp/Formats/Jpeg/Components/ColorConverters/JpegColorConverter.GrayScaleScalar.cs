// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class GrayScaleScalar : JpegColorConverterScalar
    {
        public GrayScaleScalar(int precision)
            : base(JpegColorSpace.Grayscale, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgbInPlace(in values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgbScalar(values, rLane, gLane, bLane);

        internal static void ConvertToRgbInPlace(in ComponentValues values, float maxValue)
        {
            ref float c0Base = ref MemoryMarshal.GetReference(values.Component0);
            ref float c1Base = ref MemoryMarshal.GetReference(values.Component1);
            ref float c2Base = ref MemoryMarshal.GetReference(values.Component2);

            float scale = 1F / maxValue;
            for (nuint i = 0; i < (nuint)values.Component0.Length; i++)
            {
                float c = Unsafe.Add(ref c0Base, i) * scale;

                Unsafe.Add(ref c0Base, i) = c;
                Unsafe.Add(ref c1Base, i) = c;
                Unsafe.Add(ref c2Base, i) = c;
            }
        }

        public static void ConvertToRgbInPlaceWithIcc(Configuration configuration, IccProfile profile, in ComponentValues values, float maxValue)
        {
            using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(values.Component0.Length * 3);
            Span<float> packed = memoryOwner.Memory.Span;

            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;

            ref float c0Base = ref MemoryMarshal.GetReference(c0);
            ref float c1Base = ref MemoryMarshal.GetReference(c1);
            ref float c2Base = ref MemoryMarshal.GetReference(c2);

            float scale = 1F / maxValue;
            for (nuint i = 0; i < (nuint)values.Component0.Length; i++)
            {
                ref float c = ref Unsafe.Add(ref c0Base, i);
                c *= scale;
            }

            Span<Y> source = MemoryMarshal.Cast<float, Y>(values.Component0);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed);

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };
            ColorProfileConverter converter = new(options);
            converter.Convert<Y, Rgb>(source, destination);

            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }

        internal static void ConvertFromRgbScalar(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            Span<float> c0 = values.Component0;

            for (int i = 0; i < c0.Length; i++)
            {
                // luminosity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                c0[i] = (float)((0.299f * rLane[i]) + (0.587f * gLane[i]) + (0.114f * bLane[i]));
            }
        }
    }
}

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
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
            => ConvertFromRgb(values, rLane, gLane, bLane);

        public static void ConvertToRgbInPlaceWithIcc(Configuration configuration, IccProfile profile, in ComponentValues values, float maxValue)
        {
            using IMemoryOwner<float> memoryOwner = configuration.MemoryAllocator.Allocate<float>(values.Component0.Length * 3);
            Span<float> packed = memoryOwner.Memory.Span;

            Span<float> c0 = values.Component0;
            Span<float> c1 = values.Component1;
            Span<float> c2 = values.Component2;

            PackedNormalizeInterleave3(c0, c1, c2, packed, 1F / maxValue);

            Span<Rgb> source = MemoryMarshal.Cast<float, Rgb>(packed);
            Span<Rgb> destination = MemoryMarshal.Cast<float, Rgb>(packed);

            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            };
            ColorProfileConverter converter = new(options);
            converter.Convert<Rgb, Rgb>(source, destination);

            UnpackDeinterleave3(MemoryMarshal.Cast<float, Vector3>(packed)[..source.Length], c0, c1, c2);
        }

        internal static void ConvertToRgbInPlace(ComponentValues values, float maxValue)
        {
            ref float c0Base = ref MemoryMarshal.GetReference(values.Component0);
            ref float c1Base = ref MemoryMarshal.GetReference(values.Component1);
            ref float c2Base = ref MemoryMarshal.GetReference(values.Component2);

            float scale = 1F / maxValue;

            for (nuint i = 0; i < (nuint)values.Component0.Length; i++)
            {
                Unsafe.Add(ref c0Base, i) *= scale;
                Unsafe.Add(ref c1Base, i) *= scale;
                Unsafe.Add(ref c2Base, i) *= scale;
            }
        }

        internal static void ConvertFromRgb(ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            rLane.CopyTo(values.Component0);
            gLane.CopyTo(values.Component1);
            bLane.CopyTo(values.Component2);
        }
    }
}

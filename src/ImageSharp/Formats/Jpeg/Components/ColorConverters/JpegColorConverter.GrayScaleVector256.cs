// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class GrayScaleVector256 : JpegColorConverterVector256
    {
        public GrayScaleVector256(int precision)
            : base(JpegColorSpace.Grayscale, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector256<float> c0Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));

            ref Vector256<float> c1Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component1));

            ref Vector256<float> c2Base =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component2));

            // Used for the color conversion
            Vector256<float> scale = Vector256.Create(1 / this.MaximumValue);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector256<float> c0 = ref Unsafe.Add(ref c0Base, i);
                c0 *= scale;

                Unsafe.Add(ref c1Base, i) = c0;
                Unsafe.Add(ref c2Base, i) = c0;
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => GrayScaleScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector256<float> destLuminance =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(values.Component0));

            ref Vector256<float> srcRed =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector256<float> srcGreen =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector256<float> srcBlue =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(bLane));

            // Used for the color conversion
            Vector256<float> f0299 = Vector256.Create(0.299f);
            Vector256<float> f0587 = Vector256.Create(0.587f);
            Vector256<float> f0114 = Vector256.Create(0.114f);

            nuint n = values.Component0.Vector256Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector256<float> r = ref Unsafe.Add(ref srcRed, i);
                ref Vector256<float> g = ref Unsafe.Add(ref srcGreen, i);
                ref Vector256<float> b = ref Unsafe.Add(ref srcBlue, i);

                // luminosity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                Unsafe.Add(ref destLuminance, i) = Vector256_.MultiplyAdd(Vector256_.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
            }
        }
    }
}

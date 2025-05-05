// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class GrayScaleVector512 : JpegColorConverterVector512
    {
        public GrayScaleVector512(int precision)
            : base(JpegColorSpace.Grayscale, precision)
        {
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceVectorized(in ComponentValues values)
        {
            ref Vector512<float> c0Base =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));

            // Used for the color conversion
            Vector512<float> scale = Vector512.Create(1 / this.MaximumValue);

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector512<float> c0 = ref Unsafe.Add(ref c0Base, i);
                c0 *= scale;
            }
        }

        /// <inheritdoc/>
        protected override void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            ref Vector512<float> destLuminance =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(values.Component0));

            ref Vector512<float> srcRed =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(rLane));
            ref Vector512<float> srcGreen =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(gLane));
            ref Vector512<float> srcBlue =
                ref Unsafe.As<float, Vector512<float>>(ref MemoryMarshal.GetReference(bLane));

            // Used for the color conversion
            Vector512<float> f0299 = Vector512.Create(0.299f);
            Vector512<float> f0587 = Vector512.Create(0.587f);
            Vector512<float> f0114 = Vector512.Create(0.114f);

            nuint n = values.Component0.Vector512Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector512<float> r = ref Unsafe.Add(ref srcRed, i);
                ref Vector512<float> g = ref Unsafe.Add(ref srcGreen, i);
                ref Vector512<float> b = ref Unsafe.Add(ref srcBlue, i);

                // luminosity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                Unsafe.Add(ref destLuminance, i) = Vector512Utilities.MultiplyAdd(Vector512Utilities.MultiplyAdd(f0114 * b, f0587, g), f0299, r);
            }
        }

        /// <inheritdoc/>
        protected override void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values)
            => GrayScaleScalar.ConvertToRgbInPlace(values.Component0, this.MaximumValue);

        /// <inheritdoc/>
        protected override void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => GrayScaleScalar.ConvertFromRgbScalar(values, r, g, b);
    }
}

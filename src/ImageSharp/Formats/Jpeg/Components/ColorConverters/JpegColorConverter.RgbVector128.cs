// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class RgbVector128 : JpegColorConverterVector128
    {
        public RgbVector128(int precision)
            : base(JpegColorSpace.RGB, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
        {
            ref Vector128<float> rBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component0));
            ref Vector128<float> gBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component1));
            ref Vector128<float> bBase =
                ref Unsafe.As<float, Vector128<float>>(ref MemoryMarshal.GetReference(values.Component2));

            // Used for the color conversion
            Vector128<float> scale = Vector128.Create(1 / this.MaximumValue);
            nuint n = values.Component0.Vector128Count<float>();
            for (nuint i = 0; i < n; i++)
            {
                ref Vector128<float> r = ref Unsafe.Add(ref rBase, i);
                ref Vector128<float> g = ref Unsafe.Add(ref gBase, i);
                ref Vector128<float> b = ref Unsafe.Add(ref bBase, i);
                r *= scale;
                g *= scale;
                b *= scale;
            }
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile)
            => RgbScalar.ConvertToRgbInPlaceWithIcc(configuration, profile, values, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            rLane.CopyTo(values.Component0);
            gLane.CopyTo(values.Component1);
            bLane.CopyTo(values.Component2);
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    internal sealed class GrayscaleScalar : JpegColorConverterScalar
    {
        public GrayscaleScalar(int precision)
            : base(JpegColorSpace.Grayscale, precision)
        {
        }

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(in ComponentValues values)
            => ConvertToRgbInplace(values.Component0, this.MaximumValue);

        /// <inheritdoc/>
        public override void ConvertFromRgb(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            => ConvertCoreInplaceFromRgb(values, r, g, b);

        internal static void ConvertToRgbInplace(Span<float> values, float maxValue)
        {
            ref float valuesRef = ref MemoryMarshal.GetReference(values);
            float scale = 1 / maxValue;

            for (nuint i = 0; i < (uint)values.Length; i++)
            {
                Unsafe.Add(ref valuesRef, i) *= scale;
            }
        }

        internal static void ConvertCoreInplaceFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            Span<float> c0 = values.Component0;

            for (int i = 0; i < c0.Length; i++)
            {
                // luminocity = (0.299 * r) + (0.587 * g) + (0.114 * b)
                float luma = (0.299f * rLane[i]) + (0.587f * gLane[i]) + (0.114f * bLane[i]);
                c0[i] = luma;
            }
        }
    }
}

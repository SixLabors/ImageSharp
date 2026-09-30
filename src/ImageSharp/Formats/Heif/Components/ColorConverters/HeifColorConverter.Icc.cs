// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Converts planar component rows to RGB and then to sRGB using the given ICC profile converter.
    /// </summary>
    /// <remarks>
    /// The result stays in <paramref name="vectors"/> as scaled, unassociated RGBA, ready for the final pixel conversion.
    /// A 4:0:0 frame reaches the profile as R = G = B, so a gray profile reads its luminance from the first component
    /// and an RGB profile sees a neutral value.
    /// </remarks>
    /// <param name="component0">The luma or first component row. Converted to red in place.</param>
    /// <param name="component1">The second component row. Converted to green in place.</param>
    /// <param name="component2">The third component row. Converted to blue in place.</param>
    /// <param name="alpha">The normalized alpha row, or an empty span for opaque pixels.</param>
    /// <param name="premultiplied">Whether the color components are associated with <paramref name="alpha"/>.</param>
    /// <param name="profileConverter">The converter from the embedded ICC profile to sRGB.</param>
    /// <param name="vectors">The destination RGBA vectors.</param>
    /// <param name="rgbSpan">RGB storage of the same length as <paramref name="vectors"/> for the profile transform.</param>
    public void ConvertToRgbInPlaceWithIcc(
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        ReadOnlySpan<float> alpha,
        bool premultiplied,
        ColorProfileConverter profileConverter,
        Span<Vector4> vectors,
        Span<Rgb> rgbSpan)
    {
        this.ConvertToRgbInPlace(component0, component1, component2);
        SimdUtils.InterleaveFloatPlanes(component0, component1, component2, alpha, vectors);
        if (premultiplied)
        {
            // The profile transform applies to unassociated color. Zero alpha keeps its stored color.
            Numerics.UnPremultiply(vectors);
        }

        ColorProfileConverterExtensionsPixelCompatible.Convert(profileConverter, vectors, rgbSpan);
    }
}

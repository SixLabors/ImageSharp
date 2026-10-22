// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Converts planar component rows to RGB and then to sRGB using the given ICC profile converter.
    /// </summary>
    /// <remarks>
    /// The result stays in <paramref name="vectors"/> as scaled, unassociated RGBA, ready for the final pixel conversion.
    /// RGB remains unclipped so floating-point destination pixels retain values outside [0,1].
    /// Integer destination pixels apply their own bounds during pixel conversion.
    /// A 4:0:0 frame reaches the profile as R = G = B. Thus a gray profile reads its luminance from the first component,
    /// and an RGB profile sees a neutral value.
    /// </remarks>
    /// <param name="component0">The luma or first component row. Converted to red in place.</param>
    /// <param name="component1">The second component row. Converted to green in place.</param>
    /// <param name="component2">The third component row. Converted to blue in place.</param>
    /// <param name="alpha">The normalized alpha row, or an empty span for opaque pixels.</param>
    /// <param name="premultiplied">Whether the color components are associated with <paramref name="alpha"/>.</param>
    /// <param name="profileConverter">The converter from the embedded ICC profile to sRGB.</param>
    /// <param name="vectors">The destination pixel vectors.</param>
    /// <param name="rgbSpan">RGB storage of the same length as <paramref name="vectors"/> for the profile transform.</param>
    public abstract void ConvertToRgb(
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        ReadOnlySpan<float> alpha,
        bool premultiplied,
        ColorProfileConverter profileConverter,
        Span<Vector4> vectors,
        Span<Rgb> rgbSpan);

    /// <summary>
    /// Converts working sRGB pixels through ICC and writes normalized native component rows.
    /// </summary>
    /// <remarks>
    /// The source pixels remain unchanged. The profile transform precedes the color-model transform in encoding,
    /// reversing their order in decoding. Both operations retain floating-point RGB until the native samples are packed.
    /// Alpha is encoded separately and does not enter the profile transform.
    /// </remarks>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="source">The working sRGB pixels.</param>
    /// <param name="component0">The destination luma or first component row.</param>
    /// <param name="component1">The destination second component row.</param>
    /// <param name="component2">The destination third component row.</param>
    /// <param name="profileConverter">The converter from working sRGB to the retained profile.</param>
    /// <param name="vectors">Caller-owned straight RGBA storage for the source row.</param>
    /// <param name="rgbSpan">Caller-owned RGB storage for the profile transform.</param>
    public abstract void ConvertFromRgb<TPixel>(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        ColorProfileConverter profileConverter,
        Span<Vector4> vectors,
        Span<Rgb> rgbSpan)
        where TPixel : unmanaged, IPixel<TPixel>;

    internal sealed partial class HeifColorConverter<TOperator>
    {
        /// <inheritdoc />
        public override void ConvertToRgb(
            Span<float> component0,
            Span<float> component1,
            Span<float> component2,
            ReadOnlySpan<float> alpha,
            bool premultiplied,
            ColorProfileConverter profileConverter,
            Span<Vector4> vectors,
            Span<Rgb> rgbSpan)
        {
            // The sample loader supplies normalized native components. The RGB profile describes the RGB produced by the model,
            // so run the selected model before ICC. Monochrome conversion repeats its intensity in all three RGB planes.
            this.ConvertToRgb(component0, component1, component2);

            // Join the planes as [R, G, B, A] vectors in caller-owned storage. ICC changes only RGB; alpha stays with each pixel.
            SimdUtils.InterleaveFloatPlanes(component0, component1, component2, alpha, vectors);
            if (premultiplied)
            {
                // The profile transform applies to unassociated color. Zero alpha keeps its stored color.
                Numerics.UnPremultiply(vectors);
            }

            // The shared converter uses rgbSpan for the profile transform and writes RGB back into vectors without changing alpha.
            // The decoder then packs vectors into TPixel, including the destination's alpha representation.
            profileConverter.Convert(vectors, rgbSpan);
        }

        /// <inheritdoc />
        public override void ConvertFromRgb<TPixel>(
            Configuration configuration,
            ReadOnlySpan<TPixel> source,
            Span<float> component0,
            Span<float> component1,
            Span<float> component2,
            ColorProfileConverter profileConverter,
            Span<Vector4> vectors,
            Span<Rgb> rgbSpan)
        {
            // This shared API extracts straight RGB according to TPixel, applies ICC, and retains alpha in vectors.
            // Both spans belong to the row writer and are reused for the next row. The separate alpha encoder reads source alpha.
            profileConverter.Convert(configuration, source, vectors, rgbSpan);

            // Each Rgb stores three consecutive floats. Unpack those transformed device values into the model's R, G and B planes.
            SimdUtils.UnpackDeinterleave3(MemoryMarshal.Cast<Rgb, Vector3>(rgbSpan), component0, component1, component2);

            // Encoding reverses the decode order: ICC produces device RGB, then the same closed operator encodes the model.
            // The maximum value is 1 because ICC returns scaled RGB, not byte or ushort codes. This does not bound RGB to [0,1].
            // Native sample packing applies the selected integer range afterward.
            this.ConvertFromRgb(component0, component1, component2, 1F);
        }
    }
}

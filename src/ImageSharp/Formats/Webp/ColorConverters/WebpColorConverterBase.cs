// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.ColorConverters;

/// <summary>
/// Selects the ordinary or ICC color path from the native WebP sample layout.
/// </summary>
internal abstract partial class WebpColorConverterBase
{
    private static readonly WebpColorConverterBase YCbCrConverter = new WebpColorConverter<YCbCrOperator>(WebpColorType.Yuv);
    private static readonly WebpColorConverterBase YCbCrIccConverter = new WebpColorConverter<YCbCrIccOperator>(WebpColorType.Yuv);
    private static readonly WebpColorConverterBase RgbConverter = new WebpColorConverter<RgbOperator>(WebpColorType.Rgb);
    private static readonly WebpColorConverterBase RgbaConverter = new WebpColorConverter<RgbOperator>(WebpColorType.Rgba);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpColorConverterBase"/> class.
    /// </summary>
    /// <param name="colorSpace">The native decoded sample layout.</param>
    protected WebpColorConverterBase(WebpColorType colorSpace) => this.ColorSpace = colorSpace;

    /// <summary>
    /// Gets the native WebP color space.
    /// </summary>
    public WebpColorType ColorSpace { get; }

    /// <summary>
    /// Gets the converter for the actual decoded sample layout.
    /// </summary>
    /// <param name="colorSpace">The native decoded sample layout.</param>
    /// <returns>The matching converter.</returns>
    public static WebpColorConverterBase GetConverter(WebpColorType colorSpace)
        => colorSpace switch
        {
            WebpColorType.Yuv => YCbCrConverter,
            WebpColorType.Rgb => RgbConverter,
            WebpColorType.Rgba => RgbaConverter,
            _ => throw new InvalidImageContentException($"Unsupported WebP color space {colorSpace}.")
        };

    /// <summary>
    /// Gets the converter for native YCbCr samples and the selected source profile.
    /// </summary>
    /// <param name="converter">The configured ICC converter, if present.</param>
    /// <returns>The converter that writes BGR or preserves YCbCr for its source profile.</returns>
    public static WebpColorConverterBase GetYCbCrConverter(ColorProfileConverter? converter)
        => converter?.Options.SourceIccProfile?.Header.DataColorSpace == IccColorSpaceType.YCbCr
            ? YCbCrIccConverter
            : YCbCrConverter;

    /// <summary>
    /// Creates the conversion from an embedded profile to sRGB when required.
    /// </summary>
    /// <param name="configuration">The decoding configuration.</param>
    /// <param name="profile">The embedded profile, if present.</param>
    /// <returns>The configured converter, or null when ICC conversion is not required.</returns>
    public static ColorProfileConverter? CreateIccConverter(Configuration configuration, IccProfile? profile)
    {
        if (!IccProfile.TryGetIccProfileForColorConversion(profile, out IccProfile? sourceProfile))
        {
            return null;
        }

        ColorConversionOptions options = new()
        {
            SourceIccProfile = sourceProfile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = configuration.MemoryAllocator
        };

        return new ColorProfileConverter(options);
    }

    /// <summary>
    /// Converts an image region to full-resolution luma and half-resolution chroma planes using the YCbCr converter.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="frame">The source image region.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="memoryAllocator">The allocator for temporary row storage.</param>
    /// <param name="converter">The return transform from sRGB to the encoded RGB profile, or null.</param>
    /// <param name="vectors">Reusable straight RGBA row scratch, empty when no transform is required.</param>
    /// <param name="rgb">Reusable RGB transform scratch, empty when no transform is required.</param>
    /// <param name="y">The full-resolution luma plane.</param>
    /// <param name="u">The half-resolution Cb chroma plane.</param>
    /// <param name="v">The half-resolution Cr chroma plane.</param>
    /// <returns>Whether any source pixel is non-opaque.</returns>
    public abstract bool ConvertFromRgb<TPixel>(
        Buffer2DRegion<TPixel> frame,
        Configuration configuration,
        MemoryAllocator memoryAllocator,
        ColorProfileConverter? converter,
        Span<Vector4> vectors,
        Span<Rgb> rgb,
        Span<byte> y,
        Span<byte> u,
        Span<byte> v)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Upsamples native lossy YCbCr rows into the converter's three-channel byte layout.
    /// </summary>
    /// <param name="topY">The current luma row.</param>
    /// <param name="bottomY">The next luma row, or empty for a single output row.</param>
    /// <param name="topU">The preceding chroma row.</param>
    /// <param name="topV">The preceding second chroma row.</param>
    /// <param name="curU">The current chroma row.</param>
    /// <param name="curV">The current second chroma row.</param>
    /// <param name="topDestination">The first output row.</param>
    /// <param name="bottomDestination">The second output row, or empty for a single output row.</param>
    /// <param name="length">The number of luma samples in each row.</param>
    /// <param name="uvBuffer">Scratch storage for four 64-byte interpolated chroma planes.</param>
    public abstract void UpSample(
        Span<byte> topY,
        Span<byte> bottomY,
        Span<byte> topU,
        Span<byte> topV,
        Span<byte> curU,
        Span<byte> curV,
        Span<byte> topDestination,
        Span<byte> bottomDestination,
        int length,
        Span<byte> uvBuffer);

    /// <summary>
    /// Converts an RGB source row and packs its destination pixels.
    /// </summary>
    /// <typeparam name="TPixelFrom">The source RGB pixel format.</typeparam>
    /// <typeparam name="TPixelTo">The destination pixel format.</typeparam>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="source">The source row in its pixel format's alpha representation.</param>
    /// <param name="converter">The configured ICC converter, or null for direct conversion.</param>
    /// <param name="vectors">Vector scratch for the row.</param>
    /// <param name="rgb">RGB scratch for the row.</param>
    /// <param name="destination">The destination pixels.</param>
    public abstract void ConvertRgbRow<TPixelFrom, TPixelTo>(
        Configuration configuration,
        ReadOnlySpan<TPixelFrom> source,
        ColorProfileConverter? converter,
        Span<Vector4> vectors,
        Span<Rgb> rgb,
        Span<TPixelTo> destination)
        where TPixelFrom : unmanaged, IPixel<TPixelFrom>
        where TPixelTo : unmanaged, IPixel<TPixelTo>;

    /// <summary>
    /// Converts native lossy YCbCr output and packs the destination pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="configuration">The decoding configuration.</param>
    /// <param name="source">The interpolated native three-channel row.</param>
    /// <param name="alpha">The separate alpha row, or empty for opaque pixels.</param>
    /// <param name="converter">The configured ICC converter, if present.</param>
    /// <param name="vectors">Vector scratch for the row.</param>
    /// <param name="rgb">RGB scratch for the row.</param>
    /// <param name="destination">The destination pixels.</param>
    public abstract void ConvertYuvRow<TPixel>(
        Configuration configuration,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> alpha,
        ColorProfileConverter? converter,
        Span<Vector4> vectors,
        Span<Rgb> rgb,
        Span<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Converts between RGB pixels and native WebP samples using the selected operator.
    /// </summary>
    /// <typeparam name="TOperator">The scalar and SIMD color conversion operator.</typeparam>
    private sealed partial class WebpColorConverter<TOperator> : WebpColorConverterBase
        where TOperator : struct, IWebpColorConverterOperator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebpColorConverter{TOperator}"/> class.
        /// </summary>
        /// <param name="colorSpace">The native decoded sample layout.</param>
        public WebpColorConverter(WebpColorType colorSpace)
            : base(colorSpace)
        {
        }
    }
}

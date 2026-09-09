// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes the compressed payload of a single HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal interface IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the image item type decoded by this implementation.
    /// </summary>
    public Heif4CharCode Type { get; }

    /// <summary>
    /// Decodes the compressed payload of an image item.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="chromaUpsampling">The chroma reconstruction mode.</param>
    /// <param name="item">The HEIF item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded image payload.</param>
    /// <param name="colorProfile">
    /// The container color description that overrides matching color information in the encoded image payload.
    /// </param>
    /// <param name="profile">The source ICC profile selected for conversion, or null to preserve source colors.</param>
    /// <param name="alphaFrame">The native auxiliary plane, or null for an opaque image.</param>
    /// <param name="alphaOutputSize">The complete color extent covered by the auxiliary plane.</param>
    /// <param name="alphaRectangle">The matching region within the auxiliary presentation.</param>
    /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
    /// <param name="sourceRectangle">The source area of interest in luma-sample coordinates.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    /// <param name="destination">The destination pixel region.</param>
    /// <param name="metadata">The metadata receiving the decoded image properties.</param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    public void DecodeItemData(
        DecoderOptions options,
        HeifChromaUpsampling chromaUpsampling,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile,
        IccProfile? profile,
        Av1FrameBuffer<byte>? alphaFrame,
        Size alphaOutputSize,
        Rectangle alphaRectangle,
        bool premultiplied,
        Rectangle sourceRectangle,
        HeifPixelTransform transform,
        Buffer2DRegion<TPixel> destination,
        ImageMetadata metadata,
        CancellationToken cancellationToken);
}

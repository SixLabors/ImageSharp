// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc.Color;

/// <summary>
/// Adapts HEVC color signaling and reconstructed planes to the shared HEIF color pipeline.
/// </summary>
internal static class HevcYuvConverter
{
    /// <summary>
    /// Converts reconstructed HEVC component planes to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="picture">The reconstructed HEVC picture.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="colorProfile">The effective H.273 color description.</param>
    /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the first converted pixel.</param>
    public static void ConvertToRgb<TPixel>(
        Configuration configuration,
        HevcPictureBuffer picture,
        ImageFrame<TPixel> image,
        CicpProfile colorProfile,
        HevcChromaSampleLocation chromaSampleLocation,
        int sourceX = 0,
        int sourceY = 0)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(picture, colorProfile, out HeifColorConversionMode mode);
        HevcPlanarSampleBuffer buffer = new(picture, chromaSampleLocation);
        HeifPlanarColorConverter.ConvertToRgb<TPixel, HevcPlanarSampleBuffer>(
            configuration,
            buffer,
            image,
            in parameters,
            mode,
            sourceX,
            sourceY);
    }

    /// <summary>
    /// Composes a visible HEVC luma rectangle into a packed color frame as auxiliary alpha.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="picture">The reconstructed HEVC picture containing the alpha luma plane.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="colorProfile">The effective H.273 color description defining the luma range.</param>
    /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
    /// <param name="sourceRectangle">The visible luma rectangle within the coded picture.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image or grid tile.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    public static void ComposeAlpha<TPixel>(
        Configuration configuration,
        HevcPictureBuffer picture,
        ImageFrame<TPixel> destination,
        CicpProfile colorProfile,
        HevcChromaSampleLocation chromaSampleLocation,
        Rectangle sourceRectangle,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(picture, colorProfile, out _);
        HevcPlanarSampleBuffer buffer = new(picture, chromaSampleLocation);
        HeifPlanarAlphaCompositor.Compose<TPixel, HevcPlanarSampleBuffer, ushort, HeifUShortSampleConverter>(
            configuration,
            buffer,
            destination,
            in parameters,
            sourceRectangle,
            outputSize,
            destinationRectangle,
            premultiplied);
    }

    /// <summary>
    /// Converts packed pixels to the configured HEVC component planes.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="picture">The destination HEVC picture.</param>
    /// <param name="colorProfile">The H.273 color description to encode.</param>
    /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
    public static void ConvertFromRgb<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        HevcPictureBuffer picture,
        CicpProfile colorProfile,
        HevcChromaSampleLocation chromaSampleLocation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(picture, colorProfile, out HeifColorConversionMode mode);
        HevcPlanarSampleBuffer buffer = new(picture, chromaSampleLocation);
        HeifPlanarColorConverter.ConvertFromRgb<TPixel, HevcPlanarSampleBuffer, ushort, HeifUShortSampleConverter>(
            configuration,
            image,
            buffer,
            in parameters,
            mode);
    }

    /// <summary>
    /// Resolves the shared H.273 conversion parameters for an HEVC picture.
    /// </summary>
    /// <param name="picture">The picture defining component precision and sampling.</param>
    /// <param name="colorProfile">The effective H.273 color description.</param>
    /// <param name="mode">The resolved color conversion operation.</param>
    /// <returns>The immutable scalar and SIMD conversion parameters.</returns>
    private static HeifColorConversionParameters GetConversionParameters(
        HevcPictureBuffer picture,
        CicpProfile colorProfile,
        out HeifColorConversionMode mode)
        => HeifColorConversionParameters.Create(
            colorProfile.ColorPrimaries,
            colorProfile.TransferCharacteristics,
            colorProfile.MatrixCoefficients,
            colorProfile.FullRange,
            picture.BitDepthLuma,
            picture.ChromaFormat == 0 ? picture.BitDepthLuma : picture.BitDepthChroma,
            picture.ChromaFormat == 0,
            picture.ChromaFormat == 3,
            out mode);
}

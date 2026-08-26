// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Color;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Formats.Heif.Color.HeifColorConverterBase;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Color;

/// <summary>
/// Adapts AV1 color signaling and reconstructed planes to the shared HEIF color pipeline.
/// </summary>
internal static class Av1YuvConverter
{
    /// <summary>
    /// Converts the reconstructed component planes to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="frameBuffer">The reconstructed AV1 frame.</param>
    /// <param name="image">The destination image frame.</param>
    public static void ConvertToRgb<TPixel>(Configuration configuration, Av1FrameBuffer<byte> frameBuffer, ImageFrame<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer, out HeifColorConversionMode mode);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(frameBuffer);
            HeifPlanarColorConverter.ConvertToRgb<TPixel, Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleLoader>(
                configuration,
                buffer,
                image,
                in parameters,
                mode);

            return;
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(frameBuffer);
        HeifPlanarColorConverter.ConvertToRgb<TPixel, Av1PlanarSampleBuffer<ushort>>(
            configuration,
            highBitDepthBuffer,
            image,
            in parameters,
            mode);
    }

    /// <summary>
    /// Converts packed pixels to the configured monochrome or component planes used by the AV1 encoder.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="frameBuffer">The destination AV1 frame.</param>
    public static void ConvertFromRgb<TPixel>(Configuration configuration, ImageFrame<TPixel> image, Av1FrameBuffer<byte> frameBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer, out HeifColorConversionMode mode);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(frameBuffer);
            HeifPlanarColorConverter.ConvertFromRgb<TPixel, Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleStorer>(
                configuration,
                image,
                buffer,
                in parameters,
                mode);

            return;
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(frameBuffer);
        HeifPlanarColorConverter.ConvertFromRgb<TPixel, Av1PlanarSampleBuffer<ushort>, ushort, HeifUShortSampleStorer>(
            configuration,
            image,
            highBitDepthBuffer,
            in parameters,
            mode);
    }

    /// <summary>
    /// Resolves the H.273 conversion mode, matrix coefficients, and sample range for a frame.
    /// </summary>
    /// <param name="frameBuffer">The AV1 frame containing the signaled color configuration.</param>
    /// <param name="mode">The resolved conversion mode.</param>
    /// <returns>The resolved conversion parameters.</returns>
    private static HeifColorConversionParameters GetConversionParameters(Av1FrameBuffer<byte> frameBuffer, out HeifColorConversionMode mode)
    {
        if (frameBuffer.ColorConfig.ChromaSamplePosition == ObuChromoSamplePosition.Reserved)
        {
            throw new InvalidImageContentException("The reserved AV1 chroma sample position is invalid.");
        }

        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;

        return HeifColorConversionParameters.Create(
            (CicpColorPrimaries)(byte)frameBuffer.ColorConfig.ColorPrimaries,
            (CicpTransferCharacteristics)(byte)frameBuffer.ColorConfig.TransferCharacteristics,
            (CicpMatrixCoefficients)(byte)frameBuffer.ColorConfig.MatrixCoefficients,
            frameBuffer.ColorConfig.ColorRange,
            frameBuffer.BitDepth.GetBitCount(),
            frameBuffer.BitDepth.GetBitCount(),
            isMonochrome,
            frameBuffer.ColorFormat == Av1ColorFormat.Yuv444,
            out mode);
    }
}

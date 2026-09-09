// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Color;

/// <summary>
/// Adapts AV1 color signaling and reconstructed planes to the shared HEIF color pipeline.
/// </summary>
internal static class Av1YuvConverter
{
    /// <summary>
    /// Converts a rectangular region of reconstructed component planes directly to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="frameBuffer">The reconstructed AV1 frame.</param>
    /// <param name="sourceRectangle">The luma-sample region mapped to the complete destination frame.</param>
    /// <param name="destination">The destination pixel region.</param>
    /// <param name="presentationSize">The spatial extent of the presented component planes.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    /// <param name="profile">The source profile selected for conversion, or null to preserve source colors.</param>
    /// <param name="alphaFrame">The auxiliary plane, or null for opaque pixels.</param>
    /// <param name="alphaOutputSize">The complete color extent covered by alpha.</param>
    /// <param name="alphaRectangle">The exact matching auxiliary presentation region.</param>
    /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
    /// <param name="chromaUpsampling">The chroma reconstruction mode.</param>
    /// <param name="isFullRange">Whether RGB conversion interprets the color planes as full-range samples.</param>
    public static void ConvertToRgb<TPixel>(
        Configuration configuration,
        Av1FrameBuffer<byte> frameBuffer,
        Rectangle sourceRectangle,
        Buffer2DRegion<TPixel> destination,
        Size presentationSize,
        HeifPixelTransform transform,
        IccProfile? profile,
        Av1FrameBuffer<byte>? alphaFrame,
        Size alphaOutputSize,
        Rectangle alphaRectangle,
        bool premultiplied,
        HeifChromaUpsampling chromaUpsampling,
        bool isFullRange)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using HeifAlphaRowSource? alpha = alphaFrame is null
            ? null
            : CreateAlphaRowSource(configuration, alphaFrame, alphaOutputSize, alphaRectangle);

        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer.ColorConfig, isFullRange, out HeifColorConversionMode mode);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(frameBuffer);
            if (presentationSize != new Size(buffer.Width, buffer.Height))
            {
                using Av1PresentationSampleBuffer<byte, Av1PlanarSampleBuffer<byte>> presented = new(
                    configuration, buffer, presentationSize.Width, presentationSize.Height);

                HeifPlanarColorConverter.ConvertToRgb<
                    TPixel, Av1PresentationSampleBufferView<byte, Av1PlanarSampleBuffer<byte>>, byte, HeifByteSampleConverter>(
                    configuration, presented.View, destination, in parameters, mode, sourceRectangle.X, sourceRectangle.Y, sourceRectangle.Size, transform, profile, alpha, premultiplied, chromaUpsampling);

                return;
            }

            HeifPlanarColorConverter.ConvertToRgb<TPixel, Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleConverter>(
                configuration,
                buffer,
                destination,
                in parameters,
                mode,
                sourceRectangle.X,
                sourceRectangle.Y,
                sourceRectangle.Size,
                transform,
                profile,
                alpha,
                premultiplied,
                chromaUpsampling);

            return;
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(frameBuffer);
        if (presentationSize != new Size(highBitDepthBuffer.Width, highBitDepthBuffer.Height))
        {
            using Av1PresentationSampleBuffer<ushort, Av1PlanarSampleBuffer<ushort>> presented = new(
                configuration, highBitDepthBuffer, presentationSize.Width, presentationSize.Height);

            HeifPlanarColorConverter.ConvertToRgb<TPixel, Av1PresentationSampleBufferView<ushort, Av1PlanarSampleBuffer<ushort>>>(
                configuration, presented.View, destination, in parameters, mode, sourceRectangle.X, sourceRectangle.Y, sourceRectangle.Size, transform, profile, alpha, premultiplied, chromaUpsampling);

            return;
        }

        HeifPlanarColorConverter.ConvertToRgb<TPixel, Av1PlanarSampleBuffer<ushort>>(
            configuration,
            highBitDepthBuffer,
            destination,
            in parameters,
            mode,
            sourceRectangle.X,
            sourceRectangle.Y,
            sourceRectangle.Size,
            transform,
            profile,
            alpha,
            premultiplied,
            chromaUpsampling);
    }

    /// <summary>
    /// Selects a native alpha row reader for the exact color region.
    /// </summary>
    /// <param name="configuration">The configuration providing scratch storage.</param>
    /// <param name="frame">The native auxiliary samples retained by the caller.</param>
    /// <param name="outputSize">The complete color extent before cropping and orientation.</param>
    /// <param name="window">The color region within that extent.</param>
    /// <returns>The row reader whose scratch storage must be disposed after conversion.</returns>
    public static HeifAlphaRowSource CreateAlphaRowSource(
        Configuration configuration,
        Av1FrameBuffer<byte> frame,
        Size outputSize,
        Rectangle window)
    {
        HeifColorConversionParameters parameters = GetConversionParameters(frame.ColorConfig, frame.ColorConfig.ColorRange, out _);
        Rectangle source = new(0, 0, frame.Width, frame.Height);
        if (frame.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(frame);
            return source.Size == outputSize
                ? new HeifAlphaRowSource<Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleConverter>(
                    configuration, buffer, in parameters, window)
                : new HeifPlanarAlphaResizeWorker<Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleConverter>(
                    configuration, buffer, in parameters, source, window, outputSize);
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(frame);
        return source.Size == outputSize
            ? new HeifAlphaRowSource<Av1PlanarSampleBuffer<ushort>, ushort, HeifUShortSampleConverter>(
                configuration, highBitDepthBuffer, in parameters, window)
            : new HeifPlanarAlphaResizeWorker<Av1PlanarSampleBuffer<ushort>, ushort, HeifUShortSampleConverter>(
                configuration, highBitDepthBuffer, in parameters, source, window, outputSize);
    }

    /// <summary>
    /// Composes the reconstructed luma plane into a packed color frame as auxiliary alpha.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="frameBuffer">The reconstructed AV1 frame containing the alpha luma plane.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image or grid tile.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    public static void ComposeAlpha<TPixel>(
        Configuration configuration,
        Av1FrameBuffer<byte> frameBuffer,
        Buffer2DRegion<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        HeifPixelTransform transform)
        where TPixel : unmanaged, IPixel<TPixel>
        => ComposeAlpha(
            configuration,
            frameBuffer,
            new Rectangle(0, 0, frameBuffer.Width, frameBuffer.Height),
            destination,
            outputSize,
            destinationRectangle,
            premultiplied,
            transform);

    /// <summary>
    /// Composes a rectangular reconstructed luma region into a packed color frame as auxiliary alpha.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="frameBuffer">The reconstructed AV1 frame containing the alpha luma plane.</param>
    /// <param name="sourceRectangle">The luma-sample region mapped to the destination rectangle.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image or grid tile.</param>
    /// <param name="destinationRectangle">The destination region receiving the presented alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    public static void ComposeAlpha<TPixel>(
        Configuration configuration,
        Av1FrameBuffer<byte> frameBuffer,
        Rectangle sourceRectangle,
        Buffer2DRegion<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        HeifPixelTransform transform)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer.ColorConfig, frameBuffer.ColorConfig.ColorRange, out _);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(frameBuffer);
            HeifPlanarAlphaCompositor.Compose<TPixel, Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleConverter>(
                configuration,
                buffer,
                destination,
                in parameters,
                sourceRectangle,
                outputSize,
                destinationRectangle,
                premultiplied,
                transform);

            return;
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(frameBuffer);
        HeifPlanarAlphaCompositor.Compose<TPixel, Av1PlanarSampleBuffer<ushort>, ushort, HeifUShortSampleConverter>(
            configuration,
            highBitDepthBuffer,
            destination,
            in parameters,
            sourceRectangle,
            outputSize,
            destinationRectangle,
            premultiplied,
            transform);
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
        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer.ColorConfig, frameBuffer.ColorConfig.ColorRange, out HeifColorConversionMode mode);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(frameBuffer);
            HeifPlanarColorConverter.ConvertFromRgb<TPixel, Av1PlanarSampleBuffer<byte>, byte, HeifByteSampleConverter>(
                configuration,
                image,
                buffer,
                in parameters,
                mode);

            return;
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(frameBuffer);
        HeifPlanarColorConverter.ConvertFromRgb<TPixel, Av1PlanarSampleBuffer<ushort>, ushort, HeifUShortSampleConverter>(
            configuration,
            image,
            highBitDepthBuffer,
            in parameters,
            mode);
    }

    /// <summary>
    /// Resolves the H.273 conversion mode, matrix coefficients, and sample range for a frame.
    /// </summary>
    /// <param name="colorConfig">The signaled AV1 color configuration.</param>
    /// <param name="isFullRange">Whether conversion interprets the samples as full range.</param>
    /// <param name="mode">The resolved conversion mode.</param>
    /// <returns>The resolved conversion parameters.</returns>
    public static HeifColorConversionParameters GetConversionParameters(
        ObuColorConfig colorConfig,
        bool isFullRange,
        out HeifColorConversionMode mode)
    {
        if (colorConfig.ChromaSamplePosition == ObuChromoSamplePosition.Reserved)
        {
            throw new InvalidImageContentException("The reserved AV1 chroma sample position is invalid.");
        }

        bool isMonochrome = colorConfig.IsMonochrome;

        return HeifColorConversionParameters.Create(
            (CicpColorPrimaries)(byte)colorConfig.ColorPrimaries,
            (CicpTransferCharacteristics)(byte)colorConfig.TransferCharacteristics,
            (CicpMatrixCoefficients)(byte)colorConfig.MatrixCoefficients,
            isFullRange,
            colorConfig.BitDepth.GetBitCount(),
            colorConfig.BitDepth.GetBitCount(),
            isMonochrome,
            colorConfig.GetColorFormat() == Av1ColorFormat.Yuv444,
            out mode);
    }
}

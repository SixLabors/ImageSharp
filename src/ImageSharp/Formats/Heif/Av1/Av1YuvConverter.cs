// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Color;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Formats.Heif.Color.HeifColorConverterBase;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Converts between reconstructed AV1 YUV planes and packed ImageSharp pixels.
/// </summary>
internal static partial class Av1YuvConverter
{
    /// <summary>
    /// The largest value represented by an eight-bit packed RGB component.
    /// </summary>
    private const float ByteMaximum = byte.MaxValue;

    /// <summary>
    /// The largest value represented by a 16-bit packed RGB component.
    /// </summary>
    private const float UShortMaximum = ushort.MaxValue;

    /// <summary>
    /// Converts the reconstructed YUV planes to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="frameBuffer">The reconstructed AV1 frame.</param>
    /// <param name="image">The destination image frame.</param>
    public static void ConvertToRgb<TPixel>(Configuration configuration, Av1FrameBuffer<byte> frameBuffer, ImageFrame<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer, out HeifColorConversionMode mode);

        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, frameBuffer.ColorFormat == Av1ColorFormat.Yuv400);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            YuvToRgbRowConverter<TPixel, byte, HeifByteSampleLoader> converter = new(configuration, frameBuffer, image, colorConverter);
            using IMemoryOwner<float> scratchOwner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
            using IMemoryOwner<TPixel> proxyOwner = configuration.MemoryAllocator.Allocate<TPixel>(image.Width + 3);
            Span<float> scratch = scratchOwner.GetSpan();
            Span<TPixel> proxy = proxyOwner.GetSpan()[..(image.Width + 3)];
            for (int y = 0; y < image.Height; y++)
            {
                converter.Convert(y, scratch, proxy);
            }
        }
        else
        {
            YuvToRgbRowConverter<TPixel, ushort, HeifUShortSampleLoader> converter = new(configuration, frameBuffer, image, colorConverter);
            using IMemoryOwner<float> owner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
            Span<float> scratch = owner.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                converter.Convert(y, scratch, Span<TPixel>.Empty);
            }
        }
    }

    /// <summary>
    /// Converts packed pixels to the configured monochrome or YUV planes used by the AV1 encoder.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="frameBuffer">The destination AV1 frame.</param>
    public static void ConvertFromRgb<TPixel>(Configuration configuration, ImageFrame<TPixel> image, Av1FrameBuffer<byte> frameBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(frameBuffer, out HeifColorConversionMode mode);
        float sampleMaximum = parameters.LumaSampleMaximum;

        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, isMonochrome);
        int rowShift = !isMonochrome && frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
        int iterationCount = (image.Height + rowShift) >> rowShift;
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            RgbToYuvRowConverter<TPixel, byte, HeifByteSampleStorer> converter = new(configuration, frameBuffer, image, colorConverter, sampleMaximum);
            using IMemoryOwner<float> componentOwner = configuration.MemoryAllocator.Allocate<float>(converter.ComponentBufferLength);
            Span<float> components = componentOwner.GetSpan();
            for (int y = 0; y < iterationCount; y++)
            {
                converter.Convert(y, Span<Rgb48>.Empty, components);
            }
        }
        else
        {
            RgbToYuvRowConverter<TPixel, ushort, HeifUShortSampleStorer> converter = new(configuration, frameBuffer, image, colorConverter, sampleMaximum);
            using IMemoryOwner<Rgb48> packedOwner = configuration.MemoryAllocator.Allocate<Rgb48>(image.Width);
            using IMemoryOwner<float> componentOwner = configuration.MemoryAllocator.Allocate<float>(converter.ComponentBufferLength);
            Span<Rgb48> packed = packedOwner.GetSpan()[..image.Width];
            Span<float> components = componentOwner.GetSpan();
            for (int y = 0; y < iterationCount; y++)
            {
                converter.Convert(y, packed, components);
            }
        }
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

    /// <summary>
    /// Resolves the two chroma samples and quarter-sample weight surrounding a luma coordinate.
    /// </summary>
    /// <param name="coordinate">The luma coordinate.</param>
    /// <param name="subsampling">The chroma subsampling shift.</param>
    /// <param name="isCentered">Whether chroma lies between neighboring luma samples.</param>
    /// <param name="maximum">The last available chroma coordinate.</param>
    /// <param name="lower">The lower chroma coordinate.</param>
    /// <param name="upper">The upper chroma coordinate.</param>
    /// <param name="upperWeight">The upper-coordinate weight with a denominator of four.</param>
    private static void GetChromaCoordinates(
        int coordinate,
        int subsampling,
        bool isCentered,
        int maximum,
        out int lower,
        out int upper,
        out int upperWeight)
    {
        if (subsampling == 0)
        {
            lower = coordinate;
            upper = coordinate;
            upperWeight = 0;
            return;
        }

        int sample = coordinate >> 1;
        bool isOdd = (coordinate & 1) != 0;
        if (isCentered)
        {
            lower = isOdd ? sample : Math.Max(sample - 1, 0);
            upper = isOdd ? Math.Min(sample + 1, maximum) : sample;
            upperWeight = isOdd ? 1 : 3;
            return;
        }

        lower = sample;
        upper = isOdd ? Math.Min(sample + 1, maximum) : sample;
        upperWeight = isOdd ? 2 : 0;
    }
}

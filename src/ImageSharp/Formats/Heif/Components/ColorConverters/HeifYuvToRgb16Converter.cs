// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <summary>
/// Converts high-bit-depth HEIF YUV planes to packed pixels through opaque 16-bit RGB.
/// </summary>
internal static partial class HeifYuvToRgb16Converter
{
    /// <summary>
    /// Determines whether the pinned libheif-compatible high-bit-depth conversion supports the supplied planes.
    /// </summary>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="lumaBitDepth">The luma sample precision in bits.</param>
    /// <param name="chromaBitDepth">The chroma sample precision in bits.</param>
    /// <param name="isMonochrome">Whether the image contains only luma samples.</param>
    /// <param name="mode">The resolved H.273 conversion operation.</param>
    /// <returns><see langword="true"/> when the planes can use this converter; otherwise, <see langword="false"/>.</returns>
    public static bool SupportsLibheifConversion(
        int subsamplingX,
        int subsamplingY,
        int lumaBitDepth,
        int chromaBitDepth,
        bool isMonochrome,
        HeifColorConversionMode mode)
        => (isMonochrome || (subsamplingX is 0 or 1 && subsamplingY is 0 or 1))
            && lumaBitDepth is > 8 and <= 16
            && (isMonochrome || chromaBitDepth == lumaBitDepth)
            && mode == HeifColorConversionMode.Coefficients;

    /// <summary>
    /// Converts supported high-bit-depth HEVC planes using pinned libheif arithmetic and nearest chroma sampling.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter that exposes reconstructed component rows.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="buffer">The reconstructed component-plane buffer.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
    public static void Convert<TPixel, TBuffer>(
        Configuration configuration,
        TBuffer buffer,
        ImageFrame<TPixel> image,
        in HeifColorConversionParameters parameters,
        int sourceX,
        int sourceY)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<ushort>
    {
        ConversionParameters conversionParameters = new(in parameters, buffer.LumaBitDepth);

        // Three planar rows and one packed Rgba64 row share a single image-lifetime allocation. The latter occupies
        // four UInt16 values per pixel, so the complete scratch requirement is seven samples per output pixel.
        using IMemoryOwner<ushort> rowOwner = configuration.MemoryAllocator.Allocate<ushort>(image.Width * 7);
        Span<ushort> storage = rowOwner.GetSpan();
        Span<ushort> red = storage[..image.Width];
        Span<ushort> green = storage.Slice(image.Width, image.Width);
        Span<ushort> blue = storage.Slice(image.Width * 2, image.Width);
        Span<Rgba64> packed = MemoryMarshal.Cast<ushort, Rgba64>(storage[(image.Width * 3)..]);

        for (int y = 0; y < image.Height; y++)
        {
            int lumaY = sourceY + y;
            ReadOnlySpan<ushort> luma = buffer.GetLumaRowSpan(lumaY).Slice(sourceX, image.Width);
            if (buffer.IsMonochrome)
            {
                // Pinned libheif copies the reconstructed luma code value directly to RGB for monochrome images.
                // Scaling to the 16-bit pixel domain happens after that copy, without limited-range expansion.
                ConvertRow<LibheifMonochromeOperator>(
                    luma,
                    luma,
                    luma,
                    red,
                    green,
                    blue,
                    0,
                    in conversionParameters);
            }
            else
            {
                int subsamplingX = buffer.ChromaSubsamplingX;
                int chromaY = lumaY >> buffer.ChromaSubsamplingY;
                ReadOnlySpan<ushort> chromaBlue = buffer.GetChromaBlueRowSpan(chromaY).Slice(sourceX >> subsamplingX);
                ReadOnlySpan<ushort> chromaRed = buffer.GetChromaRedRowSpan(chromaY).Slice(sourceX >> subsamplingX);

                // libheif's selected direct conversion addresses the native chroma sample at x >> subsamplingX.
                // The HEIF crop boundary already keeps sourceX aligned to complete chroma samples.
                ConvertRow<LibheifCoefficientOperator>(
                    luma,
                    chromaBlue,
                    chromaRed,
                    red,
                    green,
                    blue,
                    subsamplingX,
                    in conversionParameters);
            }

            HeifSampleConversion.PackRgba64(red, green, blue, packed);
            Span<TPixel> destination = image.PixelBuffer.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgba64(configuration, packed, destination);
        }
    }
}

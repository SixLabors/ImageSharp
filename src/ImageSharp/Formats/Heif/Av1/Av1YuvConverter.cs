// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Converts between reconstructed AV1 YUV planes and packed ImageSharp pixels.
/// </summary>
internal static class Av1YuvConverter
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
    /// Identifies the matrix operation used between encoded planes and RGB components.
    /// </summary>
    private enum ConversionMode
    {
        /// <summary>
        /// A coefficient-based YCbCr matrix conversion.
        /// </summary>
        Coefficients,

        /// <summary>
        /// Direct G, B, and R component mapping from the Y, U, and V planes.
        /// </summary>
        Identity,

        /// <summary>
        /// The reversible-style YCgCo color transform.
        /// </summary>
        YCgCo,
    }

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
        GetConversionParameters(
            frameBuffer,
            out ConversionMode mode,
            out float kr,
            out float kg,
            out float kb,
            out float lumaBias,
            out float lumaScale,
            out float chromaBias,
            out float chromaScale,
            out float sampleMaximum);

        Buffer2DRegion<byte> yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        int subX = frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;
        int subY = frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
        Buffer2DRegion<byte> uPlane = isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.U, subX, subY);
        Buffer2DRegion<byte> vPlane = isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.V, subX, subY);
        bool isEightBit = frameBuffer.BitDepth == Av1BitDepth.EightBit;
        using IMemoryOwner<Rgb24>? rowOwner = isEightBit
            ? configuration.MemoryAllocator.Allocate<Rgb24>(image.Width)
            : null;

        using IMemoryOwner<Rgb48>? highBitDepthRowOwner = isEightBit
            ? null
            : configuration.MemoryAllocator.Allocate<Rgb48>(image.Width);

        Span<Rgb24> rgbRow = rowOwner is null ? Span<Rgb24>.Empty : rowOwner.GetSpan()[..image.Width];
        Span<Rgb48> highBitDepthRgbRow = highBitDepthRowOwner is null
            ? Span<Rgb48>.Empty
            : highBitDepthRowOwner.GetSpan()[..image.Width];

        for (int y = 0; y < image.Height; y++)
        {
            int y0 = 0;
            int y1 = 0;
            int y1Weight = 0;
            if (!isMonochrome)
            {
                GetChromaCoordinates(
                    y,
                    subY,
                    subY != 0 && frameBuffer.ColorConfig.ChromaSamplePosition != ObuChromoSamplePosition.Colocated,
                    uPlane.Height - 1,
                    out y0,
                    out y1,
                    out y1Weight);
            }

            if (isEightBit)
            {
                ConvertYuvToRgbRow(
                    yPlane.DangerousGetRowSpan(y),
                    isMonochrome ? default : uPlane.DangerousGetRowSpan(y0),
                    isMonochrome ? default : uPlane.DangerousGetRowSpan(y1),
                    isMonochrome ? default : vPlane.DangerousGetRowSpan(y0),
                    isMonochrome ? default : vPlane.DangerousGetRowSpan(y1),
                    y1Weight,
                    rgbRow,
                    isMonochrome,
                    subX,
                    subY,
                    frameBuffer.ColorConfig.ChromaSamplePosition,
                    mode,
                    kr,
                    kg,
                    kb,
                    lumaBias,
                    lumaScale,
                    chromaBias,
                    chromaScale,
                    sampleMaximum);
            }
            else
            {
                // Staging high-bit-depth samples through Rgb48 preserves their precision while still using the
                // optimized packed-pixel conversion paths shared by the rest of ImageSharp.
                ConvertYuvToRgbRow(
                    frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0),
                    isMonochrome ? default : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, y0, subX, subY),
                    isMonochrome ? default : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, y1, subX, subY),
                    isMonochrome ? default : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, y0, subX, subY),
                    isMonochrome ? default : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, y1, subX, subY),
                    y1Weight,
                    highBitDepthRgbRow,
                    isMonochrome,
                    subX,
                    subY,
                    frameBuffer.ColorConfig.ChromaSamplePosition,
                    mode,
                    kr,
                    kg,
                    kb,
                    lumaBias,
                    lumaScale,
                    chromaBias,
                    chromaScale,
                    sampleMaximum);
            }

            if (isEightBit)
            {
                PixelOperations<TPixel>.Instance.FromRgb24(
                    configuration,
                    rgbRow,
                    image.PixelBuffer.DangerousGetRowSpan(y));
            }
            else
            {
                PixelOperations<TPixel>.Instance.FromRgb48(
                    configuration,
                    highBitDepthRgbRow,
                    image.PixelBuffer.DangerousGetRowSpan(y));
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
        GetConversionParameters(
            frameBuffer,
            out ConversionMode mode,
            out float kr,
            out float kg,
            out float kb,
            out float lumaBias,
            out float lumaScale,
            out float chromaBias,
            out float chromaScale,
            out float sampleMaximum);

        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        int subX = frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;
        int subY = frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
        Buffer2DRegion<byte> yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        Buffer2DRegion<byte> uPlane = isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.U, subX, subY);
        Buffer2DRegion<byte> vPlane = isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.V, subX, subY);
        int sourceRowsPerIteration = !isMonochrome && subY != 0 ? 2 : 1;
        bool isEightBit = frameBuffer.BitDepth == Av1BitDepth.EightBit;
        int rowBufferLength = image.Width * sourceRowsPerIteration;
        using IMemoryOwner<Rgb24>? rowOwner = isEightBit
            ? configuration.MemoryAllocator.Allocate<Rgb24>(rowBufferLength)
            : null;

        using IMemoryOwner<Rgb48>? highBitDepthRowOwner = isEightBit
            ? null
            : configuration.MemoryAllocator.Allocate<Rgb48>(rowBufferLength);

        Span<Rgb24> rgbRow0 = rowOwner is null ? Span<Rgb24>.Empty : rowOwner.GetSpan()[..image.Width];
        Span<Rgb24> rgbRow1 = sourceRowsPerIteration == 2 && rowOwner is not null
            ? rowOwner.GetSpan().Slice(image.Width, image.Width)
            : Span<Rgb24>.Empty;

        Span<Rgb48> highBitDepthRgbRow0 = highBitDepthRowOwner is null
            ? Span<Rgb48>.Empty
            : highBitDepthRowOwner.GetSpan()[..image.Width];

        Span<Rgb48> highBitDepthRgbRow1 = sourceRowsPerIteration == 2 && highBitDepthRowOwner is not null
            ? highBitDepthRowOwner.GetSpan().Slice(image.Width, image.Width)
            : Span<Rgb48>.Empty;

        for (int y = 0; y < image.Height; y += sourceRowsPerIteration)
        {
            if (isEightBit)
            {
                PixelOperations<TPixel>.Instance.ToRgb24(
                    configuration,
                    image.PixelBuffer.DangerousGetRowSpan(y),
                    rgbRow0);
            }
            else
            {
                // Rgb48 retains source component precision before the values are quantized to the requested
                // 10-bit or 12-bit AV1 sample range.
                PixelOperations<TPixel>.Instance.ToRgb48(
                    configuration,
                    image.PixelBuffer.DangerousGetRowSpan(y),
                    highBitDepthRgbRow0);
            }

            bool hasSecondSourceRow = sourceRowsPerIteration == 2 && y + 1 < image.Height;
            if (hasSecondSourceRow)
            {
                if (isEightBit)
                {
                    PixelOperations<TPixel>.Instance.ToRgb24(
                        configuration,
                        image.PixelBuffer.DangerousGetRowSpan(y + 1),
                        rgbRow1);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.ToRgb48(
                        configuration,
                        image.PixelBuffer.DangerousGetRowSpan(y + 1),
                        highBitDepthRgbRow1);
                }
            }

            if (isEightBit)
            {
                Span<byte> yRow0 = yPlane.DangerousGetRowSpan(y);
                if (isMonochrome || subX == 0)
                {
                    ConvertRgbToYuvRow(
                        rgbRow0,
                        yRow0,
                        isMonochrome ? Span<byte>.Empty : uPlane.DangerousGetRowSpan(y),
                        isMonochrome ? Span<byte>.Empty : vPlane.DangerousGetRowSpan(y),
                        mode,
                        kr,
                        kg,
                        kb,
                        lumaBias,
                        lumaScale,
                        chromaBias,
                        chromaScale,
                        sampleMaximum);
                }
                else
                {
                    ConvertRgbToSubsampledYuvRows(
                        rgbRow0,
                        hasSecondSourceRow ? rgbRow1 : ReadOnlySpan<Rgb24>.Empty,
                        yRow0,
                        hasSecondSourceRow ? yPlane.DangerousGetRowSpan(y + 1) : Span<byte>.Empty,
                        uPlane.DangerousGetRowSpan(y >> subY),
                        vPlane.DangerousGetRowSpan(y >> subY),
                        mode,
                        kr,
                        kg,
                        kb,
                        lumaBias,
                        lumaScale,
                        chromaBias,
                        chromaScale,
                        sampleMaximum);
                }
            }
            else
            {
                Span<ushort> yRow0 = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0);
                if (isMonochrome || subX == 0)
                {
                    ConvertRgbToYuvRow(
                        highBitDepthRgbRow0,
                        yRow0,
                        isMonochrome ? Span<ushort>.Empty : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, y, 0, 0),
                        isMonochrome ? Span<ushort>.Empty : frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, y, 0, 0),
                        mode,
                        kr,
                        kg,
                        kb,
                        lumaBias,
                        lumaScale,
                        chromaBias,
                        chromaScale,
                        sampleMaximum);
                }
                else
                {
                    ConvertRgbToSubsampledYuvRows(
                        highBitDepthRgbRow0,
                        hasSecondSourceRow ? highBitDepthRgbRow1 : ReadOnlySpan<Rgb48>.Empty,
                        yRow0,
                        hasSecondSourceRow ? frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y + 1, 0, 0) : Span<ushort>.Empty,
                        frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, y >> subY, subX, subY),
                        frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, y >> subY, subX, subY),
                        mode,
                        kr,
                        kg,
                        kb,
                        lumaBias,
                        lumaScale,
                        chromaBias,
                        chromaScale,
                        sampleMaximum);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the H.273 conversion mode, matrix coefficients, and sample range for a frame.
    /// </summary>
    /// <param name="frameBuffer">The AV1 frame containing the signaled color configuration.</param>
    /// <param name="mode">The resolved conversion mode.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="lumaBias">The encoded luma bias.</param>
    /// <param name="lumaScale">The encoded luma range.</param>
    /// <param name="chromaBias">The encoded chroma midpoint.</param>
    /// <param name="chromaScale">The encoded chroma range.</param>
    /// <param name="sampleMaximum">The largest encoded sample value.</param>
    private static void GetConversionParameters(
        Av1FrameBuffer<byte> frameBuffer,
        out ConversionMode mode,
        out float kr,
        out float kg,
        out float kb,
        out float lumaBias,
        out float lumaScale,
        out float chromaBias,
        out float chromaScale,
        out float sampleMaximum)
    {
        mode = ConversionMode.Coefficients;
        kr = 0F;
        kb = 0F;

        // These values are the matrix table used by libavif and are defined by H.273.
        switch (frameBuffer.ColorConfig.MatrixCoefficients)
        {
            case ObuMatrixCoefficients.Identity:
                mode = ConversionMode.Identity;
                break;
            case ObuMatrixCoefficients.Bt709:
                kr = 0.2126F;
                kb = 0.0722F;
                break;
            case ObuMatrixCoefficients.Fcc:
                kr = 0.30F;
                kb = 0.11F;
                break;
            case ObuMatrixCoefficients.Bt470BG:
            case ObuMatrixCoefficients.Bt601:
            case ObuMatrixCoefficients.Unspecified:
                // libavif falls back to BT.601 coefficients when the matrix is unavailable.
                kr = 0.299F;
                kb = 0.114F;
                break;
            case ObuMatrixCoefficients.Smpte240:
                kr = 0.212F;
                kb = 0.087F;
                break;
            case ObuMatrixCoefficients.SmpteYCgCo:
                mode = ConversionMode.YCgCo;
                break;
            case ObuMatrixCoefficients.Bt2020NonConstantLuminance:
                kr = 0.2627F;
                kb = 0.0593F;
                break;
            default:
                throw new NotSupportedException($"AV1 matrix coefficients '{frameBuffer.ColorConfig.MatrixCoefficients}' are not currently supported.");
        }

        kg = 1F - kr - kb;
        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        bool isFullRange = frameBuffer.ColorConfig.ColorRange;
        if (mode == ConversionMode.Identity && !isMonochrome && frameBuffer.ColorFormat != Av1ColorFormat.Yuv444)
        {
            throw new InvalidImageContentException("AV1 identity matrix coefficients require YUV 4:4:4 sampling.");
        }

        if (frameBuffer.ColorConfig.ChromaSamplePosition == ObuChromoSamplePosition.Reserved)
        {
            throw new InvalidImageContentException("The reserved AV1 chroma sample position is invalid.");
        }

        if (mode == ConversionMode.YCgCo && !isMonochrome && !isFullRange)
        {
            throw new NotSupportedException("Limited-range AV1 YCgCo color conversion is not currently supported.");
        }

        int bitCount = frameBuffer.BitDepth.GetBitCount();
        int depthScale = 1 << (bitCount - 8);
        sampleMaximum = (1 << bitCount) - 1;
        chromaBias = 128F * depthScale;
        lumaBias = isFullRange ? 0F : 16F * depthScale;
        lumaScale = isFullRange ? sampleMaximum : 219F * depthScale;
        chromaScale = isFullRange ? sampleMaximum : 224F * depthScale;
    }

    /// <summary>
    /// Converts one YUV row to packed RGB using the resolved H.273 conversion state.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <typeparam name="TRgb">The packed RGB staging type.</typeparam>
    /// <param name="ySource">The luma samples.</param>
    /// <param name="uRow0">The upper blue-difference chroma row.</param>
    /// <param name="uRow1">The lower blue-difference chroma row.</param>
    /// <param name="vRow0">The upper red-difference chroma row.</param>
    /// <param name="vRow1">The lower red-difference chroma row.</param>
    /// <param name="y1Weight">The lower chroma-row weight with a denominator of four.</param>
    /// <param name="destination">The destination RGB pixels.</param>
    /// <param name="isMonochrome">Whether the frame contains only luma samples.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="chromaSamplePosition">The spatial position of subsampled chroma.</param>
    /// <param name="mode">The conversion mode.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="lumaBias">The encoded luma bias.</param>
    /// <param name="lumaScale">The encoded luma range.</param>
    /// <param name="chromaBias">The encoded chroma midpoint.</param>
    /// <param name="chromaScale">The encoded chroma range.</param>
    /// <param name="sampleMaximum">The largest encoded sample value.</param>
    private static void ConvertYuvToRgbRow<TSample, TRgb>(
        ReadOnlySpan<TSample> ySource,
        ReadOnlySpan<TSample> uRow0,
        ReadOnlySpan<TSample> uRow1,
        ReadOnlySpan<TSample> vRow0,
        ReadOnlySpan<TSample> vRow1,
        int y1Weight,
        Span<TRgb> destination,
        bool isMonochrome,
        int subX,
        int subY,
        ObuChromoSamplePosition chromaSamplePosition,
        ConversionMode mode,
        float kr,
        float kg,
        float kb,
        float lumaBias,
        float lumaScale,
        float chromaBias,
        float chromaScale,
        float sampleMaximum)
        where TSample : unmanaged
        where TRgb : unmanaged
    {
        for (int x = 0; x < destination.Length; x++)
        {
            float y = (GetSample(ySource, x) - lumaBias) / lumaScale;
            float r;
            float g;
            float b;

            if (isMonochrome)
            {
                r = y;
                g = y;
                b = y;
            }
            else
            {
                float u = SampleChroma(uRow0, uRow1, x, subX, subY, chromaSamplePosition, y1Weight);
                float v = SampleChroma(vRow0, vRow1, x, subX, subY, chromaSamplePosition, y1Weight);
                float cb = (u - chromaBias) / chromaScale;
                float cr = (v - chromaBias) / chromaScale;

                switch (mode)
                {
                    case ConversionMode.Identity:
                        // H.273 identity coding stores the nonlinear G, B, and R signals in Y, U, and V order.
                        r = (v - lumaBias) / lumaScale;
                        g = y;
                        b = (u - lumaBias) / lumaScale;
                        break;
                    case ConversionMode.YCgCo:
                        float temporary = y - cb;
                        r = temporary + cr;
                        g = y + cb;
                        b = temporary - cr;
                        break;
                    default:
                        r = y + (2F * (1F - kr) * cr);
                        g = y - (2F * ((kr * (1F - kr) * cr) + (kb * (1F - kb) * cb)) / kg);
                        b = y + (2F * (1F - kb) * cb);
                        break;
                }
            }

            // The generic staging type is controlled by the frame bit depth. The JIT removes the inactive branch,
            // retaining direct component access without routing every pixel through Vector4 or interface dispatch.
            if (typeof(TRgb) == typeof(Rgb24))
            {
                Rgb24 pixel = new(
                    ToSample<byte>(r * ByteMaximum, ByteMaximum),
                    ToSample<byte>(g * ByteMaximum, ByteMaximum),
                    ToSample<byte>(b * ByteMaximum, ByteMaximum));

                destination[x] = Unsafe.As<Rgb24, TRgb>(ref pixel);
            }
            else
            {
                Rgb48 pixel = new(
                    ToSample<ushort>(r * UShortMaximum, UShortMaximum),
                    ToSample<ushort>(g * UShortMaximum, UShortMaximum),
                    ToSample<ushort>(b * UShortMaximum, UShortMaximum));

                destination[x] = Unsafe.As<Rgb48, TRgb>(ref pixel);
            }
        }
    }

    /// <summary>
    /// Bilinearly reconstructs a chroma sample at a luma coordinate.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <param name="row0">The upper chroma row.</param>
    /// <param name="row1">The lower chroma row.</param>
    /// <param name="x">The luma column coordinate.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="chromaSamplePosition">The spatial position of subsampled chroma.</param>
    /// <param name="y1Weight">The lower chroma-row weight with a denominator of four.</param>
    /// <returns>The reconstructed encoded chroma sample.</returns>
    private static float SampleChroma<TSample>(
        ReadOnlySpan<TSample> row0,
        ReadOnlySpan<TSample> row1,
        int x,
        int subX,
        int subY,
        ObuChromoSamplePosition chromaSamplePosition,
        int y1Weight)
        where TSample : unmanaged
    {
        // Unknown 4:2:0 and all 4:2:2 input use the centered convention employed by libavif.
        bool isCenteredX = subX != 0 && (subY == 0 || chromaSamplePosition == ObuChromoSamplePosition.Unknown);
        GetChromaCoordinates(x, subX, isCenteredX, row0.Length - 1, out int x0, out int x1, out int x1Weight);

        float top = (GetSample(row0, x0) * (4 - x1Weight)) + (GetSample(row0, x1) * x1Weight);
        float bottom = (GetSample(row1, x0) * (4 - x1Weight)) + (GetSample(row1, x1) * x1Weight);

        return ((top * (4 - y1Weight)) + (bottom * y1Weight)) / 16F;
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

    /// <summary>
    /// Converts one packed RGB row to luma and optional full-resolution chroma using the resolved H.273 conversion state.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <typeparam name="TRgb">The packed RGB staging type.</typeparam>
    /// <param name="source">The source RGB pixels.</param>
    /// <param name="yDestination">The destination luma samples.</param>
    /// <param name="uDestination">The destination blue-difference chroma samples.</param>
    /// <param name="vDestination">The destination red-difference chroma samples.</param>
    /// <param name="mode">The conversion mode.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="lumaBias">The encoded luma bias.</param>
    /// <param name="lumaScale">The encoded luma range.</param>
    /// <param name="chromaBias">The encoded chroma midpoint.</param>
    /// <param name="chromaScale">The encoded chroma range.</param>
    /// <param name="sampleMaximum">The largest encoded sample value.</param>
    private static void ConvertRgbToYuvRow<TSample, TRgb>(
        ReadOnlySpan<TRgb> source,
        Span<TSample> yDestination,
        Span<TSample> uDestination,
        Span<TSample> vDestination,
        ConversionMode mode,
        float kr,
        float kg,
        float kb,
        float lumaBias,
        float lumaScale,
        float chromaBias,
        float chromaScale,
        float sampleMaximum)
        where TSample : unmanaged
        where TRgb : unmanaged
    {
        for (int x = 0; x < source.Length; x++)
        {
            ConvertRgbToYuv(source[x], mode, kr, kg, kb, out float y, out float cb, out float cr);

            yDestination[x] = ToSample<TSample>((y * lumaScale) + lumaBias, sampleMaximum);
            if (!uDestination.IsEmpty)
            {
                if (mode == ConversionMode.Identity)
                {
                    uDestination[x] = ToSample<TSample>((cb * lumaScale) + lumaBias, sampleMaximum);
                    vDestination[x] = ToSample<TSample>((cr * lumaScale) + lumaBias, sampleMaximum);
                }
                else
                {
                    uDestination[x] = ToSample<TSample>((cb * chromaScale) + chromaBias, sampleMaximum);
                    vDestination[x] = ToSample<TSample>((cr * chromaScale) + chromaBias, sampleMaximum);
                }
            }
        }
    }

    /// <summary>
    /// Converts one or two packed RGB rows to luma and horizontally subsampled chroma.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <typeparam name="TRgb">The packed RGB staging type.</typeparam>
    /// <param name="sourceRow0">The first source row.</param>
    /// <param name="sourceRow1">The optional second source row for 4:2:0 conversion.</param>
    /// <param name="yDestination0">The first destination luma row.</param>
    /// <param name="yDestination1">The optional second destination luma row.</param>
    /// <param name="uDestination">The destination blue-difference chroma row.</param>
    /// <param name="vDestination">The destination red-difference chroma row.</param>
    /// <param name="mode">The conversion mode.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="lumaBias">The encoded luma bias.</param>
    /// <param name="lumaScale">The encoded luma range.</param>
    /// <param name="chromaBias">The encoded chroma midpoint.</param>
    /// <param name="chromaScale">The encoded chroma range.</param>
    /// <param name="sampleMaximum">The largest encoded sample value.</param>
    private static void ConvertRgbToSubsampledYuvRows<TSample, TRgb>(
        ReadOnlySpan<TRgb> sourceRow0,
        ReadOnlySpan<TRgb> sourceRow1,
        Span<TSample> yDestination0,
        Span<TSample> yDestination1,
        Span<TSample> uDestination,
        Span<TSample> vDestination,
        ConversionMode mode,
        float kr,
        float kg,
        float kb,
        float lumaBias,
        float lumaScale,
        float chromaBias,
        float chromaScale,
        float sampleMaximum)
        where TSample : unmanaged
        where TRgb : unmanaged
    {
        int rowCount = sourceRow1.IsEmpty ? 1 : 2;
        for (int x = 0; x < sourceRow0.Length; x += 2)
        {
            int columnCount = Math.Min(2, sourceRow0.Length - x);
            float cbSum = 0F;
            float crSum = 0F;
            for (int row = 0; row < rowCount; row++)
            {
                ReadOnlySpan<TRgb> source = row == 0 ? sourceRow0 : sourceRow1;
                Span<TSample> yDestination = row == 0 ? yDestination0 : yDestination1;
                for (int column = 0; column < columnCount; column++)
                {
                    int sourceIndex = x + column;
                    ConvertRgbToYuv(source[sourceIndex], mode, kr, kg, kb, out float y, out float cb, out float cr);
                    yDestination[sourceIndex] = ToSample<TSample>((y * lumaScale) + lumaBias, sampleMaximum);
                    cbSum += cb;
                    crSum += cr;
                }
            }

            // libavif's scalar average path divides by the actual edge-block dimensions, so odd widths and heights
            // do not replicate a missing RGB sample into the chroma average.
            float sampleCount = columnCount * rowCount;
            float cbAverage = cbSum / sampleCount;
            float crAverage = crSum / sampleCount;
            int chromaIndex = x >> 1;
            uDestination[chromaIndex] = ToSample<TSample>((cbAverage * chromaScale) + chromaBias, sampleMaximum);
            vDestination[chromaIndex] = ToSample<TSample>((crAverage * chromaScale) + chromaBias, sampleMaximum);
        }
    }

    /// <summary>
    /// Converts one packed RGB pixel to normalized luma and chroma values.
    /// </summary>
    /// <typeparam name="TRgb">The packed RGB staging type.</typeparam>
    /// <param name="pixel">The source RGB pixel.</param>
    /// <param name="mode">The conversion mode.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="y">The normalized luma result.</param>
    /// <param name="cb">The normalized blue-difference chroma result.</param>
    /// <param name="cr">The normalized red-difference chroma result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertRgbToYuv<TRgb>(
        TRgb pixel,
        ConversionMode mode,
        float kr,
        float kg,
        float kb,
        out float y,
        out float cb,
        out float cr)
        where TRgb : unmanaged
    {
        float r;
        float g;
        float b;

        // These are the only staging formats selected by the owning conversion methods. Keeping the format choice
        // generic lets the JIT specialize the hot loop and preserves high-bit-depth input without boxing or copies.
        if (typeof(TRgb) == typeof(Rgb24))
        {
            Rgb24 rgb24 = Unsafe.As<TRgb, Rgb24>(ref pixel);
            r = rgb24.R / ByteMaximum;
            g = rgb24.G / ByteMaximum;
            b = rgb24.B / ByteMaximum;
        }
        else
        {
            Rgb48 rgb48 = Unsafe.As<TRgb, Rgb48>(ref pixel);
            r = rgb48.R / UShortMaximum;
            g = rgb48.G / UShortMaximum;
            b = rgb48.B / UShortMaximum;
        }

        switch (mode)
        {
            case ConversionMode.Identity:
                // H.273 identity coding stores the nonlinear G, B, and R signals in Y, U, and V order.
                y = g;
                cb = b;
                cr = r;
                break;
            case ConversionMode.YCgCo:
                y = (0.5F * g) + (0.25F * (r + b));
                cb = (0.5F * g) - (0.25F * (r + b));
                cr = 0.5F * (r - b);
                break;
            default:
                y = (kr * r) + (kg * g) + (kb * b);
                cb = (b - y) / (2F * (1F - kb));
                cr = (r - y) / (2F * (1F - kr));
                break;
        }
    }

    /// <summary>
    /// Reads an 8-bit or 16-bit unsigned sample without introducing a separate conversion buffer.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <param name="source">The source samples.</param>
    /// <param name="index">The sample index.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetSample<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged
    {
        ref TSample sample = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), index);
        return typeof(TSample) == typeof(byte)
            ? Unsafe.As<TSample, byte>(ref sample)
            : Unsafe.As<TSample, ushort>(ref sample);
    }

    /// <summary>
    /// Rounds and clamps a conversion result to the encoded sample range.
    /// </summary>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <param name="value">The conversion result.</param>
    /// <param name="maximum">The largest encoded sample value.</param>
    /// <returns>The bounded sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TSample ToSample<TSample>(float value, float maximum)
        where TSample : unmanaged
    {
        int sample = Numerics.Clamp((int)MathF.Round(value, MidpointRounding.AwayFromZero), 0, (int)maximum);
        if (typeof(TSample) == typeof(byte))
        {
            byte result = (byte)sample;
            return Unsafe.As<byte, TSample>(ref result);
        }

        ushort highBitDepthResult = (ushort)sample;
        return Unsafe.As<ushort, TSample>(ref highBitDepthResult);
    }
}

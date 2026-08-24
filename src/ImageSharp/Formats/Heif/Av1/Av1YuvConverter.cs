// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1YuvConverter
{
    private const float ByteMaximum = byte.MaxValue;
    private const float ChromaBias = 128F;
    private const float FullRangeLumaScale = byte.MaxValue;
    private const float FullRangeChromaScale = byte.MaxValue;
    private const float LimitedRangeLumaBias = 16F;
    private const float LimitedRangeLumaScale = 219F;
    private const float LimitedRangeChromaScale = 224F;

    private enum ConversionMode
    {
        Coefficients,
        Identity,
        YCgCo,
    }

    /// <summary>
    /// Converts the reconstructed 8-bit YUV planes to packed pixels.
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
            out float chromaScale);

        Buffer2DRegion<byte> yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        int subX = frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;
        int subY = frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
        Buffer2DRegion<byte> uPlane = isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.U, subX, subY);
        Buffer2DRegion<byte> vPlane = isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.V, subX, subY);
        using IMemoryOwner<Rgb24> rowOwner = configuration.MemoryAllocator.Allocate<Rgb24>(image.Width);
        Span<Rgb24> rgbRow = rowOwner.GetSpan()[..image.Width];

        for (int y = 0; y < image.Height; y++)
        {
            ConvertYuvToRgbRow(
                yPlane.DangerousGetRowSpan(y),
                uPlane,
                vPlane,
                y,
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
                chromaScale);

            PixelOperations<TPixel>.Instance.FromRgb24(
                configuration,
                rgbRow,
                image.PixelBuffer.DangerousGetRowSpan(y));
        }
    }

    /// <summary>
    /// Converts packed pixels to the 8-bit YUV 4:4:4 planes used by the AV1 encoder.
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
            out float chromaScale);

        if (frameBuffer.ColorFormat != Av1ColorFormat.Yuv444)
        {
            throw new NotSupportedException("Only AV1 YUV 4:4:4 encoding color conversion is currently supported.");
        }

        Buffer2DRegion<byte> yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        Buffer2DRegion<byte> uPlane = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0);
        Buffer2DRegion<byte> vPlane = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0);
        using IMemoryOwner<Rgb24> rowOwner = configuration.MemoryAllocator.Allocate<Rgb24>(image.Width);
        Span<Rgb24> rgbRow = rowOwner.GetSpan()[..image.Width];

        for (int y = 0; y < image.Height; y++)
        {
            PixelOperations<TPixel>.Instance.ToRgb24(
                configuration,
                image.PixelBuffer.DangerousGetRowSpan(y),
                rgbRow);

            ConvertRgbToYuv444Row(
                rgbRow,
                yPlane.DangerousGetRowSpan(y),
                uPlane.DangerousGetRowSpan(y),
                vPlane.DangerousGetRowSpan(y),
                mode,
                kr,
                kg,
                kb,
                lumaBias,
                lumaScale,
                chromaScale);
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
    /// <param name="chromaScale">The encoded chroma range.</param>
    private static void GetConversionParameters(
        Av1FrameBuffer<byte> frameBuffer,
        out ConversionMode mode,
        out float kr,
        out float kg,
        out float kb,
        out float lumaBias,
        out float lumaScale,
        out float chromaScale)
    {
        if (frameBuffer.BitDepth != Av1BitDepth.EightBit)
        {
            throw new NotSupportedException("Only 8-bit AV1 color conversion is currently supported.");
        }

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

        lumaBias = isFullRange ? 0F : LimitedRangeLumaBias;
        lumaScale = isFullRange ? FullRangeLumaScale : LimitedRangeLumaScale;
        chromaScale = isFullRange ? FullRangeChromaScale : LimitedRangeChromaScale;
    }

    /// <summary>
    /// Converts one YUV row to packed RGB using the resolved H.273 conversion state.
    /// </summary>
    /// <param name="ySource">The luma samples.</param>
    /// <param name="uPlane">The blue-difference chroma plane.</param>
    /// <param name="vPlane">The red-difference chroma plane.</param>
    /// <param name="yCoordinate">The luma row coordinate.</param>
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
    /// <param name="chromaScale">The encoded chroma range.</param>
    private static void ConvertYuvToRgbRow(
        ReadOnlySpan<byte> ySource,
        in Buffer2DRegion<byte> uPlane,
        in Buffer2DRegion<byte> vPlane,
        int yCoordinate,
        Span<Rgb24> destination,
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
        float chromaScale)
    {
        for (int x = 0; x < destination.Length; x++)
        {
            float y = (ySource[x] - lumaBias) / lumaScale;
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
                float u = SampleChroma(uPlane, x, yCoordinate, subX, subY, chromaSamplePosition);
                float v = SampleChroma(vPlane, x, yCoordinate, subX, subY, chromaSamplePosition);
                float cb = (u - ChromaBias) / chromaScale;
                float cr = (v - ChromaBias) / chromaScale;

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

            destination[x] = new Rgb24(
                ToByte(r * ByteMaximum),
                ToByte(g * ByteMaximum),
                ToByte(b * ByteMaximum));
        }
    }

    /// <summary>
    /// Bilinearly reconstructs a chroma sample at a luma coordinate.
    /// </summary>
    /// <param name="plane">The subsampled chroma plane.</param>
    /// <param name="x">The luma column coordinate.</param>
    /// <param name="y">The luma row coordinate.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <param name="chromaSamplePosition">The spatial position of subsampled chroma.</param>
    /// <returns>The reconstructed encoded chroma sample.</returns>
    private static float SampleChroma(
        in Buffer2DRegion<byte> plane,
        int x,
        int y,
        int subX,
        int subY,
        ObuChromoSamplePosition chromaSamplePosition)
    {
        // Unknown 4:2:0 and all 4:2:2 input use the centered convention employed by libavif.
        bool isCenteredX = subX != 0 && (subY == 0 || chromaSamplePosition == ObuChromoSamplePosition.Unknown);
        bool isCenteredY = subY != 0 && chromaSamplePosition != ObuChromoSamplePosition.Colocated;
        GetChromaCoordinates(x, subX, isCenteredX, plane.Width - 1, out int x0, out int x1, out int x1Weight);
        GetChromaCoordinates(y, subY, isCenteredY, plane.Height - 1, out int y0, out int y1, out int y1Weight);

        ReadOnlySpan<byte> row0 = plane.DangerousGetRowSpan(y0);
        ReadOnlySpan<byte> row1 = plane.DangerousGetRowSpan(y1);
        int top = (row0[x0] * (4 - x1Weight)) + (row0[x1] * x1Weight);
        int bottom = (row1[x0] * (4 - x1Weight)) + (row1[x1] * x1Weight);

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
    /// Converts one packed RGB row to YUV 4:4:4 using the resolved H.273 conversion state.
    /// </summary>
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
    /// <param name="chromaScale">The encoded chroma range.</param>
    private static void ConvertRgbToYuv444Row(
        ReadOnlySpan<Rgb24> source,
        Span<byte> yDestination,
        Span<byte> uDestination,
        Span<byte> vDestination,
        ConversionMode mode,
        float kr,
        float kg,
        float kb,
        float lumaBias,
        float lumaScale,
        float chromaScale)
    {
        for (int x = 0; x < source.Length; x++)
        {
            Rgb24 pixel = source[x];
            float r = pixel.R / ByteMaximum;
            float g = pixel.G / ByteMaximum;
            float b = pixel.B / ByteMaximum;
            float y;
            float cb;
            float cr;

            switch (mode)
            {
                case ConversionMode.Identity:
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

            yDestination[x] = ToByte((y * lumaScale) + lumaBias);
            if (mode == ConversionMode.Identity)
            {
                uDestination[x] = ToByte((cb * lumaScale) + lumaBias);
                vDestination[x] = ToByte((cr * lumaScale) + lumaBias);
            }
            else
            {
                uDestination[x] = ToByte((cb * chromaScale) + ChromaBias);
                vDestination[x] = ToByte((cr * chromaScale) + ChromaBias);
            }
        }
    }

    /// <summary>
    /// Rounds and clamps a conversion result to an 8-bit sample.
    /// </summary>
    /// <param name="value">The conversion result.</param>
    /// <returns>The bounded sample.</returns>
    private static byte ToByte(float value)
        => (byte)Numerics.Clamp((int)MathF.Round(value, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
}

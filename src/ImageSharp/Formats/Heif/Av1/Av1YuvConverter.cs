// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

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
        GetConversionParameters(
            frameBuffer,
            out Av1ColorConversionMode mode,
            out float kr,
            out float kg,
            out float kb,
            out float lumaBias,
            out float lumaScale,
            out float chromaBias,
            out float chromaScale,
            out float sampleMaximum);

        ObuTransferCharacteristics transferCharacteristics = frameBuffer.ColorConfig.TransferCharacteristics;
        Av1ConstantLuminanceScales constantLuminanceScales = mode == Av1ColorConversionMode.ConstantLuminance
            ? new Av1ConstantLuminanceScales(transferCharacteristics, kr, kb)
            : default;

        Av1ColorConversionParameters parameters = new(
            kr,
            kg,
            kb,
            transferCharacteristics,
            in constantLuminanceScales,
            lumaBias,
            lumaScale,
            chromaBias,
            chromaScale);

        Av1ColorConverterBase colorConverter = Av1ColorConverterBase.Create(mode, in parameters, frameBuffer.ColorFormat == Av1ColorFormat.Yuv400);
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            YuvToRgbRowConverter<TPixel, byte, ByteSampleLoader> converter = new(configuration, frameBuffer, image, colorConverter);
            using IMemoryOwner<float> owner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
            Span<float> scratch = owner.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                converter.Convert(y, scratch);
            }
        }
        else
        {
            YuvToRgbRowConverter<TPixel, ushort, UShortSampleLoader> converter = new(configuration, frameBuffer, image, colorConverter);
            using IMemoryOwner<float> owner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
            Span<float> scratch = owner.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                converter.Convert(y, scratch);
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
            out Av1ColorConversionMode mode,
            out float kr,
            out float kg,
            out float kb,
            out float lumaBias,
            out float lumaScale,
            out float chromaBias,
            out float chromaScale,
            out float sampleMaximum);

        ObuTransferCharacteristics transferCharacteristics = frameBuffer.ColorConfig.TransferCharacteristics;
        Av1ConstantLuminanceScales constantLuminanceScales = mode == Av1ColorConversionMode.ConstantLuminance
            ? new Av1ConstantLuminanceScales(transferCharacteristics, kr, kb)
            : default;

        Av1ColorConversionParameters parameters = new(
            kr,
            kg,
            kb,
            transferCharacteristics,
            in constantLuminanceScales,
            lumaBias,
            lumaScale,
            chromaBias,
            chromaScale);

        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        Av1ColorConverterBase colorConverter = Av1ColorConverterBase.Create(mode, in parameters, isMonochrome);
        int rowShift = !isMonochrome && frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
        int iterationCount = (image.Height + rowShift) >> rowShift;
        if (frameBuffer.BitDepth == Av1BitDepth.EightBit)
        {
            RgbToYuvRowConverter<TPixel, byte, ByteSampleStorer> converter = new(configuration, frameBuffer, image, colorConverter, sampleMaximum);
            using IMemoryOwner<float> componentOwner = configuration.MemoryAllocator.Allocate<float>(converter.ComponentBufferLength);
            Span<float> components = componentOwner.GetSpan();
            for (int y = 0; y < iterationCount; y++)
            {
                converter.Convert(y, Span<Rgb48>.Empty, components);
            }
        }
        else
        {
            RgbToYuvRowConverter<TPixel, ushort, UShortSampleStorer> converter = new(configuration, frameBuffer, image, colorConverter, sampleMaximum);
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
        out Av1ColorConversionMode mode,
        out float kr,
        out float kg,
        out float kb,
        out float lumaBias,
        out float lumaScale,
        out float chromaBias,
        out float chromaScale,
        out float sampleMaximum)
    {
        mode = Av1ColorConversionMode.Coefficients;
        kr = 0F;
        kb = 0F;

        // These values are the matrix table used by libavif and are defined by H.273.
        switch (frameBuffer.ColorConfig.MatrixCoefficients)
        {
            case ObuMatrixCoefficients.Identity:
                mode = Av1ColorConversionMode.Identity;
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
                mode = Av1ColorConversionMode.YCgCo;
                break;
            case ObuMatrixCoefficients.Bt2020NonConstantLuminance:
                kr = 0.2627F;
                kb = 0.0593F;
                break;
            case ObuMatrixCoefficients.Bt2020ConstantLuminance:
                mode = Av1ColorConversionMode.ConstantLuminance;
                kr = 0.2627F;
                kb = 0.0593F;
                break;
            case ObuMatrixCoefficients.Smpte2085:
                mode = Av1ColorConversionMode.Smpte2085;
                break;
            case ObuMatrixCoefficients.ChromaticityDerivedNonConstantLuminance:
                GetChromaticityDerivedCoefficients(frameBuffer.ColorConfig.ColorPrimaries, out kr, out kb);
                break;
            case ObuMatrixCoefficients.ChromaticityDerivedConstantLuminance:
                mode = Av1ColorConversionMode.ConstantLuminance;
                GetChromaticityDerivedCoefficients(frameBuffer.ColorConfig.ColorPrimaries, out kr, out kb);
                break;
            case ObuMatrixCoefficients.Bt2100ICtCp:
                mode = Av1ColorConversionMode.ICtCp;
                break;
            default:
                throw new NotSupportedException($"AV1 matrix coefficients '{frameBuffer.ColorConfig.MatrixCoefficients}' are not currently supported.");
        }

        kg = 1F - kr - kb;
        bool isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
        bool isFullRange = frameBuffer.ColorConfig.ColorRange;
        if (mode == Av1ColorConversionMode.Identity && !isMonochrome && frameBuffer.ColorFormat != Av1ColorFormat.Yuv444)
        {
            throw new InvalidImageContentException("AV1 identity matrix coefficients require YUV 4:4:4 sampling.");
        }

        if (frameBuffer.ColorConfig.ChromaSamplePosition == ObuChromoSamplePosition.Reserved)
        {
            throw new InvalidImageContentException("The reserved AV1 chroma sample position is invalid.");
        }

        int bitCount = frameBuffer.BitDepth.GetBitCount();
        int depthScale = 1 << (bitCount - 8);
        sampleMaximum = (1 << bitCount) - 1;
        chromaBias = 128F * depthScale;
        lumaBias = isFullRange ? 0F : 16F * depthScale;
        lumaScale = isFullRange ? sampleMaximum : 219F * depthScale;

        // H.273 limited-range YCgCo first maps R, G, and B through the 219-code luma range, so its
        // difference components inherit that scale instead of the 224-code scale used by YCbCr.
        chromaScale = isFullRange || mode == Av1ColorConversionMode.YCgCo
            ? lumaScale
            : 224F * depthScale;
    }

    /// <summary>
    /// Computes the luma coefficients defined by the signaled H.273 primary chromaticities.
    /// </summary>
    /// <param name="colorPrimaries">The signaled color-primary code point.</param>
    /// <param name="kr">The resulting red luma coefficient.</param>
    /// <param name="kb">The resulting blue luma coefficient.</param>
    private static void GetChromaticityDerivedCoefficients(
        ObuColorPrimaries colorPrimaries,
        out float kr,
        out float kb)
    {
        float redX;
        float redY;
        float greenX;
        float greenY;
        float blueX;
        float blueY;
        float whiteX;
        float whiteY;

        switch (colorPrimaries)
        {
            case ObuColorPrimaries.Bt470M:
                redX = 0.67F;
                redY = 0.33F;
                greenX = 0.21F;
                greenY = 0.71F;
                blueX = 0.14F;
                blueY = 0.08F;
                whiteX = 0.310F;
                whiteY = 0.316F;
                break;
            case ObuColorPrimaries.Bt470BG:
                redX = 0.64F;
                redY = 0.33F;
                greenX = 0.29F;
                greenY = 0.60F;
                blueX = 0.15F;
                blueY = 0.06F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case ObuColorPrimaries.Bt601:
            case ObuColorPrimaries.Smpte240:
                redX = 0.630F;
                redY = 0.340F;
                greenX = 0.310F;
                greenY = 0.595F;
                blueX = 0.155F;
                blueY = 0.070F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case ObuColorPrimaries.GenericFilm:
                redX = 0.681F;
                redY = 0.319F;
                greenX = 0.243F;
                greenY = 0.692F;
                blueX = 0.145F;
                blueY = 0.049F;
                whiteX = 0.310F;
                whiteY = 0.316F;
                break;
            case ObuColorPrimaries.Bt2020:
                redX = 0.708F;
                redY = 0.292F;
                greenX = 0.170F;
                greenY = 0.797F;
                blueX = 0.131F;
                blueY = 0.046F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case ObuColorPrimaries.Xyz:
                redX = 1F;
                redY = 0F;
                greenX = 0F;
                greenY = 1F;
                blueX = 0F;
                blueY = 0F;
                whiteX = 1F / 3F;
                whiteY = 1F / 3F;
                break;
            case ObuColorPrimaries.Smpte431:
                redX = 0.680F;
                redY = 0.320F;
                greenX = 0.265F;
                greenY = 0.690F;
                blueX = 0.150F;
                blueY = 0.060F;
                whiteX = 0.314F;
                whiteY = 0.351F;
                break;
            case ObuColorPrimaries.Smpte432:
                redX = 0.680F;
                redY = 0.320F;
                greenX = 0.265F;
                greenY = 0.690F;
                blueX = 0.150F;
                blueY = 0.060F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case ObuColorPrimaries.Ebu3213:
                redX = 0.630F;
                redY = 0.340F;
                greenX = 0.295F;
                greenY = 0.605F;
                blueX = 0.155F;
                blueY = 0.077F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            default:
                // Unspecified and reserved code points have no chromaticities to derive. Match libavif's established
                // still-image fallback so such files retain the same deterministic BT.709 interpretation.
                redX = 0.64F;
                redY = 0.33F;
                greenX = 0.30F;
                greenY = 0.60F;
                blueX = 0.15F;
                blueY = 0.06F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
        }

        float redZ = 1F - (redX + redY);
        float greenZ = 1F - (greenX + greenY);
        float blueZ = 1F - (blueX + blueY);
        float whiteZ = 1F - (whiteX + whiteY);

        // H.273 equations 39 and 40 solve the RGB-to-XYZ primary matrix at the signaled white point. Keeping
        // the expanded determinant matches libavif and avoids introducing a general matrix inversion dependency.
        float denominator = whiteY *
            ((redX * ((greenY * blueZ) - (blueY * greenZ))) +
             (greenX * ((blueY * redZ) - (redY * blueZ))) +
             (blueX * ((redY * greenZ) - (greenY * redZ))));

        kr = (redY *
            ((whiteX * ((greenY * blueZ) - (blueY * greenZ))) +
             (whiteY * ((blueX * greenZ) - (greenX * blueZ))) +
             (whiteZ * ((greenX * blueY) - (blueX * greenY))))) / denominator;

        kb = (blueY *
            ((whiteX * ((redY * greenZ) - (greenY * redZ))) +
             (whiteY * ((greenX * redZ) - (redX * greenZ))) +
             (whiteZ * ((redX * greenY) - (greenX * redY))))) / denominator;
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

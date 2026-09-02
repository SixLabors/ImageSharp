// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Performs operation-scoped AV1 still-image frame encoding.
/// </summary>
internal static class Av1FrameEncoder
{
    /// <summary>
    /// Encodes one reduced-still-picture AV1 frame into a low-overhead OBU stream.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved native color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(configuration, image, stream, colorConfig, qIndex, false);

    /// <summary>
    /// Encodes one packed alpha channel as a reduced-still-picture monochrome AV1 frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlpha<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(configuration, image, stream, colorConfig, qIndex, true);

    private static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = image.Width;
        int height = image.Height;
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        ObuSequenceProfile sequenceProfile = colorConfig.BitDepth == Av1BitDepth.TwelveBit ||
            colorFormat == Av1ColorFormat.Yuv422
                ? ObuSequenceProfile.Professional
                : colorFormat == Av1ColorFormat.Yuv444
                    ? ObuSequenceProfile.High
                    : ObuSequenceProfile.Main;

        ObuSequenceHeader sequenceHeader = new()
        {
            IsStillPicture = true,
            IsReducedStillPictureHeader = true,
            SequenceProfile = sequenceProfile,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = 31 }],
            FrameWidthBits = width > 1 ? Av1Math.MostSignificantBit((uint)(width - 1)) + 1 : 1,
            FrameHeightBits = height > 1 ? Av1Math.MostSignificantBit((uint)(height - 1)) + 1 : 1,
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            Use128x128Superblock = false,
            ForceScreenContentTools = 2,
            ForceIntegerMotionVector = 2,
            EnableFilterIntra = true,
            EnableIntraEdgeFilter = false,
            EnableSuperResolution = false,
            EnableCdef = false,
            EnableRestoration = false,
            ColorConfig = colorConfig
        };

        int modeInfoColumnCount = 2 * ((width + 7) >> 3);
        int modeInfoRowCount = 2 * ((height + 7) >> 3);
        ObuTileGroupHeader tiles = new()
        {
            HasUniformTileSpacing = true,
            TileColumnCount = 1,
            TileRowCount = 1,
            TileSizeBytes = 4
        };

        tiles.TileColumnStartModeInfo[1] = modeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfoRowCount;
        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.KeyFrame,
            ShowFrame = true,
            ErrorResilientMode = true,
            RefreshFrameFlags = byte.MaxValue,
            DisableFrameEndUpdateCdf = true,
            TransformMode = Av1TransformMode.Largest,
            ModeInfoColumnCount = modeInfoColumnCount,
            ModeInfoRowCount = modeInfoRowCount,
            TilesInfo = tiles,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = width,
                FrameHeight = height,
                SuperResolutionDenominator = Av1Constants.ScaleNumerator,
                SuperResolutionUpscaledWidth = width,
                RenderWidth = width,
                RenderHeight = height
            }
        };

        frameHeader.QuantizationParameters.BaseQIndex = qIndex;
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

        // Libaom reserves 2.5 times the 32-sample-aligned native input for an all-intra output packet.
        // Counting the active planes directly retains that headroom without charging monochrome for unused chroma.
        int alignedWidth = Av1Math.AlignPowerOf2(width, 5);
        int alignedHeight = Av1Math.AlignPowerOf2(height, 5);
        int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
        long sampleCount = (long)alignedWidth * alignedHeight;
        if (!colorConfig.IsMonochrome)
        {
            sampleCount += 2L * (alignedWidth >> subsamplingX) * (alignedHeight >> subsamplingY);
        }

        int sampleSize = colorConfig.BitDepth == Av1BitDepth.EightBit ? 1 : 2;
        int initialTileSize = checked((int)Math.Max(8192L, (sampleCount * sampleSize * 5) / 2));
        if (colorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            EncodeByte(configuration, image, stream, sequenceHeader, frameHeader, colorFormat, initialTileSize, encodeAlpha);
        }
        else
        {
            EncodeHighBitDepth(configuration, image, stream, sequenceHeader, frameHeader, colorFormat, initialTileSize, encodeAlpha);
        }

        return sequenceHeader;
    }

    /// <summary>
    /// Converts packed pixels directly into an eight-bit bordered AV1 source frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for row-buffer allocation and pixel conversion.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="source">The operation-owned AV1 source planes.</param>
    /// <param name="colorConfig">The color configuration written to the AV1 sequence header.</param>
    public static void PrepareSource<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Av1EncoderFrame<byte> source,
        ObuColorConfig colorConfig)
        where TPixel : unmanaged, IPixel<TPixel>
        => PrepareSource<TPixel, byte, HeifByteSampleConverter>(configuration, image, source, colorConfig, false);

    /// <summary>
    /// Converts packed pixels directly into a high-bit-depth bordered AV1 source frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for row-buffer allocation and pixel conversion.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="source">The operation-owned AV1 source planes.</param>
    /// <param name="colorConfig">The color configuration written to the AV1 sequence header.</param>
    public static void PrepareSource<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Av1EncoderFrame<ushort> source,
        ObuColorConfig colorConfig)
        where TPixel : unmanaged, IPixel<TPixel>
        => PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(configuration, image, source, colorConfig, false);

    private static void EncodeByte<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat,
        int initialTileSize,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Av1EncoderFrameBuffer<byte> source = new(
            configuration,
            image.Width,
            image.Height,
            8,
            colorFormat,
            chromaPositionX: 1,
            chromaPositionY: 1);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            configuration,
            image.Width,
            image.Height,
            8,
            colorFormat,
            chromaPositionX: 1,
            chromaPositionY: 1);

        Encode(configuration, image, stream, sequenceHeader, frameHeader, source, reconstruction, initialTileSize, encodeAlpha);
    }

    private static void EncodeHighBitDepth<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat,
        int initialTileSize,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bitDepth = sequenceHeader.ColorConfig.BitDepth.GetBitCount();
        using Av1EncoderFrameBuffer<ushort> source = new(
            configuration,
            image.Width,
            image.Height,
            bitDepth,
            colorFormat,
            chromaPositionX: 1,
            chromaPositionY: 1);

        using Av1EncoderFrameBuffer<ushort> reconstruction = new(
            configuration,
            image.Width,
            image.Height,
            bitDepth,
            colorFormat,
            chromaPositionX: 1,
            chromaPositionY: 1);

        Encode(configuration, image, stream, sequenceHeader, frameHeader, source, reconstruction, initialTileSize, encodeAlpha);
    }

    private static void Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1EncoderFrameBuffer<byte> source,
        Av1EncoderFrameBuffer<byte> reconstruction,
        int initialTileSize,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrepareSource<TPixel, byte, HeifByteSampleConverter>(
            configuration,
            image,
            source.Frame,
            sequenceHeader.ColorConfig,
            encodeAlpha);

        Av1ScreenContentDetector.Detect(
            source.Frame,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        frameHeader.AllowScreenContentTools = allowScreenContentTools;
        frameHeader.AllowIntraBlockCopy = allowIntraBlockCopy;
        using Av1EncoderPictureBuffer picture = new(
            configuration,
            sequenceHeader,
            frameHeader,
            image.Width,
            image.Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            configuration,
            sequenceHeader,
            image.Width,
            image.Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration);
        using Av1IntraTileWriter tileWriter = new(
            configuration,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            initialTileSize);

        ObuWriter writer = new();
        writer.WriteAll(configuration, stream, sequenceHeader, frameHeader, tileWriter);
    }

    private static void Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1EncoderFrameBuffer<ushort> source,
        Av1EncoderFrameBuffer<ushort> reconstruction,
        int initialTileSize,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(
            configuration,
            image,
            source.Frame,
            sequenceHeader.ColorConfig,
            encodeAlpha);

        Av1ScreenContentDetector.Detect(
            source.Frame,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        frameHeader.AllowScreenContentTools = allowScreenContentTools;
        frameHeader.AllowIntraBlockCopy = allowIntraBlockCopy;
        using Av1EncoderPictureBuffer picture = new(
            configuration,
            sequenceHeader,
            frameHeader,
            image.Width,
            image.Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            configuration,
            sequenceHeader,
            image.Width,
            image.Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration);
        using Av1IntraTileWriter tileWriter = new(
            configuration,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            initialTileSize);

        ObuWriter writer = new();
        writer.WriteAll(configuration, stream, sequenceHeader, frameHeader, tileWriter);
    }

    /// <summary>
    /// Converts packed pixels into native component planes and initializes every coded and physical edge sample.
    /// </summary>
    private static void PrepareSource<TPixel, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Av1EncoderFrame<TSample> source,
        ObuColorConfig colorConfig,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        if (encodeAlpha)
        {
            HeifPlanarAlphaEncoder.Convert<
                TPixel,
                Av1EncoderFrame<TSample>.PlanarView,
                TSample,
                TStorer>(
                configuration,
                image,
                source.View);
        }
        else
        {
            HeifColorConversionParameters parameters = Av1YuvConverter.GetConversionParameters(
                colorConfig,
                out HeifColorConversionMode mode);

            // Conversion writes into the final bordered analysis planes. Later coding stages consume the native
            // source without a second full-frame copy from an intermediate component buffer.
            HeifPlanarColorConverter.ConvertFromRgb<
                TPixel,
                Av1EncoderFrame<TSample>.PlanarView,
                TSample,
                TStorer>(
                configuration,
                image,
                source.View,
                in parameters,
                mode);
        }

        source.ExtendBorders();
    }
}

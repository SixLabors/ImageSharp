// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Performs operation-scoped AV1 frame encoding.
/// </summary>
internal static class Av1FrameEncoder
{
    /// <summary>
    /// The base-two exponent used to align each frame dimension for output sizing. Rounding to 32 samples accounts
    /// for partial edge storage before the raw-plane size and all-intra expansion factor are calculated.
    /// </summary>
    private const int OutputAlignmentLog2 = 5;

    /// <summary>
    /// The lower bound, in bytes, for the bounded compressed-frame buffer. The raw-size ratio is too small for tiny
    /// images to provide useful coder headroom, so the reference allocation retains an 8 KiB floor.
    /// </summary>
    private const int MinimumCompressedFrameBufferLength = 8 * 1024;

    /// <summary>
    /// The numerator of the all-intra output-capacity ratio. Together with the denominator, this reserves 2.5 times
    /// the aligned uncompressed plane size because incompressible input can produce more output than its raw size.
    /// </summary>
    private const int AllIntraBufferScaleNumerator = 5;

    /// <summary>
    /// The denominator of the all-intra output-capacity ratio, completing the reference encoder's 5:2 sizing rule.
    /// </summary>
    private const int AllIntraBufferScaleDenominator = 2;

    /// <summary>
    /// The sequence-level value that leaves the operating point unconstrained for decoder capability signaling.
    /// </summary>
    private const int UnconstrainedSequenceLevelIndex = 31;

    /// <summary>
    /// The display frame rate the level inference assumes. The encoder has no timing input, so it uses the
    /// reference's default time base of 1/30 second (<c>aom_codec_enc_config_default</c>).
    /// </summary>
    private const int LevelFrameRate = 30;

    /// <summary>
    /// The native component precision used by the byte pipeline.
    /// </summary>
    private const int ByteSampleBitDepth = 8;

    /// <summary>
    /// The centered chroma position expressed in AV1 half-luma-sample units.
    /// </summary>
    private const int CenteredChromaSamplePosition = 1;

    /// <summary>
    /// The largest dimension of the central luma window used during candidate discovery.
    /// </summary>
    private const int MaximumGlobalMotionAnalysisDimension = 512;

    /// <summary>
    /// The number of cardinal and diagonal candidates evaluated at each motion-search step.
    /// </summary>
    private const int GlobalMotionSearchDirectionCount = 8;

    private enum FrameEncodingKind
    {
        StillColor,
        StillAlpha
    }

    /// <summary>
    /// Defines the sample-specific SIMD squared-error operation used by frame-level motion search.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    private interface IGlobalMotionSearchOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Calculates squared error between two equally sized strided sample regions.
        /// </summary>
        /// <param name="source">The first sample of the source region.</param>
        /// <param name="sourceStride">The source distance, in samples, between adjacent rows.</param>
        /// <param name="prediction">The first sample of the prediction region.</param>
        /// <param name="predictionStride">The prediction distance, in samples, between adjacent rows.</param>
        /// <param name="width">The number of samples compared in each row.</param>
        /// <param name="height">The number of rows compared.</param>
        /// <returns>The sum of squared component differences.</returns>
        public static abstract long SumSquaredError(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            int width,
            int height);
    }

    /// <summary>
    /// Encodes one reduced-still-picture AV1 frame into a low-overhead OBU stream.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved native color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="speed">The encoding speed used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
        return Encode(
            configuration,
            image,
            sourceRectangle,
            sourceRectangle.Size,
            stream,
            colorConfig,
            qIndex,
            speed,
            FrameEncodingKind.StillColor);
    }

    /// <summary>
    /// Encodes one grid cell as a reduced-still-picture AV1 frame in a low-overhead OBU stream.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded cell.</param>
    /// <param name="cellSize">The encoded cell dimensions, including any required edge padding.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved native color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="speed">The encoding speed used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeGridCell<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size cellSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(
            configuration,
            image,
            sourceRectangle,
            cellSize,
            stream,
            colorConfig,
            qIndex,
            speed,
            FrameEncodingKind.StillColor);

    /// <summary>
    /// Encodes one packed alpha channel as a reduced-still-picture monochrome AV1 frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="speed">The encoding speed used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlpha<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
        return Encode(
            configuration,
            image,
            sourceRectangle,
            sourceRectangle.Size,
            stream,
            colorConfig,
            qIndex,
            speed,
            FrameEncodingKind.StillAlpha);
    }

    /// <summary>
    /// Encodes one packed alpha grid cell as a reduced-still-picture monochrome AV1 frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded cell.</param>
    /// <param name="cellSize">The encoded cell dimensions, including any required edge padding.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="speed">The encoding speed used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlphaGridCell<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size cellSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(
            configuration,
            image,
            sourceRectangle,
            cellSize,
            stream,
            colorConfig,
            qIndex,
            speed,
            FrameEncodingKind.StillAlpha);

    /// <summary>
    /// Creates an encoder that retains reconstructed color frames for prediction by later samples in the sequence.
    /// </summary>
    public static SequenceEncoder CreateColorSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, speed, false);

    /// <summary>
    /// Creates an encoder that retains reconstructed alpha frames for prediction by later samples in the sequence.
    /// </summary>
    public static SequenceEncoder CreateAlphaSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, speed, true);

    private static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed,
        FrameEncodingKind encodingKind)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frameSize.Width;
        int height = frameSize.Height;
        bool encodeAlpha = encodingKind == FrameEncodingKind.StillAlpha;
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            width,
            height,
            colorConfig,
            speed,
            true);

        ObuFrameHeader frameHeader = CreateFrameHeader(
            sequenceHeader,
            qIndex,
            speed,
            ObuFrameType.KeyFrame);

        int tileBufferLength = GetTileBufferLength(width, height, colorConfig);
        if (colorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            EncodeByte(
                configuration,
                image,
                sourceRectangle,
                frameSize,
                stream,
                sequenceHeader,
                frameHeader,
                colorFormat,
                tileBufferLength,
                speed,
                encodeAlpha);
        }
        else
        {
            EncodeHighBitDepth(
                configuration,
                image,
                sourceRectangle,
                frameSize,
                stream,
                sequenceHeader,
                frameHeader,
                colorFormat,
                tileBufferLength,
                speed,
                encodeAlpha);
        }

        return sequenceHeader;
    }

    private static SequenceEncoder CreateSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed,
        bool encodeAlpha)
    {
        if (colorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            return new ByteSequenceEncoder(configuration, width, height, colorConfig, qIndex, speed, encodeAlpha);
        }

        return new HighBitDepthSequenceEncoder(configuration, width, height, colorConfig, qIndex, speed, encodeAlpha);
    }

    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        ObuColorConfig colorConfig,
        HeifEncodingSpeed speed,
        bool isStillPicture)
    {
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        Av1EncoderSpeedSettings speedSettings = new(speed, isStillPicture, intraFrame: true, qIndex: 0, new Size(width, height));
        ObuSequenceProfile sequenceProfile = colorConfig.BitDepth == Av1BitDepth.TwelveBit ||
            colorFormat == Av1ColorFormat.Yuv422
                ? ObuSequenceProfile.Professional
                : colorFormat == Av1ColorFormat.Yuv444
                    ? ObuSequenceProfile.High
                    : ObuSequenceProfile.Main;

        // Superblock geometry follows coding speed and resolution.
        // Small frames use 64x64 above speed zero; the fastest still-image mode also uses it below 4K.
        int minimumDimension = Math.Min(width, height);
        bool use128x128Superblock = !(speed >= HeifEncodingSpeed.Level1 && minimumDimension <= 480) &&
            !(isStillPicture && speed >= HeifEncodingSpeed.Level9 && minimumDimension < 2160);

        return new ObuSequenceHeader
        {
            IsStillPicture = isStillPicture,
            IsReducedStillPictureHeader = isStillPicture,
            SequenceProfile = sequenceProfile,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = GetSequenceLevelIndex(width, height, LevelFrameRate) }],
            FrameWidthBits = width > 1 ? Av1Math.MostSignificantBit((uint)(width - 1)) + 1 : 1,
            FrameHeightBits = height > 1 ? Av1Math.MostSignificantBit((uint)(height - 1)) + 1 : 1,
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            Use128x128Superblock = use128x128Superblock,
            ForceScreenContentTools = Av1Constants.SelectScreenContentTools,
            ForceIntegerMotionVector = Av1Constants.SelectIntegerMotionVector,
            EnableFilterIntra = true,
            EnableDualFilter = speedSettings.EnableDualFilter,
            EnableIntraEdgeFilter = true,
            EnableMaskedCompound = !isStillPicture,
            OrderHintInfo = new ObuOrderHintInfo
            {
                EnableOrderHint = !isStillPicture,
                EnableJointCompound = !isStillPicture,
                EnableReferenceFrameMotionVectors = false,
                OrderHintBits = isStillPicture ? 0 : 8
            },
            EnableSuperResolution = false,

            // The reference disables CDEF by default in all-intra mode because it blurs images
            // (av1_cx_iface.c L3088-3090); other modes keep it enabled.
            EnableCdef = !isStillPicture,
            EnableRestoration = speedSettings.EnableRestoration,
            ColorConfig = colorConfig
        };
    }

    /// <summary>
    /// Infers the lowest level whose picture size, dimension, and display sample rate limits hold the frame,
    /// as <c>set_bitstream_level_tier</c> does (encoder.c L481-560).
    /// </summary>
    /// <remarks>
    /// Levels 7.x and 8.x are only chosen by the reference when explicitly requested, so larger frames stay
    /// unconstrained.
    /// </remarks>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="frameRate">The display frame rate.</param>
    /// <returns>The sequence level index.</returns>
    private static int GetSequenceLevelIndex(int width, int height, int frameRate)
    {
        // Each row holds the level's maximum width and height, its maximum frame rate at that size, the
        // multiple of the width and height any single dimension may reach, and the level index.
        ReadOnlySpan<int> levels =
        [
            512, 288, 30, 4, 0,
            704, 396, 30, 4, 1,
            1088, 612, 30, 4, 4,
            1376, 774, 30, 4, 5,
            2048, 1152, 30, 3, 8,
            2048, 1152, 60, 3, 9,
            4096, 2176, 30, 2, 12,
            4096, 2176, 60, 2, 13,
            4096, 2176, 120, 2, 14,
            8192, 4352, 30, 2, 16,
            8192, 4352, 60, 2, 17,
            8192, 4352, 120, 2, 18
        ];

        long lumaSamples = (long)width * height;
        for (int i = 0; i < levels.Length; i += 5)
        {
            long levelLumaSamples = (long)levels[i] * levels[i + 1];
            if (lumaSamples <= levelLumaSamples &&
                lumaSamples * frameRate <= levelLumaSamples * levels[i + 2] &&
                width <= levels[i] * levels[i + 3] &&
                height <= levels[i + 1] * levels[i + 3])
            {
                return levels[i + 4];
            }
        }

        return UnconstrainedSequenceLevelIndex;
    }

    private static ObuTileGroupHeader CreateTileGroupHeader(
        int modeInfoColumnCount,
        int modeInfoRowCount,
        int superblockSizeLog2)
    {
        int superblockShift = superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        int superblockColumns = Av1Math.DivideLog2Ceiling(modeInfoColumnCount, superblockShift);
        int superblockRows = Av1Math.DivideLog2Ceiling(modeInfoRowCount, superblockShift);
        int maximumTileWidth = Av1Constants.MaxTileWidth >> superblockSizeLog2;
        int maximumTileArea = Av1Constants.MaxTileArea >> (2 * superblockSizeLog2);
        int tileColumnCountLog2 = ObuReader.TileLog2(maximumTileWidth, superblockColumns);
        int minimumTileCountLog2 = Math.Max(
            tileColumnCountLog2,
            ObuReader.TileLog2(maximumTileArea, superblockColumns * superblockRows));

        int tileRowCountLog2 = minimumTileCountLog2 - tileColumnCountLog2;
        int tileWidthSuperblocks = Av1Math.DivideLog2Ceiling(superblockColumns, tileColumnCountLog2);
        int tileHeightSuperblocks = Av1Math.DivideLog2Ceiling(superblockRows, tileRowCountLog2);
        ObuTileGroupHeader tiles = new()
        {
            HasUniformTileSpacing = true,
            TileColumnCountLog2 = tileColumnCountLog2,
            TileRowCountLog2 = tileRowCountLog2,
            TileSizeBytes = sizeof(uint)
        };

        // Uniform tile boundaries are derived in superblock units. The terminal entries retain the exact
        // visible mode-info dimensions so clipped right and bottom superblocks end at the frame boundary.
        int tileColumn = 0;
        for (int startSuperblock = 0; startSuperblock < superblockColumns; startSuperblock += tileWidthSuperblocks)
        {
            tiles.TileColumnStartModeInfo[tileColumn++] = startSuperblock << superblockShift;
        }

        tiles.TileColumnStartModeInfo[tileColumn] = modeInfoColumnCount;
        tiles.TileColumnCount = tileColumn;

        int tileRow = 0;
        for (int startSuperblock = 0; startSuperblock < superblockRows; startSuperblock += tileHeightSuperblocks)
        {
            tiles.TileRowStartModeInfo[tileRow++] = startSuperblock << superblockShift;
        }

        tiles.TileRowStartModeInfo[tileRow] = modeInfoRowCount;
        tiles.TileRowCount = tileRow;
        return tiles;
    }

    private static ObuFrameHeader CreateFrameHeader(
        ObuSequenceHeader sequenceHeader,
        int qIndex,
        HeifEncodingSpeed speed,
        ObuFrameType frameType)
    {
        int width = sequenceHeader.MaxFrameWidth;
        int height = sequenceHeader.MaxFrameHeight;
        int modeInfoColumnCount = 2 * ((width + 7) >> 3);
        int modeInfoRowCount = 2 * ((height + 7) >> 3);
        ObuTileGroupHeader tiles = CreateTileGroupHeader(
            modeInfoColumnCount,
            modeInfoRowCount,
            sequenceHeader.SuperblockSizeLog2);

        ObuFrameHeader frameHeader = new()
        {
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

        ConfigureFrameHeader(frameHeader, qIndex, speed, frameType);
        return frameHeader;
    }

    /// <summary>
    /// Restores every frame-varying encoder field while retaining the fixed geometry and syntax object graph.
    /// </summary>
    private static void ConfigureFrameHeader(
        ObuFrameHeader frameHeader,
        int qIndex,
        HeifEncodingSpeed speed,
        ObuFrameType frameType)
    {
        frameHeader.FrameType = frameType;
        frameHeader.ShowFrame = true;
        frameHeader.ErrorResilientMode = true;
        frameHeader.RefreshFrameFlags = frameType == ObuFrameType.KeyFrame ? byte.MaxValue : 1U;
        frameHeader.DisableFrameEndUpdateCdf = true;
        frameHeader.ReferenceMode = frameType == ObuFrameType.InterFrame
            ? ObuReferenceMode.ReferenceModeSelect
            : ObuReferenceMode.SingleReference;
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Regular;
        frameHeader.IsMotionModeSwitchable = false;
        frameHeader.TransformMode = qIndex == 0
            ? Av1TransformMode.Only4x4
            : Av1TransformMode.Select;

        frameHeader.AllowScreenContentTools = false;
        frameHeader.AllowIntraBlockCopy = false;
        frameHeader.ForceIntegerMotionVector = false;
        frameHeader.AllowHighPrecisionMotionVector = false;
        if (frameType == ObuFrameType.InterFrame)
        {
            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            referenceFrameIndices.Clear();
            referenceFrameIndices[(int)Av1ReferenceFrameType.Golden - 1] = 7;

            // Disabling screen-content tools makes force_integer_mv implicitly false, so inter vectors
            // use fractional-motion syntax at the precision selected for the frame quantizer.
            Av1EncoderSpeedSettings speedSettings = new(
                speed,
                allIntra: false,
                intraFrame: false,
                qIndex,
                new Size(frameHeader.FrameSize.SuperResolutionUpscaledWidth, frameHeader.FrameSize.FrameHeight));

            frameHeader.AllowHighPrecisionMotionVector = speedSettings.AllowHighPrecisionMotionVector;
            frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
        }

        frameHeader.QuantizationParameters.BaseQIndex = qIndex;
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);
    }

    private static int GetTileBufferLength(int width, int height, ObuColorConfig colorConfig)
    {
        // Reserve 2.5 times the 32-sample-aligned component storage for an all-intra output packet.
        // Counting the active planes directly retains that headroom without charging monochrome for unused chroma.
        // This is an initial estimate: the range writer grows if encoded syntax exceeds its remaining capacity.
        int alignedWidth = Av1Math.AlignPowerOf2(width, OutputAlignmentLog2);
        int alignedHeight = Av1Math.AlignPowerOf2(height, OutputAlignmentLog2);
        int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
        long sampleCount = (long)alignedWidth * alignedHeight;
        if (!colorConfig.IsMonochrome)
        {
            sampleCount += 2L * (alignedWidth >> subsamplingX) * (alignedHeight >> subsamplingY);
        }

        int sampleSize = colorConfig.BitDepth == Av1BitDepth.EightBit ? 1 : 2;
        long scaledInputLength = (sampleCount * sampleSize * AllIntraBufferScaleNumerator)
            / AllIntraBufferScaleDenominator;

        return checked((int)Math.Max(MinimumCompressedFrameBufferLength, scaledInputLength));
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
    {
        Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
        PrepareSource<TPixel, byte, HeifByteSampleConverter>(configuration, image, sourceRectangle, source, colorConfig, false);
    }

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
    {
        Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
        PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(configuration, image, sourceRectangle, source, colorConfig, false);
    }

    private static void EncodeByte<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat,
        int tileBufferLength,
        HeifEncodingSpeed speed,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Av1EncoderFrameBuffer<byte> source = new(
            configuration,
            frameSize.Width,
            frameSize.Height,
            ByteSampleBitDepth,
            colorFormat,
            chromaPositionX: CenteredChromaSamplePosition,
            chromaPositionY: CenteredChromaSamplePosition,
            lumaBorder: Av1EncoderFrame<byte>.LumaBorder);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            configuration,
            frameSize.Width,
            frameSize.Height,
            ByteSampleBitDepth,
            colorFormat,
            chromaPositionX: CenteredChromaSamplePosition,
            chromaPositionY: CenteredChromaSamplePosition,
            lumaBorder: Av1EncoderFrame<byte>.LumaBorder);

        using Av1EncoderCoefficientBuffer coefficients = new(
            configuration,
            sequenceHeader,
            frameSize.Width,
            frameSize.Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);

        Av1EncoderTileWorkspace tileWorkspace = new(frameHeader, superblockWorkspace);
        using Av1SymbolEncoder symbolEncoder = new(
            configuration,
            tileBufferLength,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        using ObuWriter obuWriter = new(configuration);

        bool isScreenContent = PrepareFrame(
            configuration,
            image,
            sourceRectangle,
            source.Frame,
            reconstruction.Frame,
            sequenceHeader,
            frameHeader,
            speed,
            encodeAlpha);

        using Av1EncoderBlockWorkspace blockWorkspace = new(
            configuration,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: frameHeader.AllowIntraBlockCopy,
            sequenceHeader.SuperblockSize);

        Av1EncoderSpeedSettings speedSettings = new(
            speed, sequenceHeader.IsStillPicture, frameHeader.IsIntra, frameHeader.QuantizationParameters.BaseQIndex, frameSize);

        Av1MotionSearchSettings motionSettings = new(
            speed,
            sequenceHeader.IsStillPicture,
            frameSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            isScreenContent);
        int maximumHashBlockSize = motionSettings.LimitIntraBlockCopyHashBlockSize ? 8 : 1 << sequenceHeader.SuperblockSizeLog2;

        using Av1EncoderPictureBuffer picture = new(
            configuration,
            sequenceHeader,
            frameHeader,
            source.Frame.Width,
            source.Frame.Height,
            maximumHashBlockSize,
            disallow4x4AllFrames: !frameHeader.CodedLossless && speedSettings.MinimumPartitionSize >= Av1BlockSize.Block8x8);

        picture.Picture.Parent.IsScreenContent = isScreenContent;
        picture.Picture.Parent.EncodingSpeed = speed;
        picture.Picture.Parent.SpeedSettings = speedSettings;
        Encode(
            obuWriter,
            stream,
            sequenceHeader,
            frameHeader,
            picture.Picture,
            source,
            default,
            reconstruction,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            symbolEncoder,
            true);
    }

    private static void EncodeHighBitDepth<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat,
        int tileBufferLength,
        HeifEncodingSpeed speed,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bitDepth = sequenceHeader.ColorConfig.BitDepth.GetBitCount();
        using Av1EncoderFrameBuffer<ushort> source = new(
            configuration,
            frameSize.Width,
            frameSize.Height,
            bitDepth,
            colorFormat,
            chromaPositionX: CenteredChromaSamplePosition,
            chromaPositionY: CenteredChromaSamplePosition,
            lumaBorder: Av1EncoderFrame<ushort>.LumaBorder);

        using Av1EncoderFrameBuffer<ushort> reconstruction = new(
            configuration,
            frameSize.Width,
            frameSize.Height,
            bitDepth,
            colorFormat,
            chromaPositionX: CenteredChromaSamplePosition,
            chromaPositionY: CenteredChromaSamplePosition,
            lumaBorder: Av1EncoderFrame<ushort>.LumaBorder);

        using Av1EncoderCoefficientBuffer coefficients = new(
            configuration,
            sequenceHeader,
            frameSize.Width,
            frameSize.Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(configuration);

        Av1EncoderTileWorkspace tileWorkspace = new(frameHeader, superblockWorkspace);
        using Av1SymbolEncoder symbolEncoder = new(
            configuration,
            tileBufferLength,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        using ObuWriter obuWriter = new(configuration);

        bool isScreenContent = PrepareFrame(
            configuration,
            image,
            sourceRectangle,
            source.Frame,
            reconstruction.Frame,
            sequenceHeader,
            frameHeader,
            speed,
            encodeAlpha);

        using Av1EncoderBlockWorkspace blockWorkspace = new(
            configuration,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: frameHeader.AllowIntraBlockCopy,
            sequenceHeader.SuperblockSize);

        Av1EncoderSpeedSettings speedSettings = new(
            speed, sequenceHeader.IsStillPicture, frameHeader.IsIntra, frameHeader.QuantizationParameters.BaseQIndex, frameSize);

        Av1MotionSearchSettings motionSettings = new(
            speed,
            sequenceHeader.IsStillPicture,
            frameSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            isScreenContent);
        int maximumHashBlockSize = motionSettings.LimitIntraBlockCopyHashBlockSize ? 8 : 1 << sequenceHeader.SuperblockSizeLog2;

        using Av1EncoderPictureBuffer picture = new(
            configuration,
            sequenceHeader,
            frameHeader,
            source.Frame.Width,
            source.Frame.Height,
            maximumHashBlockSize,
            disallow4x4AllFrames: !frameHeader.CodedLossless && speedSettings.MinimumPartitionSize >= Av1BlockSize.Block8x8);

        picture.Picture.Parent.IsScreenContent = isScreenContent;
        picture.Picture.Parent.EncodingSpeed = speed;
        picture.Picture.Parent.SpeedSettings = speedSettings;
        Encode(
            obuWriter,
            stream,
            sequenceHeader,
            frameHeader,
            picture.Picture,
            source,
            default,
            reconstruction,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            symbolEncoder,
            true);
    }

    /// <summary>
    /// Converts one source frame and resolves every content-dependent coding tool before picture-state allocation.
    /// </summary>
    private static bool PrepareFrame<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrepareSource<TPixel, byte, HeifByteSampleConverter>(
            configuration,
            image,
            sourceRectangle,
            source,
            sequenceHeader.ColorConfig,
            encodeAlpha);

        return ConfigureFrameTools(
            source,
            reference,
            sequenceHeader,
            frameHeader,
            speed);
    }

    /// <summary>
    /// Converts one sequence sample through its retained row workspace before resolving frame coding tools.
    /// </summary>
    private static bool PrepareFrame<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed,
        Av1EncoderConversionWorkspace conversionWorkspace)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrepareSource<TPixel, byte, HeifByteSampleConverter>(
            configuration,
            image,
            sourceRectangle,
            source,
            conversionWorkspace);

        return ConfigureFrameTools(
            source,
            reference,
            sequenceHeader,
            frameHeader,
            speed);
    }

    /// <summary>
    /// Resolves the eight-bit frame tools whose syntax depends on the converted source samples.
    /// </summary>
    private static bool ConfigureFrameTools(
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed)
    {
        ConfigureGlobalMotion<byte, ByteGlobalMotionSearchOperator>(
            source,
            reference,
            frameHeader,
            sequenceHeader.ColorConfig.BitDepth);

        bool isScreenContent = Av1ScreenContentDetector.SetScreenContentOptions(
            source,
            sequenceHeader.IsStillPicture,
            speed,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        Av1MotionSearchSettings motionSettings = new(
            speed,
            sequenceHeader.IsStillPicture,
            new Size(source.Width, source.Height),
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            isScreenContent);

        frameHeader.AllowScreenContentTools = allowScreenContentTools;

        frameHeader.AllowIntraBlockCopy =
            frameHeader.IsIntra &&
            motionSettings.AllowIntraBlockCopy &&
            frameHeader.AllowScreenContentTools &&
            allowIntraBlockCopy;

        return isScreenContent;
    }

    /// <summary>
    /// Converts one high-bit-depth source frame and resolves every content-dependent coding tool before picture-state allocation.
    /// </summary>
    private static bool PrepareFrame<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(
            configuration,
            image,
            sourceRectangle,
            source,
            sequenceHeader.ColorConfig,
            encodeAlpha);

        return ConfigureFrameTools(
            source,
            reference,
            sequenceHeader,
            frameHeader,
            speed);
    }

    /// <summary>
    /// Converts one high-bit-depth sequence sample through retained row storage before resolving frame coding tools.
    /// </summary>
    private static bool PrepareFrame<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed,
        Av1EncoderConversionWorkspace conversionWorkspace)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(
            configuration,
            image,
            sourceRectangle,
            source,
            conversionWorkspace);

        return ConfigureFrameTools(
            source,
            reference,
            sequenceHeader,
            frameHeader,
            speed);
    }

    /// <summary>
    /// Resolves the high-bit-depth frame tools whose syntax depends on the converted source samples.
    /// </summary>
    private static bool ConfigureFrameTools(
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed)
    {
        ConfigureGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(
            source,
            reference,
            frameHeader,
            sequenceHeader.ColorConfig.BitDepth);

        bool isScreenContent = Av1ScreenContentDetector.SetScreenContentOptions(
            source,
            sequenceHeader.IsStillPicture,
            speed,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        Av1MotionSearchSettings motionSettings = new(
            speed,
            sequenceHeader.IsStillPicture,
            new Size(source.Width, source.Height),
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            isScreenContent);

        frameHeader.AllowScreenContentTools = allowScreenContentTools;

        frameHeader.AllowIntraBlockCopy =
            frameHeader.IsIntra &&
            motionSettings.AllowIntraBlockCopy &&
            frameHeader.AllowScreenContentTools &&
            allowIntraBlockCopy;

        return isScreenContent;
    }

    private static void Encode(
        ObuWriter obuWriter,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1PictureControlSet picture,
        Av1EncoderFrameBuffer<byte> source,
        ReadOnlyMemory<Av1EncoderFrame<byte>> references,
        Av1EncoderFrameBuffer<byte> reconstruction,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        Av1SymbolEncoder symbolEncoder,
        bool writeSequenceHeader)
    {
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            references,
            reconstruction.Frame,
            picture,
            coefficients,
            tileWorkspace,
            blockWorkspace);

        if (writeSequenceHeader)
        {
            obuWriter.WriteSequenceFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
        else
        {
            obuWriter.WriteFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
    }

    private static void Encode(
        ObuWriter obuWriter,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1PictureControlSet picture,
        Av1EncoderFrameBuffer<ushort> source,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> references,
        Av1EncoderFrameBuffer<ushort> reconstruction,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        Av1SymbolEncoder symbolEncoder,
        bool writeSequenceHeader)
    {
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            references,
            reconstruction.Frame,
            picture,
            coefficients,
            tileWorkspace,
            blockWorkspace);

        if (writeSequenceHeader)
        {
            obuWriter.WriteSequenceFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
        else
        {
            obuWriter.WriteFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
    }

    /// <summary>
    /// Converts packed pixels into native component planes and initializes every coded and physical edge sample.
    /// </summary>
    private static void PrepareSource<TPixel, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Av1EncoderFrame<TSample> source,
        ObuColorConfig colorConfig,
        bool encodeAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        Av1EncoderFrame<TSample>.PlanarView destination = source.CodedView.GetSubView(
            sourceRectangle.Width,
            sourceRectangle.Height);

        if (encodeAlpha)
        {
            HeifPlanarAlphaEncoder.Convert<
                TPixel,
                Av1EncoderFrame<TSample>.PlanarView,
                TSample,
                TStorer>(
                configuration,
                image,
                sourceRectangle,
                destination);
        }
        else
        {
            HeifColorConversionParameters parameters = Av1YuvConverter.GetConversionParameters(
                colorConfig,
                colorConfig.ColorRange,
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
                sourceRectangle,
                destination,
                in parameters,
                mode);
        }

        // A short grid edge may occupy only the top-left of its AV1 frame. Replicating that edge initializes
        // both the remaining coded cell and the physical prediction border without another image or plane copy.
        source.CodedView.ExtendBorders(sourceRectangle.Width, sourceRectangle.Height);
    }

    /// <summary>
    /// Converts one sequence sample with track-owned row storage and initializes every coded and physical edge.
    /// </summary>
    private static void PrepareSource<TPixel, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Av1EncoderFrame<TSample> source,
        Av1EncoderConversionWorkspace conversionWorkspace)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        Av1EncoderFrame<TSample>.PlanarView destination = source.CodedView.GetSubView(
            sourceRectangle.Width,
            sourceRectangle.Height);

        conversionWorkspace.Convert<
            TPixel,
            Av1EncoderFrame<TSample>.PlanarView,
            TSample,
            TStorer>(
            configuration,
            image,
            sourceRectangle,
            destination);

        // Sequence geometry is fixed, but grid-edge cells can still expose less source data than their coded
        // extent. The same edge replication completes both coded padding and the physical prediction border.
        source.CodedView.ExtendBorders(sourceRectangle.Width, sourceRectangle.Height);
    }

    /// <summary>
    /// Selects a bounded whole-frame translation model for an inter frame.
    /// </summary>
    private static void ConfigureGlobalMotion<TSample, TOperator>(
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reference,
        ObuFrameHeader frameHeader,
        Av1BitDepth bitDepth)
        where TSample : unmanaged
        where TOperator : struct, IGlobalMotionSearchOperator<TSample>
    {
        Span<Av1GlobalMotionParameters> models = frameHeader.GetGlobalMotionParameters();
        models.Fill(Av1GlobalMotionParameters.Identity);
        if (frameHeader.IsIntra)
        {
            return;
        }

        Buffer2DRegion<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<TSample> referenceLuma = reference.CodedView.GetPlane(Av1Plane.Y);

        // Integer translations must fit the model's signed fixed-point range before their coding cost
        // is evaluated. Padding bounds the readable pixels independently of that syntax limit.
        const int MaximumTranslation = 1 <<
            (Av1GlobalMotionParameters.AbsoluteTranslationBits - Av1GlobalMotionParameters.TranslationPrecisionBits);

        // Every compared sample has to be a coded sample. An offset that reaches past the frame compares
        // against replicated padding, whose error can fall below the error of the real content and select
        // a model that describes no motion at all. The analysis window keeps a margin on each side, and
        // that margin bounds the search.
        int searchRadius = Math.Min(
            MaximumTranslation,
            Math.Min(
                Math.Min(source.CodedWidth, source.CodedHeight) >> 2,
                Math.Min(referenceLuma.Bounds.X, referenceLuma.Bounds.Y)));

        int analysisWidth = Math.Min(source.CodedWidth - (2 * searchRadius), MaximumGlobalMotionAnalysisDimension);
        int analysisHeight = Math.Min(source.CodedHeight - (2 * searchRadius), MaximumGlobalMotionAnalysisDimension);
        Point analysisOrigin = new(
            (source.CodedWidth - analysisWidth) >> 1,
            (source.CodedHeight - analysisHeight) >> 1);

        Point bestOffset = default;
        long bestAnalysisError = GetGlobalMotionSquaredError<TSample, TOperator>(
            sourceLuma,
            referenceLuma,
            analysisOrigin,
            analysisWidth,
            analysisHeight,
            bestOffset);

        // The reference refines one model parameter at a time and keeps stepping in the winning
        // direction until the error rises, rather than taking one step of a fixed direction set.
        // Reference: av1_refine_integerized_param() in global_motion.c, L364.
        for (int step = searchRadius; step > 0; step >>= 1)
        {
            for (int parameter = 0; parameter < 2; parameter++)
            {
                Point stageOffset = bestOffset;
                int stepDirection = 0;
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    Point candidateOffset = OffsetGlobalMotionParameter(stageOffset, parameter, step * direction);
                    if (!IsGlobalMotionOffsetInRange(candidateOffset, searchRadius))
                    {
                        continue;
                    }

                    long stepError = GetGlobalMotionSquaredError<TSample, TOperator>(
                        sourceLuma, referenceLuma, analysisOrigin, analysisWidth, analysisHeight, candidateOffset);

                    // Strict replacement preserves identity and the earlier direction on ties.
                    if (stepError < bestAnalysisError)
                    {
                        bestAnalysisError = stepError;
                        bestOffset = candidateOffset;
                        stepDirection = direction;
                    }
                }

                while (stepDirection != 0)
                {
                    Point candidateOffset = OffsetGlobalMotionParameter(bestOffset, parameter, step * stepDirection);
                    if (!IsGlobalMotionOffsetInRange(candidateOffset, searchRadius))
                    {
                        break;
                    }

                    long stepError = GetGlobalMotionSquaredError<TSample, TOperator>(
                        sourceLuma, referenceLuma, analysisOrigin, analysisWidth, analysisHeight, candidateOffset);

                    if (stepError >= bestAnalysisError)
                    {
                        break;
                    }

                    bestAnalysisError = stepError;
                    bestOffset = candidateOffset;
                }
            }
        }

        if (bestOffset == default)
        {
            return;
        }

        Point frameOrigin = default;
        long identityError = GetGlobalMotionSquaredError<TSample, TOperator>(
            sourceLuma,
            referenceLuma,
            frameOrigin,
            source.CodedWidth,
            source.CodedHeight,
            frameOrigin);

        long candidateError = GetGlobalMotionSquaredError<TSample, TOperator>(
            sourceLuma,
            referenceLuma,
            frameOrigin,
            source.CodedWidth,
            source.CodedHeight,
            bestOffset);

        Av1GlobalMotionParameters candidate = Av1GlobalMotionParameters.Identity;

        // Pure translation is stored as identity-scale rotation/zoom because the translation-only AV1 model
        // has a published row/column assignment defect. The resulting block vector remains exact.
        candidate.Type = Av1GlobalMotionType.RotationZoom;
        candidate[0] = bestOffset.X * Av1GlobalMotionParameters.ModelScale;
        candidate[1] = bestOffset.Y * Av1GlobalMotionParameters.ModelScale;
        candidate.UpdateShearParameters();

        int rateMultiplier = Av1RateDistortion.GetRateMultiplier(
            frameHeader.QuantizationParameters.BaseQIndex + frameHeader.QuantizationParameters.DeltaQDc[0],
            bitDepth,
            Av1FrameUpdateType.Last);

        int identityRate =
            ObuWriter.GetGlobalMotionModelBitCount(
                Av1GlobalMotionParameters.Identity,
                frameHeader.AllowHighPrecisionMotionVector) <<
            Av1ProbabilityCost.CostShift;

        int candidateRate =
            ObuWriter.GetGlobalMotionModelBitCount(
                candidate,
                frameHeader.AllowHighPrecisionMotionVector) <<
            Av1ProbabilityCost.CostShift;

        long identityCost = Av1RateDistortion.GetCost(
            rateMultiplier,
            identityRate,
            NormalizeGlobalMotionSquaredError(identityError, bitDepth));

        long candidateCost = Av1RateDistortion.GetCost(
            rateMultiplier,
            candidateRate,
            NormalizeGlobalMotionSquaredError(candidateError, bitDepth));

        if (candidateCost < identityCost)
        {
            models[0] = candidate;
        }
    }

    /// <summary>
    /// Offsets one translation parameter of a global-motion candidate.
    /// </summary>
    /// <param name="offset">The current integer translation.</param>
    /// <param name="parameter">Zero for the horizontal parameter, one for the vertical parameter.</param>
    /// <param name="delta">The signed sample step.</param>
    /// <returns>The offset candidate.</returns>
    private static Point OffsetGlobalMotionParameter(Point offset, int parameter, int delta)
        => parameter == 0 ? new Point(offset.X + delta, offset.Y) : new Point(offset.X, offset.Y + delta);

    /// <summary>
    /// Gets whether an integer translation stays inside the searched range.
    /// </summary>
    /// <param name="offset">The integer translation.</param>
    /// <param name="searchRadius">The inclusive range on each axis.</param>
    /// <returns>Whether both components are inside the range.</returns>
    private static bool IsGlobalMotionOffsetInRange(Point offset, int searchRadius)
        => Math.Abs(offset.X) <= searchRadius && Math.Abs(offset.Y) <= searchRadius;

    /// <summary>
    /// Calculates squared error for one translated luma candidate using the physical reference border.
    /// </summary>
    private static long GetGlobalMotionSquaredError<TSample, TOperator>(
        Buffer2DRegion<TSample> source,
        Buffer2DRegion<TSample> reference,
        Point sourceOrigin,
        int width,
        int height,
        Point referenceOffset)
        where TSample : unmanaged
        where TOperator : struct, IGlobalMotionSearchOperator<TSample>
    {
        Rectangle sourceBounds = source.Bounds;
        Rectangle referenceBounds = reference.Bounds;
        int sourceIndex =
            ((sourceBounds.Y + sourceOrigin.Y) * source.Stride) +
            sourceBounds.X +
            sourceOrigin.X;

        int referenceIndex =
            ((referenceBounds.Y + sourceOrigin.Y + referenceOffset.Y) * reference.Stride) +
            referenceBounds.X +
            sourceOrigin.X +
            referenceOffset.X;

        return TOperator.SumSquaredError(
            source.Buffer.DangerousGetSingleSpan()[sourceIndex..],
            source.Stride,
            reference.Buffer.DangerousGetSingleSpan()[referenceIndex..],
            reference.Stride,
            width,
            height);
    }

    /// <summary>
    /// Gets one cardinal or diagonal direction in the reference encoder's search order.
    /// </summary>
    private static Point GetGlobalMotionSearchDirection(int index)
        => index switch
        {
            0 => new Point(0, -1),
            1 => new Point(0, 1),
            2 => new Point(-1, 0),
            3 => new Point(1, 0),
            4 => new Point(-1, -1),
            5 => new Point(1, 1),
            6 => new Point(1, -1),
            _ => new Point(-1, 1)
        };

    /// <summary>
    /// Normalizes high-bit-depth frame error to the eight-bit distortion domain.
    /// </summary>
    private static long NormalizeGlobalMotionSquaredError(long error, Av1BitDepth bitDepth)
    {
        int shift = (bitDepth.GetBitCount() - ByteSampleBitDepth) * 2;
        return shift == 0 ? error : (error + (1L << (shift - 1))) >> shift;
    }

    /// <summary>
    /// Measures source changes and retains the block errors used by subsequent mode decisions.
    /// </summary>
    /// <typeparam name="TSample">The source sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific error operations.</typeparam>
    /// <param name="source">The current bordered source planes.</param>
    /// <param name="previousSource">The preceding bordered source planes.</param>
    /// <param name="parent">The frame analysis state and borrowed block-error storage.</param>
    /// <param name="isScreenContent">Whether screen-content tuning is active.</param>
    /// <param name="framesSinceKey">The number of completed frames since the last key frame.</param>
    /// <param name="averageSourceSad">The running average of source changes.</param>
    private static void AnalyzeTemporalSource<TSample, TOperator>(
        Av1EncoderFrame<TSample>.PlanarView source,
        Av1EncoderFrame<TSample>.PlanarView previousSource,
        Av1PictureParentControlSet parent,
        bool isScreenContent,
        int framesSinceKey,
        ref ulong averageSourceSad)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        Buffer2DRegion<TSample> current = source.GetPlane(Av1Plane.Y);
        Buffer2DRegion<TSample> previous = previousSource.GetPlane(Av1Plane.Y);
        Span<ulong> blockErrors = parent.SourceBlockSad.Span;
        int columns = (source.Width + 63) >> 6;
        int rows = (source.Height + 63) >> 6;
        int unchanged = 0;
        ulong total = 0;

        // Keep each block SAD for the following superblock pass. Both planes have replicated
        // borders, so partial visible blocks use the same complete 64x64 measurement.
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Point origin = new(column << 6, row << 6);
                ulong sad = (ulong)TOperator.SumAbsoluteDifferences(
                    Av1TransformBlockEncoder.GetPlaneSpan(current, origin),
                    current.Stride,
                    Av1TransformBlockEncoder.GetPlaneSpan(previous, origin),
                    previous.Stride,
                    64,
                    64,
                    1) >> (source.LumaBitDepth - 8);

                blockErrors[(row * columns) + column] = sad;
                total += sad;
                unchanged += sad == 0 ? 1 : 0;
            }
        }

        int count = rows * columns;
        ulong average = total / (ulong)count;
        uint minimum = isScreenContent ? 8000U : 10000U;
        int multiplier = isScreenContent ? 5 : 6;
        int unchangedLimit = average > 8 * minimum ? 3 * (count >> 2) : count >> 1;
        parent.HighSourceSad = average > Math.Max(minimum, (uint)(averageSourceSad * (ulong)multiplier)) &&
            framesSinceKey > 2 && unchanged < unchangedLimit;

        parent.FrameSourceSad = average;
        parent.SourceMotionPercentage = ((count - unchanged) * 100) / count;
        averageSourceSad = ((3 * averageSourceSad) + average) >> 2;
        parent.AverageSourceSad = averageSourceSad;
    }

    /// <summary>
    /// Routes byte samples through the SIMD-first shared residual operation.
    /// </summary>
    private readonly struct ByteGlobalMotionSearchOperator : IGlobalMotionSearchOperator<byte>
    {
        /// <inheritdoc/>
        public static long SumSquaredError(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height)
            => Av1ResidualBuilder.SumSquaredError(
                source,
                sourceStride,
                prediction,
                predictionStride,
                width,
                height);
    }

    /// <summary>
    /// Routes high-bit-depth samples through the SIMD-first shared residual operation.
    /// </summary>
    private readonly struct UInt16GlobalMotionSearchOperator : IGlobalMotionSearchOperator<ushort>
    {
        /// <inheritdoc/>
        public static long SumSquaredError(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            int width,
            int height)
            => Av1ResidualBuilder.SumSquaredError(
                source,
                sourceStride,
                prediction,
                predictionStride,
                width,
                height);
    }

    /// <summary>
    /// Owns the fixed-size packed and planar row storage reused by every sample in one sequence track.
    /// </summary>
    internal sealed class Av1EncoderConversionWorkspace : IDisposable
    {
        private readonly IMemoryOwner<float> storageOwner;
        private readonly Memory<float> componentMemory;
        private readonly Memory<float> packedMemory;
        private readonly HeifColorConversionParameters parameters;
        private readonly HeifColorConverterBase colorConverter;
        private readonly bool encodeAlpha;
        private readonly int packedPixelCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Av1EncoderConversionWorkspace"/> class.
        /// </summary>
        /// <param name="configuration">The configuration providing reusable row storage.</param>
        /// <param name="width">The fixed sequence width.</param>
        /// <param name="colorConfig">The native component layout.</param>
        /// <param name="encodeAlpha">Whether the workspace converts the auxiliary alpha track.</param>
        /// <param name="usesHighBitDepth">Whether color conversion requires a packed <see cref="Rgb48"/> row.</param>
        public Av1EncoderConversionWorkspace(
            Configuration configuration,
            int width,
            ObuColorConfig colorConfig,
            bool encodeAlpha,
            bool usesHighBitDepth)
        {
            // Resolve conversion before renting storage: a rejected color description must not strand an owner
            // in a constructor that never returns to the sequence encoder's disposal boundary.
            this.parameters = Av1YuvConverter.GetConversionParameters(colorConfig, colorConfig.ColorRange, out HeifColorConversionMode mode);
            this.colorConverter = HeifColorConverterBase.Create(mode, in this.parameters, colorConfig.IsMonochrome);
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            int componentLength = encodeAlpha
                ? HeifPlanarAlphaEncoder.GetRowStorageLength(width)
                : HeifPlanarColorConverter.GetRgbToYuvComponentBufferLength(
                    width,
                    colorConfig.IsMonochrome,
                    subsamplingY);

            int packedByteLength = usesHighBitDepth && !encodeAlpha
                ? width * Unsafe.SizeOf<Rgb48>()
                : 0;

            int packedFloatLength = (int)Numerics.DivideCeil((uint)packedByteLength, sizeof(float));
            this.storageOwner = configuration.MemoryAllocator.Allocate<float>(
                componentLength + packedFloatLength);

            Memory<float> storage = this.storageOwner.Memory;
            this.componentMemory = storage[..componentLength];
            this.packedMemory = storage.Slice(componentLength, packedFloatLength);
            this.encodeAlpha = encodeAlpha;
            this.packedPixelCount = usesHighBitDepth && !encodeAlpha ? width : 0;
        }

        /// <summary>
        /// Converts one packed frame directly into its final native component planes.
        /// </summary>
        /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
        /// <typeparam name="TBuffer">The native destination-plane adapter.</typeparam>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TStorer">The SIMD narrowing and storage operation.</typeparam>
        /// <param name="configuration">The configuration used for packed-pixel conversion.</param>
        /// <param name="image">The source image frame.</param>
        /// <param name="sourceRectangle">The source region mapped to the destination planes.</param>
        /// <param name="buffer">The destination component planes.</param>
        public void Convert<TPixel, TBuffer, TSample, TStorer>(
            Configuration configuration,
            ImageFrame<TPixel> image,
            Rectangle sourceRectangle,
            TBuffer buffer)
            where TPixel : unmanaged, IPixel<TPixel>
            where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
            where TSample : unmanaged
            where TStorer : struct, IHeifSampleConverter<TSample>
        {
            if (this.encodeAlpha)
            {
                HeifPlanarAlphaEncoder.Convert<TPixel, TBuffer, TSample, TStorer>(
                    configuration,
                    image,
                    sourceRectangle,
                    buffer,
                    this.componentMemory.Span);

                return;
            }

            Span<Rgb48> packed = MemoryMarshal.Cast<float, Rgb48>(
                this.packedMemory.Span)[..this.packedPixelCount];

            HeifPlanarColorConverter.ConvertFromRgb<TPixel, TBuffer, TSample, TStorer>(
                configuration,
                image,
                sourceRectangle,
                buffer,
                in this.parameters,
                this.colorConverter,
                packed,
                this.componentMemory.Span);
        }

        /// <inheritdoc/>
        public void Dispose() => this.storageOwner.Dispose();
    }

    /// <summary>
    /// Retains the reconstructed reference state shared by the samples of one AV1 sequence track.
    /// </summary>
    internal abstract class SequenceEncoder : IDisposable
    {
        private uint nextOrderHint;

        protected SequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            HeifEncodingSpeed speed,
            bool encodeAlpha,
            bool usesHighBitDepth)
        {
            this.Configuration = configuration;
            this.SequenceHeader = CreateSequenceHeader(width, height, colorConfig, speed, false);
            this.QIndex = qIndex;
            this.Speed = speed;
            this.TileBufferLength = GetTileBufferLength(width, height, colorConfig);
            this.EncodeAlpha = encodeAlpha;
            this.FrameHeader = CreateFrameHeader(
                this.SequenceHeader,
                qIndex,
                speed,
                ObuFrameType.KeyFrame);

            this.ConversionWorkspace = new Av1EncoderConversionWorkspace(
                configuration,
                width,
                colorConfig,
                encodeAlpha,
                usesHighBitDepth);

            try
            {
                // Screen-content eligibility follows the source classification. Reserve the
                // optional state once for the fixed-geometry sequence so any classified frame can use legal tools.
                bool allocateScreenContentState = true;
                Av1MotionSearchSettings motionSettings = new(
                    speed,
                    this.SequenceHeader.IsStillPicture,
                    new Size(width, height),
                    qIndex,
                    this.FrameHeader.IsIntra,
                    screenContent: true);

                bool allocateIntraBlockCopySearch =
                    allocateScreenContentState &&
                    motionSettings.AllowIntraBlockCopy;

                // Sequence geometry and maximum tool capacity are fixed before the first sample. Reusing this owner
                // avoids renting the complete mode grid and optional screen-content index for every frame.
                Av1EncoderSpeedSettings speedSettings = new(
                    speed, this.SequenceHeader.IsStillPicture, this.FrameHeader.IsIntra, qIndex, new Size(width, height));

                this.PictureBuffer = new Av1EncoderPictureBuffer(
                    configuration,
                    this.SequenceHeader,
                    this.FrameHeader,
                    width,
                    height,
                    motionSettings.LimitIntraBlockCopyHashBlockSize ? 8 : 1 << this.SequenceHeader.SuperblockSizeLog2,
                    disallow4x4AllFrames: !this.FrameHeader.CodedLossless && speedSettings.MinimumPartitionSize >= Av1BlockSize.Block8x8,
                    allocateScreenContentState: allocateScreenContentState,
                    allocateMotionVectorState: true,
                    allocateIntraBlockCopySearch: allocateIntraBlockCopySearch);

                this.Coefficients = new Av1EncoderCoefficientBuffer(
                    configuration,
                    this.SequenceHeader,
                    width,
                    height);

                this.PictureBuffer.Picture.Parent.EncodingSpeed = speed;
                this.PictureBuffer.Picture.Parent.SpeedSettings = speedSettings;

                this.SuperblockWorkspace = new Av1EncoderSuperblockWorkspace(configuration);

                this.TileWorkspace = new Av1EncoderTileWorkspace(this.FrameHeader, this.SuperblockWorkspace);
                this.BlockWorkspace = new Av1EncoderBlockWorkspace(
                    configuration,
                    allocateInterMotionCosts: true,
                    allocateDisplacementCosts: allocateIntraBlockCopySearch,
                    this.SequenceHeader.SuperblockSize);

                // Tile probabilities adapt within a sample, while error-resilient frame headers prohibit carrying
                // those updates into the next sample. The retained encoder is therefore reset before each frame.
                this.SymbolEncoder = new Av1SymbolEncoder(
                    configuration,
                    this.TileBufferLength,
                    qIndex,
                    updateCdf: true);

                this.SymbolEncoder.EncodingSpeed = speed;

                this.ObuWriter = new ObuWriter(configuration);
            }
            catch
            {
                // The caller receives no encoder when construction fails. Release only completed common owners;
                // derived frame construction has not started and must not be reached through virtual disposal.
                this.DisposeResources();
                throw;
            }
        }

        /// <summary>
        /// Gets the sequence header shared by every sample written by this encoder.
        /// </summary>
        public ObuSequenceHeader SequenceHeader { get; }

        protected Configuration Configuration { get; }

        protected int QIndex { get; }

        /// <summary>
        /// Gets the encoding speed retained for every frame in this track.
        /// </summary>
        protected HeifEncodingSpeed Speed { get; }

        protected int TileBufferLength { get; }

        protected bool EncodeAlpha { get; }

        /// <summary>
        /// Gets the packed and planar row storage reused by every sample in the track.
        /// </summary>
        protected Av1EncoderConversionWorkspace ConversionWorkspace { get; }

        /// <summary>
        /// Gets the fixed-geometry picture state reused by every sample in the track.
        /// </summary>
        protected Av1EncoderPictureBuffer PictureBuffer { get; }

        /// <summary>
        /// Gets the frame header and nested syntax state reused by every sample in the track.
        /// </summary>
        protected ObuFrameHeader FrameHeader { get; }

        /// <summary>
        /// Gets the frame-sized coefficient storage reused by every sample in the track.
        /// </summary>
        protected Av1EncoderCoefficientBuffer Coefficients { get; }

        /// <summary>
        /// Gets the superblock decision workspace reused serially across the track.
        /// </summary>
        protected Av1EncoderSuperblockWorkspace SuperblockWorkspace { get; }

        /// <summary>
        /// Gets the tile, superblock, and entropy cursor graph reused serially across the track.
        /// </summary>
        protected Av1EncoderTileWorkspace TileWorkspace { get; }

        /// <summary>
        /// Gets the block arithmetic workspace reused serially across the track.
        /// </summary>
        protected Av1EncoderBlockWorkspace BlockWorkspace { get; }

        /// <summary>
        /// Gets the tile probability graph and bounded output buffer reused by every sample in the track.
        /// </summary>
        protected Av1SymbolEncoder SymbolEncoder { get; }

        /// <summary>
        /// Gets the reusable OBU header writer for the track.
        /// </summary>
        protected ObuWriter ObuWriter { get; }

        protected void ConfigureFrameHeader(ObuFrameType frameType)
        {
            Av1FrameEncoder.ConfigureFrameHeader(this.FrameHeader, this.QIndex, this.Speed, frameType);
            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            this.FrameHeader.OrderHint = orderHintBits == 0
                ? 0
                : this.nextOrderHint & ((1U << orderHintBits) - 1);
            this.FrameHeader.SkipModeParameters.Derive(this.SequenceHeader.OrderHintInfo, this.FrameHeader);
            this.FrameHeader.SkipModeParameters.SkipModeFlag = this.FrameHeader.SkipModeParameters.SkipModeAllowed;
        }

        protected void CompleteFrameHeader()
        {
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            if (parent.FrameUpdateType == Av1FrameUpdateType.Last)
            {
                parent.AverageInterQuantizer = ((3 * parent.AverageInterQuantizer) + this.FrameHeader.QuantizationParameters.BaseQIndex + 2) >> 2;
            }

            Span<bool> referenceValidity = this.FrameHeader.GetReferenceValidity();
            Span<uint> referenceOrderHints = this.FrameHeader.GetReferenceOrderHints();
            for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
            {
                if ((this.FrameHeader.RefreshFrameFlags & (1U << slot)) != 0)
                {
                    referenceValidity[slot] = true;
                    referenceOrderHints[slot] = this.FrameHeader.OrderHint;
                }
            }

            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            this.nextOrderHint = orderHintBits == 0
                ? 0
                : (this.FrameHeader.OrderHint + 1) & ((1U << orderHintBits) - 1);
        }

        /// <summary>
        /// Encodes an independently decodable sample with the sequence header required for random access.
        /// </summary>
        public void EncodeKeyFrame<TPixel>(ImageFrame<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.EncodeFrame(image, stream, ObuFrameType.KeyFrame, true);

        /// <summary>
        /// Encodes a continuation sample predicted from the preceding reconstructed frame.
        /// </summary>
        public void EncodeInterFrame<TPixel>(ImageFrame<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.EncodeFrame(image, stream, ObuFrameType.InterFrame, false);

        /// <inheritdoc/>
        public void Dispose()
        {
            this.DisposeFrames();
            this.DisposeResources();
        }

        /// <summary>
        /// Releases the sample-type-specific frame buffers retained by the track encoder.
        /// </summary>
        protected abstract void DisposeFrames();

        protected abstract void EncodeFrame<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            ObuFrameType frameType,
            bool writeSequenceHeader)
            where TPixel : unmanaged, IPixel<TPixel>;

        private void DisposeResources()
        {
            // Construction can stop between any two allocations. Successful instances have every owner;
            // failed constructors retain only the prefix completed before the allocator rejected a request.
            this.ObuWriter?.Dispose();
            this.SymbolEncoder?.Dispose();
            this.BlockWorkspace?.Dispose();
            this.SuperblockWorkspace?.Dispose();
            this.Coefficients?.Dispose();
            this.PictureBuffer?.Dispose();
            this.ConversionWorkspace.Dispose();
        }
    }

    private sealed class ByteSequenceEncoder : SequenceEncoder
    {
        private Av1EncoderFrameBuffer<byte> source;
        private Av1EncoderFrameBuffer<byte>? previousSource;
        private readonly IMemoryOwner<ulong>? sourceBlockSad;
        private ulong averageSourceSad;
        private int framesSinceKey;
        private readonly Av1EncoderFrame<byte>[] references = new Av1EncoderFrame<byte>[Av1Constants.ReferenceFrameCount];
        private Av1EncoderFrameBuffer<byte> reference;
        private Av1EncoderFrameBuffer<byte> goldenReference;
        private Av1EncoderFrameBuffer<byte> reconstruction;
        private bool hasDistinctGoldenReference;

        public ByteSequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            HeifEncodingSpeed speed,
            bool encodeAlpha)
            : base(
                configuration,
                width,
                height,
                colorConfig,
                qIndex,
                speed,
                encodeAlpha,
                usesHighBitDepth: false)
        {
            try
            {
                Av1ColorFormat colorFormat = colorConfig.GetColorFormat();

                // Inter prediction needs a complete superblock beyond the image plus interpolation and alignment margins.
                int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;

                this.source = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                if (speed >= HeifEncodingSpeed.Level7)
                {
                    this.sourceBlockSad = configuration.MemoryAllocator.Allocate<ulong>(((width + 63) >> 6) * ((height + 63) >> 6));

                    // Rotate source owners after encoding so temporal analysis sees uncompressed samples
                    // without a frame copy. The padding also supplies complete edge superblocks.
                    this.previousSource = new(
                        configuration,
                        width,
                        height,
                        ByteSampleBitDepth,
                        colorFormat,
                        CenteredChromaSamplePosition,
                        CenteredChromaSamplePosition,
                        lumaBorder);
                }

                this.goldenReference = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                this.reference = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                this.reconstruction = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);
            }
            catch
            {
                // The common state already exists, and any preceding frame allocations also need returning.
                this.Dispose();
                throw;
            }
        }

        protected override void DisposeFrames()
        {
            // A derived constructor can fail before all frame owners exist.
            this.goldenReference?.Dispose();
            this.reconstruction?.Dispose();
            this.reference?.Dispose();
            this.source?.Dispose();
            this.previousSource?.Dispose();
            this.sourceBlockSad?.Dispose();
        }

        protected override void EncodeFrame<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            ObuFrameType frameType,
            bool writeSequenceHeader)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureFrameHeader(frameType);

            Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
            this.SymbolEncoder.Reset();
            bool isScreenContent = PrepareFrame(
                this.Configuration,
                image,
                sourceRectangle,
                this.source.Frame,
                this.reference.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Speed,
                this.ConversionWorkspace);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.PreviousSource = this.previousSource is not null ? this.previousSource.Frame.CodedView : default;

            parent.SourceBlockSad = this.sourceBlockSad is not null ? this.sourceBlockSad.Memory : default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;
            parent.SourceMotionPercentage = 0;
            if (this.previousSource is not null && this.framesSinceKey != 0)
            {
                AnalyzeTemporalSource<byte, Av1MotionSearchBase.ByteOperator>(
                    this.source.Frame.CodedView,
                    this.previousSource.Frame.CodedView,
                    parent,
                    isScreenContent,
                    this.framesSinceKey,
                    ref this.averageSourceSad);
            }

            if (frameHeader.IsIntra)
            {
                this.framesSinceKey = 0;
            }

            this.references[(int)Av1ReferenceFrameType.Last] = this.reference.Frame;
            this.references[(int)Av1ReferenceFrameType.Golden] = this.hasDistinctGoldenReference ? this.goldenReference.Frame : this.reference.Frame;
            parent.AvailableReferenceMask = frameHeader.IsIntra ? (byte)0 :
                (byte)((1 << (int)Av1ReferenceFrameType.Last) | (this.hasDistinctGoldenReference ? 1 << (int)Av1ReferenceFrameType.Golden : 0));

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = Math.Max(0, this.framesSinceKey - 1);
            parent.IsScreenContent = isScreenContent;
            this.PictureBuffer.Picture.Parent.SpeedSettings = new(
                this.Speed, this.SequenceHeader.IsStillPicture, frameHeader.IsIntra, this.QIndex, image.Size);

            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                this.source,
                this.references,
                this.reconstruction,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader);

            this.CompleteFrameHeader();

            this.framesSinceKey++;
            if (this.previousSource is not null)
            {
                (this.source, this.previousSource) = (this.previousSource, this.source);
            }

            this.reconstruction.Frame.ExtendBorders();

            if (frameType == ObuFrameType.KeyFrame || this.hasDistinctGoldenReference)
            {
                (this.reference, this.reconstruction) = (this.reconstruction, this.reference);
                if (frameType == ObuFrameType.KeyFrame)
                {
                    this.hasDistinctGoldenReference = false;
                }
            }
            else
            {
                // Preserve the key reconstruction as GOLDEN while the first inter reconstruction becomes LAST.
                (this.goldenReference, this.reference, this.reconstruction) =
                    (this.reference, this.reconstruction, this.goldenReference);
                this.hasDistinctGoldenReference = true;
            }
        }
    }

    private sealed class HighBitDepthSequenceEncoder : SequenceEncoder
    {
        private Av1EncoderFrameBuffer<ushort> source;
        private Av1EncoderFrameBuffer<ushort>? previousSource;
        private readonly IMemoryOwner<ulong>? sourceBlockSad;
        private ulong averageSourceSad;
        private int framesSinceKey;
        private readonly Av1EncoderFrame<ushort>[] references = new Av1EncoderFrame<ushort>[Av1Constants.ReferenceFrameCount];
        private Av1EncoderFrameBuffer<ushort> reference;
        private Av1EncoderFrameBuffer<ushort> goldenReference;
        private Av1EncoderFrameBuffer<ushort> reconstruction;
        private bool hasDistinctGoldenReference;

        public HighBitDepthSequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            HeifEncodingSpeed speed,
            bool encodeAlpha)
            : base(
                configuration,
                width,
                height,
                colorConfig,
                qIndex,
                speed,
                encodeAlpha,
                usesHighBitDepth: true)
        {
            try
            {
                int bitDepth = colorConfig.BitDepth.GetBitCount();
                Av1ColorFormat colorFormat = colorConfig.GetColorFormat();

                // Inter prediction needs a complete superblock beyond the image plus interpolation and alignment margins.
                int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;

                this.source = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                if (speed >= HeifEncodingSpeed.Level7)
                {
                    this.sourceBlockSad = configuration.MemoryAllocator.Allocate<ulong>(((width + 63) >> 6) * ((height + 63) >> 6));
                    this.previousSource = new(
                        configuration,
                        width,
                        height,
                        bitDepth,
                        colorFormat,
                        CenteredChromaSamplePosition,
                        CenteredChromaSamplePosition,
                        lumaBorder);
                }

                this.goldenReference = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                this.reference = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                this.reconstruction = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);
            }
            catch
            {
                // The common state already exists, and any preceding frame allocations also need returning.
                this.Dispose();
                throw;
            }
        }

        protected override void DisposeFrames()
        {
            // A derived constructor can fail before all frame owners exist.
            this.goldenReference?.Dispose();
            this.reconstruction?.Dispose();
            this.reference?.Dispose();
            this.source?.Dispose();
            this.previousSource?.Dispose();
            this.sourceBlockSad?.Dispose();
        }

        protected override void EncodeFrame<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            ObuFrameType frameType,
            bool writeSequenceHeader)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureFrameHeader(frameType);

            Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
            this.SymbolEncoder.Reset();
            bool isScreenContent = PrepareFrame(
                this.Configuration,
                image,
                sourceRectangle,
                this.source.Frame,
                this.reference.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Speed,
                this.ConversionWorkspace);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.IsScreenContent = isScreenContent;
            parent.SpeedSettings = new(
                this.Speed, this.SequenceHeader.IsStillPicture, frameHeader.IsIntra, this.QIndex, image.Size);

            parent.SourceBlockSad = this.sourceBlockSad is not null ? this.sourceBlockSad.Memory : default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;
            parent.SourceMotionPercentage = 0;
            if (this.previousSource is not null && this.framesSinceKey != 0)
            {
                AnalyzeTemporalSource<ushort, Av1MotionSearchBase.UInt16Operator>(
                    this.source.Frame.CodedView,
                    this.previousSource.Frame.CodedView,
                    parent,
                    isScreenContent,
                    this.framesSinceKey,
                    ref this.averageSourceSad);
            }

            if (frameHeader.IsIntra)
            {
                this.framesSinceKey = 0;
            }

            this.references[(int)Av1ReferenceFrameType.Last] = this.reference.Frame;
            this.references[(int)Av1ReferenceFrameType.Golden] = this.hasDistinctGoldenReference ? this.goldenReference.Frame : this.reference.Frame;
            parent.AvailableReferenceMask = frameHeader.IsIntra ? (byte)0 :
                (byte)((1 << (int)Av1ReferenceFrameType.Last) | (this.hasDistinctGoldenReference ? 1 << (int)Av1ReferenceFrameType.Golden : 0));

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = Math.Max(0, this.framesSinceKey - 1);
            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                this.source,
                this.references,
                this.reconstruction,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader);

            this.CompleteFrameHeader();
            this.framesSinceKey++;

            if (this.previousSource is not null)
            {
                (this.source, this.previousSource) = (this.previousSource, this.source);
            }

            this.reconstruction.Frame.ExtendBorders();

            if (frameType == ObuFrameType.KeyFrame || this.hasDistinctGoldenReference)
            {
                (this.reference, this.reconstruction) = (this.reconstruction, this.reference);
                if (frameType == ObuFrameType.KeyFrame)
                {
                    this.hasDistinctGoldenReference = false;
                }
            }
            else
            {
                // Preserve the key reconstruction as GOLDEN while the first inter reconstruction becomes LAST.
                (this.goldenReference, this.reference, this.reconstruction) =
                    (this.reference, this.reconstruction, this.goldenReference);
                this.hasDistinctGoldenReference = true;
            }
        }
    }
}

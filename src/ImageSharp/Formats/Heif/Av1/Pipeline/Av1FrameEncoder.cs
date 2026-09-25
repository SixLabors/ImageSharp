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
    /// The step sizes the warp refinement tries, each half of the one before.
    /// </summary>
    /// <remarks>Reference: GM_MAX_REFINEMENT_STEPS.</remarks>
    private const int GlobalMotionRefinementCount = 5;

    private enum FrameEncodingKind
    {
        StillColor,
        StillAlpha
    }

    /// <summary>
    /// Defines the sample-specific SIMD squared-error operation used by frame-level motion search.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    private interface IGlobalMotionSearchOperator<TSample> :
        Av1GlobalMotionEstimator.IAv1PyramidFillOperator<TSample>,
        Av1GlobalMotionSearch.IAv1GlobalMotionOperator<TSample>
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

        // Superblock geometry follows coding speed and resolution. Reference: av1_select_sb_size(). Real-time
        // coding uses 128x128 only above 720p. Otherwise small frames use 64x64 above speed zero, and the fastest
        // still-image mode also uses it below 4K.
        int minimumDimension = Math.Min(width, height);
        bool use128x128Superblock = speedSettings.IsRealtime
            ? minimumDimension > 720
            : !(speed >= HeifEncodingSpeed.Level1 && minimumDimension <= 480) &&
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
            EnableInterIntraCompound = !isStillPicture && speedSettings.EnableInterIntraCompound,

            // A sequence enables temporal motion vectors and warped motion, and the frame header decides whether
            // each frame uses them. Distance-weighted compound follows the speed features. Reference: the
            // order_hint_info and tool flags that init_seq_coding_tools() sets, with
            // DEFAULT_EXPLICIT_ORDER_HINT_BITS, and the speed-feature adjustments that follow them.
            EnableWarpedMotion = !isStillPicture,
            OrderHintInfo = new ObuOrderHintInfo
            {
                EnableOrderHint = !isStillPicture,
                EnableJointCompound = !isStillPicture && speedSettings.UseDistanceWeightedCompound,
                EnableReferenceFrameMotionVectors = !isStillPicture,
                OrderHintBits = isStillPicture ? 0 : 7
            },
            EnableSuperResolution = false,

            // The reference disables CDEF by default in all-intra mode because it blurs images.
            // Every other mode keeps it enabled.
            EnableCdef = !isStillPicture,
            EnableRestoration = speedSettings.EnableRestoration,
            ColorConfig = colorConfig
        };
    }

    /// <summary>
    /// Infers the lowest level whose picture size, dimension, and display sample rate limits hold the frame,
    /// as <c>set_bitstream_level_tier</c> does.
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

            // Real-time usage selects the reference mode per frame only while estimated compound prediction is
            // enabled, and otherwise codes single references. Reference: the frame_parameter_update and
            // use_comp_ref_nonrd branches of av1_encode_frame().
            if (speedSettings.IsRealtime && !speedSettings.UseEstimatedCompound)
            {
                frameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
            }
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
            configuration,
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
            configuration,
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
        Configuration configuration,
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed)
    {
        // Real-time usage never searches global motion. Reference: the g_usage test that clears
        // tool_cfg->enable_global_motion in av1_cx_iface.
        ConfigureGlobalMotion<byte, ByteGlobalMotionSearchOperator>(
            configuration.MemoryAllocator,
            source,
            reference,
            frameHeader,
            sequenceHeader.ColorConfig.BitDepth,
            sequenceHeader.IsStillPicture || speed < HeifEncodingSpeed.Level7);

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
            configuration,
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
            configuration,
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
        Configuration configuration,
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        HeifEncodingSpeed speed)
    {
        // Real-time usage never searches global motion. Reference: the g_usage test that clears
        // tool_cfg->enable_global_motion in av1_cx_iface.
        ConfigureGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(
            configuration.MemoryAllocator,
            source,
            reference,
            frameHeader,
            sequenceHeader.ColorConfig.BitDepth,
            sequenceHeader.IsStillPicture || speed < HeifEncodingSpeed.Level7);

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
    /// <summary>
    /// Estimates the warp model of one reference frame and codes it when it earns its cost.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific measures the search needs.</typeparam>
    /// <param name="allocator">The allocator of every buffer the search uses.</param>
    /// <param name="source">The frame being coded.</param>
    /// <param name="reference">The frame the model maps onto.</param>
    /// <param name="frameHeader">The header that receives the chosen model.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="enableGlobalMotion">Whether the encoder configuration searches global motion.</param>
    /// <remarks>
    /// Every model family is tried, and the one whose warped error is the smallest share of the
    /// unwarped error wins, as long as that share also justifies what the model costs to code. A
    /// model that reduces to a translation is never taken, because the vector such a model gives a
    /// block has a published defect. Reference: compute_global_motion_for_ref_frame().
    /// </remarks>
    private static void ConfigureGlobalMotion<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reference,
        ObuFrameHeader frameHeader,
        Av1BitDepth bitDepth,
        bool enableGlobalMotion)
        where TSample : unmanaged
        where TOperator : struct, IGlobalMotionSearchOperator<TSample>
    {
        Span<Av1GlobalMotionParameters> models = frameHeader.GetGlobalMotionParameters();
        models.Fill(Av1GlobalMotionParameters.Identity);
        if (frameHeader.IsIntra || !enableGlobalMotion)
        {
            return;
        }

        Buffer2DRegion<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<TSample> referenceLuma = reference.CodedView.GetPlane(Av1Plane.Y);

        // The flow search walks both frames with one set of level sizes, so the two planes have to
        // agree on their row stride.
        if (sourceLuma.Stride != referenceLuma.Stride)
        {
            return;
        }

        int width = source.CodedWidth;
        int height = source.CodedHeight;
        int stride = sourceLuma.Stride;
        int depth = bitDepth.GetBitCount();
        ReadOnlySpan<TSample> sourcePlane = sourceLuma.Buffer.DangerousGetSingleSpan();
        ReadOnlySpan<TSample> referencePlane = referenceLuma.Buffer.DangerousGetSingleSpan();
        int sourceOrigin = (sourceLuma.Bounds.Y * stride) + sourceLuma.Bounds.X;
        int referenceOrigin = (referenceLuma.Bounds.Y * stride) + referenceLuma.Bounds.X;

        // The map holds one mark per error block, and a frame that does not divide evenly still
        // has a partial block at its right and bottom edges.
        int mapWidth = (width + Av1GlobalMotionSearch.ErrorBlock - 1) >> Av1GlobalMotionSearch.ErrorBlockLog;
        int mapHeight = (height + Av1GlobalMotionSearch.ErrorBlock - 1) >> Av1GlobalMotionSearch.ErrorBlockLog;
        using IMemoryOwner<byte> mapOwner = allocator.Allocate<byte>(mapWidth * mapHeight);
        Span<byte> map = mapOwner.Memory.Span;

        Av1MotionModel[] fitted = [new Av1MotionModel()];
        double bestErrorAdvantage = double.MaxValue;
        for (Av1GlobalMotionType family = Av1GlobalMotionType.RotationZoom; family <= Av1GlobalMotionType.Affine; family++)
        {
            bool estimated = family == Av1GlobalMotionType.RotationZoom
                ? Av1GlobalMotionEstimator.Compute<TSample, TOperator, Av1Ransac.RotationZoomModel>(
                    allocator, sourcePlane, referencePlane, width, height, stride, sourceOrigin, depth, fitted)
                : Av1GlobalMotionEstimator.Compute<TSample, TOperator, Av1Ransac.AffineModel>(
                    allocator, sourcePlane, referencePlane, width, height, stride, sourceOrigin, depth, fitted);

            if (!estimated || fitted[0].InlierCount == 0)
            {
                continue;
            }

            Av1GlobalMotionParameters candidate = Av1GlobalMotionSearch.ConvertModelToParameters(fitted[0].Parameters);
            candidate.UpdateShearParameters();
            if (candidate.IsInvalid || candidate.Type <= Av1GlobalMotionType.Translation)
            {
                continue;
            }

            // The error is measured only where the model was fitted, so the parts of the frame that
            // move on their own do not decide whether the model is worth coding.
            Av1GlobalMotionSearch.ComputeFeatureSegmentationMap(map, mapWidth, mapHeight, fitted[0].Inliers);
            long referenceError = Av1GlobalMotionSearch.GetSegmentedFrameError<TSample, TOperator>(
                referencePlane[referenceOrigin..],
                stride,
                sourcePlane[sourceOrigin..],
                stride,
                width,
                height,
                map,
                mapWidth);

            if (referenceError == 0)
            {
                continue;
            }

            long warpError = Av1GlobalMotionSearch.RefineIntegerizedParameters<TSample, TOperator>(
                allocator,
                ref candidate,
                candidate.Type,
                referencePlane[referenceOrigin..],
                stride,
                sourcePlane[sourceOrigin..],
                stride,
                width,
                height,
                GlobalMotionRefinementCount,
                depth,
                referenceError,
                map,
                mapWidth);

            // Refinement can move a model down to a simpler family, so the family is read again.
            if (warpError == long.MaxValue || candidate.Type <= Av1GlobalMotionType.Translation)
            {
                continue;
            }

            int parametersCost =
                ObuWriter.GetGlobalMotionModelBitCount(candidate, frameHeader.AllowHighPrecisionMotionVector) <<
                Av1ProbabilityCost.CostShift;

            double errorAdvantage = (double)warpError / referenceError;
            if (!Av1GlobalMotionSearch.IsEnoughErrorAdvantage(errorAdvantage, parametersCost))
            {
                continue;
            }

            if (errorAdvantage < bestErrorAdvantage)
            {
                bestErrorAdvantage = errorAdvantage;
                models[0] = candidate;
            }
        }
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
        public static int Fill(Av1ImagePyramid pyramid, ReadOnlySpan<byte> source, int stride, int bitDepth, int levels)
            => pyramid.Fill(source, stride, levels);

        /// <inheritdoc/>
        public static void PredictWarped(
            ReadOnlySpan<byte> source,
            int sourceStride,
            int sourceWidth,
            int sourceHeight,
            Span<byte> destination,
            int destinationStride,
            Point position,
            int width,
            int height,
            int bitDepth,
            Av1GlobalMotionParameters parameters,
            Span<short> scratch)
            => Av1GlobalMotionSearch.ByteOperator.PredictWarped(
                source,
                sourceStride,
                sourceWidth,
                sourceHeight,
                destination,
                destinationStride,
                position,
                width,
                height,
                bitDepth,
                parameters,
                scratch);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<byte> source, int sourceStride, ReadOnlySpan<byte> prediction, int predictionStride, int width, int height)
            => Av1GlobalMotionSearch.ByteOperator.SumAbsoluteDifferences(
                source, sourceStride, prediction, predictionStride, width, height);

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
        public static int Fill(Av1ImagePyramid pyramid, ReadOnlySpan<ushort> source, int stride, int bitDepth, int levels)
            => pyramid.Fill(source, stride, bitDepth, levels);

        /// <inheritdoc/>
        public static void PredictWarped(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            int sourceWidth,
            int sourceHeight,
            Span<ushort> destination,
            int destinationStride,
            Point position,
            int width,
            int height,
            int bitDepth,
            Av1GlobalMotionParameters parameters,
            Span<short> scratch)
            => Av1GlobalMotionSearch.UInt16Operator.PredictWarped(
                source,
                sourceStride,
                sourceWidth,
                sourceHeight,
                destination,
                destinationStride,
                position,
                width,
                height,
                bitDepth,
                parameters,
                scratch);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<ushort> source, int sourceStride, ReadOnlySpan<ushort> prediction, int predictionStride, int width, int height)
            => Av1GlobalMotionSearch.UInt16Operator.SumAbsoluteDifferences(
                source, sourceStride, prediction, predictionStride, width, height);

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
    internal abstract class SequenceEncoder : IDisposable, IAv1ReferenceRefreshControl
    {
        /// <summary>
        /// The number of rotating slots that hold LAST and ALTREF. Reference: sh in
        /// av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        private const int RotatingSlotCount = 6;

        /// <summary>
        /// The fixed GOLDEN slot. Reference: gld_idx in av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        private const int GoldenSlot = 6;

        /// <summary>
        /// The slot that no reference uses. Reference: the ref_idx default of 7 in
        /// av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        private const int UnusedSlot = 7;

        /// <summary>
        /// The golden interval when cyclic refresh gives no refresh period. Reference: FIXED_GF_INTERVAL_RT.
        /// </summary>
        private const int FixedGoldenIntervalRealtime = 80;

        /// <summary>
        /// The golden interval after a period of high motion. Reference: set_golden_update().
        /// </summary>
        private const int LowMotionGoldenInterval = 16;

        /// <summary>
        /// The wrap point of the golden group index. Reference: MAX_STATIC_GF_GROUP_LENGTH.
        /// </summary>
        private const int MaximumStaticGoldenGroupLength = 250;

        private uint nextOrderHint;

        /// <summary>
        /// The number of frames coded before the current frame. Reference: cm->current_frame.frame_number.
        /// </summary>
        private uint frameNumber;

        /// <summary>
        /// Reference: rc->frames_till_gf_update_due.
        /// </summary>
        private int framesTillGoldenUpdateDue;

        /// <summary>
        /// Reference: cpi->gf_frame_index.
        /// </summary>
        private int goldenFrameIndex;

        /// <summary>
        /// Reference: p_rc->baseline_gf_interval.
        /// </summary>
        private int baselineGoldenInterval;

        /// <summary>
        /// The number of the frame that last refreshed GOLDEN. Reference: rc->frame_num_last_gf_refresh.
        /// </summary>
        private uint lastGoldenRefreshFrameNumber;

        /// <summary>
        /// Reference: rc->frames_since_golden.
        /// </summary>
        private int framesSinceGolden;

        /// <summary>
        /// The slot that holds the entropy context of the most recent frame of the only context type that
        /// one-layer real-time coding uses, or -1 when there is none. Reference: fb_of_context_type[0].
        /// </summary>
        private int contextTypeSlot = -1;

        /// <summary>
        /// The running warped-motion usage probability, out of 128, of each frame update type.
        /// Reference: frame_probs->warped_probs, initialized from default_warped_probs.
        /// </summary>
        private readonly int[] warpedProbabilities = [64, 64, 64, 64, 64, 64, 64];

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
                this.PictureBuffer.Picture.Parent.ReferenceRefreshControl = this;

                // One-pass constant-bitrate coding starts the running inter quantizer at the worst allowed
                // quantizer, which the fixed quantizer sets. Reference: avg_frame_qindex in av1_rc_init().
                this.PictureBuffer.Picture.Parent.AverageInterQuantizer = qIndex;

                this.MotionField = new Av1EncoderMotionField(
                    configuration,
                    this.FrameHeader.ModeInfoColumnCount,
                    this.FrameHeader.ModeInfoRowCount);

                this.PictureBuffer.Picture.Parent.MotionField = this.MotionField;

                this.SuperblockWorkspace = new Av1EncoderSuperblockWorkspace(configuration);

                this.TileWorkspace = new Av1EncoderTileWorkspace(this.FrameHeader, this.SuperblockWorkspace);
                this.BlockWorkspace = new Av1EncoderBlockWorkspace(
                    configuration,
                    allocateInterMotionCosts: true,
                    allocateDisplacementCosts: allocateIntraBlockCopySearch,
                    this.SequenceHeader.SuperblockSize);

                // Each frame starts from the defaults or from the context saved with its primary reference, and the
                // adapted context is saved with the coded frame. BeginFrame selects the start for each frame.
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

        /// <summary>
        /// Gets the number of displayed frames since GOLDEN was refreshed. Reference: rc->frames_since_golden.
        /// </summary>
        protected int FramesSinceGolden => this.framesSinceGolden;

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

        /// <summary>
        /// Gets the temporal motion field that each frame projects from its references.
        /// </summary>
        protected Av1EncoderMotionField MotionField { get; }

        protected void ConfigureFrameHeader(ObuFrameType frameType)
        {
            Av1FrameEncoder.ConfigureFrameHeader(this.FrameHeader, this.QIndex, this.Speed, frameType);

            // A sequence keeps backward adaptation, so every frame stores its final probabilities for later frames.
            // Reference: the REFRESH_FRAME_CONTEXT_BACKWARD default of refresh_frame_context, which the frame
            // header writes as disable_frame_end_update_cdf.
            this.FrameHeader.DisableFrameEndUpdateCdf = false;
            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            this.FrameHeader.OrderHint = orderHintBits == 0
                ? 0
                : this.nextOrderHint & ((1U << orderHintBits) - 1);

            // A key frame is error resilient by definition. Inter frames keep the references' state and
            // entropy contexts. Reference: set_ext_overrides(), with use_error_resilient off by default.
            this.FrameHeader.ErrorResilientMode = frameType == ObuFrameType.KeyFrame;
        }

        /// <summary>
        /// Returns the slot that LAST uses before the source analysis selects the rest of the structure.
        /// Reference: last_idx in av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        /// <returns>The reference-map slot of LAST.</returns>
        protected int GetLastSlot()
            => this.frameNumber > 1 ? (int)((this.frameNumber - 1) % RotatingSlotCount) : 0;

        /// <summary>
        /// Selects the reference slots, the refreshed slots, the primary reference, and the frame filter after the
        /// source analysis of the frame. Reference: set_gf_interval_update_onepass_rt() in
        /// av1_get_one_pass_rt_params(), then av1_set_rtc_reference_structure_one_layer() with
        /// gf_update = (gf_frame_index == 0), then choose_primary_ref_frame().
        /// </summary>
        /// <param name="parent">The frame state with the source analysis and speed settings of the frame.</param>
        /// <param name="averageSourceSad">The running average source SAD. Reference: rc->avg_source_sad.</param>
        protected void ConfigureReferenceStructure(Av1PictureParentControlSet parent, ulong averageSourceSad)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;
            bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;

            // set_gf_interval_update_onepass_rt()
            if (parent.HighSourceSad || this.framesTillGoldenUpdateDue == 0)
            {
                this.SetBaselineGoldenInterval(parent.AverageFrameLowMotion);
            }

            bool goldenUpdate = this.goldenFrameIndex == 0;
            parent.StartsGoldenGroup = !keyFrame && goldenUpdate;

            // av1_configure_buffer_updates() refreshes GOLDEN in key frames and golden-group frames.
            parent.RefreshesGolden = keyFrame || goldenUpdate;

            // encode_without_recode() restores the frame probability tables at a key frame, and at a golden refresh
            // when warped motion is pruned further. Reference: copy_frame_prob_info().
            if (keyFrame || (speedSettings.ExtraPruneWarped && parent.RefreshesGolden))
            {
                this.warpedProbabilities.AsSpan().Fill(64);
            }

            // av1_set_rtc_reference_structure_one_layer()
            uint alternateLag = 4;
            int lagLevel = speedSettings.AlternateReferenceLagLevel;
            if (lagLevel != 0)
            {
                // th_frame_sad rows HDRES CPU 9 and MIDRES CPU 9 hold one value in every column.
                ulong threshold = lagLevel == 1 ? 18000UL : 25000UL;
                alternateLag = averageSourceSad > threshold ? 3U : 6U;
            }

            uint number = this.frameNumber;
            uint lastSlot = (uint)this.GetLastSlot();
            uint lastRefreshSlot = number % RotatingSlotCount;
            uint alternateSlot = number > alternateLag ? (number - alternateLag) % RotatingSlotCount : 0;
            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            referenceFrameIndices.Fill(UnusedSlot);
            referenceFrameIndices[(int)Av1ReferenceFrameType.Last - 1] = lastSlot;
            referenceFrameIndices[(int)Av1ReferenceFrameType.Last2 - 1] = lastRefreshSlot;
            referenceFrameIndices[(int)Av1ReferenceFrameType.Golden - 1] = GoldenSlot;
            referenceFrameIndices[(int)Av1ReferenceFrameType.Alternate - 1] = alternateSlot;
            if (!keyFrame)
            {
                uint refreshFrameFlags = 1U << (int)lastRefreshSlot;
                if (goldenUpdate)
                {
                    refreshFrameFlags |= 1U << GoldenSlot;
                }

                frameHeader.RefreshFrameFlags = refreshFrameFlags;
            }

            // choose_primary_ref_frame(): the last reference whose slot holds the wanted context.
            frameHeader.PrimaryReferenceFrame = Av1Constants.PrimaryReferenceFrameNone;
            if (!frameHeader.IsIntra && !frameHeader.ErrorResilientMode)
            {
                for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
                {
                    if ((int)referenceFrameIndices[reference] == this.contextTypeSlot)
                    {
                        frameHeader.PrimaryReferenceFrame = (uint)reference;
                    }
                }
            }

            // Every frame starts with switchable filters, and fix_interp_filter() narrows the filter after the
            // frame. Reference: set_size_independent_vars() in encode_without_recode().
            if (!frameHeader.IsIntra)
            {
                frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
            }

            // Temporal motion vectors are on by default. Reference: frame_might_allow_ref_frame_mvs() with
            // use_ref_frame_mvs from enable_ref_frame_mvs, which ref_frame_mvs_lvl leaves on in real-time usage.
            ObuOrderHintInfo orderHintInfo = this.SequenceHeader.OrderHintInfo;
            frameHeader.UseReferenceFrameMotionVectors = !frameHeader.IsIntra && !frameHeader.ErrorResilientMode &&
                orderHintInfo.EnableOrderHint && orderHintInfo.EnableReferenceFrameMotionVectors;

            // Warped motion is allowed by default, and a frame whose update type has rarely used it disallows it.
            // Reference: frame_might_allow_warped_motion() in av1_setup_frame's caller, then the
            // prune_warped_prob_thresh test of encode_frame_internal().
            bool allowWarpedMotion = !frameHeader.IsIntra && !frameHeader.ErrorResilientMode &&
                this.SequenceHeader.EnableWarpedMotion;
            int warpedThreshold = speedSettings.WarpedProbabilityThreshold;
            if (allowWarpedMotion && warpedThreshold > 0 &&
                this.warpedProbabilities[(int)GetFrameUpdateType(keyFrame, parent.StartsGoldenGroup)] < warpedThreshold)
            {
                allowWarpedMotion = false;
            }

            frameHeader.AllowWarpedMotion = allowWarpedMotion;

            // OBMC is enabled by default, so the motion mode is switchable in every inter frame.
            // Reference: is_switchable_motion_mode_allowed(allow_warped_motion, enable_obmc).
            frameHeader.IsMotionModeSwitchable = !frameHeader.IsIntra;

            frameHeader.SkipModeParameters.Derive(this.SequenceHeader.OrderHintInfo, frameHeader);
            frameHeader.SkipModeParameters.SkipModeFlag = frameHeader.SkipModeParameters.SkipModeAllowed;
        }

        /// <summary>
        /// Returns the references that the frame may use, without a second reference to a buffer that an
        /// earlier reference in priority order already uses. Reference: get_ref_frame_flags(), with the
        /// LAST, GOLDEN, and ALTREF flags that av1_set_rtc_reference_structure_one_layer() enables.
        /// </summary>
        /// <param name="bufferIds">The buffer identity of each reference type, indexed by reference type.</param>
        /// <param name="speedSettings">The speed settings of the frame.</param>
        /// <returns>The available references, one bit per reference type.</returns>
        protected static byte GetReferenceFrameFlags(ReadOnlySpan<int> bufferIds, in Av1EncoderSpeedSettings speedSettings)
        {
            ReadOnlySpan<Av1ReferenceFrameType> priorityOrder =
            [
                Av1ReferenceFrameType.Last,
                Av1ReferenceFrameType.Alternate,
                Av1ReferenceFrameType.Backward,
                Av1ReferenceFrameType.Golden,
                Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Last2,
                Av1ReferenceFrameType.Last3
            ];

            int flags = (1 << (int)Av1ReferenceFrameType.Last) |
                (1 << (int)Av1ReferenceFrameType.Alternate) |
                (1 << (int)Av1ReferenceFrameType.Golden);

            for (int i = 1; i < priorityOrder.Length; i++)
            {
                Av1ReferenceFrameType reference = priorityOrder[i];

                // One-pass real-time coding compares GOLDEN only with LAST, and with ALTREF while estimated
                // search uses ALTREF.
                int index = speedSettings.IsRealtime && reference == Av1ReferenceFrameType.Golden
                    ? 1 + (speedSettings.UseEstimatedAlternateReference ? 1 : 0)
                    : i;

                for (int j = 0; j < index; j++)
                {
                    Av1ReferenceFrameType earlier = priorityOrder[j];
                    if (bufferIds[(int)reference] == bufferIds[(int)earlier] && (flags & (1 << (int)earlier)) != 0)
                    {
                        flags &= ~(1 << (int)reference);
                        break;
                    }
                }
            }

            return (byte)flags;
        }

        /// <summary>
        /// Advances the reference structure state after a coded frame. Reference: update_fb_of_context_type()
        /// and update_rc_counts().
        /// </summary>
        protected void CompleteReferenceStructure()
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            if (frameHeader.AllowWarpedMotion && parent.SpeedSettings.WarpedProbabilityThreshold > 0)
            {
                // No block selects warped motion, so the new usage probability is 0 and the running value halves.
                // Reference: the warped_probs update at the end of encode_frame_internal().
                int updateType = (int)GetFrameUpdateType(frameHeader.FrameType == ObuFrameType.KeyFrame, parent.StartsGoldenGroup);
                this.warpedProbabilities[updateType] >>= 1;
            }

            if (frameHeader.FrameType == ObuFrameType.KeyFrame)
            {
                this.contextTypeSlot = 0;
            }
            else
            {
                // The first refreshed slot. A frame that refreshes no slot keeps the previous one.
                for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
                {
                    if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
                    {
                        this.contextTypeSlot = slot;
                        break;
                    }
                }
            }

            // update_golden_frame_stats() and the golden refresh test of update_rc_counts().
            if (parent.RefreshesGolden)
            {
                this.framesSinceGolden = 0;
                this.lastGoldenRefreshFrameNumber = this.frameNumber;
            }
            else
            {
                this.framesSinceGolden++;
            }

            if (this.framesTillGoldenUpdateDue > 0)
            {
                this.framesTillGoldenUpdateDue--;
            }

            if (++this.goldenFrameIndex == MaximumStaticGoldenGroupLength)
            {
                this.goldenFrameIndex = 0;
            }

            this.frameNumber++;
        }

        /// <summary>
        /// Returns the update type of a frame in the one-layer real-time structure. Reference: the update_type that
        /// av1_get_one_pass_rt_params() and set_baseline_gf_interval() store for the frame.
        /// </summary>
        /// <param name="keyFrame">Whether the frame is a key frame.</param>
        /// <param name="startsGoldenGroup">Whether an inter frame starts a golden group.</param>
        /// <returns>The frame update type.</returns>
        private static Av1FrameUpdateType GetFrameUpdateType(bool keyFrame, bool startsGoldenGroup)
            => keyFrame ? Av1FrameUpdateType.Key : startsGoldenGroup ? Av1FrameUpdateType.Golden : Av1FrameUpdateType.Last;

        /// <summary>
        /// Starts a golden group. Reference: set_baseline_gf_interval() and set_golden_update(). Real-time usage
        /// defaults to cyclic-refresh AQ, whose refresh percentage stays 0 while the quantizer is fixed, so the
        /// interval is FIXED_GF_INTERVAL_RT unless recent frames had little zero motion.
        /// </summary>
        /// <param name="averageFrameLowMotion">The running zero-motion percentage. Reference: rc->avg_frame_low_motion.</param>
        private void SetBaselineGoldenInterval(int averageFrameLowMotion)
        {
            int interval = FixedGoldenIntervalRealtime;
            if (averageFrameLowMotion != 0 && averageFrameLowMotion < 40)
            {
                interval = LowMotionGoldenInterval;
            }

            this.baselineGoldenInterval = interval;
            this.framesTillGoldenUpdateDue = interval;
            this.goldenFrameIndex = 0;
        }

        /// <summary>
        /// Cancels a golden refresh that ends a period at a high quantizer, or forces one halfway through a period
        /// at a low quantizer or in a high-motion frame. Reference: av1_adjust_gf_refresh_qp_one_pass_rt(), which
        /// runs after av1_encode_frame() and update_motion_stat() in constant-bitrate real-time coding.
        /// </summary>
        /// <param name="parent">The frame state, with the motion statistics of the coded frame.</param>
        void IAv1ReferenceRefreshControl.AdjustRefresh(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            if (frameHeader.IsIntra || !parent.SpeedSettings.UsesQuantizerGoldenRefresh || parent.HighSourceSad)
            {
                return;
            }

            int averageQuantizer = parent.AverageInterQuantizer;
            int qIndex = frameHeader.QuantizationParameters.BaseQIndex;
            bool allowGoldenUpdate = this.framesTillGoldenUpdateDue <= this.baselineGoldenInterval - 10;
            bool refresh;
            if (this.frameNumber - this.lastGoldenRefreshFrameNumber < FixedGoldenIntervalRealtime &&
                this.framesTillGoldenUpdateDue == 1 &&
                qIndex > averageQuantizer)
            {
                refresh = false;
            }
            else if (allowGoldenUpdate &&
                (qIndex < 87 * averageQuantizer / 100 ||
                (parent.AverageFrameLowMotion != 0 && parent.AverageFrameLowMotion < 20)))
            {
                refresh = true;
            }
            else
            {
                return;
            }

            parent.RefreshesGolden = refresh;
            this.SetBaselineGoldenInterval(parent.AverageFrameLowMotion);
            frameHeader.RefreshFrameFlags = refresh
                ? frameHeader.RefreshFrameFlags | (1U << GoldenSlot)
                : frameHeader.RefreshFrameFlags & ~(1U << GoldenSlot);
        }

        protected void CompleteFrameHeader()
        {
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;

            // av1_rc_postencode_update() skips frames that refresh GOLDEN.
            if (this.FrameHeader.FrameType != ObuFrameType.KeyFrame && !parent.RefreshesGolden)
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
            this.MotionField?.Dispose();
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
        private readonly int[] referenceBufferIds = new int[Av1Constants.ReferenceFrameCount];
        private readonly Av1EncoderMotionField.SavedMotionField?[] referenceMotionFields =
            new Av1EncoderMotionField.SavedMotionField?[Av1Constants.ReferenceFrameCount];

        private Av1EncoderReferencePool<byte> referencePool;

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

                // Reconstructed frames live in the reference slots, and a slot keeps its frame until a later frame
                // refreshes it.
                this.referencePool = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder,
                    qIndex,
                    this.MotionField);
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
            this.referencePool?.Dispose();
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
            Av1EncoderReferencePool<byte>.Entry current = this.referencePool.Acquire();
            Av1EncoderReferencePool<byte>.Entry? last = frameHeader.IsIntra ? null : this.referencePool.GetSlot(this.GetLastSlot());
            bool isScreenContent = PrepareFrame(
                this.Configuration,
                image,
                sourceRectangle,
                this.source.Frame,
                (last ?? current).Buffer.Frame,
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

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = this.FramesSinceGolden;
            parent.IsScreenContent = isScreenContent;
            parent.SpeedSettings = new(
                this.Speed, this.SequenceHeader.IsStillPicture, frameHeader.IsIntra, this.QIndex, image.Size);

            // Good-quality usage with the default objective delta-q mode and the temporal model enabled pads the
            // border. Real-time usage does not. Reference: the do_border_pad test in av1_encode().
            parent.BorderPad = !this.SequenceHeader.IsStillPicture && !parent.SpeedSettings.IsRealtime;

            this.ConfigureReferenceStructure(parent, this.averageSourceSad);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent));

            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                this.source,
                this.references,
                current.Buffer,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader);

            this.SymbolEncoder.SnapshotTo(current.Context);
            this.MotionField.SaveFrameMotionVectors(this.PictureBuffer.Picture, current.MotionField);
            this.CompleteFrameHeader();
            this.CompleteReferenceStructure();

            this.framesSinceKey++;
            if (this.previousSource is not null)
            {
                (this.source, this.previousSource) = (this.previousSource, this.source);
            }

            current.Buffer.Frame.ExtendBorders();
            this.referencePool.Refresh(current, frameHeader.RefreshFrameFlags);
        }

        /// <summary>
        /// Points each reference type at the frame in its slot and returns the context of the primary reference.
        /// </summary>
        /// <param name="parent">The frame state that receives the available references.</param>
        /// <returns>The primary reference context, or <see langword="null"/> for the default distributions.</returns>
        private Av1FrameEntropyContext? BindReferences(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                this.referenceMotionFields[reference] = this.referencePool.GetSlot((int)referenceFrameIndices[reference - 1])?.MotionField;
            }

            this.MotionField.Setup(this.SequenceHeader, frameHeader, this.referenceMotionFields);
            parent.AvailableReferenceMask = 0;
            if (frameHeader.IsIntra)
            {
                return null;
            }

            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                Av1EncoderReferencePool<byte>.Entry entry = this.referencePool.GetSlot((int)referenceFrameIndices[reference - 1])!;
                this.references[reference] = entry.Buffer.Frame;
                this.referenceBufferIds[reference] = entry.Id;
            }

            parent.AvailableReferenceMask = GetReferenceFrameFlags(this.referenceBufferIds, parent.SpeedSettings);
            return frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone
                ? null
                : this.referencePool.GetSlot((int)referenceFrameIndices[(int)frameHeader.PrimaryReferenceFrame])!.Context;
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
        private readonly int[] referenceBufferIds = new int[Av1Constants.ReferenceFrameCount];
        private readonly Av1EncoderMotionField.SavedMotionField?[] referenceMotionFields =
            new Av1EncoderMotionField.SavedMotionField?[Av1Constants.ReferenceFrameCount];

        private Av1EncoderReferencePool<ushort> referencePool;

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

                // Reconstructed frames live in the reference slots, and a slot keeps its frame until a later frame
                // refreshes it.
                this.referencePool = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder,
                    qIndex,
                    this.MotionField);
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
            this.referencePool?.Dispose();
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
            Av1EncoderReferencePool<ushort>.Entry current = this.referencePool.Acquire();
            Av1EncoderReferencePool<ushort>.Entry? last = frameHeader.IsIntra ? null : this.referencePool.GetSlot(this.GetLastSlot());
            bool isScreenContent = PrepareFrame(
                this.Configuration,
                image,
                sourceRectangle,
                this.source.Frame,
                (last ?? current).Buffer.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Speed,
                this.ConversionWorkspace);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.IsScreenContent = isScreenContent;
            parent.SpeedSettings = new(
                this.Speed, this.SequenceHeader.IsStillPicture, frameHeader.IsIntra, this.QIndex, image.Size);

            // Good-quality usage with the default objective delta-q mode and the temporal model enabled pads the
            // border. Real-time usage does not. Reference: the do_border_pad test in av1_encode().
            parent.BorderPad = !this.SequenceHeader.IsStillPicture && !parent.SpeedSettings.IsRealtime;

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

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = this.FramesSinceGolden;

            this.ConfigureReferenceStructure(parent, this.averageSourceSad);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent));

            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                this.source,
                this.references,
                current.Buffer,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader);

            this.SymbolEncoder.SnapshotTo(current.Context);
            this.MotionField.SaveFrameMotionVectors(this.PictureBuffer.Picture, current.MotionField);
            this.CompleteFrameHeader();
            this.CompleteReferenceStructure();
            this.framesSinceKey++;

            if (this.previousSource is not null)
            {
                (this.source, this.previousSource) = (this.previousSource, this.source);
            }

            current.Buffer.Frame.ExtendBorders();
            this.referencePool.Refresh(current, frameHeader.RefreshFrameFlags);
        }

        /// <summary>
        /// Points each reference type at the frame in its slot and returns the context of the primary reference.
        /// </summary>
        /// <param name="parent">The frame state that receives the available references.</param>
        /// <returns>The primary reference context, or <see langword="null"/> for the default distributions.</returns>
        private Av1FrameEntropyContext? BindReferences(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                this.referenceMotionFields[reference] = this.referencePool.GetSlot((int)referenceFrameIndices[reference - 1])?.MotionField;
            }

            this.MotionField.Setup(this.SequenceHeader, frameHeader, this.referenceMotionFields);
            parent.AvailableReferenceMask = 0;
            if (frameHeader.IsIntra)
            {
                return null;
            }

            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                Av1EncoderReferencePool<ushort>.Entry entry = this.referencePool.GetSlot((int)referenceFrameIndices[reference - 1])!;
                this.references[reference] = entry.Buffer.Frame;
                this.referenceBufferIds[reference] = entry.Id;
            }

            parent.AvailableReferenceMask = GetReferenceFrameFlags(this.referenceBufferIds, parent.SpeedSettings);
            return frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone
                ? null
                : this.referencePool.GetSlot((int)referenceFrameIndices[(int)frameHeader.PrimaryReferenceFrame])!.Context;
        }
    }
}

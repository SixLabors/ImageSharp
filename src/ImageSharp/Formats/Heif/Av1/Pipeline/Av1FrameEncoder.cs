// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Resize;
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
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// The base-two exponent used to align each frame dimension for output sizing. Rounding to 32 samples accounts
    /// for partial edge storage before the raw-plane size and all-intra expansion factor are calculated.
    /// </summary>
    private const int OutputAlignmentLog2 = 5;

    /// <summary>
    /// The length of the empty temporal delimiter OBU that opens each sample: its header and a zero size.
    /// </summary>
    private const int TemporalDelimiterLength = 2;

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

    /// <summary>
    /// The constant-quality level, on libaom's zero-through-63 quantizer scale, that the encoder keeps when libavif
    /// does not set one. Reference: the cq_level default of the encoder configuration.
    /// </summary>
    private const int DefaultConstantQualityLevel = 10;

    private enum FrameEncodingKind
    {
        StillColor,
        StillAlpha
    }

    /// <summary>
    /// Defines the sample-specific SIMD squared-error operation used by frame-level motion search.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    internal interface IGlobalMotionSearchOperator<TSample> :
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
    /// <param name="speed">The cpu-used tier; the encoding uses the libaom default tune.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(configuration, image, stream, colorConfig, qIndex, Av1EncoderOptions.Create(speed));

    /// <summary>
    /// Encodes one reduced-still-picture AV1 frame into a low-overhead OBU stream.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved native color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
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
            options,
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
    /// <param name="speed">The cpu-used tier; the encoding uses the libaom default tune.</param>
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
        => EncodeGridCell(configuration, image, sourceRectangle, cellSize, stream, colorConfig, qIndex, Av1EncoderOptions.Create(speed));

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
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeGridCell<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size cellSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(
            configuration,
            image,
            sourceRectangle,
            cellSize,
            stream,
            colorConfig,
            qIndex,
            options,
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
    /// <param name="speed">The cpu-used tier; the encoding uses the libaom default tune.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlpha<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        HeifEncodingSpeed speed)
        where TPixel : unmanaged, IPixel<TPixel>
        => EncodeAlpha(configuration, image, stream, colorConfig, qIndex, Av1EncoderOptions.Create(speed));

    /// <summary>
    /// Encodes one packed alpha channel as a reduced-still-picture monochrome AV1 frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlpha<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
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
            options,
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
    /// <param name="speed">The cpu-used tier; the encoding uses the libaom default tune.</param>
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
        => EncodeAlphaGridCell(configuration, image, sourceRectangle, cellSize, stream, colorConfig, qIndex, Av1EncoderOptions.Create(speed));

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
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlphaGridCell<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size cellSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(
            configuration,
            image,
            sourceRectangle,
            cellSize,
            stream,
            colorConfig,
            qIndex,
            options,
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
        => CreateColorSequenceEncoder(configuration, width, height, colorConfig, qIndex, Av1EncoderOptions.Create(speed, allIntra: false));

    /// <summary>
    /// Creates an encoder that retains reconstructed color frames for prediction by later samples in the sequence.
    /// </summary>
    public static SequenceEncoder CreateColorSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, options, false);

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
        => CreateAlphaSequenceEncoder(configuration, width, height, colorConfig, qIndex, Av1EncoderOptions.Create(speed, allIntra: false));

    /// <summary>
    /// Creates an encoder that retains reconstructed alpha frames for prediction by later samples in the sequence.
    /// </summary>
    public static SequenceEncoder CreateAlphaSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, options, true);

    private static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options,
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
            options,
            true);

        ObuFrameHeader frameHeader = CreateFrameHeader(
            sequenceHeader,
            qIndex,
            options,
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
                options,
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
                options,
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
        Av1EncoderOptions options,
        bool encodeAlpha)
    {
        if (colorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            return new ByteSequenceEncoder(configuration, width, height, colorConfig, qIndex, options, encodeAlpha);
        }

        return new HighBitDepthSequenceEncoder(configuration, width, height, colorConfig, qIndex, options, encodeAlpha);
    }

    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        ObuColorConfig colorConfig,
        Av1EncoderOptions options,
        bool isStillPicture)
    {
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        Av1EncoderSpeedSettings speedSettings = new(
            options.Speed, isStillPicture, intraFrame: true, Av1FrameUpdateType.Key, qIndex: 0, new Size(width, height));

        ObuSequenceProfile sequenceProfile = colorConfig.BitDepth == Av1BitDepth.TwelveBit ||
            colorFormat == Av1ColorFormat.Yuv422
                ? ObuSequenceProfile.Professional
                : colorFormat == Av1ColorFormat.Yuv444
                    ? ObuSequenceProfile.High
                    : ObuSequenceProfile.Main;

        // Superblock geometry follows coding options and resolution. Reference: av1_select_sb_size(). Real-time
        // coding uses 128x128 only above 720p. Otherwise small frames use 64x64 above options zero, and the fastest
        // still-image mode also uses it below 4K.
        // Variance Boost only supports 64x64 superblocks, and so do spatial layers.
        int minimumDimension = Math.Min(width, height);
        bool use128x128Superblock = options.DeltaQMode != Av1DeltaQMode.VarianceBoost && options.LayerCount == 1 && (speedSettings.IsRealtime
            ? minimumDimension > 720
            : !(options.Speed >= HeifEncodingSpeed.Level1 && minimumDimension <= 480) &&
                !(isStillPicture && options.Speed >= HeifEncodingSpeed.Level9 && minimumDimension < 2160));

        // A layered image lists one operating point per layer. Operating point i decodes the spatial layers from 0 up
        // to the last layer minus i, in the single temporal layer, so operating point 0 decodes every layer. Each frame
        // then carries its layer in an OBU extension header. Every operating point gets the level of the frame size.
        // Reference: the operating_points_cnt_minus_1 setup of av1_change_config_seq(), av1_set_svc_seq_params() at
        // the end of init_seq_coding_tools(), and set_bitstream_level_tier().
        int sequenceLevelIndex = GetSequenceLevelIndex(width, height, LevelFrameRate);
        int layerCount = options.LayerCount;
        ObuOperatingPoint[] operatingPoints = new ObuOperatingPoint[layerCount];
        for (int i = 0; i < operatingPoints.Length; i++)
        {
            operatingPoints[i] = new ObuOperatingPoint
            {
                SequenceLevelIndex = sequenceLevelIndex,
                Idc = layerCount > 1 ? (~(~0U << (layerCount - i)) << 8) | 1U : 0U
            };
        }

        return new ObuSequenceHeader
        {
            IsStillPicture = isStillPicture,
            IsReducedStillPictureHeader = isStillPicture,
            SequenceProfile = sequenceProfile,
            OperatingPoint = operatingPoints,
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

            // Good quality speed 6 and above turns masked compound off for 720p and larger frames before the
            // sequence locks. Reference: disable_masked_comp in set_good_speed_feature_framesize_dependent(), applied
            // to enable_masked_compound by av1_set_speed_features_framesize_dependent().
            EnableMaskedCompound = !isStillPicture &&
                !(!speedSettings.IsRealtime && options.Speed >= HeifEncodingSpeed.Level6 && minimumDimension >= 720),
            EnableInterIntraCompound = !isStillPicture && speedSettings.EnableInterIntraCompound,

            // A sequence enables temporal motion vectors and warped motion, and the frame header decides whether
            // each frame uses them. Distance-weighted compound follows the options features. Reference: the
            // order_hint_info and tool flags that init_seq_coding_tools() sets, with
            // DEFAULT_EXPLICIT_ORDER_HINT_BITS, and the options-feature adjustments that follow them.
            EnableWarpedMotion = !isStillPicture,
            OrderHintInfo = new ObuOrderHintInfo
            {
                EnableOrderHint = !isStillPicture,
                EnableJointCompound = !isStillPicture && speedSettings.UseDistanceWeightedCompound,
                EnableReferenceFrameMotionVectors = !isStillPicture,
                OrderHintBits = isStillPicture ? 0 : 7
            },
            EnableSuperResolution = false,

            // All-intra usage turns CDEF off by default because it blurs images; the image tune turns it back on
            // with adaptive strengths. Reference: the enable_cdef assignment of init_seq_coding_tools().
            EnableCdef = options.CdefControl != Av1CdefControl.None,
            EnableRestoration = speedSettings.EnableRestoration && options.EnableRestoration,
            AreFilmGrainingParametersPresent = options.HasFilmGrain,
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

    /// <summary>
    /// Creates the uniform tile layout for the requested tile counts. Reference: the uniform-spacing branch of
    /// set_tile_info(), with av1_get_tile_limits(), av1_calculate_tile_cols() and av1_calculate_tile_rows().
    /// </summary>
    private static ObuTileGroupHeader CreateTileGroupHeader(
        int modeInfoColumnCount,
        int modeInfoRowCount,
        int superblockSizeLog2,
        int requestedTileColumnsLog2,
        int requestedTileRowsLog2)
    {
        int superblockShift = superblockSizeLog2 - Av1Constants.ModeInfoSizeLog2;
        int superblockColumns = Av1Math.DivideLog2Ceiling(modeInfoColumnCount, superblockShift);
        int superblockRows = Av1Math.DivideLog2Ceiling(modeInfoRowCount, superblockShift);
        int maximumTileWidth = Av1Constants.MaxTileWidth >> superblockSizeLog2;
        int maximumTileArea = Av1Constants.MaxTileArea >> (2 * superblockSizeLog2);
        int minimumTileColumnsLog2 = ObuReader.TileLog2(maximumTileWidth, superblockColumns);
        int maximumTileColumnsLog2 = ObuReader.TileLog2(1, Math.Min(superblockColumns, Av1Constants.MaxTileColumnCount));
        int maximumTileRowsLog2 = ObuReader.TileLog2(1, Math.Min(superblockRows, Av1Constants.MaxTileRowCount));
        int minimumTileCountLog2 = Math.Max(
            minimumTileColumnsLog2,
            ObuReader.TileLog2(maximumTileArea, superblockColumns * superblockRows));

        // The encoder's own column minimum takes one more column split than the bitstream minimum when the frame
        // is exactly a multiple of the widest tile.
        int encoderMinimumTileColumnsLog2 = 0;
        while ((maximumTileWidth << encoderMinimumTileColumnsLog2) <= superblockColumns)
        {
            encoderMinimumTileColumnsLog2++;
        }

        int tileColumnCountLog2 = Math.Max(requestedTileColumnsLog2, minimumTileColumnsLog2);
        tileColumnCountLog2 = Math.Max(tileColumnCountLog2, encoderMinimumTileColumnsLog2);
        tileColumnCountLog2 = Math.Min(tileColumnCountLog2, maximumTileColumnsLog2);

        int minimumTileRowsLog2 = Math.Max(minimumTileCountLog2 - tileColumnCountLog2, 0);
        int tileRowCountLog2 = Math.Max(requestedTileRowsLog2, minimumTileRowsLog2);
        tileRowCountLog2 = Math.Min(tileRowCountLog2, maximumTileRowsLog2);
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
        Av1EncoderOptions options,
        ObuFrameType frameType)
    {
        int width = sequenceHeader.MaxFrameWidth;
        int height = sequenceHeader.MaxFrameHeight;
        int modeInfoColumnCount = 2 * ((width + 7) >> 3);
        int modeInfoRowCount = 2 * ((height + 7) >> 3);
        ObuTileGroupHeader tiles = CreateTileGroupHeader(
            modeInfoColumnCount,
            modeInfoRowCount,
            sequenceHeader.SuperblockSizeLog2,
            options.TileColumnsLog2,
            options.TileRowsLog2);

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

        ConfigureFrameHeader(frameHeader, sequenceHeader, qIndex, options, frameType);
        return frameHeader;
    }

    /// <summary>
    /// Restores every frame-varying encoder field while retaining the fixed geometry and syntax object graph.
    /// </summary>
    private static void ConfigureFrameHeader(
        ObuFrameHeader frameHeader,
        ObuSequenceHeader sequenceHeader,
        int qIndex,
        Av1EncoderOptions options,
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
        frameHeader.AllowScreenContentTools = false;
        frameHeader.AllowIntraBlockCopy = false;
        frameHeader.ForceIntegerMotionVector = false;
        if (frameType == ObuFrameType.InterFrame)
        {
            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            referenceFrameIndices.Clear();
            referenceFrameIndices[(int)Av1ReferenceFrameType.Golden - 1] = 7;
            frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
        }

        ApplyFrameQuantizer(frameHeader, sequenceHeader, qIndex, options);
    }

    /// <summary>
    /// Sets the quantizer of a frame and every frame field that depends on it. Reference: av1_set_quantizer(), with
    /// av1_pick_and_set_high_precision_mv() and the reference mode choice of av1_encode_frame(), which read the
    /// final quantizer of the frame.
    /// </summary>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoder options.</param>
    private static void ApplyFrameQuantizer(
        ObuFrameHeader frameHeader,
        ObuSequenceHeader sequenceHeader,
        int qIndex,
        Av1EncoderOptions options)
    {
        frameHeader.TransformMode = qIndex == 0
            ? Av1TransformMode.Only4x4
            : Av1TransformMode.Select;

        frameHeader.AllowHighPrecisionMotionVector = false;
        if (frameHeader.FrameType == ObuFrameType.InterFrame)
        {
            // Inter vectors use fractional-motion syntax at the precision selected for the frame quantizer, unless
            // the frame forces integer vectors. Neither choice depends on the frame's update type. Reference:
            // av1_set_high_precision_mv() with cur_frame_force_integer_mv.
            Av1EncoderSpeedSettings speedSettings = new(
                options.Speed,
                allIntra: false,
                intraFrame: false,
                Av1FrameUpdateType.Last,
                qIndex,
                new Size(frameHeader.FrameSize.SuperResolutionUpscaledWidth, frameHeader.FrameSize.FrameHeight));

            frameHeader.AllowHighPrecisionMotionVector =
                speedSettings.AllowHighPrecisionMotionVector && !frameHeader.ForceIntegerMotionVector;

            // Real-time usage selects the reference mode per frame only while estimated compound prediction is
            // enabled, and otherwise codes single references. Reference: the frame_parameter_update and
            // use_comp_ref_nonrd branches of av1_encode_frame().
            frameHeader.ReferenceMode = speedSettings.IsRealtime && !speedSettings.UseEstimatedCompound
                ? ObuReferenceMode.SingleReference
                : ObuReferenceMode.ReferenceModeSelect;
        }

        Av1FrameQuantizer.SetQuantizer(
            frameHeader.QuantizationParameters,
            sequenceHeader.ColorConfig,
            qIndex,
            options,
            sequenceHeader.IsStillPicture);

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
        Av1EncoderOptions options,
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
        using ObuWriter obuWriter = new(configuration);

        // The frame starts at the requested quantizer, which a bit budget replaces after the screen content decision.
        int requestedQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        ScreenContentDecision decision = default;
        bool isScreenContent = PrepareFrame(
            configuration,
            image,
            sourceRectangle,
            source.Frame,
            reconstruction.Frame,
            sequenceHeader,
            frameHeader,
            options,
            encodeAlpha,
            ref decision);

        // The coefficient contexts start from the final quantizer.
        using Av1SymbolEncoder symbolEncoder = new(
            configuration,
            tileBufferLength,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        using Av1EncoderBlockWorkspace blockWorkspace = new(
            configuration,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: frameHeader.AllowIntraBlockCopy,
            sequenceHeader.SuperblockSize);

        Av1EncoderSpeedSettings speedSettings = new(
            options.Speed,
            sequenceHeader.IsStillPicture,
            frameHeader.IsIntra,
            Av1FrameUpdateType.Key,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameSize);

        Av1MotionSearchSettings motionSettings = new(
            options.Speed,
            sequenceHeader.IsStillPicture,
            frameSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            isScreenContent,
            options.Tuning);

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
        picture.Picture.Parent.EncodingSpeed = options.Speed;
        picture.Picture.Parent.EncoderOptions = options;
        picture.Picture.Parent.ConstantQualityIndex = GetConstantQualityLevel(options, requestedQIndex);
        picture.Picture.Parent.SpeedSettings = speedSettings;

        // libavif gives every image time stamp 0. Reference: the aom_codec_encode() call of aomCodecEncodeImage().
        Av1FilmGrainState.Create(options.FilmGrainPreset, options.FilmGrainTable, sequenceHeader.ColorConfig)?.PrepareFrame(frameHeader, 0);
        Encode(
            obuWriter,
            stream,
            sequenceHeader,
            frameHeader,
            picture.Picture,
            source,
            default,
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
        Av1EncoderOptions options,
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
        using ObuWriter obuWriter = new(configuration);

        // The frame starts at the requested quantizer, which a bit budget replaces after the screen content decision.
        int requestedQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        ScreenContentDecision decision = default;
        bool isScreenContent = PrepareFrame(
            configuration,
            image,
            sourceRectangle,
            source.Frame,
            reconstruction.Frame,
            sequenceHeader,
            frameHeader,
            options,
            encodeAlpha,
            ref decision);

        // The coefficient contexts start from the final quantizer.
        using Av1SymbolEncoder symbolEncoder = new(
            configuration,
            tileBufferLength,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        using Av1EncoderBlockWorkspace blockWorkspace = new(
            configuration,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: frameHeader.AllowIntraBlockCopy,
            sequenceHeader.SuperblockSize);

        Av1EncoderSpeedSettings speedSettings = new(
            options.Speed,
            sequenceHeader.IsStillPicture,
            frameHeader.IsIntra,
            Av1FrameUpdateType.Key,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameSize);

        Av1MotionSearchSettings motionSettings = new(
            options.Speed,
            sequenceHeader.IsStillPicture,
            frameSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            isScreenContent,
            options.Tuning);

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
        picture.Picture.Parent.EncodingSpeed = options.Speed;
        picture.Picture.Parent.EncoderOptions = options;
        picture.Picture.Parent.ConstantQualityIndex = GetConstantQualityLevel(options, requestedQIndex);
        picture.Picture.Parent.SpeedSettings = speedSettings;

        // libavif gives every image time stamp 0. Reference: the aom_codec_encode() call of aomCodecEncodeImage().
        Av1FilmGrainState.Create(options.FilmGrainPreset, options.FilmGrainTable, sequenceHeader.ColorConfig)?.PrepareFrame(frameHeader, 0);
        Encode(
            obuWriter,
            stream,
            sequenceHeader,
            frameHeader,
            picture.Picture,
            source,
            default,
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
        Av1EncoderOptions options,
        bool encodeAlpha,
        ref ScreenContentDecision decision)
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
            options,
            ref decision);
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
        Av1EncoderOptions options,
        Av1EncoderConversionWorkspace conversionWorkspace,
        ref ScreenContentDecision decision)
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
            options,
            ref decision);
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
        Av1EncoderOptions options,
        ref ScreenContentDecision decision)
    {
        if (frameHeader.IsIntra)
        {
            DecideScreenContent(source, sequenceHeader, options, ref decision);
        }

        SelectStillImageQuantizer(sequenceHeader, frameHeader, options, decision);
        ApplyScreenContentTools(sequenceHeader, frameHeader, options, new Size(source.Width, source.Height), decision);
        return decision.IsScreenContent;
    }

    /// <summary>
    /// Classifies the eight-bit source of an intra frame as screen content or not. Inter frames keep the decision of
    /// the last intra frame. Reference: av1_set_screen_content_options(), which reads the unfiltered source.
    /// </summary>
    /// <param name="source">The unfiltered source frame.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="options">The encoder options.</param>
    /// <param name="decision">Receives the decision.</param>
    private static void DecideScreenContent(
        Av1EncoderFrame<byte> source,
        ObuSequenceHeader sequenceHeader,
        Av1EncoderOptions options,
        ref ScreenContentDecision decision)
    {
        decision.IsScreenContent = Av1ScreenContentDetector.SetScreenContentOptions(
            source,
            sequenceHeader.IsStillPicture,
            options.Speed,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        decision.AllowScreenContentTools = allowScreenContentTools;
        decision.AllowIntraBlockCopy = allowIntraBlockCopy;
    }

    /// <summary>
    /// Returns the constant-quality level of the encoder. libavif sets it to the requested quantizer in the
    /// constant-quality and constrained-quality modes; the bit-rate modes keep libaom's default level of 10.
    /// Reference: the AOME_SET_CQ_LEVEL control of aomCodecEncodeImage() and the cq_level default of the encoder
    /// configuration.
    /// </summary>
    /// <param name="options">The encoder options.</param>
    /// <param name="requestedQIndex">The quantizer index of the requested quality.</param>
    /// <returns>The constant-quality level as a quantizer index. Reference: rc_cfg.cq_level.</returns>
    private static int GetConstantQualityLevel(Av1EncoderOptions options, int requestedQIndex)
        => options.UsesConstantQualityLevel ? requestedQIndex : Av1QuantizationLookup.GetQIndex(DefaultConstantQualityLevel);

    /// <summary>
    /// Sets the quantizer of a still image that codes against a bit budget. The rate model reads the screen content
    /// decision, and the intra block copy decision and the quantizer-dependent speed features read the quantizer it
    /// picks. A constant-quality still image keeps the requested quantizer. Reference: av1_set_screen_content_options()
    /// in av1_encode_strategy() before av1_rc_pick_q_and_bounds() in encode_without_recode().
    /// </summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header, which receives the quantizer.</param>
    /// <param name="options">The encoder options.</param>
    /// <param name="decision">The screen content decision of the image.</param>
    private static void SelectStillImageQuantizer(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1EncoderOptions options,
        ScreenContentDecision decision)
    {
        if (!sequenceHeader.IsStillPicture || !options.UsesBitBudget)
        {
            return;
        }

        int qIndex = Av1RateControl.GetStillImageQIndex(
            frameHeader.FrameSize.FrameWidth,
            frameHeader.FrameSize.FrameHeight,
            sequenceHeader.ColorConfig.BitDepth,
            options.Speed,
            options.RateControlMode,
            Av1QuantizationLookup.GetQIndex(options.MinimumQuantizer),
            Av1QuantizationLookup.GetQIndex(options.MaximumQuantizer),
            GetConstantQualityLevel(options, frameHeader.QuantizationParameters.BaseQIndex),
            decision.IsScreenContent);

        ApplyFrameQuantizer(frameHeader, sequenceHeader, qIndex, options);
    }

    /// <summary>
    /// Writes the screen content decision to the frame header. Inter frames keep the tools of the last intra frame
    /// and never copy blocks. Reference: the is_intra_frame test around av1_set_screen_content_options() in
    /// av1_encode_strategy().
    /// </summary>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header to configure.</param>
    /// <param name="options">The encoder options.</param>
    /// <param name="sourceSize">The source dimensions.</param>
    /// <param name="decision">The screen content decision.</param>
    private static void ApplyScreenContentTools(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1EncoderOptions options,
        Size sourceSize,
        ScreenContentDecision decision)
    {
        frameHeader.AllowScreenContentTools = decision.AllowScreenContentTools;
        if (!frameHeader.IsIntra)
        {
            frameHeader.AllowIntraBlockCopy = false;
            return;
        }

        Av1MotionSearchSettings motionSettings = new(
            options.Speed,
            sequenceHeader.IsStillPicture,
            sourceSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            decision.IsScreenContent,
            options.Tuning);

        frameHeader.AllowIntraBlockCopy =
            motionSettings.AllowIntraBlockCopy &&
            frameHeader.AllowScreenContentTools &&
            decision.AllowIntraBlockCopy;
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
        Av1EncoderOptions options,
        bool encodeAlpha,
        ref ScreenContentDecision decision)
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
            options,
            ref decision);
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
        Av1EncoderOptions options,
        Av1EncoderConversionWorkspace conversionWorkspace,
        ref ScreenContentDecision decision)
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
            options,
            ref decision);
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
        Av1EncoderOptions options,
        ref ScreenContentDecision decision)
    {
        if (frameHeader.IsIntra)
        {
            DecideScreenContent(source, sequenceHeader, options, ref decision);
        }

        SelectStillImageQuantizer(sequenceHeader, frameHeader, options, decision);
        ApplyScreenContentTools(sequenceHeader, frameHeader, options, new Size(source.Width, source.Height), decision);
        return decision.IsScreenContent;
    }

    /// <summary>
    /// Classifies the high-bit-depth source of an intra frame as screen content or not. Reference:
    /// av1_set_screen_content_options(), which reads the unfiltered source.
    /// </summary>
    /// <param name="source">The unfiltered source frame.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="options">The encoder options.</param>
    /// <param name="decision">Receives the decision.</param>
    private static void DecideScreenContent(
        Av1EncoderFrame<ushort> source,
        ObuSequenceHeader sequenceHeader,
        Av1EncoderOptions options,
        ref ScreenContentDecision decision)
    {
        decision.IsScreenContent = Av1ScreenContentDetector.SetScreenContentOptions(
            source,
            sequenceHeader.IsStillPicture,
            options.Speed,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        decision.AllowScreenContentTools = allowScreenContentTools;
        decision.AllowIntraBlockCopy = allowIntraBlockCopy;
    }

    /// <summary>
    /// Codes one eight-bit frame and writes its OBUs. Reference: av1_encode() with av1_pack_bitstream().
    /// </summary>
    /// <param name="obuWriter">The OBU writer.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames the motion search reads, indexed by prediction reference identifier: each reference, or its copy
    /// resized to the size of the coded frame.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="coefficients">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="symbolEncoder">The symbol encoder of the frame.</param>
    /// <param name="writeSequenceHeader">Whether a sequence header OBU precedes the frame.</param>
    /// <param name="writeTemporalDelimiter">Whether a temporal delimiter OBU precedes the frame.</param>
    private static void Encode(
        ObuWriter obuWriter,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1PictureControlSet picture,
        Av1EncoderFrameBuffer<byte> source,
        ReadOnlyMemory<Av1EncoderFrame<byte>> references,
        ReadOnlyMemory<Av1EncoderFrame<byte>> searchReferences,
        Av1EncoderFrameBuffer<byte> reconstruction,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        Av1SymbolEncoder symbolEncoder,
        bool writeSequenceHeader,
        bool writeTemporalDelimiter = true)
    {
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            references,
            searchReferences,
            reconstruction.Frame,
            picture,
            coefficients,
            tileWorkspace,
            blockWorkspace);

        if (writeSequenceHeader)
        {
            obuWriter.WriteSequenceFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
        else if (writeTemporalDelimiter)
        {
            obuWriter.WriteFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
        else
        {
            obuWriter.WriteFrameWithoutDelimiter(stream, sequenceHeader, frameHeader, tileWriter);
        }
    }

    /// <summary>
    /// Codes one frame of more than eight bits and writes its OBUs. Reference: av1_encode() with av1_pack_bitstream().
    /// </summary>
    /// <param name="obuWriter">The OBU writer.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames the motion search reads, indexed by prediction reference identifier: each reference, or its copy
    /// resized to the size of the coded frame.
    /// </param>
    /// <param name="reconstruction">The reconstructed frame updated during encoding.</param>
    /// <param name="coefficients">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="tileWorkspace">The retained tile, superblock, and entropy cursor graph.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    /// <param name="symbolEncoder">The symbol encoder of the frame.</param>
    /// <param name="writeSequenceHeader">Whether a sequence header OBU precedes the frame.</param>
    /// <param name="writeTemporalDelimiter">Whether a temporal delimiter OBU precedes the frame.</param>
    private static void Encode(
        ObuWriter obuWriter,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1PictureControlSet picture,
        Av1EncoderFrameBuffer<ushort> source,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> references,
        ReadOnlyMemory<Av1EncoderFrame<ushort>> searchReferences,
        Av1EncoderFrameBuffer<ushort> reconstruction,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        Av1SymbolEncoder symbolEncoder,
        bool writeSequenceHeader,
        bool writeTemporalDelimiter = true)
    {
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            references,
            searchReferences,
            reconstruction.Frame,
            picture,
            coefficients,
            tileWorkspace,
            blockWorkspace);

        if (writeSequenceHeader)
        {
            obuWriter.WriteSequenceFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
        else if (writeTemporalDelimiter)
        {
            obuWriter.WriteFrame(stream, sequenceHeader, frameHeader, tileWriter);
        }
        else
        {
            obuWriter.WriteFrameWithoutDelimiter(stream, sequenceHeader, frameHeader, tileWriter);
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
    /// Measures source changes over 64x64 blocks and retains the block errors used by subsequent mode decisions.
    /// Reference: av1_rc_scene_detection_onepass_rt().
    /// </summary>
    /// <typeparam name="TSample">The source sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific error operations.</typeparam>
    /// <param name="source">The current bordered source planes.</param>
    /// <param name="previousSource">The preceding bordered source planes.</param>
    /// <param name="gridSize">
    /// The luma size whose 64x64 blocks are measured from the top-left corner of the sources: the size the encoder
    /// holds before it sets the size of the frame. Reference: the cm->mi_params of av1_rc_scene_detection_onepass_rt().
    /// </param>
    /// <param name="parent">
    /// The frame analysis state and the block-error storage, which is empty when the block errors are not kept.
    /// </param>
    /// <param name="framesSinceKey">The number of completed frames since the last key frame.</param>
    /// <param name="averageSourceSad">The running average of source changes.</param>
    private static void AnalyzeTemporalSource<TSample, TOperator>(
        Av1EncoderFrame<TSample>.PlanarView source,
        Av1EncoderFrame<TSample>.PlanarView previousSource,
        Size gridSize,
        Av1PictureParentControlSet parent,
        int framesSinceKey,
        ref ulong averageSourceSad)
        where TSample : unmanaged
        where TOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        Av1PlaneRegion<TSample> current = source.GetPlane(Av1Plane.Y);
        Av1PlaneRegion<TSample> previous = previousSource.GetPlane(Av1Plane.Y);
        Span<ulong> blockErrors = parent.SourceBlockSad.Span;
        int columns = (gridSize.Width + 63) >> 6;
        int rows = (gridSize.Height + 63) >> 6;
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

                if (!blockErrors.IsEmpty)
                {
                    blockErrors[(row * columns) + column] = sad;
                }

                total += sad;
                unchanged += sad == 0 ? 1 : 0;
            }
        }

        int count = rows * columns;
        ulong average = total / (ulong)count;

        // Real-time speed features raise the minimum change unless the content option is screen, which is not the
        // detected screen content type and which libavif never sets; the frame rate of the 1/30 time base and
        // duration 1 that libavif passes is 30, above the rate that lowers it. Reference: higher_thresh_scene_detection
        // in av1_rc_scene_detection_onepass_rt().
        const uint minimum = 100000U;
        const int multiplier = 6;
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
    internal abstract partial class SequenceEncoder : IDisposable, IAv1ReferenceRefreshControl
    {
        /// <summary>
        /// The number of rotating slots that hold LAST and ALTREF. Reference: sh in
        /// av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        private const int RotatingSlotCount = 6;

        /// <summary>
        /// The border, in luma samples, of a reference larger than the current frame, to which the scaled prediction
        /// clamps its source positions. Reference: AOM_BORDER_IN_PIXELS, which av1_scale_references() gives such a
        /// reference and AOM_LEFT_TOP_MARGIN_SCALED() clamps to.
        /// </summary>
        protected const int ScaledReferenceBorder = Av1ReferenceScale.ClampBorder;

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
        /// The largest golden interval of real-time coding. Reference: MAX_GF_INTERVAL_RT.
        /// </summary>
        private const int MaximumGoldenIntervalRealtime = 160;

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
        /// The constant-bitrate model of a real-time sequence, or <see langword="null"/> for good-quality coding.
        /// </summary>
        private readonly Av1RateControl? rateControl;

        /// <summary>
        /// Whether the last coded frame was intra only, which the frame type of the encoder still holds while the next
        /// frame is set up. Reference: frame_is_intra_only(cm) before av1_encode() sets the new frame type.
        /// </summary>
        private bool previousFrameIntra = true;

        /// <summary>
        /// Whether the last coded frame refreshed GOLDEN, which the refresh flags of the encoder still hold when cyclic
        /// refresh sets up the next frame. Reference: cpi->refresh_frame.golden_frame before
        /// av1_configure_buffer_updates() sets the flags of the new frame.
        /// </summary>
        private bool previousRefreshesGolden;

        /// <summary>
        /// The one-pass rate model of a good-quality sequence without lookahead under a bit budget, which allocates
        /// the bits of each golden group, or <see langword="null"/> for constant-quality and real-time coding.
        /// </summary>
        private readonly Av1RateControl? groupRateControl;

        /// <summary>
        /// The cyclic refresh of a real-time sequence that uses it, or <see langword="null"/>. Reference:
        /// cpi->cyclic_refresh.
        /// </summary>
        private readonly Av1CyclicRefresh? cyclicRefresh;

        /// <summary>
        /// The noise estimate of a real-time sequence with cyclic refresh, or <see langword="null"/>. Reference:
        /// cpi->noise_estimate.
        /// </summary>
        private readonly Av1NoiseEstimate? noiseEstimate;

        /// <summary>
        /// The film grain the sequence signals, or <see langword="null"/>.
        /// </summary>
        private readonly Av1FilmGrainState? filmGrain;

        /// <summary>
        /// The frames the real-time sequence coded, which key frames do not restart. Reference:
        /// svc.num_encoded_top_layer.
        /// </summary>
        private int codedFrameCount;

        /// <summary>
        /// The requested quantizer index, which constant-quality coding keeps for inter frames. A layered image sets it
        /// for each layer. Reference: cq_level.
        /// </summary>
        private int constantQualityIndex;

        /// <summary>
        /// The number of layers of a layered image coded so far.
        /// </summary>
        private int codedLayerCount;

        /// <summary>
        /// The coded size of the current frame: the sequence size, or the size of a scaled layer. Reference: cm->width
        /// and cm->height, which av1_set_frame_size() sets from resize_pending_params.
        /// </summary>
        private Size frameSize;

        /// <summary>
        /// The frame size the encoder holds before it sets the size of the next frame: the size of the frame coded
        /// last, or the sequence size before the first frame or after a reconfiguration. The SSIM factors and the
        /// real-time scene detection of the next frame measure over this size. Reference: cm->width, cm->height and
        /// mi_params, which av1_change_config() resets to the configured size and av1_setup_frame_size() replaces.
        /// </summary>
        private Size presetupFrameSize;

        /// <summary>
        /// The size of the frame coded last, or the sequence size before the first frame. Reference:
        /// rc->prev_coded_width and rc->prev_coded_height.
        /// </summary>
        private Size codedFrameSize;

        /// <summary>
        /// Whether libaom holds a fixed resize mode for the current frame. A scaled layer after the first sets it on
        /// the running encoder, and the next quality change rebuilds the configuration without it. Reference: the
        /// resize_mode that av1_set_internal_size() sets from ctrl_set_scale_mode() once the sequence is locked, and
        /// set_encoder_config() in update_encoder_cfg().
        /// </summary>
        private bool resizesFixed;

        /// <summary>
        /// The rate multiplier scaling factors of the SSIM and image tunes, laid out for the largest grid, which keep
        /// the entries a smaller frame does not measure. Reference: cpi->ssim_rdmult_scaling_factors, which
        /// alloc_compressor_data() allocates once.
        /// </summary>
        private double[]? ssimRateMultiplierFactors;

        /// <summary>
        /// Whether the current frame is a layer after the first, which libavif codes with the flags that keep GOLDEN
        /// and the alternate references out of the frame. Reference: the AOM_EFLAG_NO_REF_GF, AOM_EFLAG_NO_REF_ARF,
        /// AOM_EFLAG_NO_REF_BWD, AOM_EFLAG_NO_REF_ARF2, AOM_EFLAG_NO_UPD_GF and AOM_EFLAG_NO_UPD_ARF flags of
        /// aomCodecEncodeImage().
        /// </summary>
        private bool usesLayerFlags;

        /// <summary>
        /// The running average quantizer index of the ordinary inter frames of constant-quality coding, which starts
        /// in the middle of the allowed range. Reference: p_rc->avg_frame_qindex[INTER_FRAME] from av1_rc_init().
        /// </summary>
        private int averageInterQIndex;

        /// <summary>
        /// The frames left before the next key frame of good-quality coding without lookahead. Reference:
        /// rc->frames_to_key.
        /// </summary>
        private int framesToKey;

        /// <summary>
        /// Whether the key frame interval placed the current key frame. Reference: p_rc->this_key_frame_forced.
        /// </summary>
        private bool thisKeyFrameForced;

        /// <summary>
        /// The quantizer index of the last key frame or golden update of good-quality coding, or a lower index of a
        /// later frame. Reference: p_rc->last_boosted_qindex.
        /// </summary>
        private int lastBoostedQIndex;

        /// <summary>
        /// Whether the current real-time golden group ends at the next key frame. Reference:
        /// p_rc->constrained_gf_group from set_baseline_gf_interval().
        /// </summary>
        private bool isConstrainedGoldenGroup;

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
        /// <summary>
        /// The initial OBMC probability of each frame update type and block size. Reference: default_obmc_probs.
        /// </summary>
        private static readonly int[] DefaultObmcProbabilities =
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 106, 90, 90, 97, 67, 59, 70, 28, 30, 38, 16, 16, 16, 0, 0, 44, 50, 26, 25,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 98, 93, 97, 68, 82, 85, 33, 30, 33, 16, 16, 16, 16, 0, 0, 43, 37, 26, 16,
            0, 0, 0, 91, 80, 76, 78, 55, 49, 24, 16, 16, 16, 16, 16, 16, 0, 0, 29, 45, 16, 38,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 103, 89, 89, 89, 62, 63, 76, 34, 35, 32, 19, 16, 16, 0, 0, 49, 55, 29, 19
        ];

        private readonly int[] warpedProbabilities = [64, 64, 64, 64, 64, 64, 64];
        private readonly int[] obmcProbabilities = (int[])DefaultObmcProbabilities.Clone();

        /// <summary>
        /// The reference structure of good-quality frames coded without lookahead.
        /// </summary>
        private readonly Av1GoodQualityReferenceStructure goodQualityStructure = new();

        /// <summary>
        /// The global motion models of the frame in each reference slot, seven per slot. A key frame refreshes every
        /// slot before any frame reads them. Reference: the global_motion of each RefCntBuffer.
        /// </summary>
        private readonly Av1GlobalMotionParameters[] slotGlobalMotion =
            new Av1GlobalMotionParameters[Av1Constants.ReferenceFrameCount * Av1Constants.ReferencesPerFrame];

        protected SequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            Av1EncoderOptions options,
            bool encodeAlpha,
            bool usesHighBitDepth)
        {
            this.Configuration = configuration;
            this.frameSize = new Size(width, height);
            this.presetupFrameSize = new Size(width, height);
            this.codedFrameSize = new Size(width, height);
            this.SequenceHeader = CreateSequenceHeader(width, height, colorConfig, options, false);
            this.filmGrain = Av1FilmGrainState.Create(options.FilmGrainPreset, options.FilmGrainTable, colorConfig);
            this.QIndex = qIndex;
            this.constantQualityIndex = qIndex;
            this.averageInterQIndex = (Av1QuantizationLookup.GetQIndex(options.MaximumQuantizer) +
                Av1QuantizationLookup.GetQIndex(options.MinimumQuantizer)) / 2;

            this.Options = options;
            this.TileBufferLength = GetTileBufferLength(width, height, colorConfig);
            this.EncodeAlpha = encodeAlpha;
            this.FrameHeader = CreateFrameHeader(
                this.SequenceHeader,
                qIndex,
                options,
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
                    options.Speed,
                    this.SequenceHeader.IsStillPicture,
                    new Size(width, height),
                    qIndex,
                    this.FrameHeader.IsIntra,
                    screenContent: true,
                    options.Tuning);

                bool allocateIntraBlockCopySearch =
                    allocateScreenContentState &&
                    motionSettings.AllowIntraBlockCopy;

                // Sequence geometry and maximum tool capacity are fixed before the first sample. Reusing this owner
                // avoids renting the complete mode grid and optional screen-content index for every frame.
                Av1EncoderSpeedSettings speedSettings = new(
                    options.Speed,
                    this.SequenceHeader.IsStillPicture,
                    this.FrameHeader.IsIntra,
                    Av1FrameUpdateType.Key,
                    qIndex,
                    new Size(width, height));

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

                this.PictureBuffer.Picture.Parent.EncodingSpeed = options.Speed;
                this.PictureBuffer.Picture.Parent.EncoderOptions = options;
                this.PictureBuffer.Picture.Parent.ConstantQualityIndex = GetConstantQualityLevel(options, qIndex);
                this.PictureBuffer.Picture.Parent.SpeedSettings = speedSettings;
                this.PictureBuffer.Picture.Parent.ReferenceRefreshControl = this;

                this.PictureBuffer.Picture.Parent.AverageInterQuantizer = qIndex;

                // Real-time usage runs the one-pass rate control of its mode between the allowed quantizers. libavif
                // selects the constant-bitrate mode for it. Reference: the AOM_CBR rc_end_usage of AOM_USAGE_REALTIME,
                // with av1_quantizer_to_qindex() of rc_min_quantizer and rc_max_quantizer.
                if (speedSettings.IsRealtime)
                {
                    int bestAllowedQIndex = Av1QuantizationLookup.GetQIndex(options.MinimumQuantizer);
                    int worstAllowedQIndex = Av1QuantizationLookup.GetQIndex(options.MaximumQuantizer);

                    // Cyclic refresh only runs with the real-time rate control. Reference: av1_cyclic_refresh_alloc().
                    if (options.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.CyclicRefresh)
                    {
                        this.cyclicRefresh = new Av1CyclicRefresh(this.FrameHeader.ModeInfoColumnCount, this.FrameHeader.ModeInfoRowCount);
                        this.PictureBuffer.Picture.Parent.CyclicRefresh = this.cyclicRefresh;

                        // The noise estimate runs for 8-bit frames above 640x480 with key frames apart, which cyclic
                        // refresh enables in constant-bitrate coding. Reference: use_temporal_noise_estimate in
                        // set_rt_speed_features(), with enable_noise_estimation().
                        if (width * height > 640 * 480 &&
                            options.KeyFrameMaximumDistance != 0 &&
                            colorConfig.BitDepth == Av1BitDepth.EightBit &&
                            options.UsesConstantBitRate)
                        {
                            this.noiseEstimate = new Av1NoiseEstimate(width, height, this.FrameHeader.ModeInfoColumnCount, this.FrameHeader.ModeInfoRowCount);
                            this.PictureBuffer.Picture.Parent.NoiseEstimate = this.noiseEstimate;
                        }
                    }

                    this.rateControl = new Av1RateControl(
                        width,
                        height,
                        colorConfig.BitDepth,
                        options.Speed,
                        options.RateControlMode,
                        realtime: true,
                        bestAllowedQIndex,
                        worstAllowedQIndex,
                        GetConstantQualityLevel(options, qIndex),
                        options.KeyFrameMaximumDistance,
                        options.AdaptiveQuantizationMode != Av1AdaptiveQuantizationMode.None,
                        this.cyclicRefresh);
                }
                else if (options.UsesBitBudget && options.LagInFrames == 0)
                {
                    // A good-quality sequence without lookahead under a bit budget runs the one-pass rate control
                    // without statistics. Reference: has_no_stats_stage() with rc_cfg.mode other than AOM_Q.
                    this.groupRateControl = new Av1RateControl(
                        width,
                        height,
                        colorConfig.BitDepth,
                        options.Speed,
                        options.RateControlMode,
                        realtime: false,
                        Av1QuantizationLookup.GetQIndex(options.MinimumQuantizer),
                        Av1QuantizationLookup.GetQIndex(options.MaximumQuantizer),
                        GetConstantQualityLevel(options, qIndex),
                        options.KeyFrameMaximumDistance,
                        options.AdaptiveQuantizationMode != Av1AdaptiveQuantizationMode.None,
                        cyclicRefresh: null);
                }

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

                this.SymbolEncoder.EncodingSpeed = options.Speed;

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

        protected int QIndex { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current frame is a layer after the first, whose flags keep GOLDEN and
        /// the alternate references out of the frame.
        /// </summary>
        protected bool UsesLayerFlags => this.usesLayerFlags;

        /// <summary>
        /// Gets a value indicating whether the current frame starts a temporal unit, and so is preceded by a temporal
        /// delimiter. Every layer of a layered image after the first continues the temporal unit of the first layer.
        /// Reference: the write_temporal_delimiter test of encoder_encode(), which writes it only for spatial layer 0.
        /// </summary>
        protected bool StartsTemporalUnit => this.FrameHeader.SpatialId == 0;

        /// <summary>
        /// Gets the coded size of the current frame: the sequence size, or the size of a scaled layer.
        /// </summary>
        protected Size FrameSize => this.frameSize;

        /// <summary>
        /// Gets a value indicating whether the frame codes at a size other than the size the encoder holds, which is
        /// the size of the frame before it or, after a configuration change, the image size. libavif sets the scale
        /// mode of every frame, so the pending size is always the frame size. Reference: is_frame_resize_pending()
        /// with the resize_pending_params of AOME_SET_SCALEMODE.
        /// </summary>
        private protected bool IsResizePending => this.frameSize != this.presetupFrameSize;

        /// <summary>
        /// Gets a value indicating whether the size of the current frame differs from the sequence size.
        /// </summary>
        protected bool IsScaledFrame =>
            this.frameSize.Width != this.SequenceHeader.MaxFrameWidth || this.frameSize.Height != this.SequenceHeader.MaxFrameHeight;

        /// <summary>
        /// Gets the encoding options retained for every frame in this track.
        /// </summary>
        protected Av1EncoderOptions Options { get; }

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
            this.ApplyFrameSize();
            Av1FrameEncoder.ConfigureFrameHeader(this.FrameHeader, this.SequenceHeader, this.QIndex, this.Options, frameType);

            // A sequence keeps backward adaptation, so every frame stores its final probabilities for later frames.
            // Reference: the REFRESH_FRAME_CONTEXT_BACKWARD default of refresh_frame_context, which the frame
            // header writes as disable_frame_end_update_cdf.
            this.FrameHeader.DisableFrameEndUpdateCdf = false;

            // A key frame restarts the frame count, and the order hint follows it. Reference: the frame_number reset
            // of av1_encode() for a key frame that resets the reference buffers.
            if (frameType == ObuFrameType.KeyFrame)
            {
                this.nextOrderHint = 0;
            }

            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            this.FrameHeader.OrderHint = orderHintBits == 0
                ? 0
                : this.nextOrderHint & ((1U << orderHintBits) - 1);

            // A key frame is error resilient by definition. Inter frames keep the references' state and
            // entropy contexts. Reference: set_ext_overrides(), with use_error_resilient off by default.
            this.FrameHeader.ErrorResilientMode = frameType == ObuFrameType.KeyFrame;
        }

        /// <summary>
        /// Measures the rate multiplier scaling factors of the SSIM and image tunes for the current frame, from the
        /// source at the size of the image and over the grid of the frame coded before it, because libaom measures them
        /// before it sets the size of the frame and resizes the source. The frame then looks them up with its own grid.
        /// Reference: the av1_set_mb_ssim_rdmult_scaling() call of encode_frame_to_data_rate(), which precedes the
        /// av1_setup_frame_size() and av1_realloc_and_scale_if_required() calls of encode_without_recode().
        /// </summary>
        /// <typeparam name="TSample">The sample storage type.</typeparam>
        /// <typeparam name="TOperator">The closed sample operations.</typeparam>
        /// <param name="unscaledSource">The source at the size of the image.</param>
        /// <param name="parent">The frame state that receives the factors.</param>
        private protected void PrepareSsimRateMultiplierFactors<TSample, TOperator>(Av1EncoderFrame<TSample> unscaledSource, Av1PictureParentControlSet parent)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        {
            parent.HasPrecomputedSsimRateMultiplierFactors = false;
            if (this.Options.Tuning != Av1Tuning.Ssim && !this.Options.Tuning.IsImageTuning())
            {
                return;
            }

            Size measured = new(2 * ((this.presetupFrameSize.Width + 7) >> 3), 2 * ((this.presetupFrameSize.Height + 7) >> 3));

            int maximumColumns = 2 * ((this.SequenceHeader.MaxFrameWidth + 7) >> 3);
            int maximumRows = 2 * ((this.SequenceHeader.MaxFrameHeight + 7) >> 3);
            this.ssimRateMultiplierFactors ??= new double[((maximumColumns + 3) / 4) * ((maximumRows + 3) / 4)];
            Av1IntraSuperblockEncoder.SetSsimRateMultiplierScaling<TSample, TOperator>(
                this.ssimRateMultiplierFactors,
                unscaledSource,
                measured.Width,
                measured.Height,
                this.SequenceHeader.ColorConfig.BitDepth);

            parent.SsimRateMultiplierFactors = this.ssimRateMultiplierFactors;
            parent.HasPrecomputedSsimRateMultiplierFactors = true;
        }

        /// <summary>
        /// Returns the frame border of the encoder configuration: 288 samples while libaom holds a fixed resize mode,
        /// otherwise a complete superblock plus 32 samples. Reference: av1_get_enc_border_size() with
        /// av1_is_resize_needed().
        /// </summary>
        /// <returns>The border in luma samples.</returns>
        private protected int GetEncoderBorder()
            => this.resizesFixed ? ScaledReferenceBorder : (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;

        /// <summary>
        /// Records the size of the current frame as the size the encoder holds for the next frame and as the coded
        /// size of the frame before it. Reference: the cm->width and mi_params that av1_setup_frame_size() leaves, and
        /// the prev_coded_width update of av1_rc_postencode_update().
        /// </summary>
        private protected void RecordFrameSize()
        {
            this.presetupFrameSize = this.frameSize;
            this.codedFrameSize = this.frameSize;
        }

        /// <summary>
        /// Gets a value indicating whether real-time scene detection runs for the current frame, which it does when
        /// the size the encoder holds equals the size of the frame coded last. A reconfiguration after a scaled layer
        /// holds the image size, so the next layer skips it. Reference: the prev_coded_width test before
        /// av1_rc_scene_detection_onepass_rt() in av1_get_one_pass_rt_params().
        /// </summary>
        /// <returns><see langword="true"/> when scene detection runs.</returns>
        private protected bool DetectsScene() => this.presetupFrameSize == this.codedFrameSize;

        /// <summary>
        /// Gets the luma size of the grid that real-time scene detection measures over, which is the size the encoder
        /// holds before it sets the size of the frame. Reference: the cm->mi_params of
        /// av1_rc_scene_detection_onepass_rt().
        /// </summary>
        /// <returns>The grid size in luma samples.</returns>
        private protected Size GetSceneDetectionSize() => this.presetupFrameSize;

        /// <summary>
        /// Gets a value indicating whether real-time scene detection keeps the error of each 64x64 block, which it
        /// does when the size the encoder holds is the image size. Reference: the render_width test that allocates
        /// src_sad_blk_64x64 in av1_rc_scene_detection_onepass_rt().
        /// </summary>
        /// <returns><see langword="true"/> when the block errors are kept.</returns>
        private protected bool KeepsSceneBlockErrors()
            => this.presetupFrameSize.Width == this.SequenceHeader.MaxFrameWidth &&
                this.presetupFrameSize.Height == this.SequenceHeader.MaxFrameHeight;

        /// <summary>
        /// Sets the coded size, the mode-information grid size and the tile layout of the current frame. The render
        /// size stays the sequence size, so a decoder shows a scaled layer at the size of the image. Reference:
        /// av1_set_frame_size() with av1_update_frame_size() and set_tile_info(), and the render size that
        /// av1_change_config() takes from the configured frame size.
        /// </summary>
        private void ApplyFrameSize()
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            ObuFrameSize size = frameHeader.FrameSize;
            int width = this.frameSize.Width;
            int height = this.frameSize.Height;
            if (size.FrameWidth == width && size.FrameHeight == height)
            {
                return;
            }

            size.FrameWidth = width;
            size.FrameHeight = height;
            size.SuperResolutionUpscaledWidth = width;
            frameHeader.ModeInfoColumnCount = 2 * ((width + 7) >> 3);
            frameHeader.ModeInfoRowCount = 2 * ((height + 7) >> 3);
            frameHeader.TilesInfo = CreateTileGroupHeader(
                frameHeader.ModeInfoColumnCount,
                frameHeader.ModeInfoRowCount,
                this.SequenceHeader.SuperblockSizeLog2,
                this.Options.TileColumnsLog2,
                this.Options.TileRowsLog2);
        }

        /// <summary>
        /// Returns the slot that LAST uses before the source analysis selects the rest of the structure. Every layer of
        /// a layered image maps LAST to slot 0. Reference: last_idx in av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        /// <returns>The reference-map slot of LAST.</returns>
        protected int GetLastSlot()
            => this.Options.LayerCount == 1 && this.frameNumber > 1 ? (int)((this.frameNumber - 1) % RotatingSlotCount) : 0;

        /// <summary>
        /// Copies the visible samples of every plane of a frame into a frame of the same size, and extends the
        /// borders of the copy.
        /// </summary>
        /// <typeparam name="TSample">The component sample type.</typeparam>
        /// <param name="source">The frame to copy.</param>
        /// <param name="destination">The frame that receives the copy.</param>
        protected static void CopyVisibleFrame<TSample>(Av1EncoderFrame<TSample> source, Av1EncoderFrame<TSample> destination)
            where TSample : unmanaged
        {
            int planeCount = source.IsMonochrome ? 1 : 3;
            for (int plane = 0; plane < planeCount; plane++)
            {
                int subsamplingX = plane == 0 ? 0 : source.ChromaSubsamplingX;
                int subsamplingY = plane == 0 ? 0 : source.ChromaSubsamplingY;
                int width = Av1Math.DivideLog2Ceiling(source.Width, subsamplingX);
                int height = Av1Math.DivideLog2Ceiling(source.Height, subsamplingY);
                Av1PlaneRegion<TSample> from = source.CodedView.GetPlane((Av1Plane)plane);
                Av1PlaneRegion<TSample> to = destination.CodedView.GetPlane((Av1Plane)plane);
                for (int y = 0; y < height; y++)
                {
                    from.Samples.Slice(((from.Bounds.Y + y) * from.Stride) + from.Bounds.X, width)
                        .CopyTo(to.Samples.Slice(((to.Bounds.Y + y) * to.Stride) + to.Bounds.X, width));
                }
            }

            destination.ExtendBorders();
        }

        /// <summary>
        /// Disposes every buffer of an array of optional buffers.
        /// </summary>
        /// <typeparam name="TSample">The component sample type.</typeparam>
        /// <param name="buffers">The buffers, any of which can be <see langword="null"/>.</param>
        protected static void DisposeBuffers<TSample>(Av1EncoderFrameBuffer<TSample>?[] buffers)
            where TSample : unmanaged
        {
            foreach (Av1EncoderFrameBuffer<TSample>? buffer in buffers)
            {
                buffer?.Dispose();
            }
        }

        /// <summary>
        /// Removes GOLDEN and ALTREF from the available references when their frame has another size than the current
        /// frame, so that the encoder never resizes them. LAST stays, and the motion search reads its resized copy.
        /// Reference: the number_spatial_layers == 1 branch of encode_without_recode(), which libavif reaches because
        /// it codes the layers of an image without spatial layer parameters.
        /// </summary>
        /// <typeparam name="TSample">The component sample type.</typeparam>
        /// <param name="availableReferenceMask">The available references, one bit per reference type.</param>
        /// <param name="references">The frame of each reference type.</param>
        /// <returns>The available references without the resized GOLDEN and ALTREF.</returns>
        protected int DisableResizedReferences<TSample>(int availableReferenceMask, Av1EncoderFrame<TSample>[] references)
            where TSample : unmanaged
        {
            Size size = this.frameSize;
            ReadOnlySpan<Av1ReferenceFrameType> disabled = [Av1ReferenceFrameType.Golden, Av1ReferenceFrameType.Alternate];
            foreach (Av1ReferenceFrameType reference in disabled)
            {
                Av1EncoderFrame<TSample> frame = references[(int)reference];
                if ((availableReferenceMask & (1 << (int)reference)) != 0 && (frame.Width != size.Width || frame.Height != size.Height))
                {
                    availableReferenceMask &= ~(1 << (int)reference);
                }
            }

            return availableReferenceMask;
        }

        /// <summary>
        /// Sets the models that the frame codes its global motion against, then searches its global motion. Real-time
        /// usage does not search. Reference: prev_frame in write_global_motion(), and
        /// av1_compute_global_motion_facade().
        /// </summary>
        /// <typeparam name="TSample">The component sample type.</typeparam>
        /// <typeparam name="TOperator">The sample-specific measures the search needs.</typeparam>
        /// <param name="source">The frame being coded.</param>
        /// <param name="references">The reference frame of each reference type, indexed by reference type.</param>
        /// <param name="parent">The frame state with the speed settings of the frame.</param>
        private protected void SearchGlobalMotion<TSample, TOperator>(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample>[] references,
            Av1PictureParentControlSet parent)
            where TSample : unmanaged
            where TOperator : struct, IGlobalMotionSearchOperator<TSample>
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Span<Av1GlobalMotionParameters> previous = frameHeader.GetPreviousGlobalMotionParameters();
            if (frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone)
            {
                previous.Fill(Av1GlobalMotionParameters.Identity);
            }
            else
            {
                int slot = (int)frameHeader.GetReferenceFrameIndices()[(int)frameHeader.PrimaryReferenceFrame];
                this.slotGlobalMotion.AsSpan(slot * Av1Constants.ReferencesPerFrame, Av1Constants.ReferencesPerFrame).CopyTo(previous);
            }

            bool realtime = parent.SpeedSettings.IsRealtime;
            GlobalMotionSearchInputs inputs = new(
                !realtime,
                parent.SpeedSettings,
                parent.FrameUpdateType,
                parent.SpeedSettings.IsBoosted,
                this.BlockWorkspace.ReferenceFrameNumbers,
                this.BlockWorkspace.EncodedFrameCount,
                this.goodQualityStructure.SlotPyramidLevels,
                this.goodQualityStructure.PyramidLevel,
                parent.AvailableReferenceMask,
                parent.IsStatConsumptionStage,
                this.globalMotionDisabledByStatistics);

            ComputeGlobalMotion<TSample, TOperator>(
                this.Configuration.MemoryAllocator,
                source,
                references,
                frameHeader,
                this.SequenceHeader.ColorConfig.BitDepth,
                in inputs);
        }

        /// <summary>
        /// Selects the update type, the reference slots, the refreshed slots, and the primary reference after the
        /// source analysis of the frame, before its quantizer and speed features, as libaom defines the group in
        /// av1_encode_strategy() before encode_without_recode(). Real-time usage follows the one-layer real-time
        /// structure, and good-quality usage the low-delay pyramid of <see cref="Av1GoodQualityReferenceStructure"/>.
        /// </summary>
        /// <param name="parent">
        /// The frame state with the source analysis of the frame. Its speed settings are those of the previous frame;
        /// the structure reads only the features that do not depend on the update type.
        /// </param>
        /// <param name="averageSourceSad">The running average source SAD. Reference: rc->avg_source_sad.</param>
        protected void ConfigureReferenceStructure(Av1PictureParentControlSet parent, ulong averageSourceSad)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;
            if (!parent.SpeedSettings.IsRealtime)
            {
                // Good-quality usage without lookahead codes low-delay pyramid groups. copy_frame_prob_info() restores
                // the frame probability tables at every key frame.
                this.goodQualityStructure.Configure(frameHeader, parent.FramesSinceKey, this.framesToKey, this.usesLayerFlags);
                parent.FrameUpdateType = this.goodQualityStructure.UpdateType;
                parent.StartsGoldenGroup = parent.FrameUpdateType == Av1FrameUpdateType.Golden;

                // The layer flags replace the GOLDEN refresh of the update type with none. Reference: the
                // update_pending branch of av1_configure_buffer_updates().
                parent.RefreshesGolden = !this.usesLayerFlags && (keyFrame || parent.StartsGoldenGroup);
                if (keyFrame)
                {
                    this.warpedProbabilities.AsSpan().Fill(64);
                    DefaultObmcProbabilities.CopyTo(this.obmcProbabilities, 0);
                }
            }
            else
            {
                this.ConfigureRealtimeReferenceStructure(parent, averageSourceSad);
                parent.FrameUpdateType = GetFrameUpdateType(keyFrame, parent.StartsGoldenGroup);
            }
        }

        /// <summary>
        /// Sets the frame tools that follow the reference structure and the quantizer of the frame: the interpolation
        /// filter, temporal motion vectors, warped motion, OBMC, and skip mode. Reference: set_size_independent_vars(),
        /// frame_might_allow_ref_frame_mvs(), frame_might_allow_warped_motion(), is_switchable_motion_mode_allowed(),
        /// and av1_setup_skip_mode_allowed().
        /// </summary>
        /// <param name="parent">The frame state with the update type and the speed settings of the frame.</param>
        protected void ConfigureReferenceTools(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;

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
                this.warpedProbabilities[(int)parent.FrameUpdateType] < warpedThreshold)
            {
                allowWarpedMotion = false;
            }

            frameHeader.AllowWarpedMotion = allowWarpedMotion;
            int obmcRow = (int)parent.FrameUpdateType * (int)Av1BlockSize.AllSizes;
            this.obmcProbabilities.AsSpan(obmcRow, (int)Av1BlockSize.AllSizes).CopyTo(parent.ObmcProbabilities);

            // OBMC is enabled by default, so the motion mode is switchable in every inter frame.
            // Reference: is_switchable_motion_mode_allowed(allow_warped_motion, enable_obmc).
            frameHeader.IsMotionModeSwitchable = !frameHeader.IsIntra;

            frameHeader.SkipModeParameters.Derive(this.SequenceHeader.OrderHintInfo, frameHeader);
            frameHeader.SkipModeParameters.SkipModeFlag = frameHeader.SkipModeParameters.SkipModeAllowed;
        }

        /// <summary>
        /// Restores the frame tools that each coding of a frame starts from before the frame is coded again: switchable
        /// filters and motion modes and the skip mode of the references. Warped motion stays as the last coding left it,
        /// and turns off when its probability fell below the threshold. Reference: the av1_encode_frame() and
        /// encode_frame_internal() setup of each pass of encode_with_recode_loop().
        /// </summary>
        /// <param name="parent">The frame state with the update type and the speed settings of the frame.</param>
        protected void ConfigureRecodedReferenceTools(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            if (frameHeader.IsIntra)
            {
                return;
            }

            frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
            frameHeader.IsMotionModeSwitchable = true;

            // The prune_warped_prob_thresh test of encode_frame_internal() reads the probability the last coding moved.
            int warpedThreshold = parent.SpeedSettings.WarpedProbabilityThreshold;
            if (frameHeader.AllowWarpedMotion && warpedThreshold > 0 &&
                this.warpedProbabilities[(int)parent.FrameUpdateType] < warpedThreshold)
            {
                frameHeader.AllowWarpedMotion = false;
            }

            int obmcRow = (int)parent.FrameUpdateType * (int)Av1BlockSize.AllSizes;
            this.obmcProbabilities.AsSpan(obmcRow, (int)Av1BlockSize.AllSizes).CopyTo(parent.ObmcProbabilities);

            // check_skip_mode_enabled() derives skip mode again, and the reference binding narrows it.
            frameHeader.SkipModeParameters.Derive(this.SequenceHeader.OrderHintInfo, frameHeader);
            frameHeader.SkipModeParameters.SkipModeFlag = frameHeader.SkipModeParameters.SkipModeAllowed;
        }

        /// <summary>
        /// Selects the reference slots, the refreshed slots, and the primary reference of a real-time frame. A layered
        /// image maps every reference to slot 0 instead of the one-layer structure. Reference:
        /// set_gf_interval_update_onepass_rt() in av1_get_one_pass_rt_params(), then
        /// av1_set_rtc_reference_structure_one_layer() with gf_update = (gf_frame_index == 0), then
        /// choose_primary_ref_frame().
        /// </summary>
        /// <param name="parent">The frame state with the source analysis and options settings of the frame.</param>
        /// <param name="averageSourceSad">The running average source SAD. Reference: rc->avg_source_sad.</param>
        private void ConfigureRealtimeReferenceStructure(Av1PictureParentControlSet parent, ulong averageSourceSad)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;
            bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;

            // set_gf_interval_update_onepass_rt(): a frame of a new size also starts a group.
            if (this.IsResizePending || parent.HighSourceSad || this.framesTillGoldenUpdateDue == 0)
            {
                // A key frame has already restarted the key frame interval. Reference: set_key_frame() in
                // av1_get_one_pass_rt_params(), which runs before set_gf_interval_update_onepass_rt().
                int framesToKey = keyFrame ? this.Options.KeyFrameMaximumDistance : this.rateControl!.FramesToKey;
                this.SetBaselineGoldenInterval(parent.AverageFrameLowMotion, framesToKey);
            }

            bool goldenUpdate = this.goldenFrameIndex == 0;
            parent.StartsGoldenGroup = !keyFrame && goldenUpdate;

            // av1_configure_buffer_updates() refreshes GOLDEN in key frames and golden-group frames. The layer flags
            // replace that refresh with none.
            parent.RefreshesGolden = keyFrame || (goldenUpdate && !this.usesLayerFlags);

            // encode_without_recode() restores the frame probability tables at a key frame, and at a golden refresh
            // when warped motion is pruned further. Reference: copy_frame_prob_info().
            if (keyFrame || (speedSettings.ExtraPruneWarped && parent.RefreshesGolden))
            {
                this.warpedProbabilities.AsSpan().Fill(64);
                DefaultObmcProbabilities.CopyTo(this.obmcProbabilities, 0);
            }

            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            if (this.Options.LayerCount > 1)
            {
                // Spatial layers turn the one-layer structure off. The key frame maps every reference to slot 0, and
                // each later layer keeps that map and refreshes the slot of LAST, because its flags leave a refresh
                // pending. Reference: use_rtc_reference_structure_one_layer(), and the update_pending tests of
                // av1_encode_strategy() and av1_get_refresh_frame_flags().
                referenceFrameIndices.Clear();
                if (!keyFrame)
                {
                    frameHeader.RefreshFrameFlags = 1U << (int)referenceFrameIndices[(int)Av1ReferenceFrameType.Last - 1];
                }
            }
            else
            {
                this.SetOneLayerReferenceStructure(frameHeader, speedSettings, averageSourceSad, keyFrame, goldenUpdate);
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
        }

        /// <summary>
        /// Maps the references of a frame and chooses its refreshed slots in the one-layer real-time structure: LAST
        /// rotates through six slots, GOLDEN keeps its own slot, and ALTREF follows a few frames behind LAST.
        /// Reference: av1_set_rtc_reference_structure_one_layer().
        /// </summary>
        /// <param name="frameHeader">The frame header that receives the slots and the refreshed slots.</param>
        /// <param name="speedSettings">The speed settings of the frame.</param>
        /// <param name="averageSourceSad">The running average source SAD. Reference: rc->avg_source_sad.</param>
        /// <param name="keyFrame">Whether the frame is a key frame, which refreshes every slot.</param>
        /// <param name="goldenUpdate">Whether the frame refreshes GOLDEN. Reference: gf_update.</param>
        private void SetOneLayerReferenceStructure(
            ObuFrameHeader frameHeader,
            in Av1EncoderSpeedSettings speedSettings,
            ulong averageSourceSad,
            bool keyFrame,
            bool goldenUpdate)
        {
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
        }

        /// <summary>
        /// Turns skip mode off when its two references lie at distances from the frame that differ by more than one
        /// frame, or when the encoder disabled either reference. Reference: check_skip_mode_enabled().
        /// </summary>
        /// <param name="sequenceHeader">The sequence header with the order hint parameters.</param>
        /// <param name="frameHeader">The frame header whose skip mode flag is updated.</param>
        /// <param name="availableReferences">The references the encoder searches, one bit per reference type.</param>
        /// <param name="onlyPastReferencesWithLag">Whether every reference precedes the frame while the sequence codes
        /// with a lookahead. Reference: the all_one_sided_refs and lag_in_frames test.</param>
        protected static void CheckSkipModeEnabled(
            ObuSequenceHeader sequenceHeader,
            ObuFrameHeader frameHeader,
            byte availableReferences,
            bool onlyPastReferencesWithLag)
        {
            ObuSkipModeParameters skipMode = frameHeader.SkipModeParameters;
            if (!skipMode.SkipModeAllowed)
            {
                skipMode.SkipModeFlag = false;
                return;
            }

            ObuOrderHintInfo orderHintInfo = sequenceHeader.OrderHintInfo;
            ReadOnlySpan<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
            ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            Av1ReferenceFrameType first = skipMode.FirstReferenceFrame;
            Av1ReferenceFrameType second = skipMode.SecondReferenceFrame;
            uint firstOrderHint = referenceOrderHints[(int)referenceFrameIndices[(int)first - (int)Av1ReferenceFrameType.Last]];
            uint secondOrderHint = referenceOrderHints[(int)referenceFrameIndices[(int)second - (int)Av1ReferenceFrameType.Last]];
            int toFirst = orderHintInfo.GetRelativeDistance(frameHeader.OrderHint, firstOrderHint);
            int toSecond = Math.Abs(orderHintInfo.GetRelativeDistance(frameHeader.OrderHint, secondOrderHint));
            if (Math.Abs(toFirst - toSecond) > 1 ||
                onlyPastReferencesWithLag ||
                (availableReferences & (1 << (int)first)) == 0 ||
                (availableReferences & (1 << (int)second)) == 0)
            {
                skipMode.SkipModeFlag = false;
            }
        }

        /// <summary>
        /// Removes references in a fixed order until the frame uses no more references than its options allows.
        /// Reference: enforce_max_ref_frames(), get_max_allowed_ref_frames(), and get_num_refs_to_disable().
        /// </summary>
        /// <param name="sequenceHeader">The sequence header with the order hint parameters.</param>
        /// <param name="frameHeader">The frame header with the reference order hints.</param>
        /// <param name="flags">The available references, one bit per reference type.</param>
        /// <param name="speedSettings">The options settings of the frame.</param>
        /// <returns>The available references that remain, one bit per reference type.</returns>
        protected static byte EnforceMaximumReferenceFrames(
            ObuSequenceHeader sequenceHeader,
            ObuFrameHeader frameHeader,
            byte flags,
            in Av1EncoderSpeedSettings speedSettings)
        {
            int level = speedSettings.SelectiveReferenceFrameLevel;
            int referencesToDisable = 0;
            if (level >= 3)
            {
                referencesToDisable++;
                if (level >= 6)
                {
                    // Disable LAST2 and ALTREF2.
                    referencesToDisable += 2;
                }
                else if (level == 5 && (flags & (1 << (int)Av1ReferenceFrameType.Last2)) != 0)
                {
                    // Disable LAST2 when it is temporally distant. The first-pass statistics test does not apply
                    // to one-pass coding.
                    int slot = (int)frameHeader.GetReferenceFrameIndices()[Av1ReferenceFrameType.Last2 - Av1ReferenceFrameType.Last];
                    int distance = sequenceHeader.OrderHintInfo.GetRelativeDistance(
                        frameHeader.GetReferenceOrderHints()[slot],
                        frameHeader.OrderHint);

                    if (Math.Abs(distance) > 2)
                    {
                        referencesToDisable++;
                    }
                }
            }

            // The max_reference_frames option keeps its default of every inter reference.
            const int maximumReferenceFrames = Av1Constants.ReferenceFrameCount - 1;
            int maximumReferences = Math.Min(Av1Constants.ReferenceFrameCount - 1 - referencesToDisable, maximumReferenceFrames);
            int validReferences = BitOperations.PopCount((uint)(flags & 0xFE));
            ReadOnlySpan<Av1ReferenceFrameType> disableOrder =
            [
                Av1ReferenceFrameType.Last3,
                Av1ReferenceFrameType.Last2,
                Av1ReferenceFrameType.Alternate2,
                Av1ReferenceFrameType.Backward
            ];

            for (int i = 0; i < disableOrder.Length && validReferences > maximumReferences; i++)
            {
                Av1ReferenceFrameType reference = disableOrder[i];
                if ((flags & (1 << (int)reference)) == 0)
                {
                    continue;
                }

                // libaom clears the GOLDEN flag, not the BWDREF flag, when it disables BWDREF.
                Av1ReferenceFrameType cleared = reference == Av1ReferenceFrameType.Backward ? Av1ReferenceFrameType.Golden : reference;
                flags &= (byte)~(1 << (int)cleared);
                validReferences--;
            }

            return flags;
        }

        /// <summary>
        /// Returns the references that the frame may use, without a second reference to a buffer that an
        /// earlier reference in priority order already uses. Reference: get_ref_frame_flags(), with the
        /// LAST, GOLDEN, and ALTREF flags that av1_set_rtc_reference_structure_one_layer() enables.
        /// </summary>
        /// <param name="bufferIds">The buffer identity of each reference type, indexed by reference type.</param>
        /// <param name="speedSettings">The options settings of the frame.</param>
        /// <param name="usesLayerFlags">
        /// Whether the frame is a layer after the first, whose flags keep only LAST, LAST2 and LAST3. Reference: the
        /// AOM_EFLAG_NO_REF flags that av1_apply_encoding_flags() applies to ext_flags.ref_frame_flags.
        /// </param>
        /// <returns>The available references, one bit per reference type.</returns>
        protected static byte GetReferenceFrameFlags(ReadOnlySpan<int> bufferIds, in Av1EncoderSpeedSettings speedSettings, bool usesLayerFlags)
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

            // Real-time usage enables LAST, GOLDEN, and ALTREF; good-quality usage starts from every reference.
            // Reference: av1_set_rtc_reference_structure_one_layer() and the AOM_REFFRAME_ALL default.
            int flags = usesLayerFlags
                ? (1 << (int)Av1ReferenceFrameType.Last) | (1 << (int)Av1ReferenceFrameType.Last2) | (1 << (int)Av1ReferenceFrameType.Last3)
                : speedSettings.IsRealtime
                    ? (1 << (int)Av1ReferenceFrameType.Last) | (1 << (int)Av1ReferenceFrameType.Alternate) | (1 << (int)Av1ReferenceFrameType.Golden)
                    : 0xFE;

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
        /// Moves the warped motion probability and the OBMC probability of each block size of the frame's update type
        /// halfway toward the frame's share of blocks that used them. Reference: the warped_probs and obmc_probs
        /// updates at the end of encode_frame_internal().
        /// </summary>
        /// <param name="parent">The frame state with the motion mode counts of its packing pass.</param>
        private protected void UpdateMotionModeProbabilities(Av1PictureParentControlSet parent)
        {
            if (this.FrameHeader.AllowWarpedMotion && parent.SpeedSettings.WarpedProbabilityThreshold > 0)
            {
                // The running probability moves halfway to this frame's share of warped blocks.
                int updateType = (int)parent.FrameUpdateType;
                int sum = parent.WarpedUsage[0] + parent.WarpedUsage[1];
                int newProbability = sum != 0 ? 128 * parent.WarpedUsage[1] / sum : 0;
                this.warpedProbabilities[updateType] = (this.warpedProbabilities[updateType] + newProbability) >> 1;
            }

            int obmcThreshold = parent.SpeedSettings.ObmcProbabilityThreshold;
            if (obmcThreshold > 0 && obmcThreshold < int.MaxValue)
            {
                // Each block size's probability moves halfway to this frame's share of OBMC blocks.
                int row = (int)parent.FrameUpdateType * (int)Av1BlockSize.AllSizes;
                for (int size = 0; size < (int)Av1BlockSize.AllSizes; size++)
                {
                    int sum = parent.ObmcUsage[size * 2] + parent.ObmcUsage[(size * 2) + 1];
                    int newProbability = sum != 0 ? 128 * parent.ObmcUsage[(size * 2) + 1] / sum : 0;
                    this.obmcProbabilities[row + size] = (this.obmcProbabilities[row + size] + newProbability) >> 1;
                }
            }
        }

        /// <summary>
        /// Advances the reference structure state after a coded frame. Reference: update_fb_of_context_type()
        /// and update_rc_counts().
        /// </summary>
        protected void CompleteReferenceStructure()
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;

            // Each refreshed slot keeps the models of this frame, which later frames code theirs against.
            // Reference: the copy to cm->cur_frame->global_motion in av1_compute_global_motion_facade().
            ReadOnlySpan<Av1GlobalMotionParameters> models = frameHeader.GetGlobalMotionParameters();
            for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
            {
                if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
                {
                    models.CopyTo(this.slotGlobalMotion.AsSpan(slot * Av1Constants.ReferencesPerFrame, Av1Constants.ReferencesPerFrame));
                }
            }

            this.UpdateMotionModeProbabilities(parent);
            if (!parent.SpeedSettings.IsRealtime)
            {
                this.goodQualityStructure.Complete(frameHeader);

                // update_keyframe_counters(): an empty interval waits for its pending forced key frame.
                if (this.framesToKey != 0)
                {
                    this.framesToKey--;
                }
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
        /// Starts a golden group. Without cyclic refresh the refresh divisor is 10, so the interval is 80 frames, or 40
        /// at speed 9 from 360p where the golden length level is 1, unless recent frames had little zero motion. Cyclic
        /// refresh divides by its refresh percentage instead. The group ends no later than the next key frame, and
        /// then it is constrained. Reference: set_baseline_gf_interval() and set_golden_update() with gf_length_lvl.
        /// </summary>
        /// <param name="averageFrameLowMotion">The running zero-motion percentage. Reference: rc->avg_frame_low_motion.</param>
        /// <param name="framesToKey">The frames left before the next key frame. Reference: rc->frames_to_key.</param>
        private void SetBaselineGoldenInterval(int averageFrameLowMotion, int framesToKey)
        {
            bool shortGoldenLength = this.Options.Speed >= HeifEncodingSpeed.Level9 &&
                Math.Min(this.SequenceHeader.MaxFrameWidth, this.SequenceHeader.MaxFrameHeight) >= 360;

            // Cyclic refresh sets the interval to a multiple of its refresh period, which the previous frame chose.
            int divisor = this.cyclicRefresh?.PercentRefresh ?? 10;
            int interval = divisor > 0
                ? Math.Min((shortGoldenLength ? 4 : 8) * (100 / divisor), MaximumGoldenIntervalRealtime)
                : FixedGoldenIntervalRealtime;

            if (averageFrameLowMotion != 0 && averageFrameLowMotion < 40)
            {
                interval = LowMotionGoldenInterval;
            }

            interval = Math.Min(interval, framesToKey);
            this.isConstrainedGoldenGroup = interval >= framesToKey;
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

            // The resize test of libaom never holds here, because the frame setup has consumed the pending size.
            if (frameHeader.IsIntra ||
                !this.Options.UsesConstantBitRate ||
                !parent.SpeedSettings.UsesQuantizerGoldenRefresh ||
                parent.HighSourceSad)
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
            this.SetBaselineGoldenInterval(parent.AverageFrameLowMotion, this.rateControl!.FramesToKey);
            frameHeader.RefreshFrameFlags = refresh
                ? frameHeader.RefreshFrameFlags | (1U << GoldenSlot)
                : frameHeader.RefreshFrameFlags & ~(1U << GoldenSlot);
        }

        /// <summary>
        /// Picks the quantizer of a real-time frame from the constant-bitrate model, raises it for a scene change, and
        /// applies every quantizer-dependent frame field. Reference: av1_rc_pick_q_and_bounds() in
        /// av1_set_size_dependent_vars(), and the av1_encodedframe_overshoot_cbr() call of encode_without_recode().
        /// </summary>
        /// <typeparam name="TSample">The sample storage type.</typeparam>
        /// <typeparam name="TMotion">The error operations.</typeparam>
        /// <typeparam name="TBlock">The block averaging operations.</typeparam>
        /// <param name="parent">The frame state, with the scene statistics of this frame.</param>
        /// <param name="source">The bordered source luma plane.</param>
        /// <param name="lastReconstruction">The bordered luma plane of the LAST reference, for an inter frame.</param>
        /// <param name="averageSourceSad">The running average source SAD after this frame. Reference: avg_source_sad.</param>
        /// <param name="previousAverageSourceSad">The running average before this frame. Reference: prev_avg_source_sad.</param>
        private protected void SelectFrameQuantizer<TSample, TMotion, TBlock>(
            Av1PictureParentControlSet parent,
            Av1PlaneRegion<TSample> source,
            Av1PlaneRegion<TSample> lastReconstruction,
            ulong averageSourceSad,
            ulong previousAverageSourceSad)
            where TSample : unmanaged
            where TMotion : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
            where TBlock : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        {
            if (this.groupRateControl is not null)
            {
                this.SelectGroupFrameQuantizer(parent);
                return;
            }

            bool keyFrame = this.FrameHeader.IsIntra;
            if (this.rateControl is null)
            {
                if (keyFrame)
                {
                    this.RestartFrameCount();
                }

                // Constant-quality coding without lookahead lowers the quantizer of the key frame and, in good-quality
                // usage, of the golden update that starts each group; every other frame codes at the
                // constant-quality index. Reference: rc_pick_q_and_bounds_q_mode() with get_active_best_quality().
                // A fixed quantizer codes every frame at the constant-quality index. Reference: the
                // use_fixed_qp_offsets == 2 branch of av1_set_size_dependent_vars().
                int bestQIndex = Av1QuantizationLookup.GetQIndex(this.Options.MinimumQuantizer);
                int worstQIndex = Av1QuantizationLookup.GetQIndex(this.Options.MaximumQuantizer);
                int frameQIndex = this.Options.UsesFixedQuantizer
                    ? this.constantQualityIndex
                    : keyFrame
                    ? this.GetConstantQualityKeyFrameQIndex(parent, bestQIndex, worstQIndex)
                    : !parent.SpeedSettings.IsRealtime && parent.FrameUpdateType == Av1FrameUpdateType.Golden
                        ? Av1RateControl.GetConstantQualityGoldenFrameQIndex(
                            this.constantQualityIndex,
                            this.averageInterQIndex,
                            parent.FramesSinceKey,
                            this.FrameHeader.FrameSize.FrameWidth,
                            this.FrameHeader.FrameSize.FrameHeight,
                            this.SequenceHeader.ColorConfig.BitDepth,
                            bestQIndex,
                            worstQIndex)
                        : this.constantQualityIndex;

                // Only the last-frame updates move the running inter average, after their quantizer is known.
                // Reference: the avg_frame_qindex[INTER_FRAME] update of av1_rc_postencode_update().
                if (parent.FrameUpdateType == Av1FrameUpdateType.Last)
                {
                    this.averageInterQIndex = ((3 * this.averageInterQIndex) + frameQIndex + 2) >> 2;
                }

                // Keep the boosted quantizer that a later key frame of the interval reads: a key frame, a golden
                // update of a group that ends before the next key frame, or any lower quantizer. Reference: the
                // last_boosted_qindex update of av1_rc_postencode_update().
                if (frameQIndex < this.lastBoostedQIndex ||
                    keyFrame ||
                    (!this.goodQualityStructure.IsConstrainedGroup && parent.RefreshesGolden))
                {
                    this.lastBoostedQIndex = frameQIndex;
                }

                if (frameQIndex != this.QIndex)
                {
                    this.QIndex = frameQIndex;
                    ApplyFrameQuantizer(this.FrameHeader, this.SequenceHeader, frameQIndex, this.Options);
                }

                this.ResetDeltaQuantizerAnchors(frameQIndex);
                return;
            }

            // The key frame decision and its bit target read the old frame count. The quantizer reads the restarted
            // one. Reference: av1_get_one_pass_rt_params() before av1_encode(), then av1_rc_pick_q_and_bounds().
            Av1RateControl.SourceSadStatistics sourceSad = new(parent.FrameSourceSad, averageSourceSad, previousAverageSourceSad);

            // A frame of a new size resets the buffer and the inter model before its target, while the frame type of
            // the frame before still holds. Reference: resize_reset_rc() in av1_get_one_pass_rt_params().
            if (this.IsResizePending)
            {
                this.rateControl.ResetForResize(
                    this.FrameSize,
                    this.presetupFrameSize,
                    this.previousFrameIntra,
                    this.frameNumber,
                    parent.IsScreenContent,
                    in sourceSad);
            }

            this.rateControl.BeginFrame(keyFrame, this.frameNumber, parent.StartsGoldenGroup, this.baselineGoldenInterval, this.presetupFrameSize);
            if (keyFrame)
            {
                this.RestartFrameCount();
            }

            // The quantizer pick reads the coded size of the frame. Reference: av1_setup_frame_size() before
            // av1_rc_pick_q_and_bounds().
            this.rateControl.SetFrameSize(this.FrameSize, this.GetPrimaryReferenceSize());

            // Cyclic refresh decides whether the frame refreshes any block before its quantizer is chosen. Reference:
            // the av1_cyclic_refresh_update_parameters() call of av1_encode_strategy().
            this.cyclicRefresh?.UpdateParameters(
                keyFrame,
                parent.HighSourceSad,
                this.rateControl.IsLosslessRequested,
                parent.FramesSinceKey,
                this.rateControl.AverageInterFrameQIndex,
                this.rateControl.BestQuality,
                parent.AverageFrameLowMotion,
                this.FrameHeader.FrameSize.FrameWidth,
                this.FrameHeader.FrameSize.FrameHeight,
                this.rateControl.AverageFrameBandwidth,
                this.SequenceHeader.SuperblockSize,
                this.Options.RateControlMode == Av1RateControlMode.VariableBitRate,
                this.previousRefreshesGolden);

            // A layered image in constant-quality coding codes every layer at its quality level. Reference: the
            // use_fixed_qp_offsets == 2 branch of av1_set_size_dependent_vars(), before av1_rc_pick_q_and_bounds().
            int qIndex = this.Options.UsesFixedQuantizer && this.Options.RateControlMode == Av1RateControlMode.Quality
                ? this.constantQualityIndex
                : this.rateControl.PickQuantizer<TSample, TMotion, TBlock>(
                    keyFrame,
                    this.frameNumber,
                    parent.StartsGoldenGroup,
                    parent.RefreshesGolden,
                    parent.IsScreenContent,
                    in sourceSad,
                    source,
                    lastReconstruction);

            // Overshoot detection is set for constant-bitrate inter frames. Reference: the FAST_DETECTION_MAXQ
            // overshoot_detection_cbr of set_rt_speed_features().
            if (!keyFrame && parent.HighSourceSad && this.Options.UsesConstantBitRate)
            {
                qIndex = this.rateControl.ApplyOvershootQuantizer(qIndex, averageSourceSad);
            }

            this.QIndex = qIndex;
            ApplyFrameQuantizer(this.FrameHeader, this.SequenceHeader, qIndex, this.Options);
            this.ResetDeltaQuantizerAnchors(qIndex);
            parent.AverageInterQuantizer = this.rateControl.AverageInterFrameQIndex;
        }

        /// <summary>
        /// Picks the quantizer of a good-quality frame without lookahead under a bit budget and applies every
        /// quantizer-dependent frame field. The key frame or the golden update that starts a group allocates the
        /// bits of the group, and every frame takes its target from that allocation. The group allocation and the
        /// target read the old frame count and the size of the frame before; the quantizer reads the restarted count
        /// and the coded size. Reference: define_gf_group_pass0() and av1_setup_target_rate() in
        /// av1_get_second_pass_params(), then av1_rc_pick_q_and_bounds() in encode_without_recode().
        /// </summary>
        /// <param name="parent">The frame state, with the update type of the frame.</param>
        private void SelectGroupFrameQuantizer(Av1PictureParentControlSet parent)
        {
            Av1RateControl rateControl = this.groupRateControl!;
            bool keyFrame = this.FrameHeader.IsIntra;
            bool goldenUpdate = parent.FrameUpdateType == Av1FrameUpdateType.Golden;

            // Every frame updates the frame rate limits before its target. Reference: adjust_frame_rate() in
            // av1_encode_strategy() before av1_get_second_pass_params().
            rateControl.UpdateFrameRate(this.presetupFrameSize);
            if (keyFrame || goldenUpdate)
            {
                rateControl.DefineGroup(this.goodQualityStructure.GroupLength, this.frameNumber, keyFrame);
            }

            rateControl.BeginGroupFrame(keyFrame, this.thisKeyFrameForced, goldenUpdate, this.framesToKey, this.presetupFrameSize);
            if (keyFrame)
            {
                this.RestartFrameCount();
            }

            rateControl.SetFrameSize(this.FrameSize, this.GetPrimaryReferenceSize());
            int qIndex = rateControl.PickGroupFrameQuantizer(keyFrame, goldenUpdate, this.frameNumber, parent.IsScreenContent);
            this.QIndex = qIndex;
            ApplyFrameQuantizer(this.FrameHeader, this.SequenceHeader, qIndex, this.Options);
            this.ResetDeltaQuantizerAnchors(qIndex);
        }

        /// <summary>
        /// Returns the size of the frame in the slot of the primary reference, which the rate model compares the
        /// frame with. Reference: cm->prev_frame, which get_primary_ref_frame_buf() sets.
        /// </summary>
        /// <returns>The size, or <see langword="null"/> when the frame has no primary reference.</returns>
        private Size? GetPrimaryReferenceSize()
        {
            uint primary = this.FrameHeader.PrimaryReferenceFrame;
            if (this.FrameHeader.IsIntra || primary == Av1Constants.PrimaryReferenceFrameNone)
            {
                return null;
            }

            int slot = (int)this.FrameHeader.GetReferenceFrameIndices()[(int)primary];
            return this.FrameHeader.GetReferenceFrameSizes()[slot];
        }

        /// <summary>
        /// Starts the delta quantizer of each tile from the frame quantizer. The picture reset ran before the frame
        /// chose its quantizer, so it left the quantizer of the frame before. Reference: the current_base_qindex that
        /// each tile starts from.
        /// </summary>
        /// <param name="qIndex">The base quantizer index of the frame.</param>
        private void ResetDeltaQuantizerAnchors(int qIndex)
            => this.PictureBuffer.Picture.Parent.PreviousQIndex.Span.Fill(qIndex);

        /// <summary>
        /// Restarts the frame count at a key frame, which resets every reference buffer. Later frames count from it,
        /// so the slot rotation, the quantizer history and the interpolation search pattern start again. Reference:
        /// the frame_number reset of av1_encode().
        /// </summary>
        private void RestartFrameCount()
        {
            this.frameNumber = 0;
            this.BlockWorkspace.EncodedFrameCount = 0;
            this.BlockWorkspace.FrameNumber = 0;
        }

        /// <summary>
        /// Returns the quantizer index of a key frame in constant-quality coding without lookahead. A key frame
        /// whose interval is one frame codes at the constant-quality index, a key frame that the interval placed
        /// stays near the last boosted quantizer, and any other key frame uses the key frame floor. Reference:
        /// get_intra_q_and_bounds() with rc_pick_q_and_bounds_q_mode().
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="bestQIndex">The lowest allowed quantizer index. Reference: best_allowed_q.</param>
        /// <param name="worstQIndex">The highest allowed quantizer index. Reference: worst_allowed_q.</param>
        /// <returns>The key frame quantizer index.</returns>
        private int GetConstantQualityKeyFrameQIndex(Av1PictureParentControlSet parent, int bestQIndex, int worstQIndex)
        {
            Av1BitDepth bitDepth = this.SequenceHeader.ColorConfig.BitDepth;
            if (!parent.SpeedSettings.IsRealtime && this.framesToKey <= 1)
            {
                int qIndex = this.constantQualityIndex > 0 ? Math.Max(1, this.constantQualityIndex) : this.constantQualityIndex;
                return Av1Math.Clamp(qIndex, bestQIndex, worstQIndex);
            }

            if (!parent.SpeedSettings.IsRealtime && this.thisKeyFrameForced)
            {
                return Av1RateControl.GetConstantQualityForcedKeyFrameQIndex(
                    this.constantQualityIndex,
                    this.lastBoostedQIndex,
                    bitDepth,
                    bestQIndex,
                    worstQIndex);
            }

            return Av1RateControl.GetConstantQualityKeyFrameQIndex(
                this.constantQualityIndex,
                this.FrameHeader.FrameSize.FrameWidth,
                this.FrameHeader.FrameSize.FrameHeight,
                bitDepth,
                parent.IsScreenContent,
                bestQIndex,
                worstQIndex);
        }

        /// <summary>
        /// Updates the rate model with the coded size of the frame. Reference: the av1_rc_postencode_update() and
        /// update_rc_counts() calls of av1_post_encode_updates().
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="frameBytes">The coded size of the frame, without the temporal delimiter.</param>
        private protected void CompleteRateControl(Av1PictureParentControlSet parent, int frameBytes)
        {
            if (this.groupRateControl is not null)
            {
                // A good-quality golden update refreshes GOLDEN; no frame of a group without lookahead is an
                // alternate reference or uses segments.
                this.groupRateControl.UpdateAfterFrame(
                    frameBytes,
                    this.FrameHeader.QuantizationParameters.BaseQIndex,
                    this.FrameHeader.IsIntra,
                    parent.FrameUpdateType == Av1FrameUpdateType.Golden,
                    this.goodQualityStructure.IsConstrainedGroup,
                    parent.IsScreenContent,
                    segmentationEnabled: false,
                    sceneChange: false);

                this.groupRateControl.EndFrame();
                return;
            }

            if (this.rateControl is null)
            {
                return;
            }

            this.codedFrameCount++;

            this.rateControl.UpdateAfterFrame(
                frameBytes,
                this.FrameHeader.QuantizationParameters.BaseQIndex,
                this.FrameHeader.IsIntra,
                parent.RefreshesGolden,
                this.isConstrainedGoldenGroup,
                parent.IsScreenContent,
                this.FrameHeader.SegmentationParameters.Enabled,
                parent.HighSourceSad);

            this.rateControl.EndFrame();
            this.previousFrameIntra = this.FrameHeader.IsIntra;
            this.previousRefreshesGolden = parent.RefreshesGolden;
        }

        protected void CompleteFrameHeader()
        {
            Span<bool> referenceValidity = this.FrameHeader.GetReferenceValidity();
            Span<uint> referenceOrderHints = this.FrameHeader.GetReferenceOrderHints();
            Span<Size> referenceSizes = this.FrameHeader.GetReferenceFrameSizes();
            Size frameSize = new(this.FrameHeader.FrameSize.SuperResolutionUpscaledWidth, this.FrameHeader.FrameSize.FrameHeight);
            for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
            {
                if ((this.FrameHeader.RefreshFrameFlags & (1U << slot)) != 0)
                {
                    referenceValidity[slot] = true;
                    referenceOrderHints[slot] = this.FrameHeader.OrderHint;
                    referenceSizes[slot] = frameSize;
                }
            }

            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            this.nextOrderHint = orderHintBits == 0
                ? 0
                : (this.FrameHeader.OrderHint + 1) & ((1U << orderHintBits) - 1);
        }

        /// <summary>
        /// Encodes an independently decodable sample with the sequence header required for random access. The key
        /// frame starts a new key frame interval, as the first frame of a sequence does.
        /// </summary>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        public void EncodeKeyFrame<TPixel>(ImageFrame<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.thisKeyFrameForced = false;
            this.framesToKey = Math.Max(1, this.Options.KeyFrameMaximumDistance);

            // The frame limit of a layered image ends the key frame interval at the last layer. Reference:
            // correct_frames_to_key() with frames_left from g_limit.
            if (this.Options.LayerCount > 1)
            {
                this.framesToKey = Math.Min(this.framesToKey, this.Options.LayerCount);
            }

            this.EncodeFrame(image, stream, ObuFrameType.KeyFrame, true);
        }

        /// <summary>
        /// Encodes one layer of a layered image at its own quantizer. The first layer is a key frame that starts the
        /// temporal unit. Each later layer is an inter frame of the same temporal unit, without a temporal delimiter,
        /// that predicts only from the frame before it and refreshes only that frame's slot. Reference: the layer loop
        /// of aomCodecEncodeImage(), with AOME_SET_CQ_LEVEL for the quality of the layer and the reference flags it sets
        /// for every layer after the first, and the write_temporal_delimiter test of encoder_encode().
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the source.</typeparam>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="qIndex">
        /// The quantizer index of the layer's quality, which constant-quality coding codes at, the constrained-quality
        /// mode reads as its quality level, and the bit-rate modes ignore. Reference: cq_level.
        /// </param>
        /// <param name="minimumQuantizer">
        /// The lowest quantizer of the layer on libaom's zero-through-63 scale, which coding under a bit budget reads.
        /// Reference: rc_min_quantizer.
        /// </param>
        /// <param name="maximumQuantizer">
        /// The highest quantizer of the layer on libaom's zero-through-63 scale, which coding under a bit budget reads.
        /// Reference: rc_max_quantizer.
        /// </param>
        /// <param name="scaleNumerator">The numerator of the size of the layer as a fraction of the image size.</param>
        /// <param name="scaleDenominator">The denominator of the size of the layer as a fraction of the image size.</param>
        /// <param name="qualityChanged">
        /// Whether the quality of the layer differs from the quality of the layer before it. libavif then reconfigures
        /// the encoder, which sets the mode-information grid back to the image size before the layer measures its SSIM
        /// factors. Reference: the quality controls of aomCodecEncodeImage() (aom_codec_enc_config_set(),
        /// AOME_SET_CQ_LEVEL and AV1E_SET_LOSSLESS), which reach av1_change_config() and av1_update_frame_size().
        /// </param>
        /// <exception cref="InvalidOperationException">Every layer of the image is already coded.</exception>
        public void EncodeLayer<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            int qIndex,
            int minimumQuantizer,
            int maximumQuantizer,
            int scaleNumerator = 1,
            int scaleDenominator = 1,
            bool qualityChanged = true)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (this.codedLayerCount >= this.Options.LayerCount)
            {
                throw new InvalidOperationException("Every layer of the image is already coded.");
            }

            // libavif changes the quality first, which rebuilds the configuration, and then sets the scale mode, which
            // the running encoder keeps as a fixed resize mode. Reference: the order of the controls in
            // aomCodecEncodeImage().
            if (qualityChanged)
            {
                this.presetupFrameSize = new Size(this.SequenceHeader.MaxFrameWidth, this.SequenceHeader.MaxFrameHeight);
                this.resizesFixed = false;
            }

            if (this.codedLayerCount > 0 && scaleNumerator != scaleDenominator)
            {
                this.resizesFixed = true;
            }

            // A scaled layer codes the image at the scaled size, rounded up to the next whole sample. Reference: the
            // resize_pending_params of av1_set_internal_size(), which AOME_SET_SCALEMODE sets for the next frame.
            int width = this.SequenceHeader.MaxFrameWidth;
            int height = this.SequenceHeader.MaxFrameHeight;
            this.frameSize = new Size(
                (scaleDenominator - 1 + (width * scaleNumerator)) / scaleDenominator,
                (scaleDenominator - 1 + (height * scaleNumerator)) / scaleDenominator);

            // libavif changes the configuration of each layer before it codes the layer, and names the layer. Reference:
            // the aom_codec_enc_config_set(), AOME_SET_CQ_LEVEL and AOME_SET_SPATIAL_LAYER_ID calls of
            // aomCodecEncodeImage().
            this.FrameHeader.SpatialId = this.codedLayerCount;

            // Constant-quality coding sets the quantizer of the layer as its quality level. Coding under a bit budget
            // also narrows the quantizer range of the rate model to the layer's quality, and the constrained-quality
            // mode bounds its frames by that level.
            this.constantQualityIndex = qIndex;
            Av1RateControl? layerRateControl = this.rateControl ?? this.groupRateControl;
            if (qualityChanged && layerRateControl is not null)
            {
                layerRateControl.ChangeConfiguration(
                    Av1QuantizationLookup.GetQIndex(minimumQuantizer),
                    Av1QuantizationLookup.GetQIndex(maximumQuantizer),
                    GetConstantQualityLevel(this.Options, qIndex));
            }

            try
            {
                if (this.codedLayerCount == 0)
                {
                    this.EncodeKeyFrame(image, stream);
                }
                else
                {
                    // The key frame interval ends after the last layer, so no layer after the first is a key frame.
                    this.usesLayerFlags = true;
                    this.EncodeNextFrame(image, stream, forceKeyFrame: false);
                }
            }
            finally
            {
                this.usesLayerFlags = false;
                this.frameSize = new Size(width, height);
            }

            this.codedLayerCount++;
        }

        /// <summary>
        /// Encodes the next sample of a sequence after its first frame: a key frame when the key frame interval
        /// ends or the caller forces one, else an inter frame. Reference: the key frame decision of
        /// av1_get_second_pass_params() and find_next_key_frame() without first-pass statistics in good-quality
        /// usage, and set_key_frame() of av1_get_one_pass_rt_params() in real-time usage.
        /// </summary>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="forceKeyFrame">Whether the caller forces a key frame. Reference: AOM_EFLAG_FORCE_KF.</param>
        /// <returns><see langword="true"/> when the frame is a key frame.</returns>
        public bool EncodeNextFrame<TPixel>(ImageFrame<TPixel> image, Stream stream, bool forceKeyFrame)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool keyFrame;
            if (this.rateControl is not null)
            {
                keyFrame = forceKeyFrame || this.rateControl.IsKeyFrameDue;
            }
            else
            {
                // A forced key frame is pending at the current frame, so the interval ends here, and the next
                // interval also reads that pending key frame and is empty. Reference: detect_app_forced_key().
                if (forceKeyFrame)
                {
                    this.framesToKey = 0;
                }

                keyFrame = this.framesToKey <= 0;
                if (keyFrame)
                {
                    this.thisKeyFrameForced = this.framesToKey == 0;
                    this.framesToKey = forceKeyFrame ? 0 : Math.Max(1, this.Options.KeyFrameMaximumDistance);
                }
            }

            this.EncodeFrame(image, stream, keyFrame ? ObuFrameType.KeyFrame : ObuFrameType.InterFrame, keyFrame);
            return keyFrame;
        }

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

        /// <summary>
        /// Copies the visible luma samples of the reconstruction that a reference slot holds, so that a decoder's
        /// output can be compared with what the encoder predicts from.
        /// </summary>
        /// <param name="slot">The reference slot.</param>
        /// <returns>The luma samples, row by row, widened to 16 bits.</returns>
        internal abstract ushort[] CopySlotLuma(int slot);

        /// <summary>
        /// Copies the visible luma samples of an 8-bit frame, widened to 16 bits.
        /// </summary>
        /// <param name="frame">The frame, which gives the visible size.</param>
        /// <returns>The samples, row by row.</returns>
        private protected static ushort[] CopyLuma(Av1EncoderFrame<byte> frame)
        {
            Av1PlaneRegion<byte> plane = frame.CodedView.GetPlane(Av1Plane.Y);
            int width = frame.Width;
            ushort[] samples = new ushort[width * frame.Height];
            for (int y = 0; y < frame.Height; y++)
            {
                ReadOnlySpan<byte> row = plane.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    samples[(y * width) + x] = row[x];
                }
            }

            return samples;
        }

        /// <summary>
        /// Copies the visible luma samples of a high bit depth frame.
        /// </summary>
        /// <param name="frame">The frame, which gives the visible size.</param>
        /// <returns>The samples, row by row.</returns>
        private protected static ushort[] CopyLuma(Av1EncoderFrame<ushort> frame)
        {
            Av1PlaneRegion<ushort> plane = frame.CodedView.GetPlane(Av1Plane.Y);
            int width = frame.Width;
            ushort[] samples = new ushort[width * frame.Height];
            for (int y = 0; y < frame.Height; y++)
            {
                plane.GetRowSpan(y)[..width].CopyTo(samples.AsSpan(y * width, width));
            }

            return samples;
        }

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
            this.measuredFrameStream?.Dispose();
        }
    }

    private sealed partial class ByteSequenceEncoder : SequenceEncoder
    {
        private Av1EncoderFrameBuffer<byte> source;
        private Av1EncoderFrameBuffer<byte>? previousSource;

        /// <summary>
        /// The source resized to the size of a scaled layer, or <see langword="null"/> before any scaled layer.
        /// Reference: cpi->scaled_source.
        /// </summary>
        private Av1EncoderFrameBuffer<byte>? scaledSource;

        /// <summary>
        /// The previous source resized to the size of a scaled layer, or <see langword="null"/> before any scaled
        /// layer. Reference: cpi->scaled_last_source.
        /// </summary>
        private Av1EncoderFrameBuffer<byte>? scaledPreviousSource;

        /// <summary>
        /// The border of every frame buffer, in luma samples.
        /// </summary>
        private readonly int lumaBorder;

        /// <summary>
        /// The sampling layout of every frame buffer.
        /// </summary>
        private readonly Av1ColorFormat colorFormat;
        private readonly IMemoryOwner<ulong>? sourceBlockSad;
        private ulong averageSourceSad;
        private int framesSinceKey;
        private readonly Av1EncoderFrame<byte>[] references = new Av1EncoderFrame<byte>[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The frame that the motion search reads for each reference type: the reference itself, or its copy resized
        /// to the size of the current frame. Reference: av1_get_scaled_ref_frame().
        /// </summary>
        private readonly Av1EncoderFrame<byte>[] searchReferences = new Av1EncoderFrame<byte>[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The resized copy of each reference type, or <see langword="null"/> before a reference of another size.
        /// Reference: cpi->scaled_ref_buf.
        /// </summary>
        private readonly Av1EncoderFrameBuffer<byte>?[] scaledReferences = new Av1EncoderFrameBuffer<byte>?[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The copy of each reference type larger than the current frame, with the border of scaled prediction, or
        /// <see langword="null"/> before such a reference. Reference: aom_yv12_realloc_with_new_border().
        /// </summary>
        private readonly Av1EncoderFrameBuffer<byte>?[] borderedReferences = new Av1EncoderFrameBuffer<byte>?[Av1Constants.ReferenceFrameCount];
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
            Av1EncoderOptions options,
            bool encodeAlpha)
            : base(
                configuration,
                width,
                height,
                colorConfig,
                qIndex,
                options,
                encodeAlpha,
                usesHighBitDepth: false)
        {
            try
            {
                Av1ColorFormat colorFormat = colorConfig.GetColorFormat();

                // Inter prediction needs a complete superblock beyond the image plus interpolation and alignment margins.
                int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;
                this.lumaBorder = lumaBorder;
                this.colorFormat = colorFormat;

                this.source = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                if (options.Speed >= HeifEncodingSpeed.Level7)
                {
                    this.sourceBlockSad = configuration.MemoryAllocator.Allocate<ulong>(((width + 63) >> 6) * ((height + 63) >> 6));
                }

                // Rotate source owners after encoding so temporal analysis and the integer vector decision see the
                // uncompressed previous source without a frame copy. The padding also supplies complete edge
                // superblocks. Reference: the last_source of choose_frame_source().
                this.previousSource = new(
                    configuration,
                    width,
                    height,
                    ByteSampleBitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

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

        /// <inheritdoc/>
        internal override ushort[] CopySlotLuma(int slot)
            => CopyLuma(this.referencePool.GetSlot(slot)!.Buffer.Frame);

        protected override void DisposeFrames()
        {
            // A derived constructor can fail before all frame owners exist.
            this.referencePool?.Dispose();
            this.source?.Dispose();
            this.previousSource?.Dispose();
            this.scaledSource?.Dispose();
            this.scaledPreviousSource?.Dispose();
            DisposeBuffers(this.scaledReferences);
            DisposeBuffers(this.borderedReferences);
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
            Av1EncoderReferencePool<byte>.Entry current = this.referencePool.Acquire(this.FrameSize.Width, this.FrameSize.Height);
            Av1EncoderReferencePool<byte>.Entry? last = frameHeader.IsIntra ? null : this.referencePool.GetSlot(this.GetLastSlot());

            // Screen content detection reads the source at the size of the image. Reference: the unfiltered_source of
            // av1_set_screen_content_options(), which runs before encode_without_recode() resizes the source.
            bool isScreenContent = PrepareFrame(
                this.Configuration,
                image,
                sourceRectangle,
                this.source.Frame,
                (last ?? current).Buffer.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Options,
                this.ConversionWorkspace,
                ref this.ScreenContent);

            // A scaled layer codes the source and the previous source resized to its size. Reference: the
            // av1_realloc_and_scale_if_required() calls for cpi->source and cpi->last_source in
            // encode_without_recode().
            Av1EncoderFrameBuffer<byte> frameSource = this.ScaleToFrame(this.source, ref this.scaledSource);
            Av1EncoderFrameBuffer<byte>? framePreviousSource = this.previousSource is null
                ? null
                : this.ScaleToFrame(this.previousSource, ref this.scaledPreviousSource);

            // The integer vector decision compares the source and the previous source at the size of the image, before
            // the encoder resizes them. Reference: the av1_is_integer_mv() call of encode_frame_to_data_rate().
            this.DecideIntegerMotionVectors<byte, Av1IntraSuperblockEncoder.ByteOperator>(this.source.Frame, this.previousSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.PreviousSource = this.sourceBlockSad is not null && framePreviousSource is not null ? framePreviousSource.Frame.CodedView : default;

            // Scene detection keeps the source changes of the frame before it when it does not run. Reference: the
            // rc fields that av1_get_one_pass_rt_params() leaves when it skips av1_rc_scene_detection_onepass_rt().
            bool detectsScene = this.DetectsScene();
            parent.SourceBlockSad = this.sourceBlockSad is not null && detectsScene && this.KeepsSceneBlockErrors()
                ? this.sourceBlockSad.Memory
                : default;

            if (detectsScene)
            {
                parent.HighSourceSad = false;
                parent.FrameSourceSad = 0;
                parent.SourceMotionPercentage = 0;
            }

            ulong previousAverageSourceSad = this.averageSourceSad;
            if (this.sourceBlockSad is not null && this.previousSource is not null && this.framesSinceKey != 0 && detectsScene)
            {
                AnalyzeTemporalSource<byte, Av1MotionSearchBase.ByteOperator>(
                    this.source.Frame.CodedView,
                    this.previousSource.Frame.CodedView,
                    this.GetSceneDetectionSize(),
                    parent,
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

            // The quantizer and the speed features follow the update type that the reference structure selects, as
            // libaom defines the golden-frame group before rc_pick_q_and_bounds() and the speed features of
            // encode_without_recode().
            this.ConfigureReferenceStructure(parent, this.averageSourceSad);
            this.SelectFrameQuantizer<byte, Av1MotionSearchBase.ByteOperator, Av1IntraSuperblockEncoder.ByteOperator>(
                parent,
                this.source.Frame.CodedView.GetPlane(Av1Plane.Y),
                last is null ? default : last.Buffer.Frame.CodedView.GetPlane(Av1Plane.Y),
                this.averageSourceSad,
                previousAverageSourceSad);

            parent.EncoderOptions = this.Options;
            parent.EncoderBorder = this.GetEncoderBorder();
            parent.ConstantQualityIndex = GetConstantQualityLevel(this.Options, this.ConstantQualityIndex);
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.SequenceHeader.IsStillPicture,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                this.FrameSize,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            // Good-quality usage with the default objective delta-q mode and the temporal model enabled pads the
            // border. Real-time usage does not. Reference: the do_border_pad test in av1_encode().
            parent.BorderPad = this.UsesBorderPad;

            this.ConfigureReferenceTools(parent);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.SearchGlobalMotion<byte, ByteGlobalMotionSearchOperator>(frameSource.Frame, this.references, parent);
            this.UpdateNoiseEstimate(
                parent,
                frameSource.Frame.CodedView.GetPlane(Av1Plane.Y),
                framePreviousSource is null ? default : framePreviousSource.Frame.CodedView.GetPlane(Av1Plane.Y),
                framePreviousSource is not null);

            this.BeginCyclicRefreshSegmentation(this.referencePool, current, parent);
            this.PrepareFilmGrain();
            this.PrepareSsimRateMultiplierFactors<byte, Av1IntraSuperblockEncoder.ByteOperator>(this.source.Frame, parent);
            this.RecordFrameSize();

            long frameStart = stream.Length;
            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                frameSource,
                this.references,
                this.searchReferences,
                current.Buffer,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader,
                this.StartsTemporalUnit);

            // A temporal delimiter precedes each temporal unit, and libaom counts the frame without it.
            this.CompleteRateControl(parent, (int)(stream.Length - frameStart) - (this.StartsTemporalUnit ? TemporalDelimiterLength : 0));
            this.CompleteCyclicRefreshSegmentation(current, this.PictureBuffer.Picture);

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
            this.RefreshFilmGrain();
        }

        /// <summary>
        /// Returns the source at the size of the current frame: the source itself, or a copy resized to the size of a
        /// scaled layer with the kernel and phase of the encoder. Reference: av1_realloc_and_scale_if_required() with
        /// the filter_scaler and phase_scaler of encode_without_recode().
        /// </summary>
        /// <param name="unscaled">The source at the size of the image.</param>
        /// <param name="scaled">The buffer of the resized copy, which is reallocated when the frame size changes.</param>
        /// <returns>The source at the frame size.</returns>
        private Av1EncoderFrameBuffer<byte> ScaleToFrame(Av1EncoderFrameBuffer<byte> unscaled, ref Av1EncoderFrameBuffer<byte>? scaled)
        {
            if (!this.IsScaledFrame)
            {
                return unscaled;
            }

            Av1EncoderFrameBuffer<byte> destination = this.GetBuffer(ref scaled, this.FrameSize, this.lumaBorder);
            (Av1InterpolationFilter filter, int phase) = Av1FrameResizer.GetFrameScaler(this.FrameSize, new Size(unscaled.Frame.Width, unscaled.Frame.Height));
            Av1FrameResizer.ResizeFrame(this.Configuration.MemoryAllocator, unscaled.Frame, destination.Frame, filter, phase);
            return destination;
        }

        /// <summary>
        /// Returns a buffer of a size, and replaces the buffer when its size differs. Reference: the
        /// aom_realloc_frame_buffer() calls of av1_realloc_and_scale_if_required() and av1_scale_references().
        /// </summary>
        /// <param name="buffer">The buffer to reuse, which receives the replacement.</param>
        /// <param name="size">The frame size of the buffer.</param>
        /// <param name="border">The luma border of a new buffer.</param>
        /// <returns>The buffer of the size.</returns>
        private Av1EncoderFrameBuffer<byte> GetBuffer(ref Av1EncoderFrameBuffer<byte>? buffer, Size size, int border)
        {
            if (buffer is null || buffer.Frame.Width != size.Width || buffer.Frame.Height != size.Height)
            {
                buffer?.Dispose();
                buffer = new(
                    this.Configuration,
                    size.Width,
                    size.Height,
                    ByteSampleBitDepth,
                    this.colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    border);
            }

            return buffer;
        }

        /// <summary>
        /// Prepares each available reference of another size than the current frame. A larger reference predicts
        /// from a copy with the wider border of scaled prediction, and the motion search reads a copy resized to the
        /// size of the frame with the kernel and phase that resize the source. Every other reference is searched in
        /// place. libavif codes in one pass without statistics, so libaom never recodes and always resizes in
        /// encode_without_recode(). Reference: av1_scale_references() with the filter_scaler and phase_scaler of
        /// encode_without_recode(), and the DISALLOW_RECODE of av1_set_speed_features_framesize_independent().
        /// </summary>
        /// <param name="availableReferenceMask">The available references, one bit per reference type.</param>
        private void ScaleReferences(int availableReferenceMask)
        {
            Size size = this.FrameSize;
            (Av1InterpolationFilter filter, int phase) = Av1FrameResizer.GetFrameScaler(size, new Size(this.source.Frame.Width, this.source.Frame.Height));
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                Av1EncoderFrame<byte> frame = this.references[reference];
                this.searchReferences[reference] = frame;
                if ((availableReferenceMask & (1 << reference)) == 0 || (frame.Width == size.Width && frame.Height == size.Height))
                {
                    continue;
                }

                // A larger reference scales a vector up, which can reach past the normal border. A smaller reference
                // scales it down, so its reads stay within the normal border. Reference: the
                // aom_yv12_realloc_with_new_border() call of av1_scale_references().
                if (frame.Width > size.Width || frame.Height > size.Height)
                {
                    Av1EncoderFrameBuffer<byte> bordered = this.GetBuffer(
                        ref this.borderedReferences[reference], new Size(frame.Width, frame.Height), ScaledReferenceBorder);

                    CopyVisibleFrame(frame, bordered.Frame);
                    this.references[reference] = bordered.Frame;
                }

                Av1EncoderFrameBuffer<byte> scaled = this.GetBuffer(ref this.scaledReferences[reference], size, ScaledReferenceBorder);
                Av1FrameResizer.ResizeFrame(this.Configuration.MemoryAllocator, frame, scaled.Frame, filter, phase);
                this.searchReferences[reference] = scaled.Frame;
            }
        }

        /// <inheritdoc/>
        private protected override Av1FrameEntropyContext? BindReferences(Av1PictureParentControlSet parent)
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

            parent.AvailableReferenceMask = EnforceMaximumReferenceFrames(
                this.SequenceHeader,
                frameHeader,
                GetReferenceFrameFlags(this.referenceBufferIds, parent.SpeedSettings, this.UsesLayerFlags),
                parent.SpeedSettings);

            parent.AvailableReferenceMask = (byte)this.DisableResizedReferences(parent.AvailableReferenceMask, this.references);
            this.ScaleReferences(parent.AvailableReferenceMask);
            CheckSkipModeEnabled(this.SequenceHeader, frameHeader, parent.AvailableReferenceMask, this.HasOnlyPastReferencesWithLag());
            return frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone
                ? null
                : this.referencePool.GetSlot((int)referenceFrameIndices[(int)frameHeader.PrimaryReferenceFrame])!.Context;
        }
    }

    private sealed partial class HighBitDepthSequenceEncoder : SequenceEncoder
    {
        private Av1EncoderFrameBuffer<ushort> source;
        private Av1EncoderFrameBuffer<ushort>? previousSource;

        /// <summary>
        /// The source resized to the size of a scaled layer, or <see langword="null"/> before any scaled layer.
        /// Reference: cpi->scaled_source.
        /// </summary>
        private Av1EncoderFrameBuffer<ushort>? scaledSource;

        /// <summary>
        /// The previous source resized to the size of a scaled layer, or <see langword="null"/> before any scaled
        /// layer. Reference: cpi->scaled_last_source.
        /// </summary>
        private Av1EncoderFrameBuffer<ushort>? scaledPreviousSource;

        /// <summary>
        /// The border of every frame buffer, in luma samples.
        /// </summary>
        private readonly int lumaBorder;

        /// <summary>
        /// The sampling layout of every frame buffer.
        /// </summary>
        private readonly Av1ColorFormat colorFormat;

        /// <summary>
        /// The sample bit depth of every frame buffer.
        /// </summary>
        private readonly int bitDepth;
        private readonly IMemoryOwner<ulong>? sourceBlockSad;
        private ulong averageSourceSad;
        private int framesSinceKey;
        private readonly Av1EncoderFrame<ushort>[] references = new Av1EncoderFrame<ushort>[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The frame that the motion search reads for each reference type: the reference itself, or its copy resized
        /// to the size of the current frame. Reference: av1_get_scaled_ref_frame().
        /// </summary>
        private readonly Av1EncoderFrame<ushort>[] searchReferences = new Av1EncoderFrame<ushort>[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The resized copy of each reference type, or <see langword="null"/> before a reference of another size.
        /// Reference: cpi->scaled_ref_buf.
        /// </summary>
        private readonly Av1EncoderFrameBuffer<ushort>?[] scaledReferences = new Av1EncoderFrameBuffer<ushort>?[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The copy of each reference type larger than the current frame, with the border of scaled prediction, or
        /// <see langword="null"/> before such a reference. Reference: aom_yv12_realloc_with_new_border().
        /// </summary>
        private readonly Av1EncoderFrameBuffer<ushort>?[] borderedReferences = new Av1EncoderFrameBuffer<ushort>?[Av1Constants.ReferenceFrameCount];
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
            Av1EncoderOptions options,
            bool encodeAlpha)
            : base(
                configuration,
                width,
                height,
                colorConfig,
                qIndex,
                options,
                encodeAlpha,
                usesHighBitDepth: true)
        {
            try
            {
                int bitDepth = colorConfig.BitDepth.GetBitCount();
                Av1ColorFormat colorFormat = colorConfig.GetColorFormat();

                // Inter prediction needs a complete superblock beyond the image plus interpolation and alignment margins.
                int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;
                this.lumaBorder = lumaBorder;
                this.colorFormat = colorFormat;
                this.bitDepth = bitDepth;

                this.source = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

                if (options.Speed >= HeifEncodingSpeed.Level7)
                {
                    this.sourceBlockSad = configuration.MemoryAllocator.Allocate<ulong>(((width + 63) >> 6) * ((height + 63) >> 6));
                }

                // Reference: the last_source of choose_frame_source().
                this.previousSource = new(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lumaBorder);

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

        /// <inheritdoc/>
        internal override ushort[] CopySlotLuma(int slot)
            => CopyLuma(this.referencePool.GetSlot(slot)!.Buffer.Frame);

        protected override void DisposeFrames()
        {
            // A derived constructor can fail before all frame owners exist.
            this.referencePool?.Dispose();
            this.source?.Dispose();
            this.previousSource?.Dispose();
            this.scaledSource?.Dispose();
            this.scaledPreviousSource?.Dispose();
            DisposeBuffers(this.scaledReferences);
            DisposeBuffers(this.borderedReferences);
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
            Av1EncoderReferencePool<ushort>.Entry current = this.referencePool.Acquire(this.FrameSize.Width, this.FrameSize.Height);
            Av1EncoderReferencePool<ushort>.Entry? last = frameHeader.IsIntra ? null : this.referencePool.GetSlot(this.GetLastSlot());

            // Screen content detection reads the source at the size of the image. Reference: the unfiltered_source of
            // av1_set_screen_content_options(), which runs before encode_without_recode() resizes the source.
            bool isScreenContent = PrepareFrame(
                this.Configuration,
                image,
                sourceRectangle,
                this.source.Frame,
                (last ?? current).Buffer.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Options,
                this.ConversionWorkspace,
                ref this.ScreenContent);

            // A scaled layer codes the source and the previous source resized to its size. Reference: the
            // av1_realloc_and_scale_if_required() calls for cpi->source and cpi->last_source in
            // encode_without_recode().
            Av1EncoderFrameBuffer<ushort> frameSource = this.ScaleToFrame(this.source, ref this.scaledSource);
            Av1EncoderFrameBuffer<ushort>? framePreviousSource = this.previousSource is null
                ? null
                : this.ScaleToFrame(this.previousSource, ref this.scaledPreviousSource);

            // The integer vector decision compares the source and the previous source at the size of the image, before
            // the encoder resizes them. Reference: the av1_is_integer_mv() call of encode_frame_to_data_rate().
            this.DecideIntegerMotionVectors<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(this.source.Frame, this.previousSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.IsScreenContent = isScreenContent;
            parent.EncoderOptions = this.Options;
            parent.EncoderBorder = this.GetEncoderBorder();

            // Scene detection keeps the source changes of the frame before it when it does not run. Reference: the
            // rc fields that av1_get_one_pass_rt_params() leaves when it skips av1_rc_scene_detection_onepass_rt().
            bool detectsScene = this.DetectsScene();
            parent.SourceBlockSad = this.sourceBlockSad is not null && detectsScene && this.KeepsSceneBlockErrors()
                ? this.sourceBlockSad.Memory
                : default;

            if (detectsScene)
            {
                parent.HighSourceSad = false;
                parent.FrameSourceSad = 0;
                parent.SourceMotionPercentage = 0;
            }

            ulong previousAverageSourceSad = this.averageSourceSad;
            if (this.sourceBlockSad is not null && this.previousSource is not null && this.framesSinceKey != 0 && detectsScene)
            {
                AnalyzeTemporalSource<ushort, Av1MotionSearchBase.UInt16Operator>(
                    this.source.Frame.CodedView,
                    this.previousSource.Frame.CodedView,
                    this.GetSceneDetectionSize(),
                    parent,
                    this.framesSinceKey,
                    ref this.averageSourceSad);
            }

            if (frameHeader.IsIntra)
            {
                this.framesSinceKey = 0;
            }

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = this.FramesSinceGolden;

            // The quantizer and the speed features follow the update type that the reference structure selects, as
            // libaom defines the golden-frame group before rc_pick_q_and_bounds() and the speed features of
            // encode_without_recode().
            this.ConfigureReferenceStructure(parent, this.averageSourceSad);
            this.SelectFrameQuantizer<ushort, Av1MotionSearchBase.UInt16Operator, Av1IntraSuperblockEncoder.UInt16Operator>(
                parent,
                this.source.Frame.CodedView.GetPlane(Av1Plane.Y),
                last is null ? default : last.Buffer.Frame.CodedView.GetPlane(Av1Plane.Y),
                this.averageSourceSad,
                previousAverageSourceSad);

            parent.ConstantQualityIndex = GetConstantQualityLevel(this.Options, this.ConstantQualityIndex);
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.SequenceHeader.IsStillPicture,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                this.FrameSize,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            // Good-quality usage with the default objective delta-q mode and the temporal model enabled pads the
            // border. Real-time usage does not. Reference: the do_border_pad test in av1_encode().
            parent.BorderPad = this.UsesBorderPad;

            this.ConfigureReferenceTools(parent);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.SearchGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(frameSource.Frame, this.references, parent);
            this.BeginCyclicRefreshSegmentation(this.referencePool, current, parent);
            this.PrepareFilmGrain();
            this.PrepareSsimRateMultiplierFactors<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(this.source.Frame, parent);
            this.RecordFrameSize();

            long frameStart = stream.Length;
            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                frameSource,
                this.references,
                this.searchReferences,
                current.Buffer,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader,
                this.StartsTemporalUnit);

            // A temporal delimiter precedes each temporal unit, and libaom counts the frame without it.
            this.CompleteRateControl(parent, (int)(stream.Length - frameStart) - (this.StartsTemporalUnit ? TemporalDelimiterLength : 0));
            this.CompleteCyclicRefreshSegmentation(current, this.PictureBuffer.Picture);

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
            this.RefreshFilmGrain();
        }

        /// <summary>
        /// Returns the source at the size of the current frame: the source itself, or a copy resized to the size of a
        /// scaled layer. Frames of more than eight bits always use the nonnormative resizer. Reference:
        /// av1_realloc_and_scale_if_required() in encode_without_recode().
        /// </summary>
        /// <param name="unscaled">The source at the size of the image.</param>
        /// <param name="scaled">The buffer of the resized copy, which is reallocated when the frame size changes.</param>
        /// <returns>The source at the frame size.</returns>
        private Av1EncoderFrameBuffer<ushort> ScaleToFrame(Av1EncoderFrameBuffer<ushort> unscaled, ref Av1EncoderFrameBuffer<ushort>? scaled)
        {
            if (!this.IsScaledFrame)
            {
                return unscaled;
            }

            Av1EncoderFrameBuffer<ushort> destination = this.GetBuffer(ref scaled, this.FrameSize, this.lumaBorder);
            Av1FrameResizer.ResizeFrame(this.Configuration.MemoryAllocator, unscaled.Frame, destination.Frame, this.bitDepth);
            return destination;
        }

        /// <summary>
        /// Returns a buffer of a size, and replaces the buffer when its size differs. Reference: the
        /// aom_realloc_frame_buffer() calls of av1_realloc_and_scale_if_required() and av1_scale_references().
        /// </summary>
        /// <param name="buffer">The buffer to reuse, which receives the replacement.</param>
        /// <param name="size">The frame size of the buffer.</param>
        /// <param name="border">The luma border of a new buffer.</param>
        /// <returns>The buffer of the size.</returns>
        private Av1EncoderFrameBuffer<ushort> GetBuffer(ref Av1EncoderFrameBuffer<ushort>? buffer, Size size, int border)
        {
            if (buffer is null || buffer.Frame.Width != size.Width || buffer.Frame.Height != size.Height)
            {
                buffer?.Dispose();
                buffer = new(
                    this.Configuration,
                    size.Width,
                    size.Height,
                    this.bitDepth,
                    this.colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    border);
            }

            return buffer;
        }

        /// <summary>
        /// Prepares each available reference of another size than the current frame. A larger reference predicts
        /// from a copy with the wider border of scaled prediction, and the motion search reads a copy resized to the
        /// size of the frame with the nonnormative resizer, which every frame of more than eight bits uses. Every
        /// other reference is searched in place. Reference: av1_scale_references().
        /// </summary>
        /// <param name="availableReferenceMask">The available references, one bit per reference type.</param>
        private void ScaleReferences(int availableReferenceMask)
        {
            Size size = this.FrameSize;
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                Av1EncoderFrame<ushort> frame = this.references[reference];
                this.searchReferences[reference] = frame;
                if ((availableReferenceMask & (1 << reference)) == 0 || (frame.Width == size.Width && frame.Height == size.Height))
                {
                    continue;
                }

                // A larger reference scales a vector up, which can reach past the normal border. A smaller reference
                // scales it down, so its reads stay within the normal border. Reference: the
                // aom_yv12_realloc_with_new_border() call of av1_scale_references().
                if (frame.Width > size.Width || frame.Height > size.Height)
                {
                    Av1EncoderFrameBuffer<ushort> bordered = this.GetBuffer(
                        ref this.borderedReferences[reference], new Size(frame.Width, frame.Height), ScaledReferenceBorder);

                    CopyVisibleFrame(frame, bordered.Frame);
                    this.references[reference] = bordered.Frame;
                }

                Av1EncoderFrameBuffer<ushort> scaled = this.GetBuffer(ref this.scaledReferences[reference], size, ScaledReferenceBorder);
                Av1FrameResizer.ResizeFrame(this.Configuration.MemoryAllocator, frame, scaled.Frame, this.bitDepth);
                this.searchReferences[reference] = scaled.Frame;
            }
        }

        /// <inheritdoc/>
        private protected override Av1FrameEntropyContext? BindReferences(Av1PictureParentControlSet parent)
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

            parent.AvailableReferenceMask = EnforceMaximumReferenceFrames(
                this.SequenceHeader,
                frameHeader,
                GetReferenceFrameFlags(this.referenceBufferIds, parent.SpeedSettings, this.UsesLayerFlags),
                parent.SpeedSettings);

            parent.AvailableReferenceMask = (byte)this.DisableResizedReferences(parent.AvailableReferenceMask, this.references);
            this.ScaleReferences(parent.AvailableReferenceMask);
            CheckSkipModeEnabled(this.SequenceHeader, frameHeader, parent.AvailableReferenceMask, this.HasOnlyPastReferencesWithLag());
            return frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone
                ? null
                : this.referencePool.GetSlot((int)referenceFrameIndices[(int)frameHeader.PrimaryReferenceFrame])!.Context;
        }
    }
}

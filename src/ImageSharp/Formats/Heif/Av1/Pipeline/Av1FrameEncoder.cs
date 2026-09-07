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
    /// The highest public effort value, which enables 128x128 superblocks for sufficiently large images.
    /// </summary>
    private const int MaximumEffort = 10;

    /// <summary>
    /// The native component precision used by the byte pipeline.
    /// </summary>
    private const int ByteSampleBitDepth = 8;

    /// <summary>
    /// The centered chroma position expressed in AV1 half-luma-sample units.
    /// </summary>
    private const int CenteredChromaSamplePosition = 1;

    /// <summary>
    /// The first effort tier that searches frame-level translation between sequence samples.
    /// </summary>
    private const int MinimumGlobalMotionSearchEffort = 6;

    /// <summary>
    /// The first effort tier that compares the three interpolation families for reference-frame prediction.
    /// </summary>
    private const int MinimumSwitchableInterpolationEffort = 8;

    /// <summary>
    /// The first effort tier that searches independent vertical and horizontal interpolation families.
    /// </summary>
    private const int MinimumDualInterpolationEffort = 9;

    /// <summary>
    /// The smallest full-pixel radius searched when frame-level motion analysis is enabled.
    /// </summary>
    private const int MinimumGlobalMotionSearchRadius = 4;

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
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        int effort)
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
            effort,
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
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeGridCell<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size cellSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        int effort)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(
            configuration,
            image,
            sourceRectangle,
            cellSize,
            stream,
            colorConfig,
            qIndex,
            effort,
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
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlpha<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        int effort)
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
            effort,
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
    /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
    public static ObuSequenceHeader EncodeAlphaGridCell<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size cellSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        int effort)
        where TPixel : unmanaged, IPixel<TPixel>
        => Encode(
            configuration,
            image,
            sourceRectangle,
            cellSize,
            stream,
            colorConfig,
            qIndex,
            effort,
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
        int effort,
        HeifEncodingSpeed speed)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, effort, speed, false);

    /// <summary>
    /// Creates an encoder that retains reconstructed alpha frames for prediction by later samples in the sequence.
    /// </summary>
    public static SequenceEncoder CreateAlphaSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        int effort,
        HeifEncodingSpeed speed)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, effort, speed, true);

    private static ObuSequenceHeader Encode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        int effort,
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
            effort,
            true);

        ObuFrameHeader frameHeader = CreateFrameHeader(
            width,
            height,
            qIndex,
            effort,
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
                effort,
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
                effort,
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
        int effort,
        HeifEncodingSpeed speed,
        bool encodeAlpha)
    {
        if (colorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            return new ByteSequenceEncoder(configuration, width, height, colorConfig, qIndex, effort, speed, encodeAlpha);
        }

        return new HighBitDepthSequenceEncoder(configuration, width, height, colorConfig, qIndex, effort, speed, encodeAlpha);
    }

    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        ObuColorConfig colorConfig,
        int effort,
        bool isStillPicture)
    {
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        ObuSequenceProfile sequenceProfile = colorConfig.BitDepth == Av1BitDepth.TwelveBit ||
            colorFormat == Av1ColorFormat.Yuv422
                ? ObuSequenceProfile.Professional
                : colorFormat == Av1ColorFormat.Yuv444
                    ? ObuSequenceProfile.High
                    : ObuSequenceProfile.Main;

        return new ObuSequenceHeader
        {
            IsStillPicture = isStillPicture,
            IsReducedStillPictureHeader = isStillPicture,
            SequenceProfile = sequenceProfile,
            OperatingPoint = [new ObuOperatingPoint { SequenceLevelIndex = UnconstrainedSequenceLevelIndex }],
            FrameWidthBits = width > 1 ? Av1Math.MostSignificantBit((uint)(width - 1)) + 1 : 1,
            FrameHeightBits = height > 1 ? Av1Math.MostSignificantBit((uint)(height - 1)) + 1 : 1,
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            Use128x128Superblock = Uses128x128Superblock(width, height, effort),
            ForceScreenContentTools = Av1Constants.SelectScreenContentTools,
            ForceIntegerMotionVector = Av1Constants.SelectIntegerMotionVector,
            EnableFilterIntra = effort >= 4,
            EnableDualFilter = !isStillPicture && effort >= MinimumDualInterpolationEffort,
            EnableIntraEdgeFilter = true,
            EnableSuperResolution = false,
            EnableCdef = false,
            EnableRestoration = false,
            ColorConfig = colorConfig
        };
    }

    private static bool Uses128x128Superblock(int width, int height, int effort)
        => effort == MaximumEffort &&
            width >= Av1BlockSize.Block128x128.GetWidth() &&
            height >= Av1BlockSize.Block128x128.GetHeight();

    private static ObuTileGroupHeader CreateTileGroupHeader(
        int width,
        int height,
        int modeInfoColumnCount,
        int modeInfoRowCount,
        int effort)
    {
        int superblockSizeLog2 = Uses128x128Superblock(width, height, effort)
            ? Av1Constants.MaxSuperBlockSizeLog2
            : Av1Constants.MaxSuperBlockSizeLog2 - 1;

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
        int width,
        int height,
        int qIndex,
        int effort,
        ObuFrameType frameType)
    {
        int modeInfoColumnCount = 2 * ((width + 7) >> 3);
        int modeInfoRowCount = 2 * ((height + 7) >> 3);
        ObuTileGroupHeader tiles = CreateTileGroupHeader(
            width,
            height,
            modeInfoColumnCount,
            modeInfoRowCount,
            effort);

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

        ConfigureFrameHeader(frameHeader, qIndex, effort, frameType);
        return frameHeader;
    }

    /// <summary>
    /// Restores every frame-varying encoder field while retaining the fixed geometry and syntax object graph.
    /// </summary>
    private static void ConfigureFrameHeader(
        ObuFrameHeader frameHeader,
        int qIndex,
        int effort,
        ObuFrameType frameType)
    {
        frameHeader.FrameType = frameType;
        frameHeader.ShowFrame = true;
        frameHeader.ErrorResilientMode = true;
        frameHeader.RefreshFrameFlags = byte.MaxValue;
        frameHeader.DisableFrameEndUpdateCdf = true;
        frameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Regular;
        frameHeader.IsMotionModeSwitchable = false;
        frameHeader.TransformMode = qIndex == 0
            ? Av1TransformMode.Only4x4
            : effort >= 6 ? Av1TransformMode.Select : Av1TransformMode.Largest;

        frameHeader.AllowScreenContentTools = false;
        frameHeader.AllowIntraBlockCopy = false;
        frameHeader.ForceIntegerMotionVector = false;
        frameHeader.AllowHighPrecisionMotionVector = false;
        if (frameType == ObuFrameType.InterFrame)
        {
            // Disabling screen-content tools makes force_integer_mv implicitly false. Lower-effort searches
            // still stop at full pixels, but their vectors use the normal fractional-motion syntax.
            frameHeader.AllowHighPrecisionMotionVector = effort >= 8;
            frameHeader.InterpolationFilter = effort >= MinimumSwitchableInterpolationEffort
                ? Av1InterpolationFilter.Switchable
                : Av1InterpolationFilter.Regular;
        }

        frameHeader.QuantizationParameters.BaseQIndex = qIndex;
        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);
    }

    private static int GetTileBufferLength(int width, int height, ObuColorConfig colorConfig)
    {
        // Libaom reserves 2.5 times the 32-sample-aligned native input for an all-intra output packet.
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
        int effort,
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
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration);
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
            effort,
            encodeAlpha);

        using Av1EncoderPictureBuffer picture = new(
            configuration,
            sequenceHeader,
            frameHeader,
            source.Frame.Width,
            source.Frame.Height,
            disallow4x4AllFrames: !frameHeader.CodedLossless && effort < 9);

        picture.Picture.Parent.IsScreenContent = isScreenContent;
        Encode(
            obuWriter,
            stream,
            sequenceHeader,
            frameHeader,
            picture.Picture,
            source,
            reconstruction,
            reconstruction,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            symbolEncoder,
            effort,
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
        int effort,
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
        using Av1EncoderBlockWorkspace blockWorkspace = new(configuration);
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
            effort,
            encodeAlpha);

        using Av1EncoderPictureBuffer picture = new(
            configuration,
            sequenceHeader,
            frameHeader,
            source.Frame.Width,
            source.Frame.Height,
            disallow4x4AllFrames: !frameHeader.CodedLossless && effort < 9);

        picture.Picture.Parent.IsScreenContent = isScreenContent;
        Encode(
            obuWriter,
            stream,
            sequenceHeader,
            frameHeader,
            picture.Picture,
            source,
            reconstruction,
            reconstruction,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            symbolEncoder,
            effort,
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
        int effort,
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
            effort);
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
        int effort,
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
            effort);
    }

    /// <summary>
    /// Resolves the eight-bit frame tools whose syntax depends on the converted source samples.
    /// </summary>
    private static bool ConfigureFrameTools(
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int effort)
    {
        ConfigureGlobalMotion<byte, ByteGlobalMotionSearchOperator>(
            source,
            reference,
            frameHeader,
            sequenceHeader.ColorConfig.BitDepth,
            effort);

        bool isScreenContent = Av1ScreenContentDetector.Detect(
            source,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        frameHeader.AllowScreenContentTools = effort >= 5 && allowScreenContentTools;

        // The current intra-block-copy search owns one 8x8 transform. Lossless coding requires reversible
        // 4x4 transforms, so palette remains available while this incompatible candidate is omitted.
        frameHeader.AllowIntraBlockCopy =
            frameHeader.IsIntra &&
            !frameHeader.CodedLossless &&
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
        int effort,
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
            effort);
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
        int effort,
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
            effort);
    }

    /// <summary>
    /// Resolves the high-bit-depth frame tools whose syntax depends on the converted source samples.
    /// </summary>
    private static bool ConfigureFrameTools(
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reference,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        int effort)
    {
        ConfigureGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(
            source,
            reference,
            frameHeader,
            sequenceHeader.ColorConfig.BitDepth,
            effort);

        bool isScreenContent = Av1ScreenContentDetector.Detect(
            source,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        frameHeader.AllowScreenContentTools = effort >= 5 && allowScreenContentTools;

        // The current intra-block-copy search owns one 8x8 transform. Lossless coding requires reversible
        // 4x4 transforms, so palette remains available while this incompatible candidate is omitted.
        frameHeader.AllowIntraBlockCopy =
            frameHeader.IsIntra &&
            !frameHeader.CodedLossless &&
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
        Av1EncoderFrameBuffer<byte> reference,
        Av1EncoderFrameBuffer<byte> reconstruction,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        Av1SymbolEncoder symbolEncoder,
        int effort,
        bool writeSequenceHeader)
    {
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reference.Frame,
            reconstruction.Frame,
            picture,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            effort);

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
        Av1EncoderFrameBuffer<ushort> reference,
        Av1EncoderFrameBuffer<ushort> reconstruction,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderTileWorkspace tileWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace,
        Av1SymbolEncoder symbolEncoder,
        int effort,
        bool writeSequenceHeader)
    {
        Av1TileEncoder tileWriter = new(
            symbolEncoder,
            source.Frame,
            reference.Frame,
            reconstruction.Frame,
            picture,
            coefficients,
            tileWorkspace,
            blockWorkspace,
            effort);

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
        Av1BitDepth bitDepth,
        int effort)
        where TSample : unmanaged
        where TOperator : struct, IGlobalMotionSearchOperator<TSample>
    {
        Span<Av1GlobalMotionParameters> models = frameHeader.GetGlobalMotionParameters();
        models.Fill(Av1GlobalMotionParameters.Identity);
        if (frameHeader.IsIntra || effort < MinimumGlobalMotionSearchEffort)
        {
            return;
        }

        Buffer2DRegion<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<TSample> referenceLuma = reference.CodedView.GetPlane(Av1Plane.Y);
        int analysisWidth = Math.Min(source.CodedWidth, MaximumGlobalMotionAnalysisDimension);
        int analysisHeight = Math.Min(source.CodedHeight, MaximumGlobalMotionAnalysisDimension);
        Point analysisOrigin = new(
            (source.CodedWidth - analysisWidth) >> 1,
            (source.CodedHeight - analysisHeight) >> 1);

        int effortShift = effort - MinimumGlobalMotionSearchEffort;
        int searchRadius = Math.Min(
            MinimumGlobalMotionSearchRadius << effortShift,
            Math.Min(referenceLuma.Bounds.X, referenceLuma.Bounds.Y));

        Point bestOffset = default;
        long bestAnalysisError = GetGlobalMotionSquaredError<TSample, TOperator>(
            sourceLuma,
            referenceLuma,
            analysisOrigin,
            analysisWidth,
            analysisHeight,
            bestOffset);

        for (int step = searchRadius; step > 0; step >>= 1)
        {
            Point stageBestOffset = bestOffset;
            long stageBestError = bestAnalysisError;
            for (int directionIndex = 0; directionIndex < GlobalMotionSearchDirectionCount; directionIndex++)
            {
                Point direction = GetGlobalMotionSearchDirection(directionIndex);
                Point candidateOffset = new(
                    bestOffset.X + (direction.X * step),
                    bestOffset.Y + (direction.Y * step));

                if (Math.Abs(candidateOffset.X) > searchRadius ||
                    Math.Abs(candidateOffset.Y) > searchRadius)
                {
                    continue;
                }

                long directionError = GetGlobalMotionSquaredError<TSample, TOperator>(
                    sourceLuma,
                    referenceLuma,
                    analysisOrigin,
                    analysisWidth,
                    analysisHeight,
                    candidateOffset);

                // Strict replacement preserves identity and the earlier reference search order on ties.
                if (directionError < stageBestError)
                {
                    stageBestError = directionError;
                    stageBestOffset = candidateOffset;
                }
            }

            bestOffset = stageBestOffset;
            bestAnalysisError = stageBestError;
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

        int rateMultiplier = Av1RateDistortion.GetInterFrameRateMultiplier(
            frameHeader.QuantizationParameters.BaseQIndex,
            bitDepth);

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
            this.parameters = Av1YuvConverter.GetConversionParameters(colorConfig, out HeifColorConversionMode mode);
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
        protected SequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            int effort,
            HeifEncodingSpeed speed,
            bool encodeAlpha,
            bool usesHighBitDepth)
        {
            this.Configuration = configuration;
            this.SequenceHeader = CreateSequenceHeader(width, height, colorConfig, effort, false);
            this.QIndex = qIndex;
            this.Effort = effort;
            this.TileBufferLength = GetTileBufferLength(width, height, colorConfig);
            this.EncodeAlpha = encodeAlpha;
            this.FrameHeader = CreateFrameHeader(
                width,
                height,
                qIndex,
                effort,
                ObuFrameType.KeyFrame);

            this.ConversionWorkspace = new Av1EncoderConversionWorkspace(
                configuration,
                width,
                colorConfig,
                encodeAlpha,
                usesHighBitDepth);

            try
            {
                bool allocateScreenContentState = effort >= 5;
                bool allocateIntraBlockCopySearch =
                    allocateScreenContentState &&
                    !this.FrameHeader.CodedLossless;

                // Sequence geometry and maximum tool capacity are fixed before the first sample. Reusing this owner
                // avoids renting the complete mode grid and optional screen-content index for every frame.
                this.PictureBuffer = new Av1EncoderPictureBuffer(
                    configuration,
                    this.SequenceHeader,
                    this.FrameHeader,
                    width,
                    height,
                    disallow4x4AllFrames: !this.FrameHeader.CodedLossless && effort < 9,
                    allocateScreenContentState: allocateScreenContentState,
                    allocateMotionVectorState: true,
                    allocateIntraBlockCopySearch: allocateIntraBlockCopySearch);

                this.Coefficients = new Av1EncoderCoefficientBuffer(
                    configuration,
                    this.SequenceHeader,
                    width,
                    height);

                this.PictureBuffer.Picture.Parent.EncodingSpeed = speed;
                this.SuperblockWorkspace = new Av1EncoderSuperblockWorkspace(configuration);

                this.TileWorkspace = new Av1EncoderTileWorkspace(this.FrameHeader, this.SuperblockWorkspace);
                this.BlockWorkspace = new Av1EncoderBlockWorkspace(configuration, allocateInterMotionCosts: true);

                // Tile probabilities adapt within a sample, while error-resilient frame headers prohibit carrying
                // those updates into the next sample. The retained encoder is therefore reset before each frame.
                this.SymbolEncoder = new Av1SymbolEncoder(
                    configuration,
                    this.TileBufferLength,
                    qIndex,
                    updateCdf: true);

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

        protected int Effort { get; }

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
        private readonly Av1EncoderFrameBuffer<byte> source;
        private Av1EncoderFrameBuffer<byte> reference;
        private Av1EncoderFrameBuffer<byte> reconstruction;

        public ByteSequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            int effort,
            HeifEncodingSpeed speed,
            bool encodeAlpha)
            : base(
                configuration,
                width,
                height,
                colorConfig,
                qIndex,
                effort,
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
            // A derived constructor can fail before all three frame owners exist.
            this.reconstruction?.Dispose();
            this.reference?.Dispose();
            this.source?.Dispose();
        }

        protected override void EncodeFrame<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            ObuFrameType frameType,
            bool writeSequenceHeader)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            ConfigureFrameHeader(
                frameHeader,
                this.QIndex,
                this.Effort,
                frameType);

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
                this.Effort,
                this.ConversionWorkspace);

            this.PictureBuffer.Reset(frameHeader);
            this.PictureBuffer.Picture.Parent.IsScreenContent = isScreenContent;
            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                this.source,
                this.reference,
                this.reconstruction,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                this.Effort,
                writeSequenceHeader);

            this.reconstruction.Frame.ExtendBorders();

            // The just-reconstructed frame becomes LAST_FRAME for the next sample without copying any plane.
            (this.reference, this.reconstruction) = (this.reconstruction, this.reference);
        }
    }

    private sealed class HighBitDepthSequenceEncoder : SequenceEncoder
    {
        private readonly Av1EncoderFrameBuffer<ushort> source;
        private Av1EncoderFrameBuffer<ushort> reference;
        private Av1EncoderFrameBuffer<ushort> reconstruction;

        public HighBitDepthSequenceEncoder(
            Configuration configuration,
            int width,
            int height,
            ObuColorConfig colorConfig,
            int qIndex,
            int effort,
            HeifEncodingSpeed speed,
            bool encodeAlpha)
            : base(
                configuration,
                width,
                height,
                colorConfig,
                qIndex,
                effort,
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
            // A derived constructor can fail before all three frame owners exist.
            this.reconstruction?.Dispose();
            this.reference?.Dispose();
            this.source?.Dispose();
        }

        protected override void EncodeFrame<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            ObuFrameType frameType,
            bool writeSequenceHeader)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            ConfigureFrameHeader(
                frameHeader,
                this.QIndex,
                this.Effort,
                frameType);

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
                this.Effort,
                this.ConversionWorkspace);

            this.PictureBuffer.Reset(frameHeader);
            this.PictureBuffer.Picture.Parent.IsScreenContent = isScreenContent;
            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                this.source,
                this.reference,
                this.reconstruction,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                this.Effort,
                writeSequenceHeader);

            this.reconstruction.Frame.ExtendBorders();

            // Swapping the frame owners preserves the complete reconstructed reference, including extended borders.
            (this.reference, this.reconstruction) = (this.reconstruction, this.reference);
        }
    }
}

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
    /// The length of the empty temporal delimiter OBU that opens each sample: its header and a zero size.
    /// </summary>
    private const int TemporalDelimiterLength = 2;

    /// <summary>
    /// The sequence-level value that leaves the operating point unconstrained for decoder capability signaling.
    /// </summary>
    private const int UnconstrainedSequenceLevelIndex = 31;

    /// <summary>
    /// The display frame rate that the level inference assumes. The encoder has no timing input, so it uses a default time base of 1/30 second.
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
    /// The default constant-quality level on the zero-through-63 quantizer scale. The encoder keeps this level when the caller does not set one.
    /// </summary>
    private const int DefaultConstantQualityLevel = 10;

    /// <summary>
    /// Selects which source channels a still frame codes.
    /// </summary>
    private enum FrameEncodingKind
    {
        /// <summary>
        /// The color channels.
        /// </summary>
        StillColor,

        /// <summary>
        /// The alpha channel, coded even when it is opaque.
        /// </summary>
        StillAlpha,

        /// <summary>
        /// The alpha channel of a single image, which is not coded when every sample is opaque.
        /// </summary>
        SingleImageAlpha
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
    /// <param name="speed">The encoding speed tier. The encoding uses the default tuning.</param>
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
    /// <param name="speed">The encoding speed tier. The encoding uses the default tuning.</param>
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
    /// <param name="speed">The encoding speed tier. The encoding uses the default tuning.</param>
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
    /// <param name="speed">The encoding speed tier. The encoding uses the default tuning.</param>
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
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="width">The frame width, in samples.</param>
    /// <param name="height">The frame height, in samples.</param>
    /// <param name="colorConfig">The resolved native color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="speed">The encoding speed tier. The sequence uses inter prediction.</param>
    /// <returns>The sequence encoder.</returns>
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
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="width">The frame width, in samples.</param>
    /// <param name="height">The frame height, in samples.</param>
    /// <param name="colorConfig">The resolved native color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <returns>The sequence encoder.</returns>
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
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="width">The frame width, in samples.</param>
    /// <param name="height">The frame height, in samples.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="speed">The encoding speed tier. The sequence uses inter prediction.</param>
    /// <returns>The sequence encoder.</returns>
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
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="width">The frame width, in samples.</param>
    /// <param name="height">The frame height, in samples.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <returns>The sequence encoder.</returns>
    public static SequenceEncoder CreateAlphaSequenceEncoder(
        Configuration configuration,
        int width,
        int height,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options)
        => CreateSequenceEncoder(configuration, width, height, colorConfig, qIndex, options, true);

    /// <summary>
    /// Encodes the alpha channel of a single image as a reduced-still-picture monochrome AV1 frame, unless every converted alpha sample is opaque.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="sequenceHeader">Receives the sequence header describing the encoded payload.</param>
    /// <returns><see langword="false"/> when the alpha is opaque and nothing was written.</returns>
    public static bool TryEncodeSingleImageAlpha<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options,
        out ObuSequenceHeader sequenceHeader)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
        return TryEncode(
            configuration,
            image,
            sourceRectangle,
            sourceRectangle.Size,
            stream,
            colorConfig,
            qIndex,
            options,
            FrameEncodingKind.SingleImageAlpha,
            out sequenceHeader);
    }

    /// <summary>
    /// Returns whether every converted alpha sample of every grid cell of a single image is opaque.
    /// </summary>
    /// <remarks>
    /// The method converts the cells one at a time into one reused buffer. The test stops at the first cell that has a transparent sample.
    /// </remarks>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="cellSize">The size of each grid cell in the source image.</param>
    /// <param name="encodedCellSize">The encoded cell dimensions, including any required edge padding.</param>
    /// <param name="colorConfig">The resolved monochrome precision configuration of the alpha.</param>
    /// <returns><see langword="true"/> when every alpha sample is opaque.</returns>
    public static bool IsGridAlphaOpaque<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Size cellSize,
        Size encodedCellSize,
        ObuColorConfig colorConfig)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        if (colorConfig.BitDepth == Av1BitDepth.EightBit)
        {
            using Av1EncoderFrameBuffer<byte> buffer = new(
                configuration,
                encodedCellSize.Width,
                encodedCellSize.Height,
                ByteSampleBitDepth,
                colorFormat,
                chromaPositionX: CenteredChromaSamplePosition,
                chromaPositionY: CenteredChromaSamplePosition,
                lumaBorder: Av1EncoderFrame<byte>.LumaBorder);

            for (int y = 0; y < image.Height; y += cellSize.Height)
            {
                for (int x = 0; x < image.Width; x += cellSize.Width)
                {
                    Rectangle cell = new(x, y, Math.Min(cellSize.Width, image.Width - x), Math.Min(cellSize.Height, image.Height - y));
                    PrepareSource<TPixel, byte, HeifByteSampleConverter>(configuration, image, cell, buffer.Frame, colorConfig, true);
                    if (!IsOpaque(buffer.Frame, cell.Size, byte.MaxValue))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        int bitDepth = colorConfig.BitDepth.GetBitCount();
        ushort opaqueValue = (ushort)((1 << bitDepth) - 1);
        using Av1EncoderFrameBuffer<ushort> highBitDepthBuffer = new(
            configuration,
            encodedCellSize.Width,
            encodedCellSize.Height,
            bitDepth,
            colorFormat,
            chromaPositionX: CenteredChromaSamplePosition,
            chromaPositionY: CenteredChromaSamplePosition,
            lumaBorder: Av1EncoderFrame<ushort>.LumaBorder);

        for (int y = 0; y < image.Height; y += cellSize.Height)
        {
            for (int x = 0; x < image.Width; x += cellSize.Width)
            {
                Rectangle cell = new(x, y, Math.Min(cellSize.Width, image.Width - x), Math.Min(cellSize.Height, image.Height - y));
                PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(configuration, image, cell, highBitDepthBuffer.Frame, colorConfig, true);
                if (!IsOpaque(highBitDepthBuffer.Frame, cell.Size, opaqueValue))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Encodes one reduced-still-picture AV1 frame of the color or the alpha of a source region.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded frame.</param>
    /// <param name="frameSize">The encoded frame dimensions.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="encodingKind">Whether the frame codes color or alpha.</param>
    /// <returns>The sequence header describing the encoded payload.</returns>
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
        _ = TryEncode(
            configuration,
            image,
            sourceRectangle,
            frameSize,
            stream,
            colorConfig,
            qIndex,
            options,
            encodingKind,
            out ObuSequenceHeader sequenceHeader);

        return sequenceHeader;
    }

    /// <summary>
    /// Encodes one reduced-still-picture AV1 frame of the color or the alpha of a source region, or writes nothing
    /// for the opaque alpha of a single image.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded frame.</param>
    /// <param name="frameSize">The encoded frame dimensions.</param>
    /// <param name="stream">The destination receiving the complete AV1 item payload.</param>
    /// <param name="colorConfig">The resolved color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="encodingKind">Whether the frame codes color, alpha, or the alpha of a single image.</param>
    /// <param name="sequenceHeader">Receives the sequence header describing the encoded payload.</param>
    /// <returns><see langword="false"/> when the alpha of a single image is opaque and nothing was written.</returns>
    private static bool TryEncode<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options,
        FrameEncodingKind encodingKind,
        out ObuSequenceHeader sequenceHeader)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frameSize.Width;
        int height = frameSize.Height;
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        sequenceHeader = CreateSequenceHeader(
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

        return colorConfig.BitDepth == Av1BitDepth.EightBit
            ? EncodeByte(
                configuration,
                image,
                sourceRectangle,
                frameSize,
                stream,
                sequenceHeader,
                frameHeader,
                colorFormat,
                options,
                encodingKind)
            : EncodeHighBitDepth(
                configuration,
                image,
                sourceRectangle,
                frameSize,
                stream,
                sequenceHeader,
                frameHeader,
                colorFormat,
                options,
                encodingKind);
    }

    /// <summary>
    /// Creates the sequence encoder that matches the sample precision of the color configuration.
    /// </summary>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="width">The frame width, in samples.</param>
    /// <param name="height">The frame height, in samples.</param>
    /// <param name="colorConfig">The resolved color and precision configuration.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="encodeAlpha">Whether the sequence codes the alpha channel instead of the color channels.</param>
    /// <returns>A byte sequence encoder for 8-bit samples, otherwise a high bit depth sequence encoder.</returns>
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

    /// <summary>
    /// Creates the sequence header that selects the profile, superblock size, level, operating points and coding tools for the frame size and options.
    /// </summary>
    /// <param name="width">The frame width, in samples.</param>
    /// <param name="height">The frame height, in samples.</param>
    /// <param name="colorConfig">The resolved color and precision configuration.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="isStillPicture">Whether the sequence holds one still picture with a reduced header and no inter tools.</param>
    /// <returns>The sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        ObuColorConfig colorConfig,
        Av1EncoderOptions options,
        bool isStillPicture)
    {
        Av1ColorFormat colorFormat = colorConfig.GetColorFormat();
        Av1EncoderSpeedSettings speedSettings = new(
            options.Speed, options.IsAllIntra, intraFrame: true, Av1FrameUpdateType.Key, qIndex: 0, new Size(width, height));

        ObuSequenceProfile sequenceProfile = colorConfig.BitDepth == Av1BitDepth.TwelveBit ||
            colorFormat == Av1ColorFormat.Yuv422
                ? ObuSequenceProfile.Professional
                : colorFormat == Av1ColorFormat.Yuv444
                    ? ObuSequenceProfile.High
                    : ObuSequenceProfile.Main;

        // The superblock size depends on the speed and the frame size. Real-time coding uses 128x128 only when the smaller dimension is more than 720.
        // Otherwise, speed 1 and higher use 64x64 when the smaller dimension is 480 or less. All-intra speed 9 and higher also use 64x64 below 2160.
        // Variance Boost supports only 64x64 superblocks. Spatial layers also use only 64x64 superblocks.
        int minimumDimension = Math.Min(width, height);
        bool use128x128Superblock = options.DeltaQMode != Av1DeltaQMode.VarianceBoost && options.LayerCount == 1 && (speedSettings.IsRealtime
            ? minimumDimension > 720
            : !(options.Speed >= HeifEncodingSpeed.Level1 && minimumDimension <= 480) &&
                !(options.IsAllIntra && options.Speed >= HeifEncodingSpeed.Level9 && minimumDimension < 2160));

        // A layered image lists one operating point per layer. Operating point i decodes the spatial layers from 0 up to the last layer minus i.
        // All layers use the single temporal layer, so operating point 0 decodes every layer. Each frame carries its layer in an OBU extension header.
        // The Idc value sets one bit for each decoded spatial layer in bits 8 and higher, and bit 0 for temporal layer 0.
        // Every operating point gets the level of the frame size.
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

            // Good-quality speed 6 and higher turns off masked compound when the smaller dimension is 720 or more.
            // The sequence header fixes this choice for every frame.
            EnableMaskedCompound = !isStillPicture &&
                !(!speedSettings.IsRealtime && options.Speed >= HeifEncodingSpeed.Level6 && minimumDimension >= 720),
            EnableInterIntraCompound = !isStillPicture && speedSettings.EnableInterIntraCompound,

            // A sequence enables temporal motion vectors and warped motion. The frame header decides whether each frame uses them.
            // Distance-weighted compound follows the speed settings. An inter sequence uses 7 order hint bits.
            EnableWarpedMotion = !isStillPicture,
            OrderHintInfo = new ObuOrderHintInfo
            {
                EnableOrderHint = !isStillPicture,
                EnableJointCompound = !isStillPicture && speedSettings.UseDistanceWeightedCompound,
                EnableReferenceFrameMotionVectors = !isStillPicture,
                OrderHintBits = isStillPicture ? 0 : 7
            },
            EnableSuperResolution = false,

            // The CDEF control option turns CDEF on or off. All-intra coding turns CDEF off by default because CDEF blurs images.
            // The image quality tune turns it on again with adaptive strengths.
            EnableCdef = options.CdefControl != Av1CdefControl.None,
            EnableRestoration = speedSettings.EnableRestoration && options.EnableRestoration,
            AreFilmGrainingParametersPresent = options.HasFilmGrain,
            ColorConfig = colorConfig
        };
    }

    /// <summary>
    /// Infers the lowest level whose picture size, dimension, and display sample rate limits hold the frame.
    /// </summary>
    /// <remarks>
    /// The table stops at level 6.2. A frame that is too large for level 6.2 gets the unconstrained level index instead of level 7.x or 8.x.
    /// </remarks>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="frameRate">The display frame rate.</param>
    /// <returns>The sequence level index.</returns>
    private static int GetSequenceLevelIndex(int width, int height, int frameRate)
    {
        // Each row holds the maximum width and height of the level, its maximum frame rate at that size, the multiple of the width and height
        // that a single dimension can reach, and the level index.
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
    /// Creates the uniform tile layout for the requested tile counts, clamped to the tile limits of the bitstream and the encoder.
    /// </summary>
    /// <param name="modeInfoColumnCount">The frame width, in 4x4 mode-info units.</param>
    /// <param name="modeInfoRowCount">The frame height, in 4x4 mode-info units.</param>
    /// <param name="superblockSizeLog2">The base-2 logarithm of the superblock size, in samples.</param>
    /// <param name="requestedTileColumnsLog2">The requested base-2 logarithm of the tile column count.</param>
    /// <param name="requestedTileRowsLog2">The requested base-2 logarithm of the tile row count.</param>
    /// <returns>The tile layout with uniform spacing.</returns>
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

        // The encoder column minimum is the smallest split whose widest tiles cover more than every superblock column.
        // It is one split more than the bitstream minimum when the superblock column count is the maximum tile width times a power of two.
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

        // The uniform tile boundaries use superblock units. The last entries keep the exact visible mode-info dimensions,
        // so clipped right and bottom superblocks end at the frame boundary.
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

    /// <summary>
    /// Creates a frame header with the frame geometry and tile layout of the sequence, then sets the frame-varying fields.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header that gives the frame size and superblock size.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="frameType">The type of the frame.</param>
    /// <returns>The frame header.</returns>
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
    /// Restores every frame-varying encoder field and keeps the fixed geometry and syntax object graph.
    /// </summary>
    /// <param name="frameHeader">The frame header to update.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoding options used to select frame and block search policies.</param>
    /// <param name="frameType">The type of the frame.</param>
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
    /// Sets the quantizer of a frame and every frame field that depends on it. The motion vector precision and the reference mode use the final
    /// quantizer of the frame.
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
        frameHeader.AllowHighPrecisionMotionVector = false;
        if (frameHeader.FrameType == ObuFrameType.InterFrame)
        {
            // The speed settings select the fractional motion vector precision from the frame quantizer. A frame that forces integer vectors
            // turns off high precision. The update type does not change this choice, so the settings use the Last update type.
            Av1EncoderSpeedSettings speedSettings = new(
                options.Speed,
                allIntra: false,
                intraFrame: false,
                Av1FrameUpdateType.Last,
                qIndex,
                new Size(frameHeader.FrameSize.SuperResolutionUpscaledWidth, frameHeader.FrameSize.FrameHeight));

            frameHeader.AllowHighPrecisionMotionVector =
                speedSettings.AllowHighPrecisionMotionVector && !frameHeader.ForceIntegerMotionVector;

            // Real-time coding without estimated compound prediction codes only single references.
            // All other inter frames let each block select its reference mode.
            frameHeader.ReferenceMode = speedSettings.IsRealtime && !speedSettings.UseEstimatedCompound
                ? ObuReferenceMode.SingleReference
                : ObuReferenceMode.ReferenceModeSelect;
        }

        Av1FrameQuantizer.SetQuantizer(
            frameHeader.QuantizationParameters,
            sequenceHeader.ColorConfig,
            qIndex,
            options,
            options.IsAllIntra);

        Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

        // Only a frame where every segment is lossless fixes its transforms at 4x4. The coded-lossless flag needs the plane quantizer
        // adjustments and the segment quantizers, so this assignment comes after the quantizer state update.
        frameHeader.TransformMode = frameHeader.CodedLossless
            ? Av1TransformMode.Only4x4
            : Av1TransformMode.Select;
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

    /// <summary>
    /// Encodes one eight-bit still frame, or writes nothing for the opaque alpha of a single image.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded frame.</param>
    /// <param name="frameSize">The encoded frame dimensions.</param>
    /// <param name="stream">The destination receiving the AV1 payload.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="colorFormat">The coded color format.</param>
    /// <param name="options">The encoding options.</param>
    /// <param name="encodingKind">Whether the frame codes color, alpha, or the alpha of a single image.</param>
    /// <returns><see langword="false"/> when the alpha of a single image is opaque and nothing was written.</returns>
    private static bool EncodeByte<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat,
        Av1EncoderOptions options,
        FrameEncodingKind encodingKind)
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

        PrepareSource<TPixel, byte, HeifByteSampleConverter>(
            configuration, image, sourceRectangle, source.Frame, sequenceHeader.ColorConfig, encodingKind != FrameEncodingKind.StillColor);

        // A single image whose converted alpha samples are all opaque gets no alpha item.
        if (encodingKind == FrameEncodingKind.SingleImageAlpha && IsOpaque(source.Frame, sourceRectangle.Size, byte.MaxValue))
        {
            return false;
        }

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

        // The frame starts at the requested quantizer. A bit budget can replace it after the screen content decision.
        int requestedQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        ScreenContentDecision decision = default;
        bool isScreenContent = ConfigureFrameTools(
            configuration,
            source.Frame,
            reconstruction.Frame,
            sequenceHeader,
            frameHeader,
            options,
            ref decision);

        // The coefficient contexts start from the final quantizer.
        using Av1SymbolEncoder symbolEncoder = new(
            configuration,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        using Av1EncoderBlockWorkspace blockWorkspace = new(
            configuration,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: frameHeader.AllowIntraBlockCopy,
            sequenceHeader.SuperblockSize);

        Av1EncoderSpeedSettings speedSettings = new(
            options.Speed,
            options.IsAllIntra,
            frameHeader.IsIntra,
            Av1FrameUpdateType.Key,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameSize);

        Av1MotionSearchSettings motionSettings = new(
            options.Speed,
            options.IsAllIntra,
            frameSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            frameHeader.AllowScreenContentTools,
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

        // Every still image uses time stamp 0.
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

        return true;
    }

    /// <summary>
    /// Encodes one high-bit-depth still frame, or writes nothing for the opaque alpha of a single image.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded frame.</param>
    /// <param name="frameSize">The encoded frame dimensions.</param>
    /// <param name="stream">The destination receiving the AV1 payload.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="colorFormat">The coded color format.</param>
    /// <param name="options">The encoding options.</param>
    /// <param name="encodingKind">Whether the frame codes color, alpha, or the alpha of a single image.</param>
    /// <returns><see langword="false"/> when the alpha of a single image is opaque and nothing was written.</returns>
    private static bool EncodeHighBitDepth<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        Size frameSize,
        Stream stream,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1ColorFormat colorFormat,
        Av1EncoderOptions options,
        FrameEncodingKind encodingKind)
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

        PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(
            configuration, image, sourceRectangle, source.Frame, sequenceHeader.ColorConfig, encodingKind != FrameEncodingKind.StillColor);

        // A single image whose converted alpha samples are all opaque gets no alpha item.
        if (encodingKind == FrameEncodingKind.SingleImageAlpha && IsOpaque(source.Frame, sourceRectangle.Size, (ushort)((1 << bitDepth) - 1)))
        {
            return false;
        }

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

        // The frame starts at the requested quantizer. A bit budget can replace it after the screen content decision.
        int requestedQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        ScreenContentDecision decision = default;
        bool isScreenContent = ConfigureFrameTools(
            configuration,
            source.Frame,
            reconstruction.Frame,
            sequenceHeader,
            frameHeader,
            options,
            ref decision);

        // The coefficient contexts start from the final quantizer.
        using Av1SymbolEncoder symbolEncoder = new(
            configuration,
            frameHeader.QuantizationParameters.BaseQIndex,
            updateCdf: !frameHeader.DisableCdfUpdate);

        using Av1EncoderBlockWorkspace blockWorkspace = new(
            configuration,
            allocateInterMotionCosts: false,
            allocateDisplacementCosts: frameHeader.AllowIntraBlockCopy,
            sequenceHeader.SuperblockSize);

        Av1EncoderSpeedSettings speedSettings = new(
            options.Speed,
            options.IsAllIntra,
            frameHeader.IsIntra,
            Av1FrameUpdateType.Key,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameSize);

        Av1MotionSearchSettings motionSettings = new(
            options.Speed,
            options.IsAllIntra,
            frameSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            frameHeader.AllowScreenContentTools,
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

        // Every still image uses time stamp 0.
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

        return true;
    }

    /// <summary>
    /// Returns whether every sample of the visible luma plane equals the opaque value. The luma plane holds the converted alpha samples.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <param name="frame">The converted frame, whose luma plane holds the alpha samples.</param>
    /// <param name="size">The visible size of the plane.</param>
    /// <param name="opaqueValue">The opaque value of the coded depth.</param>
    /// <returns><see langword="true"/> when every visible sample is opaque.</returns>
    private static bool IsOpaque<TSample>(Av1EncoderFrame<TSample> frame, Size size, TSample opaqueValue)
        where TSample : unmanaged, IEquatable<TSample>
    {
        Av1EncoderFrame<TSample>.PlanarView view = frame.CodedView.GetSubView(size.Width, size.Height);
        for (int y = 0; y < size.Height; y++)
        {
            if (view.GetLumaRowSpan(y)[..size.Width].ContainsAnyExcept(opaqueValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts one eight-bit sequence sample through its retained row workspace, then resolves the frame coding tools.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded frame.</param>
    /// <param name="source">The source frame that receives the converted samples.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header that receives the frame tools.</param>
    /// <param name="options">The encoding options.</param>
    /// <param name="conversionWorkspace">The retained row workspace for the pixel conversion.</param>
    /// <param name="decision">The screen content decision. Intra frames replace it. Inter frames keep it.</param>
    /// <returns><see langword="true"/> when the frame codes as screen content.</returns>
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
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="source">The converted source frame.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header that receives the quantizer and the screen content tools.</param>
    /// <param name="options">The encoding options.</param>
    /// <param name="decision">The screen content decision. Intra frames replace it. Inter frames keep it.</param>
    /// <returns><see langword="true"/> when the frame codes as screen content.</returns>
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
    /// Classifies the unfiltered eight-bit source of an intra frame as screen content or not. Inter frames keep the decision of the last intra frame.
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
            options.IsAllIntra,
            options.Speed,
            options.Tuning,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        decision.AllowScreenContentTools = allowScreenContentTools;
        decision.AllowIntraBlockCopy = allowIntraBlockCopy;
    }

    /// <summary>
    /// Returns the constant-quality level of the encoder. The constant-quality and constrained-quality modes use the requested quantizer.
    /// The bit-rate modes use the default level of 10.
    /// </summary>
    /// <param name="options">The encoder options.</param>
    /// <param name="requestedQIndex">The quantizer index of the requested quality.</param>
    /// <returns>The constant-quality level as a quantizer index.</returns>
    private static int GetConstantQualityLevel(Av1EncoderOptions options, int requestedQIndex)
        => options.UsesConstantQualityLevel ? requestedQIndex : Av1QuantizationLookup.GetQIndex(DefaultConstantQualityLevel);

    /// <summary>
    /// Sets the quantizer of a still image that codes against a bit budget. The rate model reads the screen content decision.
    /// The intra block copy decision and the quantizer-dependent speed features then read the quantizer that the rate model picks.
    /// A constant-quality still image keeps the requested quantizer.
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
    /// Writes the screen content decision to the frame header. Inter frames keep the tools of the last intra frame and never copy blocks.
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
            options.IsAllIntra,
            sourceSize,
            frameHeader.QuantizationParameters.BaseQIndex,
            frameHeader.IsIntra,
            frameHeader.AllowScreenContentTools,
            options.Tuning);

        frameHeader.AllowIntraBlockCopy =
            motionSettings.AllowIntraBlockCopy &&
            frameHeader.AllowScreenContentTools &&
            decision.AllowIntraBlockCopy;
    }

    /// <summary>
    /// Converts one high-bit-depth sequence sample through its retained row workspace, then resolves the frame coding tools.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the encoded frame.</param>
    /// <param name="source">The source frame that receives the converted samples.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header that receives the frame tools.</param>
    /// <param name="options">The encoding options.</param>
    /// <param name="conversionWorkspace">The retained row workspace for the pixel conversion.</param>
    /// <param name="decision">The screen content decision. Intra frames replace it. Inter frames keep it.</param>
    /// <returns><see langword="true"/> when the frame codes as screen content.</returns>
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
    /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
    /// <param name="source">The converted source frame.</param>
    /// <param name="reference">The reconstructed reference frame.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header that receives the quantizer and the screen content tools.</param>
    /// <param name="options">The encoding options.</param>
    /// <param name="decision">The screen content decision. Intra frames replace it. Inter frames keep it.</param>
    /// <returns><see langword="true"/> when the frame codes as screen content.</returns>
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
    /// Classifies the unfiltered high-bit-depth source of an intra frame as screen content or not.
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
            options.IsAllIntra,
            options.Speed,
            options.Tuning,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);

        decision.AllowScreenContentTools = allowScreenContentTools;
        decision.AllowIntraBlockCopy = allowIntraBlockCopy;
    }

    /// <summary>
    /// Codes one eight-bit frame and writes its OBUs.
    /// </summary>
    /// <param name="obuWriter">The OBU writer.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier. Each entry is the reference, or its copy resized to the
    /// size of the coded frame.
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
    /// Codes one frame of more than eight bits and writes its OBUs.
    /// </summary>
    /// <param name="obuWriter">The OBU writer.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="sequenceHeader">The sequence header.</param>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="source">The coded source frame.</param>
    /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
    /// <param name="searchReferences">
    /// The frames that the motion search reads, indexed by prediction reference identifier. Each entry is the reference, or its copy resized to the
    /// size of the coded frame.
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
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <typeparam name="TSample">The native component sample type.</typeparam>
    /// <typeparam name="TStorer">The converter that stores normalized components as native samples.</typeparam>
    /// <param name="configuration">The configuration used for row-buffer allocation and pixel conversion.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the frame.</param>
    /// <param name="source">The bordered frame that receives the converted samples.</param>
    /// <param name="colorConfig">The color configuration that selects the color conversion.</param>
    /// <param name="encodeAlpha">Whether to convert the alpha channel into the luma plane instead of the color channels.</param>
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

            // The conversion writes into the final bordered analysis planes. Later coding stages read the native source,
            // so no second full-frame copy from an intermediate component buffer is necessary.
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

        // A short grid edge cell can fill only the top-left of its AV1 frame. Edge replication initializes the remaining coded cell
        // and the physical prediction border, without another image or plane copy.
        source.CodedView.ExtendBorders(sourceRectangle.Width, sourceRectangle.Height);
    }

    /// <summary>
    /// Converts one sequence sample with track-owned row storage and initializes every coded and physical edge.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <typeparam name="TSample">The native component sample type.</typeparam>
    /// <typeparam name="TStorer">The converter that stores normalized components as native samples.</typeparam>
    /// <param name="configuration">The configuration used for row-buffer allocation and pixel conversion.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="sourceRectangle">The source region copied into the top-left of the frame.</param>
    /// <param name="source">The bordered frame that receives the converted samples.</param>
    /// <param name="conversionWorkspace">The track-owned row workspace that does the pixel conversion.</param>
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

        // The sequence geometry is fixed, but a grid edge cell can hold less source data than its coded extent.
        // Edge replication completes the coded padding and the physical prediction border.
        source.CodedView.ExtendBorders(sourceRectangle.Width, sourceRectangle.Height);
    }

    /// <summary>
    /// Measures source changes over 64x64 blocks and keeps the block errors that later mode decisions use.
    /// </summary>
    /// <typeparam name="TSample">The source sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific error operations.</typeparam>
    /// <param name="source">The current bordered source planes.</param>
    /// <param name="previousSource">The preceding bordered source planes.</param>
    /// <param name="gridSize">
    /// The luma size whose 64x64 blocks the method measures from the top-left corner of the sources. This is the size that the encoder holds
    /// before it sets the size of the frame.
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
        ReadOnlySpan<TSample> currentSamples = current.Samples;
        ReadOnlySpan<TSample> previousSamples = previous.Samples;
        int columns = (gridSize.Width + 63) >> 6;
        int rows = (gridSize.Height + 63) >> 6;
        int unchanged = 0;
        ulong total = 0;

        // Each block SAD stays in storage for the later superblock pass. Both planes have replicated borders, so partial visible blocks use
        // the same complete 64x64 measurement. The shift scales a high-bit-depth SAD to the eight-bit range.
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Point origin = new(column << 6, row << 6);
                ulong sad = (ulong)TOperator.SumAbsoluteDifferences(
                    currentSamples[current.GetOffset(origin.X, origin.Y)..],
                    current.Stride,
                    previousSamples[previous.GetOffset(origin.X, origin.Y)..],
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

        // The scene change test always uses the higher thresholds. The lower thresholds apply only to the screen content option and to low
        // frame rates. The encoder never sets that option, and its fixed frame rate of 30 is not low enough.
        // A frame has a high source change when its average SAD is more than the minimum and more than six times the running average.
        // The frame must also come more than two frames after the key frame, and the count of unchanged blocks must be less than a limit.
        // The limit is three quarters of the blocks when the average SAD is more than eight times the minimum, otherwise half of the blocks.
        const uint minimum = 100000U;
        const int multiplier = 6;
        int unchangedLimit = average > 8 * minimum ? 3 * (count >> 2) : count >> 1;
        parent.HighSourceSad = average > Math.Max(minimum, (uint)(averageSourceSad * (ulong)multiplier)) &&
            framesSinceKey > 2 && unchanged < unchangedLimit;

        parent.FrameSourceSad = average;
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
            Span<short> intermediateTile)
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
                intermediateTile);

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
            Span<short> intermediateTile)
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
                intermediateTile);

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
            // The conversion parameters resolve before the storage rent. If the color description is not supported, the constructor throws before
            // it owns memory. The sequence encoder never receives a failed workspace, so it cannot dispose its storage.
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
    internal abstract partial class SequenceEncoder : IDisposable
    {
        /// <summary>
        /// The number of rotating slots that hold LAST and ALTREF.
        /// </summary>
        private const int RotatingSlotCount = 6;

        /// <summary>
        /// The border, in luma samples, of a reference that is larger than the current frame. The scaled prediction clamps its source positions
        /// to this border.
        /// </summary>
        protected const int ScaledReferenceBorder = Av1ReferenceScale.ClampBorder;

        /// <summary>
        /// The fixed GOLDEN slot.
        /// </summary>
        private const int GoldenSlot = 6;

        /// <summary>
        /// The slot that no reference uses. Every reference index starts at this slot.
        /// </summary>
        private const int UnusedSlot = 7;

        /// <summary>
        /// The golden interval when cyclic refresh gives no refresh period.
        /// </summary>
        private const int FixedGoldenIntervalRealtime = 80;

        /// <summary>
        /// The largest golden interval of real-time coding.
        /// </summary>
        private const int MaximumGoldenIntervalRealtime = 160;

        /// <summary>
        /// The golden interval when recent frames had little zero motion.
        /// </summary>
        private const int LowMotionGoldenInterval = 16;

        /// <summary>
        /// The wrap point of the golden group index.
        /// </summary>
        private const int MaximumStaticGoldenGroupLength = 250;

        private uint nextOrderHint;

        /// <summary>
        /// The number of frames coded before the current frame.
        /// </summary>
        private uint frameNumber;

        /// <summary>
        /// The constant-bitrate model of a real-time sequence, or <see langword="null"/> for good-quality coding.
        /// </summary>
        private readonly Av1RateControl? rateControl;

        /// <summary>
        /// Whether the last coded frame was intra only. The setup of the next frame reads this value before the new frame type is set.
        /// </summary>
        private bool previousFrameIntra = true;

        /// <summary>
        /// Whether the last coded frame refreshed GOLDEN. Cyclic refresh reads this value when it sets up the next frame, before the refresh flags
        /// of the new frame are set.
        /// </summary>
        private bool previousRefreshesGolden;

        /// <summary>
        /// The one-pass rate model of a good-quality sequence without lookahead under a bit budget, or <see langword="null"/> for constant-quality
        /// and real-time coding. The model allocates the bits of each golden group.
        /// </summary>
        private readonly Av1RateControl? groupRateControl;

        /// <summary>
        /// The cyclic refresh of a real-time sequence that uses it, or <see langword="null"/>.
        /// </summary>
        private readonly Av1CyclicRefresh? cyclicRefresh;

        /// <summary>
        /// The noise estimate of a real-time sequence with cyclic refresh, or <see langword="null"/>.
        /// </summary>
        private readonly Av1NoiseEstimate? noiseEstimate;

        /// <summary>
        /// The film grain that the sequence signals, or <see langword="null"/>.
        /// </summary>
        private readonly Av1FilmGrainState? filmGrain;

        /// <summary>
        /// The number of frames that the real-time sequence coded. Key frames do not reset this count.
        /// </summary>
        private int codedFrameCount;

        /// <summary>
        /// The requested quantizer index, which constant-quality coding keeps for inter frames. A layered image sets it for each layer.
        /// </summary>
        private int constantQualityIndex;

        /// <summary>
        /// The number of layers of a layered image coded so far.
        /// </summary>
        private int codedLayerCount;

        /// <summary>
        /// The coded size of the current frame. This is the sequence size, or the size of a scaled layer.
        /// </summary>
        private Size frameSize;

        /// <summary>
        /// The frame size that the encoder holds before it sets the size of the next frame. This is the size of the last coded frame,
        /// or the sequence size before the first frame or after a reconfiguration. The SSIM factors and the real-time scene detection of the next
        /// frame measure over this size.
        /// </summary>
        private Size presetupFrameSize;

        /// <summary>
        /// The size of the last coded frame, or the sequence size before the first frame.
        /// </summary>
        private Size codedFrameSize;

        /// <summary>
        /// Whether a fixed resize mode applies to the current frame. A scaled layer after the first sets it on the running encoder.
        /// The next quality change rebuilds the configuration without it.
        /// </summary>
        private bool resizesFixed;

        /// <summary>
        /// The rate multiplier scaling factors of the SSIM and image tunes. The array has the layout of the largest grid and is allocated once,
        /// so it keeps the entries that a smaller frame does not measure.
        /// </summary>
        private double[]? ssimRateMultiplierFactors;

        /// <summary>
        /// Whether the current frame is a layer after the first. Such a layer does not reference or update GOLDEN and the alternate references.
        /// </summary>
        private bool usesLayerFlags;

        /// <summary>
        /// The running average quantizer index of the ordinary inter frames of constant-quality coding. It starts in the middle of the allowed range.
        /// </summary>
        private int averageInterQIndex;

        /// <summary>
        /// The frames left before the next key frame of good-quality coding without lookahead.
        /// </summary>
        private int framesToKey;

        /// <summary>
        /// Whether the key frame interval placed the current key frame.
        /// </summary>
        private bool thisKeyFrameForced;

        /// <summary>
        /// The quantizer index of the last key frame or golden update of good-quality coding, or a lower index of a later frame.
        /// </summary>
        private int lastBoostedQIndex;

        /// <summary>
        /// Whether the current real-time golden group ends at the next key frame.
        /// </summary>
        private bool isConstrainedGoldenGroup;

        /// <summary>
        /// The number of frames left before the next golden update is due.
        /// </summary>
        private int framesTillGoldenUpdateDue;

        /// <summary>
        /// The position of the current frame in its golden group. A value of 0 marks a golden update.
        /// </summary>
        private int goldenFrameIndex;

        /// <summary>
        /// The golden interval of the current real-time golden group.
        /// </summary>
        private int baselineGoldenInterval;

        /// <summary>
        /// The number of the frame that last refreshed GOLDEN.
        /// </summary>
        private uint lastGoldenRefreshFrameNumber;

        /// <summary>
        /// The number of frames since the last GOLDEN refresh.
        /// </summary>
        private int framesSinceGolden;

        /// <summary>
        /// The slot that holds the entropy context of the most recent frame of the only context type that one-layer real-time coding uses,
        /// or -1 when there is none.
        /// </summary>
        private int contextTypeSlot = -1;

        /// <summary>
        /// The initial OBMC probability of each frame update type and block size.
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

        /// <summary>
        /// The running warped-motion usage probability, out of 128, of each frame update type.
        /// </summary>
        private readonly int[] warpedProbabilities = [64, 64, 64, 64, 64, 64, 64];

        /// <summary>
        /// The running OBMC usage probability of each frame update type and block size. It starts from <see cref="DefaultObmcProbabilities"/>.
        /// </summary>
        private readonly int[] obmcProbabilities = (int[])DefaultObmcProbabilities.Clone();

        /// <summary>
        /// The reference structure of good-quality frames coded without lookahead.
        /// </summary>
        private readonly Av1GoodQualityReferenceStructure goodQualityStructure = new();

        /// <summary>
        /// The global motion models of the frame in each reference slot, seven for each slot. A key frame refreshes every slot before any frame
        /// reads them.
        /// </summary>
        private readonly Av1GlobalMotionParameters[] slotGlobalMotion =
            new Av1GlobalMotionParameters[Av1Constants.ReferenceFrameCount * Av1Constants.ReferencesPerFrame];

        /// <summary>
        /// Initializes a new instance of the <see cref="SequenceEncoder"/> class.
        /// </summary>
        /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
        /// <param name="width">The sequence width, in samples.</param>
        /// <param name="height">The sequence height, in samples.</param>
        /// <param name="colorConfig">The resolved color and precision configuration.</param>
        /// <param name="qIndex">The requested quantizer index.</param>
        /// <param name="options">The encoding options used to select frame and block search policies.</param>
        /// <param name="encodeAlpha">Whether the sequence codes the alpha channel instead of the color channels.</param>
        /// <param name="usesHighBitDepth">Whether the samples use more than eight bits.</param>
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
                // The source classification decides screen content eligibility for each frame. The sequence allocates the optional state once,
                // so any frame classified as screen content can use those tools. Intra block copy search also needs the speed features to allow it.
                // These features do not read the screen content decision, and real-time coding never searches intra block copy.
                bool allocateScreenContentState = true;
                Av1MotionSearchSettings motionSettings = new(
                    options.Speed,
                    options.IsAllIntra,
                    new Size(width, height),
                    qIndex,
                    this.FrameHeader.IsIntra,
                    screenContent: false,
                    options.Tuning);

                bool allocateIntraBlockCopySearch =
                    allocateScreenContentState &&
                    motionSettings.AllowIntraBlockCopy;

                // The sequence geometry and the maximum tool capacity are fixed before the first sample. One reused owner prevents a rent of the
                // complete mode grid and the optional screen content index for every frame.
                Av1EncoderSpeedSettings speedSettings = new(
                    options.Speed,
                    options.IsAllIntra,
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

                // Real-time coding runs the one-pass rate control of its mode between the minimum and maximum allowed quantizers.
                if (speedSettings.IsRealtime)
                {
                    int bestAllowedQIndex = Av1QuantizationLookup.GetQIndex(options.MinimumQuantizer);
                    int worstAllowedQIndex = Av1QuantizationLookup.GetQIndex(options.MaximumQuantizer);

                    // Cyclic refresh runs only with the real-time rate control.
                    if (options.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.CyclicRefresh)
                    {
                        this.cyclicRefresh = new Av1CyclicRefresh(this.FrameHeader.ModeInfoColumnCount, this.FrameHeader.ModeInfoRowCount);
                        this.PictureBuffer.Picture.Parent.CyclicRefresh = this.cyclicRefresh;

                        // The noise estimate runs for 8-bit constant-bitrate coding of frames larger than 640x480,
                        // when the maximum key frame distance is not 0.
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
                    // A good-quality sequence without lookahead under a bit budget runs the one-pass rate control without statistics.
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
                    qIndex,
                    updateCdf: true);

                this.SymbolEncoder.EncodingSpeed = options.Speed;

                this.ObuWriter = new ObuWriter(configuration);
            }
            catch
            {
                // If construction fails, the caller receives no encoder. This code releases only the completed common owners.
                // The derived constructor did not start, so the code must not call the virtual disposal.
                this.DisposeResources();
                throw;
            }
        }

        /// <summary>
        /// Gets the sequence header shared by every sample written by this encoder.
        /// </summary>
        public ObuSequenceHeader SequenceHeader { get; }

        /// <summary>
        /// Gets the configuration providing every operation-scoped allocation.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <summary>
        /// Gets the number of displayed frames since the last GOLDEN refresh.
        /// </summary>
        protected int FramesSinceGolden => this.framesSinceGolden;

        /// <summary>
        /// Gets the quantizer index of the current frame.
        /// </summary>
        protected int QIndex { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current frame is a layer after the first. Such a layer does not reference or update GOLDEN and
        /// the alternate references.
        /// </summary>
        protected bool UsesLayerFlags => this.usesLayerFlags;

        /// <summary>
        /// Gets a value indicating whether the current frame starts a temporal unit. Only spatial layer 0 starts a temporal unit and gets a temporal
        /// delimiter. Every later layer of a layered image continues the temporal unit of the first layer.
        /// </summary>
        protected bool StartsTemporalUnit => this.FrameHeader.SpatialId == 0;

        /// <summary>
        /// Gets the coded size of the current frame. This is the sequence size, or the size of a scaled layer.
        /// </summary>
        protected Size FrameSize => this.frameSize;

        /// <summary>
        /// Gets a value indicating whether the frame codes at a size other than the size that the encoder holds. The held size is the size of the
        /// previous frame or, after a configuration change, the image size. Every frame sets its scale mode, so the pending size is always the
        /// frame size.
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

        /// <summary>
        /// Gets a value indicating whether the track codes the alpha channel instead of the color channels.
        /// </summary>
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
        /// Gets the tile probability graph and the tile buffers reused by every sample in the track.
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

        /// <summary>
        /// Applies the current frame size, then resets the frame-varying header fields for a frame of the given type.
        /// </summary>
        /// <param name="frameType">The type of the frame.</param>
        protected void ConfigureFrameHeader(ObuFrameType frameType)
        {
            this.ApplyFrameSize();
            Av1FrameEncoder.ConfigureFrameHeader(this.FrameHeader, this.SequenceHeader, this.QIndex, this.Options, frameType);

            // A sequence keeps backward adaptation, so every frame stores its final probabilities for later frames.
            this.FrameHeader.DisableFrameEndUpdateCdf = false;

            // A key frame restarts the frame count, and the order hint follows it.
            if (frameType == ObuFrameType.KeyFrame)
            {
                this.nextOrderHint = 0;
            }

            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            this.FrameHeader.OrderHint = orderHintBits == 0
                ? 0
                : this.nextOrderHint & ((1U << orderHintBits) - 1);

            // A key frame is error resilient by definition. Inter frames are not error resilient, so they keep the state and the entropy contexts
            // of their references.
            this.FrameHeader.ErrorResilientMode = frameType == ObuFrameType.KeyFrame;
        }

        /// <summary>
        /// Measures the rate multiplier scaling factors of the SSIM and image tunes for the current frame. The measurement reads the source at the
        /// size of the image over the grid of the previous coded frame, because it runs before the frame size is set and the source is resized.
        /// The frame then looks up the factors with its own grid.
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
        /// Returns the frame border of the encoder configuration. The border is <see cref="ScaledReferenceBorder"/> while a fixed resize mode
        /// applies, otherwise a complete superblock plus 32 samples.
        /// </summary>
        /// <returns>The border in luma samples.</returns>
        private protected int GetEncoderBorder()
            => this.resizesFixed ? ScaledReferenceBorder : (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;

        /// <summary>
        /// Records the size of the current frame as the size that the encoder holds for the next frame. It also becomes the previous coded size.
        /// </summary>
        private protected void RecordFrameSize()
        {
            this.presetupFrameSize = this.frameSize;
            this.codedFrameSize = this.frameSize;
        }

        /// <summary>
        /// Gets a value indicating whether real-time scene detection runs for the current frame. It runs when the size that the encoder holds equals
        /// the size of the last coded frame. A reconfiguration after a scaled layer holds the image size, so the next layer skips scene detection.
        /// </summary>
        /// <returns><see langword="true"/> when scene detection runs.</returns>
        private protected bool DetectsScene() => this.presetupFrameSize == this.codedFrameSize;

        /// <summary>
        /// Gets the luma size of the grid that real-time scene detection measures. This is the size that the encoder holds before it sets the size
        /// of the frame.
        /// </summary>
        /// <returns>The grid size in luma samples.</returns>
        private protected Size GetSceneDetectionSize() => this.presetupFrameSize;

        /// <summary>
        /// Gets a value indicating whether real-time scene detection keeps the error of each 64x64 block. It keeps them when the size that the
        /// encoder holds is the image size.
        /// </summary>
        /// <returns><see langword="true"/> when the block errors are kept.</returns>
        private protected bool KeepsSceneBlockErrors()
            => this.presetupFrameSize.Width == this.SequenceHeader.MaxFrameWidth &&
                this.presetupFrameSize.Height == this.SequenceHeader.MaxFrameHeight;

        /// <summary>
        /// Sets the coded size, the mode-information grid size and the tile layout of the current frame. The render size stays the sequence size,
        /// so a decoder shows a scaled layer at the size of the image.
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
        /// Returns the slot that LAST uses before the source analysis selects the rest of the structure. One-layer coding rotates LAST through
        /// <see cref="RotatingSlotCount"/> slots, one frame behind the frame number. Every layer of a layered image maps LAST to slot 0.
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
                ReadOnlySpan<TSample> fromSamples = from.Samples;
                Span<TSample> toSamples = to.Samples;
                for (int y = 0; y < height; y++)
                {
                    fromSamples.Slice(from.GetOffset(0, y), width).CopyTo(toSamples.Slice(to.GetOffset(0, y), width));
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
        /// Removes GOLDEN and ALTREF from the available references when their frame has another size than the current frame, so that the encoder
        /// never resizes them. LAST stays, and the motion search reads its resized copy. The layers of an image code as one spatial layer, so this
        /// rule applies to them.
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
        /// Sets the models that the frame codes its global motion against, then searches its global motion. Real-time coding does not search.
        /// A frame without a primary reference codes against identity models. Other frames code against the models of the primary reference slot.
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
        /// Selects the update type, the reference slots, the refreshed slots, and the primary reference. This runs after the source analysis of the
        /// frame and before its quantizer and speed features. Real-time coding uses the one-layer real-time structure. Good-quality coding uses the
        /// low-delay pyramid of <see cref="Av1GoodQualityReferenceStructure"/>.
        /// </summary>
        /// <param name="parent">
        /// The frame state with the source analysis of the frame. Its speed settings are those of the previous frame. The structure reads only the
        /// features that do not depend on the update type.
        /// </param>
        /// <param name="averageSourceSad">The running average source SAD.</param>
        protected void ConfigureReferenceStructure(Av1PictureParentControlSet parent, ulong averageSourceSad)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;
            if (!parent.SpeedSettings.IsRealtime)
            {
                // Good-quality coding without lookahead codes low-delay pyramid groups. Every key frame restores the frame probability tables.
                this.goodQualityStructure.Configure(frameHeader, parent.FramesSinceKey, this.framesToKey, this.usesLayerFlags);
                parent.FrameUpdateType = this.goodQualityStructure.UpdateType;
                parent.StartsGoldenGroup = parent.FrameUpdateType == Av1FrameUpdateType.Golden;

                // The layer flags remove the GOLDEN refresh of the update type.
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
        /// Sets the frame tools that follow the reference structure and the quantizer of the frame. These tools are the interpolation filter,
        /// temporal motion vectors, warped motion, OBMC, and skip mode.
        /// </summary>
        /// <param name="parent">The frame state with the update type and the speed settings of the frame.</param>
        protected void ConfigureReferenceTools(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;

            // Every inter frame starts with switchable filters. The encoder narrows the filter after it codes the frame.
            if (!frameHeader.IsIntra)
            {
                frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
            }

            // Temporal motion vectors are on by default, also in real-time coding. Intra and error-resilient frames cannot use them.
            ObuOrderHintInfo orderHintInfo = this.SequenceHeader.OrderHintInfo;
            frameHeader.UseReferenceFrameMotionVectors = !frameHeader.IsIntra && !frameHeader.ErrorResilientMode &&
                orderHintInfo.EnableOrderHint && orderHintInfo.EnableReferenceFrameMotionVectors;

            // Warped motion is allowed by default. A frame turns it off when the warped probability of its update type is less than the threshold.
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

            // OBMC is on by default, so the motion mode is switchable in every inter frame.
            frameHeader.IsMotionModeSwitchable = !frameHeader.IsIntra;

            frameHeader.SkipModeParameters.Derive(this.SequenceHeader.OrderHintInfo, frameHeader);
            frameHeader.SkipModeParameters.SkipModeFlag = frameHeader.SkipModeParameters.SkipModeAllowed;
        }

        /// <summary>
        /// Restores the frame tools that each coding pass of a frame starts from. These tools are the switchable filters, the switchable motion
        /// modes and the skip mode of the references. Warped motion keeps the state of the last pass, and turns off when its probability fell below
        /// the threshold.
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

            // The threshold test reads the warped probability that the last coding pass updated.
            int warpedThreshold = parent.SpeedSettings.WarpedProbabilityThreshold;
            if (frameHeader.AllowWarpedMotion && warpedThreshold > 0 &&
                this.warpedProbabilities[(int)parent.FrameUpdateType] < warpedThreshold)
            {
                frameHeader.AllowWarpedMotion = false;
            }

            int obmcRow = (int)parent.FrameUpdateType * (int)Av1BlockSize.AllSizes;
            this.obmcProbabilities.AsSpan(obmcRow, (int)Av1BlockSize.AllSizes).CopyTo(parent.ObmcProbabilities);

            // Each pass derives skip mode again. The reference binding narrows it later.
            frameHeader.SkipModeParameters.Derive(this.SequenceHeader.OrderHintInfo, frameHeader);
            frameHeader.SkipModeParameters.SkipModeFlag = frameHeader.SkipModeParameters.SkipModeAllowed;
        }

        /// <summary>
        /// Selects the reference slots, the refreshed slots, and the primary reference of a real-time frame. The method first updates the golden
        /// interval, then builds the one-layer reference structure, then selects the primary reference. A layered image maps every reference to
        /// slot 0 instead of the one-layer structure.
        /// </summary>
        /// <param name="parent">The frame state with the source analysis and speed settings of the frame.</param>
        /// <param name="averageSourceSad">The running average source SAD.</param>
        private void ConfigureRealtimeReferenceStructure(Av1PictureParentControlSet parent, ulong averageSourceSad)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1EncoderSpeedSettings speedSettings = parent.SpeedSettings;
            bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;

            // A new golden group starts when the golden update is due, at a scene change, and at a frame of a new size.
            if (this.IsResizePending || parent.HighSourceSad || this.framesTillGoldenUpdateDue == 0)
            {
                // A key frame restarted the key frame interval before this step, so the full maximum distance applies.
                int framesToKey = keyFrame ? this.Options.KeyFrameMaximumDistance : this.rateControl!.FramesToKey;
                this.SetBaselineGoldenInterval(parent.AverageFrameLowMotion, framesToKey);
            }

            bool goldenUpdate = this.goldenFrameIndex == 0;
            parent.StartsGoldenGroup = !keyFrame && goldenUpdate;

            // Key frames and golden update frames refresh GOLDEN. The layer flags remove the refresh of a golden update frame.
            parent.RefreshesGolden = keyFrame || (goldenUpdate && !this.usesLayerFlags);

            // A key frame restores the frame probability tables. A golden refresh also restores them when the speed settings prune warped motion further.
            if (keyFrame || (speedSettings.ExtraPruneWarped && parent.RefreshesGolden))
            {
                this.warpedProbabilities.AsSpan().Fill(64);
                DefaultObmcProbabilities.CopyTo(this.obmcProbabilities, 0);
            }

            Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            if (this.Options.LayerCount > 1)
            {
                // Spatial layers turn off the one-layer structure. The key frame maps every reference to slot 0.
                // Each later layer keeps that map and refreshes the slot of LAST, because its layer flags leave a refresh pending.
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

            // The primary reference is the last reference whose slot holds the entropy context of the most recent frame.
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
        /// Maps the references of a frame and chooses its refreshed slots in the one-layer real-time structure. LAST rotates through six slots,
        /// GOLDEN keeps its own slot, and ALTREF follows a few frames behind LAST. LAST2 points to the slot that the frame refreshes.
        /// </summary>
        /// <param name="frameHeader">The frame header that receives the slots and the refreshed slots.</param>
        /// <param name="speedSettings">The speed settings of the frame.</param>
        /// <param name="averageSourceSad">The running average source SAD.</param>
        /// <param name="keyFrame">Whether the frame is a key frame, which refreshes every slot.</param>
        /// <param name="goldenUpdate">Whether the frame refreshes GOLDEN.</param>
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
                // The SAD threshold depends only on the lag level. A busier source shortens the ALTREF lag to 3 frames, otherwise the lag is 6.
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
        /// Turns off skip mode when the distances from the frame to its two references differ by more than one frame, or when the encoder disabled
        /// either reference. The test compares the signed distance to the first reference with the absolute distance to the second reference.
        /// </summary>
        /// <param name="sequenceHeader">The sequence header with the order hint parameters.</param>
        /// <param name="frameHeader">The frame header whose skip mode flag is updated.</param>
        /// <param name="availableReferences">The references that the encoder searches, one bit for each reference type.</param>
        /// <param name="onlyPastReferencesWithLag">Whether every reference comes before the frame while the sequence codes with a lookahead.</param>
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
        /// Removes references in a fixed order until the frame uses no more references than its speed settings allow.
        /// </summary>
        /// <param name="sequenceHeader">The sequence header with the order hint parameters.</param>
        /// <param name="frameHeader">The frame header with the reference order hints.</param>
        /// <param name="flags">The available references, one bit for each reference type.</param>
        /// <param name="speedSettings">The speed settings of the frame.</param>
        /// <returns>The available references that remain, one bit for each reference type.</returns>
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
                    // Level 6 and higher also disable LAST2 and ALTREF2.
                    referencesToDisable += 2;
                }
                else if (level == 5 && (flags & (1 << (int)Av1ReferenceFrameType.Last2)) != 0)
                {
                    // Level 5 also disables LAST2 when it is more than two frames away. The first-pass statistics test does not apply to one-pass coding.
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

            // The maximum reference count keeps its default of every inter reference.
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

                // The removal of BWDREF clears the GOLDEN flag, not the BWDREF flag. This matches the output of the AVIF reference encoder.
                Av1ReferenceFrameType cleared = reference == Av1ReferenceFrameType.Backward ? Av1ReferenceFrameType.Golden : reference;
                flags &= (byte)~(1 << (int)cleared);
                validReferences--;
            }

            return flags;
        }

        /// <summary>
        /// Returns the references that the frame can use. A reference is removed when an earlier reference in priority order uses the same buffer.
        /// </summary>
        /// <param name="bufferIds">The buffer identity of each reference type, indexed by reference type.</param>
        /// <param name="speedSettings">The speed settings of the frame.</param>
        /// <param name="usesLayerFlags">Whether the frame is a layer after the first. Such a layer keeps only LAST, LAST2 and LAST3.</param>
        /// <returns>The available references, one bit for each reference type.</returns>
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

            // Real-time coding enables LAST, GOLDEN, and ALTREF. Good-quality coding starts from every reference.
            int flags = usesLayerFlags
                ? (1 << (int)Av1ReferenceFrameType.Last) | (1 << (int)Av1ReferenceFrameType.Last2) | (1 << (int)Av1ReferenceFrameType.Last3)
                : speedSettings.IsRealtime
                    ? (1 << (int)Av1ReferenceFrameType.Last) | (1 << (int)Av1ReferenceFrameType.Alternate) | (1 << (int)Av1ReferenceFrameType.Golden)
                    : 0xFE;

            for (int i = 1; i < priorityOrder.Length; i++)
            {
                Av1ReferenceFrameType reference = priorityOrder[i];

                // One-pass real-time coding compares GOLDEN only with LAST, and also with ALTREF when the estimated search uses ALTREF.
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
        /// Moves the warped motion probability and the OBMC probability of each block size of the update type of the frame halfway toward the share
        /// of blocks in the frame that used them. The probabilities are out of 128.
        /// </summary>
        /// <param name="parent">The frame state with the motion mode counts of its packing pass.</param>
        private protected void UpdateMotionModeProbabilities(Av1PictureParentControlSet parent)
        {
            if (this.FrameHeader.AllowWarpedMotion && parent.SpeedSettings.WarpedProbabilityThreshold > 0)
            {
                // The running probability moves halfway to the share of warped blocks in this frame.
                int updateType = (int)parent.FrameUpdateType;
                int sum = parent.WarpedUsage[0] + parent.WarpedUsage[1];
                int newProbability = sum != 0 ? 128 * parent.WarpedUsage[1] / sum : 0;
                this.warpedProbabilities[updateType] = (this.warpedProbabilities[updateType] + newProbability) >> 1;
            }

            int obmcThreshold = parent.SpeedSettings.ObmcProbabilityThreshold;
            if (obmcThreshold > 0 && obmcThreshold < int.MaxValue)
            {
                // The probability of each block size moves halfway to the share of OBMC blocks of that size in this frame.
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
        /// Advances the reference structure state after a coded frame. This updates the slot global motion, the motion mode probabilities, the
        /// entropy context slot and the golden counters.
        /// </summary>
        protected void CompleteReferenceStructure()
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;

            // Each refreshed slot keeps the global motion models of this frame. Later frames code their models against them.
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

                // A key frame interval that reached 0 waits for its pending forced key frame.
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
                // The context slot is the first refreshed slot. A frame that refreshes no slot keeps the previous context slot.
                for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
                {
                    if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
                    {
                        this.contextTypeSlot = slot;
                        break;
                    }
                }
            }

            // The golden counters advance. The golden group index wraps at the maximum static group length.
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
        /// Returns the update type of a frame in the one-layer real-time structure.
        /// </summary>
        /// <param name="keyFrame">Whether the frame is a key frame.</param>
        /// <param name="startsGoldenGroup">Whether an inter frame starts a golden group.</param>
        /// <returns>The frame update type.</returns>
        private static Av1FrameUpdateType GetFrameUpdateType(bool keyFrame, bool startsGoldenGroup)
            => keyFrame ? Av1FrameUpdateType.Key : startsGoldenGroup ? Av1FrameUpdateType.Golden : Av1FrameUpdateType.Last;

        /// <summary>
        /// Starts a golden group. Without cyclic refresh the refresh divisor is 10, so the interval is 80 frames. At speed 9 and higher, when the
        /// smaller dimension is 360 or more, the interval is 40 frames. Cyclic refresh divides by its refresh percentage instead. When recent frames
        /// had little zero motion, the interval is <see cref="LowMotionGoldenInterval"/>. The group ends no later than the next key frame, and
        /// then it is constrained.
        /// </summary>
        /// <param name="averageFrameLowMotion">The running zero-motion percentage.</param>
        /// <param name="framesToKey">The frames left before the next key frame.</param>
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
        /// Cancels a golden refresh that ends a period at a high quantizer. Forces a golden refresh at least 10 frames into a period at a low
        /// quantizer or in a high-motion frame. This runs in constant-bitrate real-time coding, after the frame is coded and its motion statistics
        /// are updated.
        /// </summary>
        /// <param name="parent">The frame state, with the motion statistics of the coded frame.</param>
        public void AdjustRefresh(Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;

            // No resize test is necessary here, because the frame setup already used the pending size.
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
        /// Picks the quantizer of a frame, raises it for a scene change, and applies every quantizer-dependent frame field. Real-time frames use the
        /// constant-bitrate model. Good-quality frames under a bit budget use the group model. Other frames use the constant-quality rules.
        /// </summary>
        /// <typeparam name="TSample">The sample storage type.</typeparam>
        /// <typeparam name="TMotion">The error operations.</typeparam>
        /// <typeparam name="TBlock">The block averaging operations.</typeparam>
        /// <param name="parent">The frame state, with the scene statistics of this frame.</param>
        /// <param name="source">The bordered source luma plane.</param>
        /// <param name="lastReconstruction">The bordered luma plane of the LAST reference, for an inter frame.</param>
        /// <param name="averageSourceSad">The running average source SAD after this frame.</param>
        /// <param name="previousAverageSourceSad">The running average source SAD before this frame.</param>
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

                // Constant-quality coding without lookahead lowers the quantizer of the key frame. Good-quality coding also lowers the quantizer of
                // the golden update that starts each group. Every other frame codes at the constant-quality index.
                // A fixed quantizer codes every frame at the constant-quality index.
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

                // Only frames of the Last update type move the running inter average. The new average weights the old average by 3/4 and rounds.
                if (parent.FrameUpdateType == Av1FrameUpdateType.Last)
                {
                    this.averageInterQIndex = ((3 * this.averageInterQIndex) + frameQIndex + 2) >> 2;
                }

                // A later key frame of the interval reads the last boosted quantizer. A key frame, a golden refresh of a group that is not constrained,
                // or any lower quantizer replaces it.
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

            // The key frame decision and its bit target read the old frame count. The quantizer reads the restarted frame count.
            Av1RateControl.SourceSadStatistics sourceSad = new(parent.FrameSourceSad, averageSourceSad, previousAverageSourceSad);

            // A frame of a new size resets the buffer and the inter model before its target. The reset reads the frame type of the previous frame.
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

            // The quantizer pick reads the coded size of the frame.
            this.rateControl.SetFrameSize(this.FrameSize, this.GetPrimaryReferenceSize());

            // Cyclic refresh decides whether the frame refreshes any block before the frame chooses its quantizer.
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

            // A fixed quantizer in constant-quality coding codes every frame at its quality level. A layered image uses this for every layer.
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

            // Constant-bitrate inter frames with a high source change use fast overshoot detection, which can raise the quantizer.
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
        /// Picks the quantizer of a good-quality frame without lookahead under a bit budget and applies every quantizer-dependent frame field.
        /// The key frame or the golden update that starts a group allocates the bits of the group. Every frame takes its target from that
        /// allocation. The group allocation and the target read the old frame count and the size of the previous frame. The quantizer reads the
        /// restarted frame count and the coded size.
        /// </summary>
        /// <param name="parent">The frame state, with the update type of the frame.</param>
        private void SelectGroupFrameQuantizer(Av1PictureParentControlSet parent)
        {
            Av1RateControl rateControl = this.groupRateControl!;
            bool keyFrame = this.FrameHeader.IsIntra;
            bool goldenUpdate = parent.FrameUpdateType == Av1FrameUpdateType.Golden;

            // Every frame updates the frame rate limits before its target.
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
        /// Returns the size of the frame in the slot of the primary reference. The rate model compares the current frame with this frame.
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
        /// Starts the delta quantizer of each tile from the frame quantizer. The picture reset ran before the frame chose its quantizer, so it left
        /// the quantizer of the previous frame.
        /// </summary>
        /// <param name="qIndex">The base quantizer index of the frame.</param>
        private void ResetDeltaQuantizerAnchors(int qIndex)
            => this.PictureBuffer.Picture.Parent.PreviousQIndex.Span.Fill(qIndex);

        /// <summary>
        /// Restarts the frame count at a key frame, which resets every reference buffer. Later frames count from it, so the slot rotation, the
        /// quantizer history and the interpolation search pattern start again.
        /// </summary>
        private void RestartFrameCount()
        {
            this.frameNumber = 0;
            this.BlockWorkspace.EncodedFrameCount = 0;
            this.BlockWorkspace.FrameNumber = 0;
        }

        /// <summary>
        /// Returns the quantizer index of a key frame in constant-quality coding without lookahead. In good-quality coding, a key frame whose
        /// interval is one frame codes at the constant-quality index, and a key frame that the interval placed stays near the last boosted quantizer.
        /// Any other key frame uses the key frame floor.
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="bestQIndex">The lowest allowed quantizer index.</param>
        /// <param name="worstQIndex">The highest allowed quantizer index.</param>
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
        /// Updates the rate model with the coded size of the frame.
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="frameBytes">The coded size of the frame, without the temporal delimiter.</param>
        private protected void CompleteRateControl(Av1PictureParentControlSet parent, int frameBytes)
        {
            if (this.groupRateControl is not null)
            {
                // A good-quality golden update refreshes GOLDEN. No frame of a group without lookahead is an alternate reference or uses segments.
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

        /// <summary>
        /// Records the order hint and the size of the coded frame in every slot that it refreshes, then advances the order hint for the next frame.
        /// The order hint wraps at the order hint bit count.
        /// </summary>
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
        /// Encodes an independently decodable sample with the sequence header that random access needs. The key frame starts a new key frame
        /// interval, as the first frame of a sequence does.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the source.</typeparam>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        public void EncodeKeyFrame<TPixel>(ImageFrame<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.thisKeyFrameForced = false;
            this.framesToKey = Math.Max(1, this.Options.KeyFrameMaximumDistance);

            // The frame limit of a layered image ends the key frame interval at the last layer.
            if (this.Options.LayerCount > 1)
            {
                this.framesToKey = Math.Min(this.framesToKey, this.Options.LayerCount);
            }

            this.EncodeFrame(image, stream, ObuFrameType.KeyFrame, true);
        }

        /// <summary>
        /// Encodes one layer of a layered image at its own quantizer. The first layer is a key frame that starts the temporal unit. Each later layer
        /// is an inter frame of the same temporal unit, without a temporal delimiter. A later layer predicts only from the previous layer and
        /// refreshes only the slot of that layer.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the source.</typeparam>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="qIndex">
        /// The quantizer index of the quality of the layer. Constant-quality coding codes at this index. The constrained-quality mode reads it as
        /// its quality level. The bit-rate modes ignore it.
        /// </param>
        /// <param name="minimumQuantizer">
        /// The lowest quantizer of the layer on the zero-through-63 scale. Coding under a bit budget reads this value.
        /// </param>
        /// <param name="maximumQuantizer">
        /// The highest quantizer of the layer on the zero-through-63 scale. Coding under a bit budget reads this value.
        /// </param>
        /// <param name="scaleNumerator">The numerator of the size of the layer as a fraction of the image size.</param>
        /// <param name="scaleDenominator">The denominator of the size of the layer as a fraction of the image size.</param>
        /// <param name="qualityChanged">
        /// Whether the quality of the layer differs from the quality of the previous layer. A quality change reconfigures the encoder. This sets
        /// the mode-information grid back to the image size before the layer measures its SSIM factors.
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

            // The quality change comes first and rebuilds the configuration. The scale mode comes next, and the running encoder keeps it as a fixed
            // resize mode.
            if (qualityChanged)
            {
                this.presetupFrameSize = new Size(this.SequenceHeader.MaxFrameWidth, this.SequenceHeader.MaxFrameHeight);
                this.resizesFixed = false;
            }

            if (this.codedLayerCount > 0 && scaleNumerator != scaleDenominator)
            {
                this.resizesFixed = true;
            }

            // A scaled layer codes the image at the scaled size, rounded up to the next whole sample.
            int width = this.SequenceHeader.MaxFrameWidth;
            int height = this.SequenceHeader.MaxFrameHeight;
            this.frameSize = new Size(
                (scaleDenominator - 1 + (width * scaleNumerator)) / scaleDenominator,
                (scaleDenominator - 1 + (height * scaleNumerator)) / scaleDenominator);

            // Each layer gets its configuration and its spatial layer identifier before it is coded.
            this.FrameHeader.SpatialId = this.codedLayerCount;

            // Constant-quality coding sets the quantizer of the layer as its quality level. Coding under a bit budget also narrows the quantizer
            // range of the rate model to the quality of the layer. The constrained-quality mode bounds its frames by that level.
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
        /// Encodes the next sample of a sequence after its first frame. The frame is a key frame when the key frame interval ends or the caller
        /// forces one. Otherwise it is an inter frame.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the source.</typeparam>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="forceKeyFrame">Whether the caller forces a key frame.</param>
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
                // A forced key frame is pending at the current frame, so the interval ends here. The next interval also reads that pending key frame,
                // so it is empty.
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
        /// Copies the visible luma samples of the reconstruction that a reference slot holds. The copy lets a test compare the output of a decoder
        /// with the samples that the encoder predicts from.
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
            ReadOnlySpan<byte> planeSamples = plane.Samples;
            for (int y = 0; y < frame.Height; y++)
            {
                ReadOnlySpan<byte> row = planeSamples.Slice(plane.GetOffset(0, y), width);
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
            ReadOnlySpan<ushort> planeSamples = plane.Samples;
            for (int y = 0; y < frame.Height; y++)
            {
                planeSamples.Slice(plane.GetOffset(0, y), width).CopyTo(samples.AsSpan(y * width, width));
            }

            return samples;
        }

        /// <summary>
        /// Encodes one sample of the sequence as a frame of the given type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the source.</typeparam>
        /// <param name="image">The frame to encode.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="frameType">The type of the frame.</param>
        /// <param name="writeSequenceHeader">Whether a sequence header OBU precedes the frame.</param>
        protected abstract void EncodeFrame<TPixel>(
            ImageFrame<TPixel> image,
            Stream stream,
            ObuFrameType frameType,
            bool writeSequenceHeader)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Releases the common owners of the sequence encoder. This also runs for a partly constructed instance.
        /// </summary>
        private void DisposeResources()
        {
            // Construction can stop between any two allocations. A complete instance has every owner. A failed constructor keeps only the owners
            // that it allocated before the allocator rejected a request, so every release accepts null.
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
        /// </summary>
        private Av1EncoderFrameBuffer<byte>? scaledSource;

        /// <summary>
        /// The previous source resized to the size of a scaled layer, or <see langword="null"/> before any scaled layer.
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
        /// The frame that the motion search reads for each reference type. This is the reference itself, or its copy resized to the size of the
        /// current frame.
        /// </summary>
        private readonly Av1EncoderFrame<byte>[] searchReferences = new Av1EncoderFrame<byte>[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The resized copy of each reference type, or <see langword="null"/> before a reference of another size.
        /// </summary>
        private readonly Av1EncoderFrameBuffer<byte>?[] scaledReferences = new Av1EncoderFrameBuffer<byte>?[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The copy of each reference type that is larger than the current frame, with the border of scaled prediction. The entry is
        /// <see langword="null"/> before such a reference.
        /// </summary>
        private readonly Av1EncoderFrameBuffer<byte>?[] borderedReferences = new Av1EncoderFrameBuffer<byte>?[Av1Constants.ReferenceFrameCount];
        private readonly int[] referenceBufferIds = new int[Av1Constants.ReferenceFrameCount];
        private readonly Av1EncoderMotionField.SavedMotionField?[] referenceMotionFields =
            new Av1EncoderMotionField.SavedMotionField?[Av1Constants.ReferenceFrameCount];

        private Av1EncoderReferencePool<byte> referencePool;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteSequenceEncoder"/> class.
        /// </summary>
        /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
        /// <param name="width">The sequence width, in samples.</param>
        /// <param name="height">The sequence height, in samples.</param>
        /// <param name="colorConfig">The resolved color and precision configuration.</param>
        /// <param name="qIndex">The requested quantizer index.</param>
        /// <param name="options">The encoding options used to select frame and block search policies.</param>
        /// <param name="encodeAlpha">Whether the sequence codes the alpha channel instead of the color channels.</param>
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

                // The source owners swap after each frame, so the temporal analysis and the integer vector decision read the uncompressed previous
                // source without a frame copy. The padding also supplies complete edge superblocks.
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
                // The common state already exists, and every earlier frame allocation must also be returned.
                this.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        internal override ushort[] CopySlotLuma(int slot)
            => CopyLuma(this.referencePool.GetSlot(slot)!.Buffer.Frame);

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

            // Screen content detection reads the unfiltered source at the size of the image, before the source is resized.
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

            // A scaled layer codes the source and the previous source resized to its size.
            Av1EncoderFrameBuffer<byte> frameSource = this.ScaleToFrame(this.source, ref this.scaledSource);
            Av1EncoderFrameBuffer<byte>? framePreviousSource = this.previousSource is null
                ? null
                : this.ScaleToFrame(this.previousSource, ref this.scaledPreviousSource);

            // The integer vector decision compares the source and the previous source at the size of the image, before the encoder resizes them.
            this.DecideIntegerMotionVectors<byte, Av1IntraSuperblockEncoder.ByteOperator>(this.source.Frame, this.previousSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.PreviousSource = this.sourceBlockSad is not null && framePreviousSource is not null ? framePreviousSource.Frame.CodedView : default;

            // When scene detection does not run, the frame keeps the source changes of the previous frame.
            bool detectsScene = this.DetectsScene();
            parent.SourceBlockSad = this.sourceBlockSad is not null && detectsScene && this.KeepsSceneBlockErrors()
                ? this.sourceBlockSad.Memory
                : default;

            if (detectsScene)
            {
                parent.HighSourceSad = false;
                parent.FrameSourceSad = 0;
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

            // The quantizer and the speed features follow the update type that the reference structure selects, so the structure comes first.
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
                this.Options.IsAllIntra,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                this.FrameSize,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            // Good-quality coding with the default objective delta-q mode and the temporal model on pads the border. Real-time coding does not.
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

            // A temporal delimiter precedes each temporal unit. The rate model counts the frame bytes without the delimiter.
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
        /// Returns the source at the size of the current frame. This is the source itself, or a copy resized to the size of a scaled layer with the
        /// kernel and phase of the encoder.
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
        /// Returns a buffer of a size, and replaces the buffer when its size differs.
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
        /// Prepares each available reference of another size than the current frame. A larger reference predicts from a copy with the wider border
        /// of scaled prediction. The motion search reads a copy resized to the size of the frame with the kernel and phase that resize the source.
        /// The search reads every other reference in place. Coding is one pass without recode, so the references are always resized here.
        /// </summary>
        /// <param name="availableReferenceMask">The available references, one bit for each reference type.</param>
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

                // A larger reference scales a vector up, which can reach past the normal border. A smaller reference scales it down, so its reads
                // stay within the normal border.
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
        /// </summary>
        private Av1EncoderFrameBuffer<ushort>? scaledSource;

        /// <summary>
        /// The previous source resized to the size of a scaled layer, or <see langword="null"/> before any scaled layer.
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
        /// The frame that the motion search reads for each reference type. This is the reference itself, or its copy resized to the size of the
        /// current frame.
        /// </summary>
        private readonly Av1EncoderFrame<ushort>[] searchReferences = new Av1EncoderFrame<ushort>[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The resized copy of each reference type, or <see langword="null"/> before a reference of another size.
        /// </summary>
        private readonly Av1EncoderFrameBuffer<ushort>?[] scaledReferences = new Av1EncoderFrameBuffer<ushort>?[Av1Constants.ReferenceFrameCount];

        /// <summary>
        /// The copy of each reference type that is larger than the current frame, with the border of scaled prediction. The entry is
        /// <see langword="null"/> before such a reference.
        /// </summary>
        private readonly Av1EncoderFrameBuffer<ushort>?[] borderedReferences = new Av1EncoderFrameBuffer<ushort>?[Av1Constants.ReferenceFrameCount];
        private readonly int[] referenceBufferIds = new int[Av1Constants.ReferenceFrameCount];
        private readonly Av1EncoderMotionField.SavedMotionField?[] referenceMotionFields =
            new Av1EncoderMotionField.SavedMotionField?[Av1Constants.ReferenceFrameCount];

        private Av1EncoderReferencePool<ushort> referencePool;

        /// <summary>
        /// Initializes a new instance of the <see cref="HighBitDepthSequenceEncoder"/> class.
        /// </summary>
        /// <param name="configuration">The configuration providing every operation-scoped allocation.</param>
        /// <param name="width">The sequence width, in samples.</param>
        /// <param name="height">The sequence height, in samples.</param>
        /// <param name="colorConfig">The resolved color and precision configuration.</param>
        /// <param name="qIndex">The requested quantizer index.</param>
        /// <param name="options">The encoding options used to select frame and block search policies.</param>
        /// <param name="encodeAlpha">Whether the sequence codes the alpha channel instead of the color channels.</param>
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

                // The source owners swap after each frame, so the temporal analysis and the integer vector decision read the uncompressed previous
                // source without a frame copy. The padding also supplies complete edge superblocks.
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
                // The common state already exists, and every earlier frame allocation must also be returned.
                this.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        internal override ushort[] CopySlotLuma(int slot)
            => CopyLuma(this.referencePool.GetSlot(slot)!.Buffer.Frame);

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

            // Screen content detection reads the unfiltered source at the size of the image, before the source is resized.
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

            // A scaled layer codes the source and the previous source resized to its size.
            Av1EncoderFrameBuffer<ushort> frameSource = this.ScaleToFrame(this.source, ref this.scaledSource);
            Av1EncoderFrameBuffer<ushort>? framePreviousSource = this.previousSource is null
                ? null
                : this.ScaleToFrame(this.previousSource, ref this.scaledPreviousSource);

            // The integer vector decision compares the source and the previous source at the size of the image, before the encoder resizes them.
            this.DecideIntegerMotionVectors<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(this.source.Frame, this.previousSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.IsScreenContent = isScreenContent;
            parent.EncoderOptions = this.Options;
            parent.EncoderBorder = this.GetEncoderBorder();

            // When scene detection does not run, the frame keeps the source changes of the previous frame.
            bool detectsScene = this.DetectsScene();
            parent.SourceBlockSad = this.sourceBlockSad is not null && detectsScene && this.KeepsSceneBlockErrors()
                ? this.sourceBlockSad.Memory
                : default;

            if (detectsScene)
            {
                parent.HighSourceSad = false;
                parent.FrameSourceSad = 0;
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

            // The quantizer and the speed features follow the update type that the reference structure selects, so the structure comes first.
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
                this.Options.IsAllIntra,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                this.FrameSize,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            // Good-quality coding with the default objective delta-q mode and the temporal model on pads the border. Real-time coding does not.
            parent.BorderPad = this.UsesBorderPad;

            this.ConfigureReferenceTools(parent);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.SearchGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(frameSource.Frame, this.references, parent);

            // The 8-bit encoder also sets the previous source and updates the noise estimate here. A high-bit-depth frame needs neither: the
            // superblock comparison with the previous source and the noise estimate run only for 8-bit samples.
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

            // A temporal delimiter precedes each temporal unit. The rate model counts the frame bytes without the delimiter.
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
        /// Returns the source at the size of the current frame. This is the source itself, or a copy resized to the size of a scaled layer.
        /// Frames of more than eight bits always use the nonnormative resizer.
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
        /// Returns a buffer of a size, and replaces the buffer when its size differs.
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
        /// Prepares each available reference of another size than the current frame. A larger reference predicts from a copy with the wider border
        /// of scaled prediction. The motion search reads a copy resized to the size of the frame with the nonnormative resizer, which every frame
        /// of more than eight bits uses. The search reads every other reference in place.
        /// </summary>
        /// <param name="availableReferenceMask">The available references, one bit for each reference type.</param>
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

                // A larger reference scales a vector up, which can reach past the normal border. A smaller reference scales it down, so its reads
                // stay within the normal border.
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

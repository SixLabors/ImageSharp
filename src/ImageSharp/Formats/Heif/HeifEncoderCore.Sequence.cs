// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

internal sealed partial class HeifEncoderCore
{
    /// <summary>
    /// The millisecond media timescale used when every frame delay can be represented exactly.
    /// </summary>
    private const uint DefaultSequenceTimescale = 1000;

    /// <summary>
    /// The number of frames that the encoder of a good-quality sequence reads ahead.
    /// </summary>
    private const int DefaultLagInFrames = 35;

    /// <summary>
    /// The duration that the encoder gives each frame, in ticks of 100 nanoseconds. This value is one unit of a 1/30 second
    /// time base. The encoder ignores the real frame delays, so its rate control sees 30 frames per second.
    /// </summary>
    private const long LibavifFrameDurationTicks = 10_000_000 / 30;

    /// <summary>
    /// The microsecond fallback used when the exact common frame-delay timescale exceeds 32 bits.
    /// </summary>
    private const uint FallbackSequenceTimescale = 1000000;

    /// <summary>
    /// The identity value for signed 16.16 movie and track matrix entries.
    /// </summary>
    private const uint UnityFixed16Point16 = 1U << 16;

    /// <summary>
    /// The identity value for the signed 2.30 homogeneous movie and track matrix entry.
    /// </summary>
    private const uint UnityFixed2Point30 = 1U << 30;

    /// <summary>
    /// The identity value for unsigned 8.8 track volume.
    /// </summary>
    private const ushort UnityFixed8Point8 = 1 << 8;

    /// <summary>
    /// The packed ISO 639-2/T language code for undetermined content.
    /// </summary>
    private const ushort PackedUndeterminedLanguage = 0x55C4;

    /// <summary>
    /// The coding-constraints flag stating that every reference picture is intra.
    /// </summary>
    private const uint AllReferencePicturesIntraMask = 1U << 31;

    /// <summary>
    /// The coding-constraints flag stating that intra prediction is used.
    /// </summary>
    private const uint IntraPicturePredictionUsedMask = 1U << 30;

    /// <summary>
    /// The conventional 72-dpi horizontal and vertical resolution stored as unsigned 16.16.
    /// </summary>
    private const uint DefaultVisualSampleResolution = 72U << 16;

    /// <summary>
    /// The fixed visual-sample-entry compressor-name field length.
    /// </summary>
    private const int VisualSampleCompressorNameLength = 32;

    /// <summary>
    /// The visual-sample-entry depth used for color pictures.
    /// </summary>
    private const ushort VisualSampleDepth = 24;

    /// <summary>
    /// Chooses the chroma sampling of an encoding that does not request one. A grayscale source gets 4:0:0. Lossless
    /// encoding of a color source gets 4:4:4. A decoded HEIF source keeps its own sampling. A JPEG source keeps its
    /// 4:2:0 or 4:2:2 sampling. All other sources get 4:4:4.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="metadata">The HEIF view of the source metadata.</param>
    /// <returns>The chroma sampling to encode.</returns>
    private HeifChromaSubsampling GetDefaultChromaSubsampling<TPixel>(Image<TPixel> image, HeifMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (metadata.IsMonochrome)
        {
            return HeifChromaSubsampling.Monochrome;
        }

        if (this.encoder.Lossless)
        {
            return HeifChromaSubsampling.Yuv444;
        }

        if (metadata.ChromaSubsampling is HeifChromaSubsampling heifSubsampling)
        {
            return heifSubsampling;
        }

        if (image.Metadata.DecodedImageFormat == JpegFormat.Instance)
        {
            return image.Metadata.GetJpegMetadata().ColorType switch
            {
                JpegColorType.YCbCrRatio420 => HeifChromaSubsampling.Yuv420,
                JpegColorType.YCbCrRatio422 => HeifChromaSubsampling.Yuv422,
                _ => HeifChromaSubsampling.Yuv444,
            };
        }

        return HeifChromaSubsampling.Yuv444;
    }

    /// <summary>
    /// Resolves the bit depth, chroma sampling, color description, quantizers and codec options of an encoding.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="allIntra">Whether the encoding is a still image without layers, which codes every frame as a key frame.</param>
    /// <param name="layers">The layers of a layered still image, or <see langword="null"/> for an image without layers.</param>
    /// <param name="cancellationToken">The token that stops the encode, which the codec options carry.</param>
    /// <returns>The resolved settings.</returns>
    private Av1EncodingSettings ResolveAv1Encoding<TPixel>(
        Image<TPixel> image,
        bool allIntra,
        IReadOnlyList<HeifLayer>? layers,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool layered = layers is not null;
        int layerCount = layers?.Count ?? 1;
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        HeifBitDepth bitDepth = this.encoder.BitDepth ?? metadata.BitDepth;
        Av1BitDepth av1BitDepth = bitDepth switch
        {
            HeifBitDepth.Bit8 => Av1BitDepth.EightBit,
            HeifBitDepth.Bit10 => Av1BitDepth.TenBit,
            HeifBitDepth.Bit12 => Av1BitDepth.TwelveBit,
            _ => throw new NotSupportedException($"HEIF bit depth '{bitDepth}' is not supported.")
        };

        HeifChromaSubsampling chromaSubsampling = this.encoder.ChromaSubsampling ?? this.GetDefaultChromaSubsampling(image, metadata);

        if (image.Frames.Count == 1
            && (image.Width > Av1Constants.MaxFrameDimension || image.Height > Av1Constants.MaxFrameDimension)
            && ((chromaSubsampling == HeifChromaSubsampling.Yuv420
                    && (((image.Width & 1) != 0) || ((image.Height & 1) != 0)))
                || (chromaSubsampling == HeifChromaSubsampling.Yuv422 && (image.Width & 1) != 0)))
        {
            // A derived grid needs an even output size on every subsampled axis. Switch to 4:4:4 so that the grid
            // can keep the full source size.
            chromaSubsampling = HeifChromaSubsampling.Yuv444;
        }

        (bool isMonochrome, bool subsamplingX, bool subsamplingY) = chromaSubsampling switch
        {
            HeifChromaSubsampling.Monochrome => (true, true, true),
            HeifChromaSubsampling.Yuv420 => (false, true, true),
            HeifChromaSubsampling.Yuv422 => (false, true, false),
            HeifChromaSubsampling.Yuv444 => (false, false, false),
            _ => throw new NotSupportedException($"HEIF chroma sampling '{chromaSubsampling}' is not supported.")
        };

        CicpProfile? sourceColorProfile = image.Metadata.CicpProfile;
        CicpProfile colorProfile;
        if (sourceColorProfile is null)
        {
            // A source without a color description gets unspecified primaries and transfer, the BT.601 matrix and
            // full range.
            colorProfile = new CicpProfile(
                (byte)CicpColorPrimaries.Unspecified,
                (byte)CicpTransferCharacteristics.Unspecified,
                (byte)CicpMatrixCoefficients.ItuRBt601_7_525,
                true);
        }
        else
        {
            bool identityMatrix = sourceColorProfile.MatrixCoefficients == CicpMatrixCoefficients.Identity;
            bool legalIdentityMatrix = !isMonochrome
                && chromaSubsampling == HeifChromaSubsampling.Yuv444;

            bool reversibleMatrix = sourceColorProfile.MatrixCoefficients is CicpMatrixCoefficients.YCgCoRe or CicpMatrixCoefficients.YCgCoRo;
            if (sourceColorProfile.MatrixCoefficients == CicpMatrixCoefficients.Unspecified
                || (identityMatrix && !legalIdentityMatrix)
                || (reversibleMatrix && !isMonochrome && chromaSubsampling != HeifChromaSubsampling.Yuv444))
            {
                // The encoder can convert packed source pixels to any sampling, also when the metadata gives a matrix
                // that needs 4:4:4. Encode and signal BT.601, and keep the source metadata unchanged.
                colorProfile = new CicpProfile(
                    (byte)sourceColorProfile.ColorPrimaries,
                    (byte)sourceColorProfile.TransferCharacteristics,
                    (byte)CicpMatrixCoefficients.ItuRBt601_7_525,
                    sourceColorProfile.FullRange);
            }
            else if (identityMatrix
                && sourceColorProfile.ColorPrimaries == CicpColorPrimaries.ItuRBt709_6
                && sourceColorProfile.TransferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1
                && !sourceColorProfile.FullRange)
            {
                // Only the BT.709 and sRGB identity description has no range bit, and it always means full range.
                // Other identity descriptions carry the range bit, so they can keep a limited range.
                colorProfile = new CicpProfile(
                    (byte)sourceColorProfile.ColorPrimaries,
                    (byte)sourceColorProfile.TransferCharacteristics,
                    (byte)sourceColorProfile.MatrixCoefficients,
                    true);
            }
            else
            {
                colorProfile = sourceColorProfile;
            }
        }

        // Lossless 4:4:4 color codes RGB through the identity matrix at full range. A source with a reversible matrix
        // (identity or YCgCo) keeps that matrix and its range. A gray image codes its one plane exactly with any
        // matrix. AV1 allows the identity matrix with 4:4:4 sampling only. Thus a lossless encode that asks for
        // subsampled chroma keeps the matrix chosen above and codes its YUV samples exactly.
        bool reversibleColorMatrix = colorProfile.MatrixCoefficients is CicpMatrixCoefficients.Identity
            or CicpMatrixCoefficients.YCgCoRe
            or CicpMatrixCoefficients.YCgCoRo;

        if (this.encoder.Lossless && !isMonochrome && chromaSubsampling == HeifChromaSubsampling.Yuv444 && !reversibleColorMatrix)
        {
            colorProfile = new CicpProfile(
                (byte)colorProfile.ColorPrimaries,
                (byte)colorProfile.TransferCharacteristics,
                (byte)CicpMatrixCoefficients.Identity,
                true);
        }

        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            IsMonochrome = isMonochrome,
            ColorPrimaries = (ObuColorPrimaries)colorProfile.ColorPrimaries,
            TransferCharacteristics = (ObuTransferCharacteristics)colorProfile.TransferCharacteristics,
            MatrixCoefficients = (ObuMatrixCoefficients)colorProfile.MatrixCoefficients,
            ColorRange = colorProfile.FullRange,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = av1BitDepth
        };

        ObuColorConfig alphaConfig = new()
        {
            IsMonochrome = true,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = av1BitDepth
        };

        // A tune that the caller sets applies to color and alpha. Otherwise the encoder picks the tune:
        // - Lossless color and all alpha use PSNR. For alpha, PSNR limits ringing.
        // - Still and layered color images use the image tune, unless they use the identity matrix.
        // - All other color uses SSIM.
        // Only the image tune changes the curve from quality to quantizer.
        Av1Tuning? requestedTuning = this.encoder.Tuning switch
        {
            HeifTuning.Psnr => Av1Tuning.Psnr,
            HeifTuning.Ssim => Av1Tuning.Ssim,
            HeifTuning.ImageQuality => Av1Tuning.Iq,
            HeifTuning.Ssimulacra2 => Av1Tuning.Ssimulacra2,
            _ => null
        };

        Av1Tuning colorTuning = requestedTuning ?? (this.encoder.Lossless
            ? Av1Tuning.Psnr
            : (allIntra || layered) && colorProfile.MatrixCoefficients != CicpMatrixCoefficients.Identity ? Av1Tuning.Iq : Av1Tuning.Ssim);

        Av1Tuning alphaTuning = requestedTuning ?? Av1Tuning.Psnr;

        // The default quality is 60. A layered image starts the encoder at the quality of its first layer.
        int quality = this.encoder.Quality ?? 60;
        int alphaQuality = this.encoder.AlphaQuality ?? quality;
        if (layers is not null)
        {
            quality = layers[0].Quality ?? quality;
            alphaQuality = layers[0].AlphaQuality ?? alphaQuality;
        }

        int colorQIndex = this.encoder.Lossless ? 0 : GetAv1QuantizerIndex(quality, colorTuning == Av1Tuning.Iq);
        int alphaQIndex = this.encoder.Lossless ? 0 : GetAv1QuantizerIndex(alphaQuality, alphaTuning == Av1Tuning.Iq);
        bool hasAlpha = TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.None;

        // Loop restoration is off for 12-bit images, because the encoder can overflow at that depth.
        bool enableRestoration = av1BitDepth != Av1BitDepth.TwelveBit;

        // A sequence or a layered image at speed 7 or faster uses real-time coding. Real-time coding uses a constant
        // bit rate by default. All other images use constant quality. The rate control option replaces this default.
        // The bit-rate modes keep the quantizer within four steps of the requested one. Lossless coding keeps
        // quantizer zero. Lossy coding keeps quantizer one or higher, as GetAv1Quantizer() does.
        bool realtime = !allIntra && this.encoder.Speed >= HeifEncodingSpeed.Level7;
        Av1RateControlMode defaultRateControlMode = realtime ? Av1RateControlMode.ConstantBitRate : Av1RateControlMode.Quality;
        Av1RateControlMode rateControlMode = this.encoder.RateControl is { } rateControl
            ? GetRateControlMode(rateControl)
            : defaultRateControlMode;

        int colorQuantizer = this.encoder.Lossless ? 0 : GetAv1Quantizer(quality, colorTuning == Av1Tuning.Iq);
        int alphaQuantizer = this.encoder.Lossless ? 0 : GetAv1Quantizer(alphaQuality, alphaTuning == Av1Tuning.Iq);

        // A good-quality sequence reads 35 frames ahead, unless the image has alpha. A layered image codes without
        // lookahead, so each layer gives its own output.
        int colorLag = allIntra || realtime || hasAlpha || layered ? 0 : DefaultLagInFrames;

        // Automatic tiling sizes the tiles from the first cell. The first cell is the whole frame, unless an oversized
        // still image becomes a grid.
        int tileRowsLog2 = BitOperations.Log2((uint)this.encoder.TileRows);
        int tileColumnsLog2 = BitOperations.Log2((uint)this.encoder.TileColumns);
        if (this.encoder.AutoTiling)
        {
            Size firstCellSize = GetFirstCellSize(image.Size, allIntra, !isMonochrome && subsamplingX, !isMonochrome && subsamplingY);
            (tileRowsLog2, tileColumnsLog2) = GetAutomaticTileConfiguration(firstCellSize);
        }

        return new Av1EncodingSettings(
            bitDepth,
            chromaSubsampling,
            colorProfile,
            colorConfig,
            alphaConfig,
            colorQIndex,
            alphaQIndex,
            hasAlpha,
            CreateOptions(colorTuning, colorQuantizer, colorLag),
            CreateOptions(alphaTuning, alphaQuantizer, 0));

        // Creates the codec options of the color or the alpha.
        Av1EncoderOptions CreateOptions(Av1Tuning tuning, int quantizer, int lagInFrames)
            => new(this.encoder.Speed, tuning, enableRestoration, allIntra)
            {
                RateControlMode = rateControlMode,
                MinimumQuantizer = GetQuantizerRange(quantizer, rateControlMode).Minimum,
                MaximumQuantizer = GetQuantizerRange(quantizer, rateControlMode).Maximum,
                LagInFrames = lagInFrames,

                // A good-quality layered image in constant-quality coding codes every layer at the quantizer of its own
                // quality. A real-time layered image never uses fixed quantizers. A good-quality layered image with a
                // bit budget also codes without them, because fixed quantizers do not work with a bit budget.
                LayerCount = layerCount,
                UsesFixedQuantizer = layered && !realtime && rateControlMode == Av1RateControlMode.Quality,

                // The key frame interval applies to animations, so a layered still image keeps the default.
                KeyFrameMaximumDistance = layered
                    ? Av1EncoderOptions.DefaultKeyFrameMaximumDistance
                    : this.encoder.KeyFrameInterval ?? Av1EncoderOptions.DefaultKeyFrameMaximumDistance,
                Sharpness = this.encoder.Sharpness ?? Av1EncoderOptions.GetDefaultSharpness(tuning),
                CancellationToken = cancellationToken,
                TileRowsLog2 = tileRowsLog2,
                TileColumnsLog2 = tileColumnsLog2,

                // Lossless coding does not allow chroma delta q. A lossless quantizer of zero keeps every chroma delta
                // at zero, so the option needs no lossless test here.
                EnableChromaDeltaQ = this.encoder.SeparateChromaQuality ?? tuning.IsImageTuning(),
                FilmGrainPreset = this.encoder.FilmGrainPreset ?? 0,
                FilmGrainTable = this.encoder.ParsedFilmGrainTable,

                // Lossless coding does not allow adaptive quantization.
                AdaptiveQuantizationMode = this.encoder.Lossless
                    ? Av1AdaptiveQuantizationMode.None
                    : this.encoder.AdaptiveQuantization switch
                    {
                        HeifAdaptiveQuantization.Variance => Av1AdaptiveQuantizationMode.Variance,
                        HeifAdaptiveQuantization.Complexity => Av1AdaptiveQuantizationMode.Complexity,
                        HeifAdaptiveQuantization.CyclicRefresh => Av1AdaptiveQuantizationMode.CyclicRefresh,
                        _ => Av1AdaptiveQuantizationMode.None
                    }
            };
    }

    /// <summary>
    /// Returns the codec rate control mode of a rate control option.
    /// </summary>
    /// <param name="rateControl">The rate control option.</param>
    /// <returns>The codec rate control mode.</returns>
    private static Av1RateControlMode GetRateControlMode(HeifRateControl rateControl) => rateControl switch
    {
        HeifRateControl.ConstrainedQuality => Av1RateControlMode.ConstrainedQuality,
        HeifRateControl.VariableBitRate => Av1RateControlMode.VariableBitRate,
        HeifRateControl.ConstantBitRate => Av1RateControlMode.ConstantBitRate,
        _ => Av1RateControlMode.Quality
    };

    /// <summary>
    /// Returns the quantizer range of a coding. Lossless coding is the only coding at quantizer 0, and it keeps 0 in
    /// every mode. The constant-quality and constrained-quality modes keep the default range of 0 to 63. The variable
    /// and constant bit-rate modes narrow the range to four steps either side of the requested quantizer.
    /// </summary>
    /// <param name="quantizer">The requested quantizer on the external scale of 0 to 63.</param>
    /// <param name="mode">The rate control mode.</param>
    /// <returns>The lowest and highest quantizer on the external scale of 0 to 63.</returns>
    private static (int Minimum, int Maximum) GetQuantizerRange(int quantizer, Av1RateControlMode mode)
    {
        if (quantizer == 0)
        {
            return (0, 0);
        }

        if (mode is not (Av1RateControlMode.VariableBitRate or Av1RateControlMode.ConstantBitRate))
        {
            return (0, 63);
        }

        return (Math.Max(quantizer - 4, 0), Math.Min(quantizer + 4, 63));
    }

    /// <summary>
    /// Gets the size of the first coded cell: the frame itself, or the first grid cell of an oversized still image.
    /// </summary>
    /// <param name="imageSize">The size of the image.</param>
    /// <param name="allIntra">Whether the encoding is a still image without layers. Only such an image can become a grid.</param>
    /// <param name="isSubsampledX">Whether the chroma is subsampled horizontally, which makes the cell width even.</param>
    /// <param name="isSubsampledY">Whether the chroma is subsampled vertically, which makes the cell height even.</param>
    /// <returns>The size of the first cell.</returns>
    private static Size GetFirstCellSize(Size imageSize, bool allIntra, bool isSubsampledX, bool isSubsampledY)
    {
        if (!allIntra || (imageSize.Width <= Av1Constants.MaxFrameDimension && imageSize.Height <= Av1Constants.MaxFrameDimension))
        {
            return imageSize;
        }

        int columns = GetGridCellCount(imageSize.Width, Av1Constants.MaxFrameDimension);
        int rows = GetGridCellCount(imageSize.Height, Av1Constants.MaxFrameDimension);
        return new Size(
            Math.Max(GetGridCellSize(imageSize.Width, columns, isSubsampledX), MinimumGridCellDimension),
            Math.Max(GetGridCellSize(imageSize.Height, rows, isSubsampledY), MinimumGridCellDimension));
    }

    /// <summary>
    /// Chooses the tile rows and columns for automatic tiling. The budget is a fixed 8 threads. The cell gets at most
    /// one tile per thread and one tile per 512x512 area. The longer side gets more tiles.
    /// </summary>
    /// <param name="cellSize">The size of the first coded cell.</param>
    /// <returns>The base-2 logarithms of the tile row count and the tile column count.</returns>
    internal static (int RowsLog2, int ColumnsLog2) GetAutomaticTileConfiguration(Size cellSize)
    {
        const uint threads = 8;
        const uint minimumTileArea = 512 * 512;
        const uint maximumTiles = 32;
        ulong imageArea = (ulong)cellSize.Width * (uint)cellSize.Height;
        uint tiles = (uint)((imageArea + minimumTileArea - 1) / minimumTileArea);
        tiles = Math.Min(Math.Min(tiles, maximumTiles), threads);
        int tilesLog2 = BitOperations.Log2(tiles);

        // The longer dimension takes the extra tiles, so each tile is closer to a square.
        if (cellSize.Width >= cellSize.Height)
        {
            int rowsLog2 = SplitTilesLog2((uint)cellSize.Width, (uint)cellSize.Height, tilesLog2);
            return (rowsLog2, tilesLog2 - rowsLog2);
        }

        int columnsLog2 = SplitTilesLog2((uint)cellSize.Height, (uint)cellSize.Width, tilesLog2);
        return (tilesLog2 - columnsLog2, columnsLog2);

        // Returns the base-2 logarithm of the tile count along the shorter dimension.
        static int SplitTilesLog2(uint longer, uint shorter, int tilesLog2)
        {
            int differenceLog2 = BitOperations.Log2(longer / shorter);
            return Math.Max(tilesLog2 - differenceLog2, 0) / 2;
        }
    }

    /// <summary>
    /// Encodes the color frames and the alpha frames of an image sequence into the stream and records one sample per frame.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The stream that receives the coded samples.</param>
    /// <param name="settings">The resolved encoding settings.</param>
    /// <param name="samples">The sample table. The color track uses the first slice, and the alpha track uses the second slice.</param>
    /// <param name="firstFrameIndex">The index of the first frame of the sequence.</param>
    /// <param name="cancellationToken">The token that stops the encode.</param>
    /// <returns>The coded tracks and the metadata of the sequence.</returns>
    private HeifSequenceEncoding CompressAv1Sequence<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        Memory<HeifSequenceSampleInfo> samples,
        int firstFrameIndex,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte[]? exifData = null;
        uint tiffHeaderOffset = 0;
        byte[]? xmpData = null;
        if (!this.encoder.SkipMetadata)
        {
            exifData = GetExifData(image.Metadata, out tiffHeaderOffset);
            byte[]? sourceXmpData = image.Metadata.XmpProfile?.Data;
            if (sourceXmpData is not null && sourceXmpData.Length > 0)
            {
                xmpData = sourceXmpData;
            }
        }

        int frameCount = image.Frames.Count - firstFrameIndex;
        uint timescale = GetSequenceTimescale(image, firstFrameIndex);

        // After each frame, the container needs only the offset, length and duration of its sample. Color and alpha
        // share one table from the memory allocator. Each track uses one contiguous slice of the table.
        Span<HeifSequenceSampleInfo> colorSamples = samples.Span[..frameCount];
        ImageFrame<TPixel> firstFrame = image.Frames[firstFrameIndex];
        ObuSequenceHeader colorHeader;
        using (Av1FrameEncoder.SequenceEncoder colorEncoder = Av1FrameEncoder.CreateColorSequenceEncoder(
            this.configuration,
            image.Width,
            image.Height,
            settings.ColorConfig,
            settings.ColorQIndex,
            options: settings.ColorOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            long colorOffset = stream.Length;
            if (settings.ColorOptions.LagInFrames > 0)
            {
                // A lookahead codes frames out of display order, so each sample is one temporal unit that ends with a
                // shown frame. The encoder gives every frame a duration of 1/30 second.
                using IMemoryOwner<long> sampleEndsOwner = this.configuration.MemoryAllocator.Allocate<long>(frameCount);
                using IMemoryOwner<bool> syncSamplesOwner = this.configuration.MemoryAllocator.Allocate<bool>(frameCount);
                Span<long> sampleEnds = sampleEndsOwner.Memory.Span;
                Span<bool> syncSamples = syncSamplesOwner.Memory.Span;
                colorEncoder.EncodeWithLookahead(
                    image,
                    firstFrameIndex,
                    frameCount,
                    LibavifFrameDurationTicks,
                    stream,
                    sampleEnds,
                    syncSamples,
                    cancellationToken);

                colorHeader = colorEncoder.SequenceHeader;
                long sampleStart = colorOffset;
                for (int sampleIndex = 0; sampleIndex < frameCount; sampleIndex++)
                {
                    long sampleEnd = sampleEnds[sampleIndex];
                    uint duration = GetSequenceSampleDuration(
                        image.Frames[firstFrameIndex + sampleIndex].Metadata.GetHeifMetadata().FrameDelay,
                        timescale);

                    colorSamples[sampleIndex] = new HeifSequenceSampleInfo(
                        sampleStart,
                        checked((int)(sampleEnd - sampleStart)),
                        duration,
                        syncSamples[sampleIndex]);

                    sampleStart = sampleEnd;
                }
            }
            else
            {
                CompressUnlaggedColorSequence(colorEncoder, image, stream, colorSamples, firstFrameIndex, frameCount, timescale, cancellationToken);
                colorHeader = colorEncoder.SequenceHeader;
            }
        }

        HeifSequenceTrackEncoding colorTrack = new(
            new Av1CodecConfiguration(colorHeader),
            samples[..frameCount],
            false);

        HeifSequenceTrackEncoding? alphaTrack = null;
        if (settings.HasAlpha)
        {
            Memory<HeifSequenceSampleInfo> alphaSampleMemory = samples.Slice(frameCount, frameCount);
            Span<HeifSequenceSampleInfo> alphaSamples = alphaSampleMemory.Span;
            ObuSequenceHeader alphaHeader;
            using (Av1FrameEncoder.SequenceEncoder alphaEncoder = Av1FrameEncoder.CreateAlphaSequenceEncoder(
                this.configuration,
                image.Width,
                image.Height,
                settings.AlphaConfig,
                settings.AlphaQIndex,
                options: settings.AlphaOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                long alphaOffset = stream.Length;
                alphaEncoder.EncodeKeyFrame(firstFrame, stream);
                alphaHeader = alphaEncoder.SequenceHeader;

                alphaSamples[0] = new HeifSequenceSampleInfo(
                    alphaOffset,
                    checked((int)(stream.Length - alphaOffset)),
                    colorSamples[0].Duration,
                    isSyncSample: true);

                for (int sampleIndex = 1; sampleIndex < frameCount; sampleIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int frameIndex = firstFrameIndex + sampleIndex;
                    alphaOffset = stream.Length;

                    // A color key frame forces an alpha key frame, so both tracks can start at that sample.
                    bool keyFrame = alphaEncoder.EncodeNextFrame(
                        image.Frames[frameIndex],
                        stream,
                        forceKeyFrame: colorSamples[sampleIndex].IsSyncSample);

                    alphaSamples[sampleIndex] = new HeifSequenceSampleInfo(
                        alphaOffset,
                        checked((int)(stream.Length - alphaOffset)),
                        colorSamples[sampleIndex].Duration,
                        isSyncSample: keyFrame);
                }
            }

            alphaTrack = new HeifSequenceTrackEncoding(
                new Av1CodecConfiguration(alphaHeader),
                alphaSampleMemory,
                true);
        }

        ReadOnlyMemory<byte> iccProfileData = ReadOnlyMemory<byte>.Empty;
        IccProfile? iccProfile = image.Metadata.IccProfile;
        if (!this.encoder.SkipMetadata && iccProfile is not null)
        {
            iccProfileData = iccProfile.ToByteArray();
        }

        return new HeifSequenceEncoding(
            image.Width,
            image.Height,
            this.encoder.RepeatCount ?? image.Metadata.GetHeifMetadata().RepeatCount,
            timescale,
            colorTrack,
            alphaTrack,
            settings.ColorProfile,
            iccProfileData,
            exifData,
            tiffHeaderOffset,
            xmpData);
    }

    /// <summary>
    /// Codes a color sequence one frame per sample: a key frame, then inter frames.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="colorEncoder">The color sequence encoder.</param>
    /// <param name="image">The image that holds the frames.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="colorSamples">Receives the sample of each frame.</param>
    /// <param name="firstFrameIndex">The index of the first frame to encode.</param>
    /// <param name="frameCount">The number of frames to encode.</param>
    /// <param name="timescale">The track timescale.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private static void CompressUnlaggedColorSequence<TPixel>(
        Av1FrameEncoder.SequenceEncoder colorEncoder,
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        Span<HeifSequenceSampleInfo> colorSamples,
        int firstFrameIndex,
        int frameCount,
        uint timescale,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageFrame<TPixel> firstFrame = image.Frames[firstFrameIndex];
        long colorOffset = stream.Length;
        colorEncoder.EncodeKeyFrame(firstFrame, stream);
        colorSamples[0] = new HeifSequenceSampleInfo(
            colorOffset,
            checked((int)(stream.Length - colorOffset)),
            GetSequenceSampleDuration(firstFrame.Metadata.GetHeifMetadata().FrameDelay, timescale),
            isSyncSample: true);

        for (int sampleIndex = 1; sampleIndex < frameCount; sampleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int frameIndex = firstFrameIndex + sampleIndex;
            ImageFrame<TPixel> frame = image.Frames[frameIndex];
            uint duration = GetSequenceSampleDuration(frame.Metadata.GetHeifMetadata().FrameDelay, timescale);
            colorOffset = stream.Length;
            bool keyFrame = colorEncoder.EncodeNextFrame(frame, stream, forceKeyFrame: false);
            colorSamples[sampleIndex] = new HeifSequenceSampleInfo(
                colorOffset,
                checked((int)(stream.Length - colorOffset)),
                duration,
                isSyncSample: keyFrame);
        }
    }

    /// <summary>
    /// Writes the file type box of an image sequence, with the major brand avis and the compatible AVIF, MIAF and
    /// ISO base media brands.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <returns>The number of bytes written.</returns>
    private int WriteSequenceFileTypeBox(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[44];
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avis);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avif);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avio);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avis);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Msf1);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Iso8);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Mif1);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Miaf);
        bytesWritten += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer[..bytesWritten]);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the movie box of an image sequence. The movie box comes before the media data box, so the method
    /// patches each chunk offset after it knows the movie box length.
    /// </summary>
    /// <param name="sequence">The coded sequence.</param>
    /// <param name="precedingBoxLength">The number of bytes in the file before the movie box.</param>
    /// <param name="stream">The destination stream.</param>
    private void WriteSequenceMovieBox(HeifSequenceEncoding sequence, int precedingBoxLength, Stream stream)
    {
        int movieLength = GetSequenceMovieBoxLength(sequence);
        using IMemoryOwner<byte> movieOwner = this.configuration.MemoryAllocator.Allocate<byte>(movieLength);
        Span<byte> memory = movieOwner.Memory.Span[..movieLength];
        int offset = 0;
        int movieStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Moov);
        ulong mediaDuration = GetSequenceMediaDuration(sequence.ColorTrack.Samples);
        ulong trackDuration = sequence.RepeatCount == 0
            ? ulong.MaxValue
            : checked(mediaDuration * sequence.RepeatCount);

        bool hasAlpha = sequence.AlphaTrack.HasValue;

        WriteSequenceMovieHeader(
            memory,
            ref offset,
            sequence.Timescale,
            trackDuration,
            hasAlpha ? 3U : 2U);

        int colorChunkOffsetPosition = WriteSequenceTrack(
            memory,
            ref offset,
            sequence,
            sequence.ColorTrack,
            1,
            mediaDuration,
            trackDuration);

        int alphaChunkOffsetPosition = -1;
        long alphaPayloadOffset = 0;
        if (hasAlpha)
        {
            HeifSequenceTrackEncoding alphaTrack = sequence.AlphaTrack.GetValueOrDefault();
            alphaPayloadOffset = alphaTrack.Samples[0].Offset;
            alphaChunkOffsetPosition = WriteSequenceTrack(
                memory,
                ref offset,
                sequence,
                alphaTrack,
                2,
                mediaDuration,
                trackDuration);
        }

        EndSequenceBox(memory, movieStart, offset);
        ulong mediaDataOffset = checked((ulong)precedingBoxLength + (uint)offset + 8U);
        BinaryPrimitives.WriteUInt64BigEndian(
            memory[colorChunkOffsetPosition..],
            checked(mediaDataOffset + (ulong)sequence.ColorTrack.Samples[0].Offset));

        if (alphaChunkOffsetPosition >= 0)
        {
            BinaryPrimitives.WriteUInt64BigEndian(
                memory[alphaChunkOffsetPosition..],
                checked(mediaDataOffset + (ulong)alphaPayloadOffset));
        }

        stream.Write(memory);
    }

    /// <summary>
    /// Gets the exact length of the movie box that <see cref="WriteSequenceMovieBox"/> writes.
    /// </summary>
    /// <param name="sequence">The coded sequence.</param>
    /// <returns>The length of the movie box in bytes.</returns>
    private static int GetSequenceMovieBoxLength(HeifSequenceEncoding sequence)
    {
        const int movieHeaderBoxLength = 120;
        const int trackHeaderBoxLength = 104;
        const int trackReferenceBoxLength = 20;
        const int editListBoxLength = 44;
        const int mediaBoxFixedLength = 129;
        const int colorInformationBoxLength = 19;
        const int codecConfigurationBoxLength = 12;
        const int codingConstraintsBoxLength = 16;
        const int visualSampleEntryLength = 86;
        const int sampleDescriptionBoxLength = 16;
        const int sampleTableBoxHeaderLength = 8;
        const int timeToSampleBoxFixedLength = 16;
        const int sampleToChunkBoxLength = 28;
        const int sampleSizeBoxFixedLength = 20;
        const int chunkOffsetBoxLength = 24;
        const int syncSampleBoxFixedLength = 16;
        const int timingRunLength = 8;
        const int sampleSizeEntryLength = sizeof(uint);
        const int syncSampleEntryLength = sizeof(uint);
        const int sampleTableFixedLength =
            sampleTableBoxHeaderLength
            + sampleDescriptionBoxLength
            + visualSampleEntryLength
            + codecConfigurationBoxLength
            + codingConstraintsBoxLength
            + timeToSampleBoxFixedLength
            + sampleToChunkBoxLength
            + sampleSizeBoxFixedLength
            + chunkOffsetBoxLength
            + syncSampleBoxFixedLength;

        const int metadataFixedLength = 83;
        const int metadataLocationLength = 16;
        const int exifInformationLength = 25;
        const int xmpInformationLength = 44;
        const int exifOffsetLength = sizeof(uint);

        int repeatBoxLength = sequence.RepeatCount == 1 ? 0 : editListBoxLength;
        int colorRunCount = GetSequenceTimingRunCount(sequence.ColorTrack.Samples);
        int colorSyncSampleCount = GetSequenceSyncSampleCount(sequence.ColorTrack.Samples);
        long colorSampleTableLength =
            (long)sampleTableFixedLength
            + (colorRunCount * timingRunLength)
            + (sequence.ColorTrack.Samples.Length * sampleSizeEntryLength)
            + (colorSyncSampleCount * syncSampleEntryLength)
            + colorInformationBoxLength;

        if (!sequence.IccProfileData.IsEmpty)
        {
            colorSampleTableLength = colorSampleTableLength
                + IccColorInformationPropertyBoxFixedLength
                + sequence.IccProfileData.Length;
        }

        byte[]? exifData = sequence.ExifData;
        byte[]? xmpData = sequence.XmpData;
        long metadataLength = 0;
        if (exifData is not null || xmpData is not null)
        {
            int metadataItemCount = (exifData is not null ? 1 : 0) + (xmpData is not null ? 1 : 0);
            metadataLength = (long)metadataFixedLength
                + (metadataItemCount * metadataLocationLength)
                + (exifData is not null ? (long)exifInformationLength + exifOffsetLength + exifData.Length : 0)
                + (xmpData is not null ? (long)xmpInformationLength + xmpData.Length : 0);
        }

        long colorTrackLength =
            BasicBoxHeaderLength
            + trackHeaderBoxLength
            + repeatBoxLength
            + metadataLength
            + mediaBoxFixedLength
            + colorSampleTableLength;

        long alphaTrackLength = 0;
        if (sequence.AlphaTrack.HasValue)
        {
            HeifSequenceTrackEncoding alphaTrack = sequence.AlphaTrack.GetValueOrDefault();
            int alphaRunCount = GetSequenceTimingRunCount(alphaTrack.Samples);
            int alphaSyncSampleCount = GetSequenceSyncSampleCount(alphaTrack.Samples);
            int auxiliaryTypeBoxLength =
                FullBoxHeaderLength
                + Encoding.UTF8.GetByteCount(HeifConstants.AlphaAuxiliaryType)
                + 1;

            long alphaSampleTableLength =
                (long)sampleTableFixedLength
                + (alphaRunCount * timingRunLength)
                + (alphaTrack.Samples.Length * sampleSizeEntryLength)
                + (alphaSyncSampleCount * syncSampleEntryLength)
                + auxiliaryTypeBoxLength;

            alphaTrackLength =
                BasicBoxHeaderLength
                + trackHeaderBoxLength
                + trackReferenceBoxLength
                + repeatBoxLength
                + mediaBoxFixedLength
                + alphaSampleTableLength;
        }

        // The movie box contains one header and one or two tracks. The length is exact, so the writer fills one
        // buffer and never grows or copies it.
        long movieLength = BasicBoxHeaderLength + movieHeaderBoxLength + colorTrackLength + alphaTrackLength;
        return checked((int)movieLength);
    }

    /// <summary>
    /// Writes a version 1 movie header box with the identity matrix and unit rate and volume.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="timescale">The number of time units in one second.</param>
    /// <param name="duration">The duration of the movie in time units.</param>
    /// <param name="nextTrackId">The next free track identifier.</param>
    private static void WriteSequenceMovieHeader(
        Span<byte> memory,
        ref int offset,
        uint timescale,
        ulong duration,
        uint nextTrackId)
    {
        int movieHeaderStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Mvhd);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, timescale);
        WriteSequenceUInt64(memory, ref offset, duration);
        WriteSequenceUInt32(memory, ref offset, UnityFixed16Point16);
        WriteSequenceUInt16(memory, ref offset, UnityFixed8Point8);
        WriteSequenceUInt16(memory, ref offset, 0);
        WriteSequenceZeros(memory, ref offset, 2 * sizeof(uint));
        WriteSequenceIdentityMatrix(memory, ref offset);
        WriteSequenceZeros(memory, ref offset, 6 * sizeof(uint));
        WriteSequenceUInt32(memory, ref offset, nextTrackId);
        EndSequenceBox(memory, movieHeaderStart, offset);
    }

    /// <summary>
    /// Writes a track box for the color or the alpha track.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="sequence">The coded sequence.</param>
    /// <param name="track">The track to write.</param>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="mediaDuration">The duration of one play of the samples, in time units.</param>
    /// <param name="trackDuration">The duration of the track with all repeats, in time units.</param>
    /// <returns>The position of the chunk offset field, which the caller patches later.</returns>
    private static int WriteSequenceTrack(
        Span<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence,
        HeifSequenceTrackEncoding track,
        uint trackId,
        ulong mediaDuration,
        ulong trackDuration)
    {
        int trackStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Trak);
        WriteSequenceTrackHeader(
            memory,
            ref offset,
            sequence.Width,
            sequence.Height,
            trackId,
            trackDuration);

        if (track.IsAlpha)
        {
            WriteSequenceTrackReference(memory, ref offset, Heif4CharCode.Auxl, 1);
        }

        if (sequence.RepeatCount != 1)
        {
            WriteSequenceEditList(memory, ref offset, mediaDuration);
        }

        if (!track.IsAlpha && (sequence.ExifData is not null || sequence.XmpData is not null))
        {
            WriteSequenceTrackMetadata(memory, ref offset, sequence);
        }

        int mediaStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Mdia);
        WriteSequenceMediaHeader(memory, ref offset, sequence.Timescale, mediaDuration);
        WriteSequenceHandler(memory, ref offset, track.IsAlpha ? Heif4CharCode.Auxv : Heif4CharCode.Pict);
        int mediaInformationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Minf);
        WriteSequenceDataInformation(memory, ref offset);
        int chunkOffsetPosition = WriteSequenceSampleTable(memory, ref offset, sequence, track);
        EndSequenceBox(memory, mediaInformationStart, offset);
        EndSequenceBox(memory, mediaStart, offset);
        EndSequenceBox(memory, trackStart, offset);
        return chunkOffsetPosition;
    }

    /// <summary>
    /// Writes a version 1 track header box with the enabled flag, the identity matrix and the presentation size.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="width">The presentation width in pixels.</param>
    /// <param name="height">The presentation height in pixels.</param>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="duration">The duration of the track in time units.</param>
    private static void WriteSequenceTrackHeader(
        Span<byte> memory,
        ref int offset,
        int width,
        int height,
        uint trackId,
        ulong duration)
    {
        int trackHeaderStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Tkhd);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 1);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, trackId);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, duration);
        WriteSequenceZeros(memory, ref offset, (2 * sizeof(uint)) + (4 * sizeof(ushort)));
        WriteSequenceIdentityMatrix(memory, ref offset);
        WriteSequenceUInt32(memory, ref offset, (uint)width << 16);
        WriteSequenceUInt32(memory, ref offset, (uint)height << 16);
        EndSequenceBox(memory, trackHeaderStart, offset);
    }

    /// <summary>
    /// Writes a track reference box with one reference to one track.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="referenceType">The reference type.</param>
    /// <param name="referencedTrackId">The identifier of the referenced track.</param>
    private static void WriteSequenceTrackReference(
        Span<byte> memory,
        ref int offset,
        Heif4CharCode referenceType,
        uint referencedTrackId)
    {
        int referencesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Tref);
        int referenceStart = BeginSequenceBox(memory, ref offset, referenceType);
        WriteSequenceUInt32(memory, ref offset, referencedTrackId);
        EndSequenceBox(memory, referenceStart, offset);
        EndSequenceBox(memory, referencesStart, offset);
    }

    /// <summary>
    /// Writes an edit box with one edit that plays all the media. The repeat flag makes a reader play the edit again
    /// for the full track duration.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="mediaDuration">The duration of one play of the samples, in time units.</param>
    private static void WriteSequenceEditList(
        Span<byte> memory,
        ref int offset,
        ulong mediaDuration)
    {
        int editStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Edts);
        int editListStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Elst);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 1);
        WriteSequenceUInt32(memory, ref offset, 1);
        WriteSequenceUInt64(memory, ref offset, mediaDuration);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceUInt16(memory, ref offset, 0);
        EndSequenceBox(memory, editListStart, offset);
        EndSequenceBox(memory, editStart, offset);
    }

    /// <summary>
    /// Writes a track-level meta box that holds the Exif item, the XMP item, or both, with the item data in an idat box.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="sequence">The coded sequence that holds the metadata.</param>
    private static void WriteSequenceTrackMetadata(
        Span<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence)
    {
        const byte fourByteOffsetAndLengthSizes = 0x44;
        byte[]? exifData = sequence.ExifData;
        byte[]? xmpData = sequence.XmpData;
        ushort itemCount = (ushort)((exifData is not null ? 1 : 0) + (xmpData is not null ? 1 : 0));
        int metadataStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Meta);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceHandler(memory, ref offset, Heif4CharCode.Pict);

        // Construction method 1 makes each extent relative to the idat payload. Thus the metadata does not depend on
        // the final offsets of the file or the movie box.
        int locationsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Iloc);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 0);
        memory[offset++] = fourByteOffsetAndLengthSizes;
        memory[offset++] = 0;
        WriteSequenceUInt16(memory, ref offset, itemCount);
        ushort itemId = 1;
        uint itemDataOffset = 0;
        if (exifData is not null)
        {
            uint exifLength = (uint)exifData.Length + sizeof(uint);
            WriteSequenceTrackMetadataLocation(memory, ref offset, itemId++, itemDataOffset, exifLength);
            itemDataOffset += exifLength;
        }

        if (xmpData is not null)
        {
            WriteSequenceTrackMetadataLocation(
                memory,
                ref offset,
                itemId,
                itemDataOffset,
                (uint)xmpData.Length);
        }

        EndSequenceBox(memory, locationsStart, offset);

        int informationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Iinf);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt16(memory, ref offset, itemCount);
        itemId = 1;
        if (exifData is not null)
        {
            WriteSequenceTrackMetadataItem(memory, ref offset, itemId++, Heif4CharCode.Exif);
        }

        if (xmpData is not null)
        {
            WriteSequenceTrackMetadataItem(memory, ref offset, itemId, Heif4CharCode.Mime);
        }

        EndSequenceBox(memory, informationStart, offset);

        int itemDataStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Idat);
        if (exifData is not null)
        {
            WriteSequenceUInt32(memory, ref offset, sequence.ExifTiffHeaderOffset);
            WriteSequenceBytes(memory, ref offset, exifData);
        }

        if (xmpData is not null)
        {
            WriteSequenceBytes(memory, ref offset, xmpData);
        }

        EndSequenceBox(memory, itemDataStart, offset);
        EndSequenceBox(memory, metadataStart, offset);
    }

    /// <summary>
    /// Writes one version 1 item location entry with construction method 1 and one extent in the idat box.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the entry.</param>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="itemDataOffset">The offset of the item data in the idat payload.</param>
    /// <param name="itemLength">The length of the item data in bytes.</param>
    private static void WriteSequenceTrackMetadataLocation(
        Span<byte> memory,
        ref int offset,
        ushort itemId,
        uint itemDataOffset,
        uint itemLength)
    {
        WriteSequenceUInt16(memory, ref offset, itemId);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceUInt16(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceUInt32(memory, ref offset, itemDataOffset);
        WriteSequenceUInt32(memory, ref offset, itemLength);
    }

    /// <summary>
    /// Writes a version 2 item information entry for an Exif item or an XMP item. An XMP item also gets its content type.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the entry.</param>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="itemType">The item type: Exif or mime.</param>
    private static void WriteSequenceTrackMetadataItem(
        Span<byte> memory,
        ref int offset,
        ushort itemId,
        Heif4CharCode itemType)
    {
        int itemStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Infe);
        WriteSequenceFullBoxHeader(memory, ref offset, 2, 0);
        WriteSequenceUInt16(memory, ref offset, itemId);
        WriteSequenceUInt16(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)itemType);
        ReadOnlySpan<byte> itemName = itemType == Heif4CharCode.Exif ? "Exif"u8 : "XMP"u8;
        WriteSequenceBytes(memory, ref offset, itemName);
        memory[offset++] = 0;
        if (itemType == Heif4CharCode.Mime)
        {
            WriteSequenceBytes(memory, ref offset, "application/rdf+xml"u8);
            memory[offset++] = 0;
        }

        EndSequenceBox(memory, itemStart, offset);
    }

    /// <summary>
    /// Writes a version 1 media header box with an undetermined language.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="timescale">The number of time units in one second.</param>
    /// <param name="mediaDuration">The duration of one play of the samples, in time units.</param>
    private static void WriteSequenceMediaHeader(
        Span<byte> memory,
        ref int offset,
        uint timescale,
        ulong mediaDuration)
    {
        int mediaHeaderStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Mdhd);
        WriteSequenceFullBoxHeader(memory, ref offset, 1, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt64(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, timescale);
        WriteSequenceUInt64(memory, ref offset, mediaDuration);
        WriteSequenceUInt16(memory, ref offset, PackedUndeterminedLanguage);
        WriteSequenceUInt16(memory, ref offset, 0);
        EndSequenceBox(memory, mediaHeaderStart, offset);
    }

    /// <summary>
    /// Writes a handler box with an empty name.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="handlerType">The handler type.</param>
    private static void WriteSequenceHandler(
        Span<byte> memory,
        ref int offset,
        Heif4CharCode handlerType)
    {
        int handlerStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Hdlr);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)handlerType);
        WriteSequenceZeros(memory, ref offset, 12);
        memory[offset++] = 0;
        EndSequenceBox(memory, handlerStart, offset);
    }

    /// <summary>
    /// Writes a data information box with one self-contained data reference. The media data is in the same file.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    private static void WriteSequenceDataInformation(Span<byte> memory, ref int offset)
    {
        int dataInformationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Dinf);
        int dataReferenceStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Dref);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int locationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Url);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 1);
        EndSequenceBox(memory, locationStart, offset);
        EndSequenceBox(memory, dataReferenceStart, offset);
        EndSequenceBox(memory, dataInformationStart, offset);
    }

    /// <summary>
    /// Writes the sample table box of a track: the sample description, the timing, one chunk, the sample sizes and
    /// the sync samples.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="sequence">The coded sequence.</param>
    /// <param name="track">The track to write.</param>
    /// <returns>The position of the chunk offset field, which the caller patches later.</returns>
    private static int WriteSequenceSampleTable(
        Span<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence,
        HeifSequenceTrackEncoding track)
    {
        int sampleTableStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stbl);
        WriteSequenceSampleDescription(memory, ref offset, sequence, track);
        WriteSequenceSampleTiming(memory, ref offset, track.Samples);

        // The samples of each track are contiguous, so one chunk holds every sample of the track.
        int sampleToChunkStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsc);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        WriteSequenceUInt32(memory, ref offset, 1);
        WriteSequenceUInt32(memory, ref offset, (uint)track.Samples.Length);
        WriteSequenceUInt32(memory, ref offset, 1);
        EndSequenceBox(memory, sampleToChunkStart, offset);

        int sampleSizesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsz);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)track.Samples.Length);
        foreach (HeifSequenceSampleInfo sample in track.Samples)
        {
            WriteSequenceUInt32(memory, ref offset, (uint)sample.Length);
        }

        EndSequenceBox(memory, sampleSizesStart, offset);

        int chunkOffsetsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Co64);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int chunkOffsetPosition = offset;
        WriteSequenceUInt64(memory, ref offset, 0);
        EndSequenceBox(memory, chunkOffsetsStart, offset);

        int syncSampleCount = GetSequenceSyncSampleCount(track.Samples);
        int syncSamplesStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stss);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)syncSampleCount);
        uint sampleNumber = 1;
        foreach (HeifSequenceSampleInfo sample in track.Samples)
        {
            if (sample.IsSyncSample)
            {
                WriteSequenceUInt32(memory, ref offset, sampleNumber);
            }

            sampleNumber++;
        }

        EndSequenceBox(memory, syncSamplesStart, offset);
        EndSequenceBox(memory, sampleTableStart, offset);
        return chunkOffsetPosition;
    }

    /// <summary>
    /// Writes the sample description box with one AV1 visual sample entry. The entry holds the codec configuration,
    /// the color information or the auxiliary type, and the coding constraints.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="sequence">The coded sequence.</param>
    /// <param name="track">The track to describe.</param>
    private static void WriteSequenceSampleDescription(
        Span<byte> memory,
        ref int offset,
        HeifSequenceEncoding sequence,
        HeifSequenceTrackEncoding track)
    {
        int descriptionStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stsd);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, 1);
        int sampleEntryStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Av01);
        WriteSequenceZeros(memory, ref offset, 6);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceZeros(memory, ref offset, (2 * sizeof(ushort)) + (3 * sizeof(uint)));
        WriteSequenceUInt16(memory, ref offset, (ushort)sequence.Width);
        WriteSequenceUInt16(memory, ref offset, (ushort)sequence.Height);
        WriteSequenceUInt32(memory, ref offset, DefaultVisualSampleResolution);
        WriteSequenceUInt32(memory, ref offset, DefaultVisualSampleResolution);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt16(memory, ref offset, 1);
        WriteSequenceZeros(memory, ref offset, VisualSampleCompressorNameLength);
        WriteSequenceUInt16(memory, ref offset, VisualSampleDepth);
        WriteSequenceUInt16(memory, ref offset, ushort.MaxValue);

        int configurationStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Av1C);
        track.Configuration.WriteFixedHeader(memory.Slice(offset, Av1CodecConfiguration.FixedHeaderSize));
        offset += Av1CodecConfiguration.FixedHeaderSize;
        EndSequenceBox(memory, configurationStart, offset);

        if (track.IsAlpha)
        {
            int auxiliaryTypeStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Auxi);
            WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
            int auxiliaryTypeLength = Encoding.UTF8.GetByteCount(HeifConstants.AlphaAuxiliaryType);
            Span<byte> auxiliaryType = memory.Slice(offset, auxiliaryTypeLength + 1);
            offset += Encoding.UTF8.GetBytes(HeifConstants.AlphaAuxiliaryType, auxiliaryType);
            memory[offset++] = 0;
            EndSequenceBox(memory, auxiliaryTypeStart, offset);
        }
        else
        {
            if (!sequence.IccProfileData.IsEmpty)
            {
                offset += WriteIccColorInformationPropertyBox(memory, offset, sequence.IccProfileData);
            }

            offset += WriteColorInformationPropertyBox(memory, offset, sequence.ColorProfile);
        }

        int codingConstraintsStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Ccst);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);

        // Sync samples are key frames in this encoder. Thus the all-intra flag is valid when every sample is a sync
        // sample.
        uint codingConstraints = IntraPicturePredictionUsedMask;
        if (GetSequenceSyncSampleCount(track.Samples) == track.Samples.Length)
        {
            codingConstraints |= AllReferencePicturesIntraMask;
        }

        WriteSequenceUInt32(memory, ref offset, codingConstraints);
        EndSequenceBox(memory, codingConstraintsStart, offset);
        EndSequenceBox(memory, sampleEntryStart, offset);
        EndSequenceBox(memory, descriptionStart, offset);
    }

    /// <summary>
    /// Writes the time-to-sample box of a track.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the box.</param>
    /// <param name="samples">The samples of the track.</param>
    private static void WriteSequenceSampleTiming(
        Span<byte> memory,
        ref int offset,
        ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        // The time-to-sample table stores runs of equal durations. Only adjacent samples with equal durations join a
        // run, so every duration stays exact.
        int runCount = GetSequenceTimingRunCount(samples);

        int timingStart = BeginSequenceBox(memory, ref offset, Heif4CharCode.Stts);
        WriteSequenceFullBoxHeader(memory, ref offset, 0, 0);
        WriteSequenceUInt32(memory, ref offset, (uint)runCount);
        uint runDuration = samples[0].Duration;
        uint runLength = 1;
        for (int sampleIndex = 1; sampleIndex <= samples.Length; sampleIndex++)
        {
            if (sampleIndex < samples.Length && samples[sampleIndex].Duration == runDuration)
            {
                runLength++;
                continue;
            }

            WriteSequenceUInt32(memory, ref offset, runLength);
            WriteSequenceUInt32(memory, ref offset, runDuration);
            if (sampleIndex < samples.Length)
            {
                runDuration = samples[sampleIndex].Duration;
                runLength = 1;
            }
        }

        EndSequenceBox(memory, timingStart, offset);
    }

    /// <summary>
    /// Counts the runs of adjacent samples with equal durations.
    /// </summary>
    /// <param name="samples">The samples of the track. The span holds at least one sample.</param>
    /// <returns>The number of time-to-sample entries.</returns>
    private static int GetSequenceTimingRunCount(ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        int runCount = 1;
        for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++)
        {
            runCount += samples[sampleIndex].Duration == samples[sampleIndex - 1].Duration ? 0 : 1;
        }

        return runCount;
    }

    /// <summary>
    /// Counts the sync samples of a track.
    /// </summary>
    /// <param name="samples">The samples of the track.</param>
    /// <returns>The number of sync samples.</returns>
    private static int GetSequenceSyncSampleCount(ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        int count = 0;
        foreach (HeifSequenceSampleInfo sample in samples)
        {
            if (sample.IsSyncSample)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Converts a frame delay to a sample duration in time units, rounded to the nearest unit.
    /// </summary>
    /// <param name="delay">The frame delay in seconds.</param>
    /// <param name="timescale">The number of time units in one second.</param>
    /// <returns>The sample duration. The value is at least 1.</returns>
    private static uint GetSequenceSampleDuration(Rational delay, uint timescale)
    {
        // HEIF metadata uses a zero numerator or a zero denominator for an unspecified duration. A sample needs a
        // positive duration, so the method writes the smallest duration of one time unit.
        if (delay.Numerator == 0 || delay.Denominator == 0)
        {
            return 1;
        }

        ulong scaledDuration = ((ulong)delay.Numerator * timescale) + (delay.Denominator / 2U);
        return checked((uint)Math.Max(1UL, scaledDuration / delay.Denominator));
    }

    /// <summary>
    /// Gets a media timescale that holds every frame delay exactly. The timescale is the least common multiple of
    /// 1000 and the delay denominators.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="firstFrameIndex">The index of the first frame of the sequence.</param>
    /// <returns>The number of time units in one second.</returns>
    private static uint GetSequenceTimescale<TPixel>(Image<TPixel> image, int firstFrameIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint timescale = DefaultSequenceTimescale;
        for (int frameIndex = firstFrameIndex; frameIndex < image.Frames.Count; frameIndex++)
        {
            ImageFrame<TPixel> frame = image.Frames[frameIndex];
            Rational delay = frame.Metadata.GetHeifMetadata().FrameDelay;
            if (delay.Numerator == 0 || delay.Denominator == 0)
            {
                continue;
            }

            uint commonDivisor = GetGreatestCommonDivisor(timescale, delay.Denominator);
            ulong commonTimescale = ((ulong)timescale / commonDivisor) * delay.Denominator;
            if (commonTimescale > uint.MaxValue)
            {
                // A media timescale is a 32-bit field. If the exact least common multiple does not fit, use
                // microseconds. Then each duration has an error of at most half a microsecond.
                return FallbackSequenceTimescale;
            }

            timescale = (uint)commonTimescale;
        }

        return timescale;
    }

    /// <summary>
    /// Gets the greatest common divisor of two values with the Euclidean algorithm.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The greatest common divisor.</returns>
    private static uint GetGreatestCommonDivisor(uint left, uint right)
    {
        while (right != 0)
        {
            uint remainder = left % right;
            left = right;
            right = remainder;
        }

        return left;
    }

    /// <summary>
    /// Gets the sum of the sample durations of a track.
    /// </summary>
    /// <param name="samples">The samples of the track.</param>
    /// <returns>The duration of one play of the samples, in time units.</returns>
    private static ulong GetSequenceMediaDuration(ReadOnlySpan<HeifSequenceSampleInfo> samples)
    {
        ulong duration = 0;
        foreach (HeifSequenceSampleInfo sample in samples)
        {
            duration = checked(duration + sample.Duration);
        }

        return duration;
    }

    /// <summary>
    /// Writes the header of a box with a size of zero. The matching <see cref="EndSequenceBox"/> call writes the size.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the header.</param>
    /// <param name="type">The box type.</param>
    /// <returns>The start position of the box.</returns>
    private static int BeginSequenceBox(
        Span<byte> memory,
        ref int offset,
        Heif4CharCode type)
    {
        // The size is not known until the nested boxes are complete, so EndSequenceBox writes it later.
        int start = offset;
        offset += WriteBoxHeader(memory[offset..], type);
        return start;
    }

    /// <summary>
    /// Writes the size of a box that <see cref="BeginSequenceBox"/> started.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="start">The start position of the box.</param>
    /// <param name="offset">The end position of the box.</param>
    private static void EndSequenceBox(Span<byte> memory, int start, int offset)
        => BinaryPrimitives.WriteUInt32BigEndian(
            memory.Slice(start, sizeof(uint)),
            (uint)(offset - start));

    /// <summary>
    /// Writes the version byte and the 24-bit flags of a full box.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it by four bytes.</param>
    /// <param name="version">The box version.</param>
    /// <param name="flags">The box flags. Only the low 24 bits are written.</param>
    private static void WriteSequenceFullBoxHeader(
        Span<byte> memory,
        ref int offset,
        byte version,
        uint flags)
    {
        Span<byte> destination = memory.Slice(offset, sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(destination, flags);
        destination[0] = version;
        offset += sizeof(uint);
    }

    /// <summary>
    /// Writes the identity transformation matrix of a movie or track header. The first eight entries are 16.16 fixed
    /// point, and the last entry is 2.30 fixed point.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the matrix.</param>
    private static void WriteSequenceIdentityMatrix(Span<byte> memory, ref int offset)
    {
        WriteSequenceUInt32(memory, ref offset, UnityFixed16Point16);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, UnityFixed16Point16);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, 0);
        WriteSequenceUInt32(memory, ref offset, UnityFixed2Point30);
    }

    /// <summary>
    /// Writes zero bytes.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it by <paramref name="length"/>.</param>
    /// <param name="length">The number of zero bytes.</param>
    private static void WriteSequenceZeros(Span<byte> memory, ref int offset, int length)
    {
        memory.Slice(offset, length).Clear();
        offset += length;
    }

    /// <summary>
    /// Copies bytes into the buffer.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it past the bytes.</param>
    /// <param name="source">The bytes to copy.</param>
    private static void WriteSequenceBytes(
        Span<byte> memory,
        ref int offset,
        ReadOnlySpan<byte> source)
    {
        source.CopyTo(memory[offset..]);
        offset += source.Length;
    }

    /// <summary>
    /// Writes a big-endian 16-bit value.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it by two bytes.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteSequenceUInt16(Span<byte> memory, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(memory[offset..], value);
        offset += sizeof(ushort);
    }

    /// <summary>
    /// Writes a big-endian 32-bit value.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it by four bytes.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteSequenceUInt32(Span<byte> memory, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(memory[offset..], value);
        offset += sizeof(uint);
    }

    /// <summary>
    /// Writes a big-endian 64-bit value.
    /// </summary>
    /// <param name="memory">The buffer of the movie box.</param>
    /// <param name="offset">The write position in <paramref name="memory"/>. The method advances it by eight bytes.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteSequenceUInt64(Span<byte> memory, ref int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(memory[offset..], value);
        offset += sizeof(ulong);
    }

    /// <summary>
    /// Holds the resolved settings of an encoding for the color image and the alpha image.
    /// </summary>
    private readonly struct Av1EncodingSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Av1EncodingSettings"/> struct.
        /// </summary>
        /// <param name="bitDepth">The encoded bit depth.</param>
        /// <param name="chromaSubsampling">The encoded chroma sampling.</param>
        /// <param name="colorProfile">The color description that the file signals.</param>
        /// <param name="colorConfig">The color configuration of the color image.</param>
        /// <param name="alphaConfig">The color configuration of the alpha image.</param>
        /// <param name="colorQIndex">The quantizer index of the color image.</param>
        /// <param name="alphaQIndex">The quantizer index of the alpha image.</param>
        /// <param name="hasAlpha">Whether the pixel type has alpha.</param>
        /// <param name="colorOptions">The codec options of the color image.</param>
        /// <param name="alphaOptions">The codec options of the alpha image.</param>
        public Av1EncodingSettings(
            HeifBitDepth bitDepth,
            HeifChromaSubsampling chromaSubsampling,
            CicpProfile colorProfile,
            ObuColorConfig colorConfig,
            ObuColorConfig alphaConfig,
            int colorQIndex,
            int alphaQIndex,
            bool hasAlpha,
            Av1EncoderOptions colorOptions,
            Av1EncoderOptions alphaOptions)
        {
            this.BitDepth = bitDepth;
            this.ChromaSubsampling = chromaSubsampling;
            this.ColorProfile = colorProfile;
            this.ColorConfig = colorConfig;
            this.AlphaConfig = alphaConfig;
            this.ColorQIndex = colorQIndex;
            this.AlphaQIndex = alphaQIndex;
            this.HasAlpha = hasAlpha;
            this.ColorOptions = colorOptions;
            this.AlphaOptions = alphaOptions;
        }

        /// <summary>
        /// Gets the encoded bit depth.
        /// </summary>
        public HeifBitDepth BitDepth { get; }

        /// <summary>
        /// Gets the encoded chroma sampling.
        /// </summary>
        public HeifChromaSubsampling ChromaSubsampling { get; }

        /// <summary>
        /// Gets the color description that the file signals.
        /// </summary>
        public CicpProfile ColorProfile { get; }

        /// <summary>
        /// Gets the color configuration of the color image.
        /// </summary>
        public ObuColorConfig ColorConfig { get; }

        /// <summary>
        /// Gets the color configuration of the alpha image.
        /// </summary>
        public ObuColorConfig AlphaConfig { get; }

        /// <summary>
        /// Gets the quantizer index of the color image.
        /// </summary>
        public int ColorQIndex { get; }

        /// <summary>
        /// Gets the quantizer index of the alpha image.
        /// </summary>
        public int AlphaQIndex { get; }

        /// <summary>
        /// Gets a value indicating whether the pixel type has alpha.
        /// </summary>
        public bool HasAlpha { get; }

        /// <summary>
        /// Gets the codec options of the color image.
        /// </summary>
        public Av1EncoderOptions ColorOptions { get; }

        /// <summary>
        /// Gets the codec options of the alpha image.
        /// </summary>
        public Av1EncoderOptions AlphaOptions { get; }
    }

    /// <summary>
    /// Describes one coded sample of a track.
    /// </summary>
    private readonly struct HeifSequenceSampleInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeifSequenceSampleInfo"/> struct.
        /// </summary>
        /// <param name="offset">The offset of the sample in the media data payload.</param>
        /// <param name="length">The length of the sample in bytes.</param>
        /// <param name="duration">The duration of the sample in time units.</param>
        /// <param name="isSyncSample">Whether the sample starts with a key frame.</param>
        public HeifSequenceSampleInfo(long offset, int length, uint duration, bool isSyncSample)
        {
            this.Offset = offset;
            this.Length = length;
            this.Duration = duration;
            this.IsSyncSample = isSyncSample;
        }

        /// <summary>
        /// Gets the offset of the sample in the media data payload.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Gets the length of the sample in bytes.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the duration of the sample in time units.
        /// </summary>
        public uint Duration { get; }

        /// <summary>
        /// Gets a value indicating whether the sample starts with a key frame.
        /// </summary>
        public bool IsSyncSample { get; }
    }

    /// <summary>
    /// Holds a coded image sequence and the metadata that the movie box needs.
    /// </summary>
    private readonly struct HeifSequenceEncoding
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeifSequenceEncoding"/> struct.
        /// </summary>
        /// <param name="width">The presentation width in pixels.</param>
        /// <param name="height">The presentation height in pixels.</param>
        /// <param name="repeatCount">The number of plays, or 0 to repeat without end.</param>
        /// <param name="timescale">The number of time units in one second.</param>
        /// <param name="colorTrack">The color track.</param>
        /// <param name="alphaTrack">The alpha track, or <see langword="null"/> when the image has no alpha.</param>
        /// <param name="colorProfile">The color description that the file signals.</param>
        /// <param name="iccProfileData">The ICC profile bytes, or empty when the file has no ICC profile.</param>
        /// <param name="exifData">The Exif bytes, or <see langword="null"/>.</param>
        /// <param name="exifTiffHeaderOffset">The offset of the TIFF header in <paramref name="exifData"/>.</param>
        /// <param name="xmpData">The XMP bytes, or <see langword="null"/>.</param>
        public HeifSequenceEncoding(
            int width,
            int height,
            ushort repeatCount,
            uint timescale,
            HeifSequenceTrackEncoding colorTrack,
            HeifSequenceTrackEncoding? alphaTrack,
            CicpProfile colorProfile,
            ReadOnlyMemory<byte> iccProfileData,
            byte[]? exifData,
            uint exifTiffHeaderOffset,
            byte[]? xmpData)
        {
            this.Width = width;
            this.Height = height;
            this.RepeatCount = repeatCount;
            this.Timescale = timescale;
            this.ColorTrack = colorTrack;
            this.AlphaTrack = alphaTrack;
            this.ColorProfile = colorProfile;
            this.IccProfileData = iccProfileData;
            this.ExifData = exifData;
            this.ExifTiffHeaderOffset = exifTiffHeaderOffset;
            this.XmpData = xmpData;
        }

        /// <summary>
        /// Gets the presentation width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the presentation height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the number of plays, or 0 to repeat without end.
        /// </summary>
        public ushort RepeatCount { get; }

        /// <summary>
        /// Gets the number of time units in one second.
        /// </summary>
        public uint Timescale { get; }

        /// <summary>
        /// Gets the color track.
        /// </summary>
        public HeifSequenceTrackEncoding ColorTrack { get; }

        /// <summary>
        /// Gets the alpha track, or <see langword="null"/> when the image has no alpha.
        /// </summary>
        public HeifSequenceTrackEncoding? AlphaTrack { get; }

        /// <summary>
        /// Gets the color description that the file signals.
        /// </summary>
        public CicpProfile ColorProfile { get; }

        /// <summary>
        /// Gets the ICC profile bytes, or empty when the file has no ICC profile.
        /// </summary>
        public ReadOnlyMemory<byte> IccProfileData { get; }

        /// <summary>
        /// Gets the Exif bytes, or <see langword="null"/>.
        /// </summary>
        public byte[]? ExifData { get; }

        /// <summary>
        /// Gets the offset of the TIFF header in <see cref="ExifData"/>.
        /// </summary>
        public uint ExifTiffHeaderOffset { get; }

        /// <summary>
        /// Gets the XMP bytes, or <see langword="null"/>.
        /// </summary>
        public byte[]? XmpData { get; }
    }

    /// <summary>
    /// Holds the codec configuration and the samples of one track.
    /// </summary>
    private readonly struct HeifSequenceTrackEncoding
    {
        private readonly ReadOnlyMemory<HeifSequenceSampleInfo> samples;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeifSequenceTrackEncoding"/> struct.
        /// </summary>
        /// <param name="configuration">The codec configuration of the track.</param>
        /// <param name="samples">The samples of the track.</param>
        /// <param name="isAlpha">Whether the track is the alpha track.</param>
        public HeifSequenceTrackEncoding(
            Av1CodecConfiguration configuration,
            ReadOnlyMemory<HeifSequenceSampleInfo> samples,
            bool isAlpha)
        {
            this.Configuration = configuration;
            this.samples = samples;
            this.IsAlpha = isAlpha;
        }

        /// <summary>
        /// Gets the codec configuration of the track.
        /// </summary>
        public Av1CodecConfiguration Configuration { get; }

        /// <summary>
        /// Gets the samples of the track.
        /// </summary>
        public ReadOnlySpan<HeifSequenceSampleInfo> Samples
            => this.samples.Span;

        /// <summary>
        /// Gets a value indicating whether the track is the alpha track.
        /// </summary>
        public bool IsAlpha { get; }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Webp.BitWriter;
using SixLabors.ImageSharp.Formats.Webp.Chunks;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

/// <summary>
/// Encoder for lossless webp images.
/// </summary>
internal class Vp8LEncoder : IDisposable
{
    /// <summary>
    /// Scratch buffer to reduce allocations.
    /// </summary>
    private ScratchBuffer scratch;  // mutable struct, don't make readonly

    private readonly int[][] histoArgb = { new int[256], new int[256], new int[256], new int[256] };

    private readonly int[][] bestHisto = { new int[256], new int[256], new int[256], new int[256] };

    /// <summary>
    /// The <see cref="MemoryAllocator"/> to use for buffer allocations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Maximum number of reference blocks the image will be segmented into.
    /// </summary>
    private const int MaxRefsBlockPerImage = 16;

    /// <summary>
    /// Minimum block size for backward references.
    /// </summary>
    private const int MinBlockSize = 256;

    /// <summary>
    /// A bit writer for writing lossless webp streams.
    /// </summary>
    private Vp8LBitWriter bitWriter;

    /// <summary>
    /// The quality, that will be used to encode the image.
    /// </summary>
    private readonly uint quality;

    /// <summary>
    /// Quality/speed trade-off (0=fast, 6=slower-better).
    /// </summary>
    private readonly WebpEncodingMethod method;

    /// <summary>
    /// Flag indicating whether to preserve the exact RGB values under transparent area. Otherwise, discard this invisible
    /// RGB information for better compression.
    /// </summary>
    private readonly WebpTransparentColorMode transparentColorMode;

    /// <summary>
    /// Whether to skip metadata during encoding.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// Indicating whether near lossless mode should be used.
    /// </summary>
    private readonly bool nearLossless;

    /// <summary>
    /// The near lossless quality. The range is 0 (maximum preprocessing) to 100 (no preprocessing, the default).
    /// </summary>
    private readonly int nearLosslessQuality;

    private const int ApplyPaletteGreedyMax = 4;

    private const int PaletteInvSizeBits = 11;

    private const int PaletteInvSize = 1 << PaletteInvSizeBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LEncoder"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="width">The width of the input image.</param>
    /// <param name="height">The height of the input image.</param>
    /// <param name="quality">The encoding quality.</param>
    /// <param name="skipMetadata">Whether to skip metadata encoding.</param>
    /// <param name="method">Quality/speed trade-off (0=fast, 6=slower-better).</param>
    /// <param name="transparentColorMode">Flag indicating whether to preserve the exact RGB values under transparent area.
    /// Otherwise, discard this invisible RGB information for better compression.</param>
    /// <param name="nearLossless">Indicating whether near lossless mode should be used.</param>
    /// <param name="nearLosslessQuality">The near lossless quality. The range is 0 (maximum preprocessing) to 100 (no preprocessing, the default).</param>
    public Vp8LEncoder(
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        int width,
        int height,
        uint quality,
        bool skipMetadata,
        WebpEncodingMethod method,
        WebpTransparentColorMode transparentColorMode,
        bool nearLossless,
        int nearLosslessQuality)
    {
        int pixelCount = width * height;
        int initialSize = pixelCount * 2;

        this.memoryAllocator = memoryAllocator;
        this.configuration = configuration;
        this.quality = Math.Min(quality, 100u);
        this.skipMetadata = skipMetadata;
        this.method = method;
        this.transparentColorMode = transparentColorMode;
        this.nearLossless = nearLossless;
        this.nearLosslessQuality = Numerics.Clamp(nearLosslessQuality, 0, 100);
        this.bitWriter = new Vp8LBitWriter(initialSize);
        this.Bgra = memoryAllocator.Allocate<uint>(pixelCount);
        this.EncodedData = memoryAllocator.Allocate<uint>(pixelCount);
        this.Palette = memoryAllocator.Allocate<uint>(WebpConstants.MaxPaletteSize);
        this.Refs = new Vp8LBackwardRefs[3];
        this.HashChain = new Vp8LHashChain(memoryAllocator, pixelCount);

        // We round the block size up, so we're guaranteed to have at most MaxRefsBlockPerImage blocks used:
        int refsBlockSize = ((pixelCount - 1) / MaxRefsBlockPerImage) + 1;
        for (int i = 0; i < this.Refs.Length; i++)
        {
            this.Refs[i] = new Vp8LBackwardRefs(pixelCount)
            {
                BlockSize = refsBlockSize < MinBlockSize ? MinBlockSize : refsBlockSize
            };
        }
    }

    // RFC 1951 will calm you down if you are worried about this funny sequence.
    // This sequence is tuned from that, but more weighted for lower symbol count,
    // and more spiking histograms.
    // This uses C#'s compiler optimization to refer to assembly's static data directly.
    private static ReadOnlySpan<byte> StorageOrder => new byte[] { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

    // This uses C#'s compiler optimization to refer to assembly's static data directly.
    private static ReadOnlySpan<byte> Order => new byte[] { 1, 2, 0, 3 };

    /// <summary>
    /// Gets the memory for the image data as packed bgra values.
    /// </summary>
    public IMemoryOwner<uint> Bgra { get; }

    /// <summary>
    /// Gets the memory for the encoded output image data.
    /// </summary>
    public IMemoryOwner<uint> EncodedData { get; }

    /// <summary>
    /// Gets or sets the scratch memory for bgra rows used for predictions.
    /// </summary>
    public IMemoryOwner<uint> BgraScratch { get; set; }

    /// <summary>
    /// Gets or sets the packed image width.
    /// </summary>
    public int CurrentWidth { get; set; }

    /// <summary>
    /// Gets or sets the huffman image bits.
    /// </summary>
    public int HistoBits { get; set; }

    /// <summary>
    /// Gets or sets the bits used for the transformation.
    /// </summary>
    public int TransformBits { get; set; }

    /// <summary>
    /// Gets or sets the transform data.
    /// </summary>
    public IMemoryOwner<uint> TransformData { get; set; }

    /// <summary>
    /// Gets or sets the cache bits. If equal to 0, don't use color cache.
    /// </summary>
    public int CacheBits { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the cross color transform.
    /// </summary>
    public bool UseCrossColorTransform { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the subtract green transform.
    /// </summary>
    public bool UseSubtractGreenTransform { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the predictor transform.
    /// </summary>
    public bool UsePredictorTransform { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use color indexing transform.
    /// </summary>
    public bool UsePalette { get; set; }

    /// <summary>
    /// Gets or sets the palette size.
    /// </summary>
    public int PaletteSize { get; set; }

    /// <summary>
    /// Gets the palette.
    /// </summary>
    public IMemoryOwner<uint> Palette { get; }

    /// <summary>
    /// Gets the backward references.
    /// </summary>
    public Vp8LBackwardRefs[] Refs { get; }

    /// <summary>
    /// Gets the hash chain.
    /// </summary>
    public Vp8LHashChain HashChain { get; }

    public WebpVp8X EncodeHeader<TPixel>(Image<TPixel> image, Stream stream, bool hasAnimation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Write bytes from the bit-writer buffer to the stream.
        ImageMetadata metadata = image.Metadata;
        metadata.SyncProfiles();

        ExifProfile exifProfile = this.skipMetadata ? null : metadata.ExifProfile;
        XmpProfile xmpProfile = this.skipMetadata ? null : metadata.XmpProfile;

        // The alpha flag is updated following encoding.
        WebpVp8X vp8x = BitWriterBase.WriteTrunksBeforeData(
            stream,
            (uint)image.Width,
            (uint)image.Height,
            exifProfile,
            xmpProfile,
            metadata.IccProfile,
            false,
            hasAnimation);

        if (hasAnimation)
        {
            WebpMetadata webpMetadata = WebpCommonUtils.GetWebpMetadata(image);
            BitWriterBase.WriteAnimationParameter(stream, webpMetadata.BackgroundColor, webpMetadata.RepeatCount);
        }

        return vp8x;
    }

    public void EncodeFooter<TPixel>(Image<TPixel> image, in WebpVp8X vp8x, bool hasAlpha, Stream stream, long initialPosition)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Write bytes from the bit-writer buffer to the stream.
        ImageMetadata metadata = image.Metadata;

        ExifProfile exifProfile = this.skipMetadata ? null : metadata.ExifProfile;
        XmpProfile xmpProfile = this.skipMetadata ? null : metadata.XmpProfile;

        bool updateVp8x = hasAlpha && vp8x != default;
        WebpVp8X updated = updateVp8x ? vp8x.WithAlpha(true) : vp8x;
        BitWriterBase.WriteTrunksAfterData(stream, in updated, updateVp8x, initialPosition, exifProfile, xmpProfile);
    }

    /// <summary>
    /// Encodes the image as lossless webp to the specified stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to encode from.</param>
    /// <param name="bounds">The region of interest within the frame to encode.</param>
    /// <param name="frameMetadata">The frame metadata.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="hasAnimation">Flag indicating, if an animation parameter is present.</param>
    /// <returns>A <see cref="bool"/> indicating whether the frame contains an alpha channel.</returns>
    public bool Encode<TPixel>(ImageFrame<TPixel> frame, Rectangle bounds, WebpFrameMetadata frameMetadata, Stream stream, bool hasAnimation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Convert image pixels to bgra array.
        bool hasAlpha = this.ConvertPixelsToBgra(frame.PixelBuffer.GetRegion(bounds));

        // Write the image size.
        this.WriteImageSize(bounds.Width, bounds.Height);

        // Write the non-trivial Alpha flag and lossless version.
        this.WriteAlphaAndVersion(hasAlpha);

        // Encode the main image stream.
        this.EncodeStream(bounds.Width, bounds.Height);

        this.bitWriter.Finish();

        long prevPosition = 0;

        if (hasAnimation)
        {
            prevPosition = new WebpFrameData(
                    (uint)bounds.Left,
                    (uint)bounds.Top,
                    (uint)bounds.Width,
                    (uint)bounds.Height,
                    frameMetadata.FrameDelay,
                    frameMetadata.BlendMethod,
                    frameMetadata.DisposalMethod)
                .WriteHeaderTo(stream);
        }

        // Write bytes from the bit-writer buffer to the stream.
        this.bitWriter.WriteEncodedImageToStream(stream);

        if (hasAnimation)
        {
            RiffHelper.EndWriteChunk(stream, prevPosition);
        }

        return hasAlpha;
    }

    /// <summary>
    /// Encodes the alpha image data using the webp lossless compression.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="frame">The alpha-pixel data to encode from.</param>
    /// <param name="alphaData">The destination buffer to write the encoded alpha data to.</param>
    /// <returns>The size of the compressed data in bytes.
    /// If the size of the data is the same as the pixel count, the compression would not yield in smaller data and is left uncompressed.
    /// </returns>
    public int EncodeAlphaImageData<TPixel>(Buffer2DRegion<TPixel> frame, IMemoryOwner<byte> alphaData)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        int pixelCount = width * height;

        // Convert image pixels to bgra array.
        this.ConvertPixelsToBgra(frame);

        // The image-stream will NOT contain any headers describing the image dimension, the dimension is already known.
        this.EncodeStream(width, height);
        this.bitWriter.Finish();
        int size = this.bitWriter.NumBytes;
        if (size >= pixelCount)
        {
            // Compressing would not yield in smaller data -> leave the data uncompressed.
            return pixelCount;
        }

        this.bitWriter.WriteToBuffer(alphaData.GetSpan());
        return size;
    }

    /// <summary>
    /// Writes the image size to the bit writer buffer.
    /// </summary>
    /// <param name="inputImgWidth">The input image width.</param>
    /// <param name="inputImgHeight">The input image height.</param>
    private void WriteImageSize(int inputImgWidth, int inputImgHeight)
    {
        uint width = (uint)inputImgWidth - 1;
        uint height = (uint)inputImgHeight - 1;

        this.bitWriter.PutBits(width, WebpConstants.Vp8LImageSizeBits);
        this.bitWriter.PutBits(height, WebpConstants.Vp8LImageSizeBits);
    }

    /// <summary>
    /// Writes a flag indicating if alpha channel is used and the VP8L version to the bit-writer buffer.
    /// </summary>
    /// <param name="hasAlpha">Indicates if a alpha channel is present.</param>
    private void WriteAlphaAndVersion(bool hasAlpha)
    {
        this.bitWriter.PutBits(hasAlpha ? 1U : 0, 1);
        this.bitWriter.PutBits(WebpConstants.Vp8LVersion, WebpConstants.Vp8LVersionBits);
    }

    /// <summary>
    /// Encodes the image stream using lossless webp format.
    /// </summary>
    /// <param name="width">The image frame width.</param>
    /// <param name="height">The image frame height.</param>
    private void EncodeStream(int width, int height)
    {
        Span<uint> bgra = this.Bgra.GetSpan();
        Span<uint> encodedData = this.EncodedData.GetSpan();
        bool lowEffort = this.method == 0;

        // Analyze image (entropy, numPalettes etc).
        CrunchConfig[] crunchConfigs = this.EncoderAnalyze(bgra, width, height, out bool redAndBlueAlwaysZero);

        int bestSize = 0;
        Vp8LBitWriter bitWriterInit = this.bitWriter;
        Vp8LBitWriter bitWriterBest = this.bitWriter.Clone();
        bool isFirstConfig = true;
        foreach (CrunchConfig crunchConfig in crunchConfigs)
        {
            bgra.CopyTo(encodedData);
            const bool useCache = true;
            this.UsePalette = crunchConfig.EntropyIdx is EntropyIx.Palette or EntropyIx.PaletteAndSpatial;
            this.UseSubtractGreenTransform = crunchConfig.EntropyIdx is EntropyIx.SubGreen or EntropyIx.SpatialSubGreen;
            this.UsePredictorTransform = crunchConfig.EntropyIdx is EntropyIx.Spatial or EntropyIx.SpatialSubGreen;
            if (lowEffort)
            {
                this.UseCrossColorTransform = false;
            }
            else
            {
                this.UseCrossColorTransform = !redAndBlueAlwaysZero && this.UsePredictorTransform;
            }

            this.AllocateTransformBuffer(width, height);

            // Reset any parameter in the encoder that is set in the previous iteration.
            this.CacheBits = 0;
            this.ClearRefs();

            if (this.nearLossless)
            {
                // Apply near-lossless preprocessing.
                bool useNearLossless = this.nearLosslessQuality < 100 && !this.UsePalette && !this.UsePredictorTransform;
                if (useNearLossless)
                {
                    this.AllocateTransformBuffer(width, height);
                    NearLosslessEnc.ApplyNearLossless(width, height, this.nearLosslessQuality, bgra, bgra, width);
                }
            }

            // Encode palette.
            if (this.UsePalette)
            {
                this.EncodePalette(lowEffort);
                this.MapImageFromPalette(width, height);

                // If using a color cache, do not have it bigger than the number of colors.
                if (useCache && this.PaletteSize < 1 << WebpConstants.MaxColorCacheBits)
                {
                    this.CacheBits = BitOperations.Log2((uint)this.PaletteSize) + 1;
                }
            }

            // Apply transforms and write transform data.
            if (this.UseSubtractGreenTransform)
            {
                this.ApplySubtractGreen();
            }

            if (this.UsePredictorTransform)
            {
                this.ApplyPredictFilter(this.CurrentWidth, height, lowEffort);
            }

            if (this.UseCrossColorTransform)
            {
                this.ApplyCrossColorFilter(this.CurrentWidth, height, lowEffort);
            }

            this.bitWriter.PutBits(0, 1); // No more transforms.

            // Encode and write the transformed image.
            this.EncodeImage(
                this.CurrentWidth,
                height,
                useCache,
                crunchConfig,
                this.CacheBits,
                lowEffort);

            // If we are better than what we already have.
            if (isFirstConfig || this.bitWriter.NumBytes < bestSize)
            {
                bestSize = this.bitWriter.NumBytes;
                BitWriterSwap(ref this.bitWriter, ref bitWriterBest);
            }

            // Reset the bit writer for the following iteration if any.
            if (crunchConfigs.Length > 1)
            {
                this.bitWriter.Reset(bitWriterInit);
            }

            isFirstConfig = false;
        }

        BitWriterSwap(ref bitWriterBest, ref this.bitWriter);
    }

    /// <summary>
    /// Converts the pixels of the image to bgra.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixels.</typeparam>
    /// <param name="pixels">The frame pixel buffer to convert.</param>
    /// <returns>true, if the image is non opaque.</returns>
    public bool ConvertPixelsToBgra<TPixel>(Buffer2DRegion<TPixel> pixels)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool nonOpaque = false;
        Span<uint> bgra = this.Bgra.GetSpan();
        Span<byte> bgraBytes = MemoryMarshal.Cast<uint, byte>(bgra);
        int widthBytes = pixels.Width * 4;
        for (int y = 0; y < pixels.Height; y++)
        {
            Span<TPixel> rowSpan = pixels.DangerousGetRowSpan(y);
            Span<byte> rowBytes = bgraBytes.Slice(y * widthBytes, widthBytes);
            PixelOperations<TPixel>.Instance.ToBgra32Bytes(this.configuration, rowSpan, rowBytes, pixels.Width);
            if (!nonOpaque)
            {
                Span<Bgra32> rowBgra = MemoryMarshal.Cast<byte, Bgra32>(rowBytes);
                nonOpaque = WebpCommonUtils.CheckNonOpaque(rowBgra);
            }
        }

        return nonOpaque;
    }

    /// <summary>
    /// Analyzes the image and decides which transforms should be used.
    /// </summary>
    /// <param name="bgra">The image as packed bgra values.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="redAndBlueAlwaysZero">Indicates if red and blue are always zero.</param>
    private CrunchConfig[] EncoderAnalyze(ReadOnlySpan<uint> bgra, int width, int height, out bool redAndBlueAlwaysZero)
    {
        // Check if we only deal with a small number of colors and should use a palette.
        bool usePalette = this.AnalyzeAndCreatePalette(bgra, width, height);

        // Empirical bit sizes.
        this.HistoBits = GetHistoBits(this.method, usePalette, width, height);
        this.TransformBits = GetTransformBits(this.method, this.HistoBits);

        // Try out multiple LZ77 on images with few colors.
        int nlz77s = this.PaletteSize is > 0 and <= 16 ? 2 : 1;
        EntropyIx entropyIdx = this.AnalyzeEntropy(bgra, width, height, usePalette, this.PaletteSize, this.TransformBits, out redAndBlueAlwaysZero);

        bool doNotCache = false;
        List<CrunchConfig> crunchConfigs = new();

        if (this.method == WebpEncodingMethod.BestQuality && this.quality == 100)
        {
            doNotCache = true;

            // Go brute force on all transforms.
            foreach (EntropyIx entropyIx in Enum.GetValues<EntropyIx>())
            {
                // We can only apply kPalette or kPaletteAndSpatial if we can indeed use a palette.
                if ((entropyIx != EntropyIx.Palette && entropyIx != EntropyIx.PaletteAndSpatial) || usePalette)
                {
                    crunchConfigs.Add(new CrunchConfig { EntropyIdx = entropyIx });
                }
            }
        }
        else
        {
            // Only choose the guessed best transform.
            crunchConfigs.Add(new CrunchConfig { EntropyIdx = entropyIdx });
            if (this.quality >= 75 && this.method == WebpEncodingMethod.Level5)
            {
                // Test with and without color cache.
                doNotCache = true;

                // If we have a palette, also check in combination with spatial.
                if (entropyIdx == EntropyIx.Palette)
                {
                    crunchConfigs.Add(new CrunchConfig { EntropyIdx = EntropyIx.PaletteAndSpatial });
                }
            }
        }

        // Fill in the different LZ77s.
        foreach (CrunchConfig crunchConfig in crunchConfigs)
        {
            for (int j = 0; j < nlz77s; j++)
            {
                crunchConfig.SubConfigs.Add(new CrunchSubConfig
                {
                    Lz77 = j == 0 ? (int)Vp8LLz77Type.Lz77Standard | (int)Vp8LLz77Type.Lz77Rle : (int)Vp8LLz77Type.Lz77Box,
                    DoNotCache = doNotCache
                });
            }
        }

        return crunchConfigs.ToArray();
    }

    private void EncodeImage(int width, int height, bool useCache, CrunchConfig config, int cacheBits, bool lowEffort)
    {
        // bgra data with transformations applied.
        Span<uint> bgra = this.EncodedData.GetSpan();
        int histogramImageXySize = LosslessUtils.SubSampleSize(width, this.HistoBits) * LosslessUtils.SubSampleSize(height, this.HistoBits);
        Span<ushort> histogramSymbols = histogramImageXySize <= 64 ? stackalloc ushort[histogramImageXySize] : new ushort[histogramImageXySize];
        Span<HuffmanTree> huffTree = stackalloc HuffmanTree[3 * WebpConstants.CodeLengthCodes];

        if (useCache)
        {
            if (cacheBits == 0)
            {
                cacheBits = WebpConstants.MaxColorCacheBits;
            }
        }
        else
        {
            cacheBits = 0;
        }

        // Calculate backward references from BGRA image.
        this.HashChain.Fill(bgra, this.quality, width, height, lowEffort);

        Vp8LBitWriter bitWriterBest = config.SubConfigs.Count > 1 ? this.bitWriter.Clone() : this.bitWriter;
        Vp8LBitWriter bwInit = this.bitWriter;
        bool isFirstIteration = true;
        foreach (CrunchSubConfig subConfig in config.SubConfigs)
        {
            Vp8LBackwardRefs refsBest = BackwardReferenceEncoder.GetBackwardReferences(
                width,
                height,
                bgra,
                this.quality,
                subConfig.Lz77,
                ref cacheBits,
                this.memoryAllocator,
                this.HashChain,
                this.Refs[0],
                this.Refs[1]);

            // Keep the best references aside and use the other element from the first
            // two as a temporary for later usage.
            Vp8LBackwardRefs refsTmp = this.Refs[refsBest.Equals(this.Refs[0]) ? 1 : 0];

            this.bitWriter.Reset(bwInit);
            using OwnedVp8LHistogram tmpHisto = OwnedVp8LHistogram.Create(this.memoryAllocator, cacheBits);
            using Vp8LHistogramSet histogramImage = new(this.memoryAllocator, histogramImageXySize, cacheBits);

            // Build histogram image and symbols from backward references.
            HistogramEncoder.GetHistoImageSymbols(
                this.memoryAllocator,
                width,
                height,
                refsBest,
                this.quality,
                this.HistoBits,
                cacheBits,
                histogramImage,
                tmpHisto,
                histogramSymbols);

            // Create Huffman bit lengths and codes for each histogram image.
            int histogramImageSize = histogramImage.Count;
            int bitArraySize = 5 * histogramImageSize;
            HuffmanTreeCode[] huffmanCodes = new HuffmanTreeCode[bitArraySize];

            GetHuffBitLengthsAndCodes(histogramImage, huffmanCodes);

            // Color Cache parameters.
            if (cacheBits > 0)
            {
                this.bitWriter.PutBits(1, 1);
                this.bitWriter.PutBits((uint)cacheBits, 4);
            }
            else
            {
                this.bitWriter.PutBits(0, 1);
            }

            // Huffman image + meta huffman.
            bool writeHistogramImage = histogramImageSize > 1;
            this.bitWriter.PutBits((uint)(writeHistogramImage ? 1 : 0), 1);
            if (writeHistogramImage)
            {
                using IMemoryOwner<uint> histogramBgraBuffer = this.memoryAllocator.Allocate<uint>(histogramImageXySize);
                Span<uint> histogramBgra = histogramBgraBuffer.GetSpan();
                int maxIndex = 0;
                for (int i = 0; i < histogramImageXySize; i++)
                {
                    int symbolIndex = histogramSymbols[i] & 0xffff;
                    histogramBgra[i] = (uint)(symbolIndex << 8);
                    if (symbolIndex >= maxIndex)
                    {
                        maxIndex = symbolIndex + 1;
                    }
                }

                histogramImageSize = maxIndex;

                this.bitWriter.PutBits((uint)(this.HistoBits - 2), 3);
                this.EncodeImageNoHuffman(
                    histogramBgra,
                    this.HashChain,
                    refsTmp,
                    this.Refs[2],
                    LosslessUtils.SubSampleSize(width, this.HistoBits),
                    LosslessUtils.SubSampleSize(height, this.HistoBits),
                    this.quality,
                    lowEffort);
            }

            // Store Huffman codes.
            // Find maximum number of symbols for the huffman tree-set.
            int maxTokens = 0;
            for (int i = 0; i < 5 * histogramImageSize; i++)
            {
                HuffmanTreeCode codes = huffmanCodes[i];
                if (maxTokens < codes.NumSymbols)
                {
                    maxTokens = codes.NumSymbols;
                }
            }

            HuffmanTreeToken[] tokens = new HuffmanTreeToken[maxTokens];
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = new HuffmanTreeToken();
            }

            for (int i = 0; i < 5 * histogramImageSize; i++)
            {
                HuffmanTreeCode codes = huffmanCodes[i];
                this.StoreHuffmanCode(huffTree, tokens, codes);
                ClearHuffmanTreeIfOnlyOneSymbol(codes);
            }

            // Store actual literals.
            this.StoreImageToBitMask(width, this.HistoBits, refsBest, histogramSymbols, huffmanCodes);

            // Keep track of the smallest image so far.
            if (isFirstIteration || (bitWriterBest != null && this.bitWriter.NumBytes < bitWriterBest.NumBytes))
            {
                (bitWriterBest, this.bitWriter) = (this.bitWriter, bitWriterBest);
            }

            isFirstIteration = false;
        }

        this.bitWriter = bitWriterBest;
    }

    /// <summary>
    /// Save the palette to the bitstream.
    /// </summary>
    private void EncodePalette(bool lowEffort)
    {
        Span<uint> tmpPalette = stackalloc uint[WebpConstants.MaxPaletteSize];
        int paletteSize = this.PaletteSize;
        Span<uint> palette = this.Palette.Memory.Span;
        this.bitWriter.PutBits(WebpConstants.TransformPresent, 1);
        this.bitWriter.PutBits((uint)Vp8LTransformType.ColorIndexingTransform, 2);
        this.bitWriter.PutBits((uint)paletteSize - 1, 8);
        for (int i = paletteSize - 1; i >= 1; i--)
        {
            tmpPalette[i] = LosslessUtils.SubPixels(palette[i], palette[i - 1]);
        }

        tmpPalette[0] = palette[0];
        this.EncodeImageNoHuffman(tmpPalette, this.HashChain, this.Refs[0], this.Refs[1], width: paletteSize, height: 1, quality: 20, lowEffort);
    }

    /// <summary>
    /// Applies the subtract green transformation to the pixel data of the image.
    /// </summary>
    private void ApplySubtractGreen()
    {
        this.bitWriter.PutBits(WebpConstants.TransformPresent, 1);
        this.bitWriter.PutBits((uint)Vp8LTransformType.SubtractGreen, 2);
        LosslessUtils.SubtractGreenFromBlueAndRed(this.EncodedData.GetSpan());
    }

    private void ApplyPredictFilter(int width, int height, bool lowEffort)
    {
        // We disable near-lossless quantization if palette is used.
        int nearLosslessStrength = this.UsePalette ? 100 : this.nearLosslessQuality;
        int predBits = this.TransformBits;
        int transformWidth = LosslessUtils.SubSampleSize(width, predBits);
        int transformHeight = LosslessUtils.SubSampleSize(height, predBits);

        PredictorEncoder.ResidualImage(
            width,
            height,
            predBits,
            this.EncodedData.GetSpan(),
            this.BgraScratch.GetSpan(),
            this.TransformData.GetSpan(),
            this.histoArgb,
            this.bestHisto,
            this.nearLossless,
            nearLosslessStrength,
            this.transparentColorMode,
            this.UseSubtractGreenTransform,
            lowEffort);

        this.bitWriter.PutBits(WebpConstants.TransformPresent, 1);
        this.bitWriter.PutBits((uint)Vp8LTransformType.PredictorTransform, 2);
        this.bitWriter.PutBits((uint)(predBits - 2), 3);

        this.EncodeImageNoHuffman(this.TransformData.GetSpan(), this.HashChain, this.Refs[0], this.Refs[1], transformWidth, transformHeight, this.quality, lowEffort);
    }

    private void ApplyCrossColorFilter(int width, int height, bool lowEffort)
    {
        int colorTransformBits = this.TransformBits;
        int transformWidth = LosslessUtils.SubSampleSize(width, colorTransformBits);
        int transformHeight = LosslessUtils.SubSampleSize(height, colorTransformBits);

        PredictorEncoder.ColorSpaceTransform(width, height, colorTransformBits, this.quality, this.EncodedData.GetSpan(), this.TransformData.GetSpan(), this.scratch.Span);

        this.bitWriter.PutBits(WebpConstants.TransformPresent, 1);
        this.bitWriter.PutBits((uint)Vp8LTransformType.CrossColorTransform, 2);
        this.bitWriter.PutBits((uint)(colorTransformBits - 2), 3);

        this.EncodeImageNoHuffman(this.TransformData.GetSpan(), this.HashChain, this.Refs[0], this.Refs[1], transformWidth, transformHeight, this.quality, lowEffort);
    }

    private void EncodeImageNoHuffman(Span<uint> bgra, Vp8LHashChain hashChain, Vp8LBackwardRefs refsTmp1, Vp8LBackwardRefs refsTmp2, int width, int height, uint quality, bool lowEffort)
    {
        int cacheBits = 0;
        ushort[] histogramSymbols = new ushort[1]; // Only one tree, one symbol.

        HuffmanTreeCode[] huffmanCodes = new HuffmanTreeCode[5];
        Span<HuffmanTree> huffTree = stackalloc HuffmanTree[3 * WebpConstants.CodeLengthCodes];

        // Calculate backward references from the image pixels.
        hashChain.Fill(bgra, quality, width, height, lowEffort);

        Vp8LBackwardRefs refs = BackwardReferenceEncoder.GetBackwardReferences(
            width,
            height,
            bgra,
            quality,
            (int)Vp8LLz77Type.Lz77Standard | (int)Vp8LLz77Type.Lz77Rle,
            ref cacheBits,
            this.memoryAllocator,
            hashChain,
            refsTmp1,
            refsTmp2);

        // Build histogram image and symbols from backward references.
        using Vp8LHistogramSet histogramImage = new(this.memoryAllocator, refs, 1, cacheBits);

        // Create Huffman bit lengths and codes for each histogram image.
        GetHuffBitLengthsAndCodes(histogramImage, huffmanCodes);

        // No color cache, no Huffman image.
        this.bitWriter.PutBits(0, 1);

        // Find maximum number of symbols for the huffman tree-set.
        int maxTokens = 0;
        for (int i = 0; i < 5; i++)
        {
            HuffmanTreeCode codes = huffmanCodes[i];
            if (maxTokens < codes.NumSymbols)
            {
                maxTokens = codes.NumSymbols;
            }
        }

        HuffmanTreeToken[] tokens = new HuffmanTreeToken[maxTokens];
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i] = new HuffmanTreeToken();
        }

        // Store Huffman codes.
        for (int i = 0; i < 5; i++)
        {
            HuffmanTreeCode codes = huffmanCodes[i];
            this.StoreHuffmanCode(huffTree, tokens, codes);
            ClearHuffmanTreeIfOnlyOneSymbol(codes);
        }

        // Store actual literals.
        this.StoreImageToBitMask(width, 0, refs, histogramSymbols, huffmanCodes);
    }

    private void StoreHuffmanCode(Span<HuffmanTree> huffTree, HuffmanTreeToken[] tokens, HuffmanTreeCode huffmanCode)
    {
        int count = 0;
        Span<int> symbols = this.scratch.Span[..2];
        symbols.Clear();
        const int maxBits = 8;
        const int maxSymbol = 1 << maxBits;

        // Check whether it's a small tree.
        for (int i = 0; i < huffmanCode.NumSymbols && count < 3; i++)
        {
            if (huffmanCode.CodeLengths[i] != 0)
            {
                if (count < 2)
                {
                    symbols[count] = i;
                }

                count++;
            }
        }

        if (count == 0)
        {
            // Emit minimal tree for empty cases.
            // bits: small tree marker: 1, count-1: 0, large 8-bit code: 0, code: 0
            this.bitWriter.PutBits(0x01, 4);
        }
        else if (count <= 2 && symbols[0] < maxSymbol && symbols[1] < maxSymbol)
        {
            this.bitWriter.PutBits(1, 1);  // Small tree marker to encode 1 or 2 symbols.
            this.bitWriter.PutBits((uint)(count - 1), 1);
            if (symbols[0] <= 1)
            {
                this.bitWriter.PutBits(0, 1);  // Code bit for small (1 bit) symbol value.
                this.bitWriter.PutBits((uint)symbols[0], 1);
            }
            else
            {
                this.bitWriter.PutBits(1, 1);
                this.bitWriter.PutBits((uint)symbols[0], 8);
            }

            if (count == 2)
            {
                this.bitWriter.PutBits((uint)symbols[1], 8);
            }
        }
        else
        {
            this.StoreFullHuffmanCode(huffTree, tokens, huffmanCode);
        }
    }

    private void StoreFullHuffmanCode(Span<HuffmanTree> huffTree, HuffmanTreeToken[] tokens, HuffmanTreeCode tree)
    {
        // TODO: Allocations. This method is called in a loop.
        int i;
        byte[] codeLengthBitDepth = new byte[WebpConstants.CodeLengthCodes];
        short[] codeLengthBitDepthSymbols = new short[WebpConstants.CodeLengthCodes];
        HuffmanTreeCode huffmanCode = new()
        {
            NumSymbols = WebpConstants.CodeLengthCodes,
            CodeLengths = codeLengthBitDepth,
            Codes = codeLengthBitDepthSymbols
        };

        this.bitWriter.PutBits(0, 1);
        int numTokens = HuffmanUtils.CreateCompressedHuffmanTree(tree, tokens);
        uint[] histogram = new uint[WebpConstants.CodeLengthCodes + 1];
        bool[] bufRle = new bool[WebpConstants.CodeLengthCodes + 1];
        for (i = 0; i < numTokens; i++)
        {
            histogram[tokens[i].Code]++;
        }

        HuffmanUtils.CreateHuffmanTree(histogram, 7, bufRle, huffTree, huffmanCode);
        this.StoreHuffmanTreeOfHuffmanTreeToBitMask(codeLengthBitDepth);
        ClearHuffmanTreeIfOnlyOneSymbol(huffmanCode);

        int trailingZeroBits = 0;
        int trimmedLength = numTokens;
        i = numTokens;
        while (i-- > 0)
        {
            int ix = tokens[i].Code;
            if (ix is 0 or 17 or 18)
            {
                trimmedLength--;   // Discount trailing zeros.
                trailingZeroBits += codeLengthBitDepth[ix];
                if (ix == 17)
                {
                    trailingZeroBits += 3;
                }
                else if (ix == 18)
                {
                    trailingZeroBits += 7;
                }
            }
            else
            {
                break;
            }
        }

        bool writeTrimmedLength = trimmedLength > 1 && trailingZeroBits > 12;
        int length = writeTrimmedLength ? trimmedLength : numTokens;
        this.bitWriter.PutBits((uint)(writeTrimmedLength ? 1 : 0), 1);
        if (writeTrimmedLength)
        {
            if (trimmedLength == 2)
            {
                this.bitWriter.PutBits(0, 3 + 2); // nbitpairs=1, trimmedLength=2
            }
            else
            {
                int nBits = BitOperations.Log2((uint)trimmedLength - 2);
                int nBitPairs = (int)(((uint)nBits / 2) + 1);
                this.bitWriter.PutBits((uint)nBitPairs - 1, 3);
                this.bitWriter.PutBits((uint)trimmedLength - 2, nBitPairs * 2);
            }
        }

        this.StoreHuffmanTreeToBitMask(tokens, length, huffmanCode);
    }

    private void StoreHuffmanTreeToBitMask(HuffmanTreeToken[] tokens, int numTokens, HuffmanTreeCode huffmanCode)
    {
        for (int i = 0; i < numTokens; i++)
        {
            int ix = tokens[i].Code;
            int extraBits = tokens[i].ExtraBits;
            this.bitWriter.PutBits((uint)huffmanCode.Codes[ix], huffmanCode.CodeLengths[ix]);
            switch (ix)
            {
                case 16:
                    this.bitWriter.PutBits((uint)extraBits, 2);
                    break;
                case 17:
                    this.bitWriter.PutBits((uint)extraBits, 3);
                    break;
                case 18:
                    this.bitWriter.PutBits((uint)extraBits, 7);
                    break;
            }
        }
    }

    private void StoreHuffmanTreeOfHuffmanTreeToBitMask(byte[] codeLengthBitDepth)
    {
        // Throw away trailing zeros:
        int codesToStore = WebpConstants.CodeLengthCodes;
        for (; codesToStore > 4; codesToStore--)
        {
            if (codeLengthBitDepth[StorageOrder[codesToStore - 1]] != 0)
            {
                break;
            }
        }

        this.bitWriter.PutBits((uint)codesToStore - 4, 4);
        for (int i = 0; i < codesToStore; i++)
        {
            this.bitWriter.PutBits(codeLengthBitDepth[StorageOrder[i]], 3);
        }
    }

    private void StoreImageToBitMask(
        int width,
        int histoBits,
        Vp8LBackwardRefs backwardRefs,
        Span<ushort> histogramSymbols,
        HuffmanTreeCode[] huffmanCodes)
    {
        int histoXSize = histoBits > 0 ? LosslessUtils.SubSampleSize(width, histoBits) : 1;
        int tileMask = histoBits == 0 ? 0 : -(1 << histoBits);

        // x and y trace the position in the image.
        int x = 0;
        int y = 0;
        int tileX = x & tileMask;
        int tileY = y & tileMask;
        int histogramIx = histogramSymbols[0];
        Span<HuffmanTreeCode> codes = huffmanCodes.AsSpan(5 * histogramIx);

        for (int i = 0; i < backwardRefs.Refs.Count; i++)
        {
            PixOrCopy v = backwardRefs.Refs[i];
            if (tileX != (x & tileMask) || tileY != (y & tileMask))
            {
                tileX = x & tileMask;
                tileY = y & tileMask;
                histogramIx = histogramSymbols[((y >> histoBits) * histoXSize) + (x >> histoBits)];
                codes = huffmanCodes.AsSpan(5 * histogramIx);
            }

            if (v.IsLiteral())
            {
                for (int k = 0; k < 4; k++)
                {
                    int code = v.Literal(Order[k]);
                    this.bitWriter.WriteHuffmanCode(codes[k], code);
                }
            }
            else if (v.IsCacheIdx())
            {
                int code = (int)v.CacheIdx();
                int literalIx = 256 + WebpConstants.NumLengthCodes + code;
                this.bitWriter.WriteHuffmanCode(codes[0], literalIx);
            }
            else
            {
                int bits = 0;
                int nBits = 0;
                int distance = (int)v.Distance();
                int code = LosslessUtils.PrefixEncode(v.Len, ref nBits, ref bits);
                this.bitWriter.WriteHuffmanCodeWithExtraBits(codes[0], 256 + code, bits, nBits);

                // Don't write the distance with the extra bits code since
                // the distance can be up to 18 bits of extra bits, and the prefix
                // 15 bits, totaling to 33, and our PutBits only supports up to 32 bits.
                code = LosslessUtils.PrefixEncode(distance, ref nBits, ref bits);
                this.bitWriter.WriteHuffmanCode(codes[4], code);
                this.bitWriter.PutBits((uint)bits, nBits);
            }

            x += v.Length();
            while (x >= width)
            {
                x -= width;
                y++;
            }
        }
    }

    /// <summary>
    /// Analyzes the entropy of the input image to determine which transforms to use during encoding the image.
    /// </summary>
    /// <param name="bgra">The image to analyze as a bgra span.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="usePalette">Indicates whether a palette should be used.</param>
    /// <param name="paletteSize">The palette size.</param>
    /// <param name="transformBits">The transformation bits.</param>
    /// <param name="redAndBlueAlwaysZero">Indicates if red and blue are always zero.</param>
    /// <returns>The entropy mode to use.</returns>
    private EntropyIx AnalyzeEntropy(ReadOnlySpan<uint> bgra, int width, int height, bool usePalette, int paletteSize, int transformBits, out bool redAndBlueAlwaysZero)
    {
        if (usePalette && paletteSize <= 16)
        {
            // In the case of small palettes, we pack 2, 4 or 8 pixels together. In
            // practice, small palettes are better than any other transform.
            redAndBlueAlwaysZero = true;
            return EntropyIx.Palette;
        }

        using IMemoryOwner<uint> histoBuffer = this.memoryAllocator.Allocate<uint>((int)HistoIx.HistoTotal * 256, AllocationOptions.Clean);
        Span<uint> histo = histoBuffer.Memory.Span;
        uint pixPrev = bgra[0]; // Skip the first pixel.
        ReadOnlySpan<uint> prevRow = null;
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<uint> currentRow = bgra.Slice(y * width, width);
            for (int x = 0; x < width; x++)
            {
                uint pix = currentRow[x];
                uint pixDiff = LosslessUtils.SubPixels(pix, pixPrev);
                pixPrev = pix;
                if (pixDiff == 0 || (prevRow.Length > 0 && pix == prevRow[x]))
                {
                    continue;
                }

                AddSingle(
                    pix,
                    histo[..],
                    histo[((int)HistoIx.HistoRed * 256)..],
                    histo[((int)HistoIx.HistoGreen * 256)..],
                    histo[((int)HistoIx.HistoBlue * 256)..]);
                AddSingle(
                    pixDiff,
                    histo[((int)HistoIx.HistoAlphaPred * 256)..],
                    histo[((int)HistoIx.HistoRedPred * 256)..],
                    histo[((int)HistoIx.HistoGreenPred * 256)..],
                    histo[((int)HistoIx.HistoBluePred * 256)..]);
                AddSingleSubGreen(
                    pix,
                    histo[((int)HistoIx.HistoRedSubGreen * 256)..],
                    histo[((int)HistoIx.HistoBlueSubGreen * 256)..]);
                AddSingleSubGreen(
                    pixDiff,
                    histo[((int)HistoIx.HistoRedPredSubGreen * 256)..],
                    histo[((int)HistoIx.HistoBluePredSubGreen * 256)..]);

                // Approximate the palette by the entropy of the multiplicative hash.
                uint hash = HashPix(pix);
                histo[((int)HistoIx.HistoPalette * 256) + (int)hash]++;
            }

            prevRow = currentRow;
        }

        Span<double> entropyComp = stackalloc double[(int)HistoIx.HistoTotal];
        Span<double> entropy = stackalloc double[(int)EntropyIx.NumEntropyIx];
        int lastModeToAnalyze = usePalette ? (int)EntropyIx.Palette : (int)EntropyIx.SpatialSubGreen;

        // Let's add one zero to the predicted histograms. The zeros are removed
        // too efficiently by the pixDiff == 0 comparison, at least one of the
        // zeros is likely to exist.
        histo[(int)HistoIx.HistoRedPredSubGreen * 256]++;
        histo[(int)HistoIx.HistoBluePredSubGreen * 256]++;
        histo[(int)HistoIx.HistoRedPred * 256]++;
        histo[(int)HistoIx.HistoGreenPred * 256]++;
        histo[(int)HistoIx.HistoBluePred * 256]++;
        histo[(int)HistoIx.HistoAlphaPred * 256]++;

        Vp8LBitEntropy bitEntropy = new();
        for (int j = 0; j < (int)HistoIx.HistoTotal; j++)
        {
            bitEntropy.Init();
            Span<uint> curHisto = histo.Slice(j * 256, 256);
            bitEntropy.BitsEntropyUnrefined(curHisto, 256);
            entropyComp[j] = bitEntropy.BitsEntropyRefine();
        }

        entropy[(int)EntropyIx.Direct] =
            entropyComp[(int)HistoIx.HistoAlpha] +
            entropyComp[(int)HistoIx.HistoRed] +
            entropyComp[(int)HistoIx.HistoGreen] +
            entropyComp[(int)HistoIx.HistoBlue];
        entropy[(int)EntropyIx.Spatial] =
            entropyComp[(int)HistoIx.HistoAlphaPred] +
            entropyComp[(int)HistoIx.HistoRedPred] +
            entropyComp[(int)HistoIx.HistoGreenPred] +
            entropyComp[(int)HistoIx.HistoBluePred];
        entropy[(int)EntropyIx.SubGreen] =
            entropyComp[(int)HistoIx.HistoAlpha] +
            entropyComp[(int)HistoIx.HistoRedSubGreen] +
            entropyComp[(int)HistoIx.HistoGreen] +
            entropyComp[(int)HistoIx.HistoBlueSubGreen];
        entropy[(int)EntropyIx.SpatialSubGreen] =
            entropyComp[(int)HistoIx.HistoAlphaPred] +
            entropyComp[(int)HistoIx.HistoRedPredSubGreen] +
            entropyComp[(int)HistoIx.HistoGreenPred] +
            entropyComp[(int)HistoIx.HistoBluePredSubGreen];
        entropy[(int)EntropyIx.Palette] = entropyComp[(int)HistoIx.HistoPalette];

        // When including transforms, there is an overhead in bits from
        // storing them. This overhead is small but matters for small images.
        // For spatial, there are 14 transformations.
        entropy[(int)EntropyIx.Spatial] +=
            LosslessUtils.SubSampleSize(width, transformBits) *
            LosslessUtils.SubSampleSize(height, transformBits) *
            LosslessUtils.FastLog2(14);

        // For color transforms: 24 as only 3 channels are considered in a ColorTransformElement.
        entropy[(int)EntropyIx.SpatialSubGreen] +=
            LosslessUtils.SubSampleSize(width, transformBits) *
            LosslessUtils.SubSampleSize(height, transformBits) *
            LosslessUtils.FastLog2(24);

        // For palettes, add the cost of storing the palette.
        // We empirically estimate the cost of a compressed entry as 8 bits.
        // The palette is differential-coded when compressed hence a much
        // lower cost than sizeof(uint32_t)*8.
        entropy[(int)EntropyIx.Palette] += paletteSize * 8;

        EntropyIx minEntropyIx = EntropyIx.Direct;
        for (int k = (int)EntropyIx.Direct + 1; k <= lastModeToAnalyze; k++)
        {
            if (entropy[(int)minEntropyIx] > entropy[k])
            {
                minEntropyIx = (EntropyIx)k;
            }
        }

        redAndBlueAlwaysZero = true;

        // Let's check if the histogram of the chosen entropy mode has
        // non-zero red and blue values. If all are zero, we can later skip
        // the cross color optimization.
        byte[][] histoPairs =
        {
            new[] { (byte)HistoIx.HistoRed, (byte)HistoIx.HistoBlue },
            new[] { (byte)HistoIx.HistoRedPred, (byte)HistoIx.HistoBluePred },
            new[] { (byte)HistoIx.HistoRedSubGreen, (byte)HistoIx.HistoBlueSubGreen },
            new[] { (byte)HistoIx.HistoRedPredSubGreen, (byte)HistoIx.HistoBluePredSubGreen },
            new[] { (byte)HistoIx.HistoRed, (byte)HistoIx.HistoBlue }
        };
        Span<uint> redHisto = histo[(256 * histoPairs[(int)minEntropyIx][0])..];
        Span<uint> blueHisto = histo[(256 * histoPairs[(int)minEntropyIx][1])..];
        for (int i = 1; i < 256; i++)
        {
            if ((redHisto[i] | blueHisto[i]) != 0)
            {
                redAndBlueAlwaysZero = false;
                break;
            }
        }

        return minEntropyIx;
    }

    /// <summary>
    /// If number of colors in the image is less than or equal to MaxPaletteSize,
    /// creates a palette and returns true, else returns false.
    /// </summary>
    /// <param name="bgra">The image as packed bgra values.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>true, if a palette should be used.</returns>
    private bool AnalyzeAndCreatePalette(ReadOnlySpan<uint> bgra, int width, int height)
    {
        Span<uint> palette = this.Palette.Memory.Span;
        this.PaletteSize = GetColorPalette(bgra, width, height, palette);
        if (this.PaletteSize > WebpConstants.MaxPaletteSize)
        {
            this.PaletteSize = 0;
            return false;
        }

        Span<uint> paletteSlice = palette[..this.PaletteSize];
        paletteSlice.Sort();

        if (PaletteHasNonMonotonousDeltas(palette, this.PaletteSize))
        {
            GreedyMinimizeDeltas(palette, this.PaletteSize);
        }

        return true;
    }

    /// <summary>
    /// Gets the color palette.
    /// </summary>
    /// <param name="bgra">The image to get the palette from as packed bgra values.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="palette">The span to store the palette into.</param>
    /// <returns>The number of palette entries.</returns>
    private static int GetColorPalette(ReadOnlySpan<uint> bgra, int width, int height, Span<uint> palette)
    {
        HashSet<uint> colors = new();
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<uint> bgraRow = bgra.Slice(y * width, width);
            for (int x = 0; x < width; x++)
            {
                colors.Add(bgraRow[x]);
                if (colors.Count > WebpConstants.MaxPaletteSize)
                {
                    // Exact count is not needed, because a palette will not be used then anyway.
                    return WebpConstants.MaxPaletteSize + 1;
                }
            }
        }

        // Fill the colors into the palette.
        using HashSet<uint>.Enumerator colorEnumerator = colors.GetEnumerator();
        int idx = 0;
        while (colorEnumerator.MoveNext())
        {
            palette[idx++] = colorEnumerator.Current;
        }

        return colors.Count;
    }

    private void MapImageFromPalette(int width, int height)
    {
        Span<uint> src = this.EncodedData.GetSpan();
        int srcStride = this.CurrentWidth;
        Span<uint> dst = this.EncodedData.GetSpan(); // Applying the palette will be done in place.
        Span<uint> palette = this.Palette.GetSpan();
        int paletteSize = this.PaletteSize;
        int xBits;

        // Replace each input pixel by corresponding palette index.
        // This is done line by line.
        if (paletteSize <= 4)
        {
            xBits = paletteSize <= 2 ? 3 : 2;
        }
        else
        {
            xBits = paletteSize <= 16 ? 1 : 0;
        }

        this.CurrentWidth = LosslessUtils.SubSampleSize(width, xBits);
        this.ApplyPalette(src, srcStride, dst, this.CurrentWidth, palette, paletteSize, width, height, xBits);
    }

    /// <summary>
    /// Remap bgra values in src[] to packed palettes entries in dst[]
    /// using 'row' as a temporary buffer of size 'width'.
    /// We assume that all src[] values have a corresponding entry in the palette.
    /// Note: src[] can be the same as dst[]
    /// </summary>
    private void ApplyPalette(Span<uint> src, int srcStride, Span<uint> dst, int dstStride, Span<uint> palette, int paletteSize, int width, int height, int xBits)
    {
        using IMemoryOwner<byte> tmpRowBuffer = this.memoryAllocator.Allocate<byte>(width);
        Span<byte> tmpRow = tmpRowBuffer.GetSpan();

        if (paletteSize < ApplyPaletteGreedyMax)
        {
            uint prevPix = palette[0];
            uint prevIdx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint pix = src[x];
                    if (pix != prevPix)
                    {
                        prevIdx = SearchColorGreedy(palette, pix);
                        prevPix = pix;
                    }

                    tmpRow[x] = (byte)prevIdx;
                }

                BundleColorMap(tmpRow, width, xBits, dst);
                src = src[srcStride..];
                dst = dst[dstStride..];
            }
        }
        else
        {
            uint[] buffer = new uint[PaletteInvSize];

            // Try to find a perfect hash function able to go from a color to an index
            // within 1 << PaletteInvSize in order to build a hash map to go from color to index in palette.
            int i;
            for (i = 0; i < 3; i++)
            {
                bool useLut = true;

                // Set each element in buffer to max value.
                buffer.AsSpan().Fill(uint.MaxValue);

                for (int j = 0; j < paletteSize; j++)
                {
                    uint ind = 0;
                    switch (i)
                    {
                        case 0:
                            ind = ApplyPaletteHash0(palette[j]);
                            break;
                        case 1:
                            ind = ApplyPaletteHash1(palette[j]);
                            break;
                        case 2:
                            ind = ApplyPaletteHash2(palette[j]);
                            break;
                    }

                    if (buffer[ind] != uint.MaxValue)
                    {
                        useLut = false;
                        break;
                    }

                    buffer[ind] = (uint)j;
                }

                if (useLut)
                {
                    break;
                }
            }

            if (i is 0 or 1 or 2)
            {
                ApplyPaletteFor(width, height, palette, i, src, srcStride, dst, dstStride, tmpRow, buffer, xBits);
            }
            else
            {
                uint[] idxMap = new uint[paletteSize];
                uint[] paletteSorted = new uint[paletteSize];
                PrepareMapToPalette(palette, paletteSize, paletteSorted, idxMap);
                ApplyPaletteForWithIdxMap(width, height, palette, src, srcStride, dst, dstStride, tmpRow, idxMap, xBits, paletteSorted, paletteSize);
            }
        }
    }

    private static void ApplyPaletteFor(int width, int height, Span<uint> palette, int hashIdx, Span<uint> src, int srcStride, Span<uint> dst, int dstStride, Span<byte> tmpRow, uint[] buffer, int xBits)
    {
        uint prevPix = palette[0];
        uint prevIdx = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                uint pix = src[x];
                if (pix != prevPix)
                {
                    switch (hashIdx)
                    {
                        case 0:
                            prevIdx = buffer[ApplyPaletteHash0(pix)];
                            break;
                        case 1:
                            prevIdx = buffer[ApplyPaletteHash1(pix)];
                            break;
                        case 2:
                            prevIdx = buffer[ApplyPaletteHash2(pix)];
                            break;
                    }

                    prevPix = pix;
                }

                tmpRow[x] = (byte)prevIdx;
            }

            LosslessUtils.BundleColorMap(tmpRow, width, xBits, dst);

            src = src[srcStride..];
            dst = dst[dstStride..];
        }
    }

    private static void ApplyPaletteForWithIdxMap(int width, int height, Span<uint> palette, Span<uint> src, int srcStride, Span<uint> dst, int dstStride, Span<byte> tmpRow, uint[] idxMap, int xBits, uint[] paletteSorted, int paletteSize)
    {
        uint prevPix = palette[0];
        uint prevIdx = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                uint pix = src[x];
                if (pix != prevPix)
                {
                    prevIdx = idxMap[SearchColorNoIdx(paletteSorted, pix, paletteSize)];
                    prevPix = pix;
                }

                tmpRow[x] = (byte)prevIdx;
            }

            LosslessUtils.BundleColorMap(tmpRow, width, xBits, dst);

            src = src[srcStride..];
            dst = dst[dstStride..];
        }
    }

    /// <summary>
    /// Sort palette in increasing order and prepare an inverse mapping array.
    /// </summary>
    private static void PrepareMapToPalette(Span<uint> palette, int numColors, uint[] sorted, uint[] idxMap)
    {
        palette[..numColors].CopyTo(sorted);
        Array.Sort(sorted, PaletteCompareColorsForSort);
        for (int i = 0; i < numColors; i++)
        {
            idxMap[SearchColorNoIdx(sorted, palette[i], numColors)] = (uint)i;
        }
    }

    private static int SearchColorNoIdx(uint[] sorted, uint color, int hi)
    {
        int low = 0;
        if (sorted[low] == color)
        {
            return low;  // loop invariant: sorted[low] != color
        }

        while (true)
        {
            int mid = (low + hi) >> 1;
            if (sorted[mid] == color)
            {
                return mid;
            }

            if (sorted[mid] < color)
            {
                low = mid;
            }
            else
            {
                hi = mid;
            }
        }
    }

    private static void ClearHuffmanTreeIfOnlyOneSymbol(HuffmanTreeCode huffmanCode)
    {
        int count = 0;
        for (int k = 0; k < huffmanCode.NumSymbols; k++)
        {
            if (huffmanCode.CodeLengths[k] != 0)
            {
                count++;
                if (count > 1)
                {
                    return;
                }
            }
        }

        for (int k = 0; k < huffmanCode.NumSymbols; k++)
        {
            huffmanCode.CodeLengths[k] = 0;
            huffmanCode.Codes[k] = 0;
        }
    }

    /// <summary>
    /// The palette has been sorted by alpha. This function checks if the other components of the palette
    /// have a monotonic development with regards to position in the palette.
    /// If all have monotonic development, there is no benefit to re-organize them greedily. A monotonic development
    /// would be spotted in green-only situations (like lossy alpha) or gray-scale images.
    /// </summary>
    /// <param name="palette">The palette.</param>
    /// <param name="numColors">Number of colors in the palette.</param>
    /// <returns>True, if the palette has no monotonous deltas.</returns>
    private static bool PaletteHasNonMonotonousDeltas(Span<uint> palette, int numColors)
    {
        const uint predict = 0x000000;
        byte signFound = 0x00;
        for (int i = 0; i < numColors; i++)
        {
            uint diff = LosslessUtils.SubPixels(palette[i], predict);
            byte rd = (byte)((diff >> 16) & 0xff);
            byte gd = (byte)((diff >> 8) & 0xff);
            byte bd = (byte)((diff >> 0) & 0xff);
            if (rd != 0x00)
            {
                signFound |= (byte)(rd < 0x80 ? 1 : 2);
            }

            if (gd != 0x00)
            {
                signFound |= (byte)(gd < 0x80 ? 8 : 16);
            }

            if (bd != 0x00)
            {
                signFound |= (byte)(bd < 0x80 ? 64 : 128);
            }
        }

        return (signFound & (signFound << 1)) != 0;  // two consequent signs.
    }

    /// <summary>
    /// Find greedily always the closest color of the predicted color to minimize
    /// deltas in the palette. This reduces storage needs since the palette is stored with delta encoding.
    /// </summary>
    /// <param name="palette">The palette.</param>
    /// <param name="numColors">The number of colors in the palette.</param>
    private static void GreedyMinimizeDeltas(Span<uint> palette, int numColors)
    {
        uint predict = 0x00000000;
        for (int i = 0; i < numColors; i++)
        {
            int bestIdx = i;
            uint bestScore = ~0U;
            for (int k = i; k < numColors; k++)
            {
                uint curScore = PaletteColorDistance(palette[k], predict);
                if (bestScore > curScore)
                {
                    bestScore = curScore;
                    bestIdx = k;
                }
            }

            // Swap color(palette[bestIdx], palette[i]);
            (palette[i], palette[bestIdx]) = (palette[bestIdx], palette[i]);
            predict = palette[i];
        }
    }

    private static void GetHuffBitLengthsAndCodes(Vp8LHistogramSet histogramImage, HuffmanTreeCode[] huffmanCodes)
    {
        int maxNumSymbols = 0;

        // Iterate over all histograms and get the aggregate number of codes used.
        for (int i = 0; i < histogramImage.Count; i++)
        {
            Vp8LHistogram histo = histogramImage[i];
            int startIdx = 5 * i;
            for (int k = 0; k < 5; k++)
            {
                int numSymbols;
                if (k == 0)
                {
                    numSymbols = histo.NumCodes();
                }
                else if (k == 4)
                {
                    numSymbols = WebpConstants.NumDistanceCodes;
                }
                else
                {
                    numSymbols = 256;
                }

                huffmanCodes[startIdx + k].NumSymbols = numSymbols;
            }
        }

        // TODO: Allocations.
        int end = 5 * histogramImage.Count;
        for (int i = 0; i < end; i++)
        {
            int bitLength = huffmanCodes[i].NumSymbols;
            huffmanCodes[i].Codes = new short[bitLength];
            huffmanCodes[i].CodeLengths = new byte[bitLength];
            if (maxNumSymbols < bitLength)
            {
                maxNumSymbols = bitLength;
            }
        }

        // Create Huffman trees.
        // TODO: Allocations.
        bool[] bufRle = new bool[maxNumSymbols];
        HuffmanTree[] huffTree = new HuffmanTree[3 * maxNumSymbols];

        for (int i = 0; i < histogramImage.Count; i++)
        {
            int codesStartIdx = 5 * i;
            Vp8LHistogram histo = histogramImage[i];
            HuffmanUtils.CreateHuffmanTree(histo.Literal, 15, bufRle, huffTree, huffmanCodes[codesStartIdx]);
            HuffmanUtils.CreateHuffmanTree(histo.Red, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 1]);
            HuffmanUtils.CreateHuffmanTree(histo.Blue, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 2]);
            HuffmanUtils.CreateHuffmanTree(histo.Alpha, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 3]);
            HuffmanUtils.CreateHuffmanTree(histo.Distance, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 4]);
        }
    }

    /// <summary>
    /// Computes a value that is related to the entropy created by the palette entry diff.
    /// </summary>
    /// <param name="col1">First color.</param>
    /// <param name="col2">Second color.</param>
    /// <returns>The color distance.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint PaletteColorDistance(uint col1, uint col2)
    {
        uint diff = LosslessUtils.SubPixels(col1, col2);
        const uint moreWeightForRGBThanForAlpha = 9;
        uint score = PaletteComponentDistance((diff >> 0) & 0xff);
        score += PaletteComponentDistance((diff >> 8) & 0xff);
        score += PaletteComponentDistance((diff >> 16) & 0xff);
        score *= moreWeightForRGBThanForAlpha;
        score += PaletteComponentDistance((diff >> 24) & 0xff);

        return score;
    }

    /// <summary>
    /// Calculates the huffman image bits.
    /// </summary>
    private static int GetHistoBits(WebpEncodingMethod method, bool usePalette, int width, int height)
    {
        // Make tile size a function of encoding method (Range: 0 to 6).
        int histoBits = (usePalette ? 9 : 7) - (int)method;
        while (true)
        {
            int huffImageSize = LosslessUtils.SubSampleSize(width, histoBits) * LosslessUtils.SubSampleSize(height, histoBits);
            if (huffImageSize <= WebpConstants.MaxHuffImageSize)
            {
                break;
            }

            histoBits++;
        }

        if (histoBits < WebpConstants.MinHuffmanBits)
        {
            return WebpConstants.MinHuffmanBits;
        }
        else if (histoBits > WebpConstants.MaxHuffmanBits)
        {
            return WebpConstants.MaxHuffmanBits;
        }
        else
        {
            return histoBits;
        }
    }

    /// <summary>
    /// Bundles multiple (1, 2, 4 or 8) pixels into a single pixel.
    /// </summary>
    private static void BundleColorMap(Span<byte> row, int width, int xBits, Span<uint> dst)
    {
        int x;
        if (xBits > 0)
        {
            int bitDepth = 1 << (3 - xBits);
            int mask = (1 << xBits) - 1;
            uint code = 0xff000000;
            for (x = 0; x < width; x++)
            {
                int xSub = x & mask;
                if (xSub == 0)
                {
                    code = 0xff000000;
                }

                code |= (uint)(row[x] << (8 + (bitDepth * xSub)));
                dst[x >> xBits] = code;
            }
        }
        else
        {
            for (x = 0; x < width; x++)
            {
                dst[x] = (uint)(0xff000000 | (row[x] << 8));
            }
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void BitWriterSwap(ref Vp8LBitWriter src, ref Vp8LBitWriter dst)
        => (dst, src) = (src, dst);

    /// <summary>
    /// Calculates the bits used for the transformation.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int GetTransformBits(WebpEncodingMethod method, int histoBits)
    {
        int maxTransformBits;
        if ((int)method < 4)
        {
            maxTransformBits = 6;
        }
        else if (method > WebpEncodingMethod.Level4)
        {
            maxTransformBits = 4;
        }
        else
        {
            maxTransformBits = 5;
        }

        return histoBits > maxTransformBits ? maxTransformBits : histoBits;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void AddSingle(uint p, Span<uint> a, Span<uint> r, Span<uint> g, Span<uint> b)
    {
        a[(int)(p >> 24) & 0xff]++;
        r[(int)(p >> 16) & 0xff]++;
        g[(int)(p >> 8) & 0xff]++;
        b[(int)(p >> 0) & 0xff]++;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void AddSingleSubGreen(uint p, Span<uint> r, Span<uint> b)
    {
        int green = (int)p >> 8;  // The upper bits are masked away later.
        r[(int)((p >> 16) - green) & 0xff]++;
        b[(int)((p >> 0) - green) & 0xff]++;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint SearchColorGreedy(Span<uint> palette, uint color)
    {
        if (color == palette[0])
        {
            return 0;
        }

        if (color == palette[1])
        {
            return 1;
        }

        if (color == palette[2])
        {
            return 2;
        }

        return 3;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint ApplyPaletteHash0(uint color) => (color >> 8) & 0xff; // Focus on the green color.

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint ApplyPaletteHash1(uint color) => (uint)((color & 0x00ffffffu) * 4222244071ul) >> (32 - PaletteInvSizeBits); // Forget about alpha.

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint ApplyPaletteHash2(uint color) => (uint)((color & 0x00ffffffu) * ((1ul << 31) - 1)) >> (32 - PaletteInvSizeBits); // Forget about alpha.

    // Note that masking with 0xffffffffu is for preventing an
    // 'unsigned int overflow' warning. Doesn't impact the compiled code.
    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint HashPix(uint pix) => (uint)((((long)pix + (pix >> 19)) * 0x39c5fba7L) & 0xffffffffu) >> 24;

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int PaletteCompareColorsForSort(uint p1, uint p2) => p1 < p2 ? -1 : 1;

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint PaletteComponentDistance(uint v) => (v <= 128) ? v : (256 - v);

    public void AllocateTransformBuffer(int width, int height)
    {
        // VP8LResidualImage needs room for 2 scanlines of uint32 pixels with an extra
        // pixel in each, plus 2 regular scanlines of bytes.
        int bgraScratchSize = this.UsePredictorTransform ? ((width + 1) * 2) + (((width * 2) + 4 - 1) / 4) : 0;
        int transformDataSize = this.UsePredictorTransform || this.UseCrossColorTransform ? LosslessUtils.SubSampleSize(width, this.TransformBits) * LosslessUtils.SubSampleSize(height, this.TransformBits) : 0;

        this.BgraScratch = this.memoryAllocator.Allocate<uint>(bgraScratchSize);
        this.TransformData = this.memoryAllocator.Allocate<uint>(transformDataSize);
        this.CurrentWidth = width;
    }

    /// <summary>
    /// Clears the backward references.
    /// </summary>
    public void ClearRefs()
    {
        foreach (Vp8LBackwardRefs t in this.Refs)
        {
            t.Refs.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Bgra.Dispose();
        this.EncodedData.Dispose();
        this.BgraScratch?.Dispose();
        this.Palette.Dispose();
        this.TransformData?.Dispose();
        this.HashChain.Dispose();
    }

    /// <summary>
    /// Scratch buffer to reduce allocations.
    /// </summary>
    private unsafe struct ScratchBuffer
    {
        private const int Size = 256;
        private fixed int scratch[Size];

        public Span<int> Span => MemoryMarshal.CreateSpan(ref this.scratch[0], Size);
    }
}

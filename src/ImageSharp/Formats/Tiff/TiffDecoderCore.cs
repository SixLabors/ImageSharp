// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Performs the tiff decoding operation.
/// </summary>
internal class TiffDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// General configuration options.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// A value indicating whether the metadata should be ignored when the image is being decoded.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// The maximum number of frames to decode. Inclusive.
    /// </summary>
    private readonly uint maxFrames;

    /// <summary>
    /// The stream to decode from.
    /// </summary>
    private BufferedReadStream inputStream;

    /// <summary>
    /// Indicates the byte order of the stream.
    /// </summary>
    private ByteOrder byteOrder;

    /// <summary>
    /// Indicating whether is BigTiff format.
    /// </summary>
    private bool isBigTiff;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public TiffDecoderCore(DecoderOptions options)
        : base(options)
    {
        this.configuration = options.Configuration;
        this.skipMetadata = options.SkipMetadata;
        this.maxFrames = options.MaxFrames;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <summary>
    /// Gets or sets the bits per sample.
    /// </summary>
    public TiffBitsPerSample BitsPerSample { get; set; }

    /// <summary>
    /// Gets or sets the bits per pixel.
    /// </summary>
    public int BitsPerPixel { get; set; }

    /// <summary>
    /// Gets or sets the lookup table for RGB palette colored images.
    /// </summary>
    public ushort[] ColorMap { get; set; }

    /// <summary>
    /// Gets or sets the photometric interpretation implementation to use when decoding the image.
    /// </summary>
    public TiffColorType ColorType { get; set; }

    /// <summary>
    /// Gets or sets the reference black and white for decoding YCbCr pixel data.
    /// </summary>
    public Rational[] ReferenceBlackAndWhite { get; set; }

    /// <summary>
    /// Gets or sets the YCbCr coefficients.
    /// </summary>
    public Rational[] YcbcrCoefficients { get; set; }

    /// <summary>
    /// Gets or sets the YCbCr sub sampling.
    /// </summary>
    public ushort[] YcbcrSubSampling { get; set; }

    /// <summary>
    /// Gets or sets the compression used, when the image was encoded.
    /// </summary>
    public TiffDecoderCompressionType CompressionType { get; set; }

    /// <summary>
    /// Gets or sets the Fax specific compression options.
    /// </summary>
    public FaxCompressionOptions FaxCompressionOptions { get; set; }

    /// <summary>
    /// Gets or sets the logical order of bits within a byte.
    /// </summary>
    public TiffFillOrder FillOrder { get; set; }

    /// <summary>
    /// Gets or sets the extra samples type.
    /// </summary>
    public TiffExtraSampleType? ExtraSamplesType { get; set; }

    /// <summary>
    /// Gets or sets the JPEG tables when jpeg compression is used.
    /// </summary>
    public byte[] JpegTables { get; set; }

    /// <summary>
    /// Gets or sets the start of image marker for old Jpeg compression.
    /// </summary>
    public uint? OldJpegCompressionStartOfImageMarker { get; set; }

    /// <summary>
    /// Gets or sets the planar configuration type to use when decoding the image.
    /// </summary>
    public TiffPlanarConfiguration PlanarConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the photometric interpretation.
    /// </summary>
    public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

    /// <summary>
    /// Gets or sets the sample format.
    /// </summary>
    public TiffSampleFormat SampleFormat { get; set; }

    /// <summary>
    /// Gets or sets the horizontal predictor.
    /// </summary>
    public TiffPredictor Predictor { get; set; }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        List<ImageFrame<TPixel>> frames = [];
        List<ImageFrameMetadata> framesMetadata = [];
        try
        {
            this.inputStream = stream;
            DirectoryReader reader = new(stream, this.configuration.MemoryAllocator);

            IList<ExifProfile> directories = reader.Read();
            this.byteOrder = reader.ByteOrder;
            this.isBigTiff = reader.IsBigTiff;

            Size? size = null;
            uint frameCount = 0;
            foreach (ExifProfile ifd in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(ifd, size, cancellationToken);

                if (!size.HasValue)
                {
                    size = frame.Size;
                }

                frames.Add(frame);
                framesMetadata.Add(frame.Metadata);

                if (++frameCount == this.maxFrames)
                {
                    break;
                }
            }

            this.Dimensions = frames[0].Size;
            ImageMetadata metadata = TiffDecoderMetadataCreator.Create(framesMetadata, this.skipMetadata, reader.ByteOrder, reader.IsBigTiff);
            return new Image<TPixel>(this.configuration, metadata, frames);
        }
        catch
        {
            foreach (ImageFrame<TPixel> f in frames)
            {
                f.Dispose();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.inputStream = stream;
        DirectoryReader reader = new(stream, this.configuration.MemoryAllocator);
        IList<ExifProfile> directories = reader.Read();

        List<ImageFrameMetadata> framesMetadata = [];
        int width = 0;
        int height = 0;

        for (int i = 0; i < directories.Count; i++)
        {
            (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffMetadata) meta
                = this.CreateFrameMetadata(directories[i]);

            framesMetadata.Add(meta.FrameMetadata);

            width = Math.Max(width, meta.TiffMetadata.EncodingWidth);
            height = Math.Max(height, meta.TiffMetadata.EncodingHeight);
        }

        ImageMetadata metadata = TiffDecoderMetadataCreator.Create(framesMetadata, this.skipMetadata, reader.ByteOrder, reader.IsBigTiff);

        return new ImageInfo(new Size(width, height), metadata, framesMetadata);
    }

    /// <summary>
    /// Decodes the image data from a specified IFD.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="tags">The IFD tags.</param>
    /// <param name="size">The previously determined root frame size if decoded.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    /// <returns>The tiff frame.</returns>
    private ImageFrame<TPixel> DecodeFrame<TPixel>(ExifProfile tags, Size? size, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffFrameMetadata) metadata = this.CreateFrameMetadata(tags);
        bool isTiled = this.VerifyAndParse(tags, metadata.TiffFrameMetadata);

        int width = metadata.TiffFrameMetadata.EncodingWidth;
        int height = metadata.TiffFrameMetadata.EncodingHeight;

        // If size has a value and the width/height off the tiff is smaller we much capture the delta.
        if (size.HasValue)
        {
            if (size.Value.Width < width || size.Value.Height < height)
            {
                TiffThrowHelper.ThrowNotSupported("Images with frames of size greater than the root frame are not supported.");
            }
        }
        else
        {
            size = new Size(width, height);
        }

        ImageFrame<TPixel> frame = new(this.configuration, size.Value.Width, size.Value.Height, metadata.FrameMetadata);

        if (isTiled)
        {
            this.DecodeImageWithTiles(tags, frame, width, height, cancellationToken);
        }
        else
        {
            this.DecodeImageWithStrips(tags, frame, width, height, cancellationToken);
        }

        return frame;
    }

    private (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffMetadata) CreateFrameMetadata(ExifProfile tags)
    {
        ImageFrameMetadata imageFrameMetaData = new();
        if (!this.skipMetadata)
        {
            imageFrameMetaData.ExifProfile = tags;

            // We resolve the ICC profile early so that we can use it for color conversion if needed.
            if (tags.TryGetValue(ExifTag.IccProfile, out IExifValue<byte[]> iccProfileBytes))
            {
                imageFrameMetaData.IccProfile = new IccProfile(iccProfileBytes.Value);
            }
        }

        TiffFrameMetadata tiffMetadata = TiffFrameMetadata.Parse(tags);
        imageFrameMetaData.SetFormatMetadata(TiffFormat.Instance, tiffMetadata);

        return (imageFrameMetaData, tiffMetadata);
    }

    /// <summary>
    /// Decodes the image data for Tiff's which arrange the pixel data in stripes.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="tags">The IFD tags.</param>
    /// <param name="frame">The image frame to decode into.</param>
    /// <param name="width">The width in px units of the frame data.</param>
    /// <param name="height">The height in px units of the frame data.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeImageWithStrips<TPixel>(ExifProfile tags, ImageFrame<TPixel> frame, int width, int height, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int rowsPerStrip;
        if (tags.TryGetValue(ExifTag.RowsPerStrip, out IExifValue<Number> value))
        {
            rowsPerStrip = (int)value.Value;
        }
        else
        {
            rowsPerStrip = TiffConstants.RowsPerStripInfinity;
        }

        Array stripOffsetsArray = (Array)tags.GetValueInternal(ExifTag.StripOffsets).GetValue();
        Array stripByteCountsArray = (Array)tags.GetValueInternal(ExifTag.StripByteCounts).GetValue();

        using IMemoryOwner<ulong> stripOffsetsMemory = this.ConvertNumbers(stripOffsetsArray, out Span<ulong> stripOffsets);
        using IMemoryOwner<ulong> stripByteCountsMemory = this.ConvertNumbers(stripByteCountsArray, out Span<ulong> stripByteCounts);

        if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
        {
            this.DecodeStripsPlanar(
                frame,
                width,
                height,
                rowsPerStrip,
                stripOffsets,
                stripByteCounts,
                cancellationToken);
        }
        else
        {
            this.DecodeStripsChunky(
                frame,
                width,
                height,
                rowsPerStrip,
                stripOffsets,
                stripByteCounts,
                cancellationToken);
        }
    }

    /// <summary>
    /// Decodes the image data for Tiff's which arrange the pixel data in tiles.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="tags">The IFD tags.</param>
    /// <param name="frame">The image frame to decode into.</param>
    /// <param name="width">The width in px units of the frame data.</param>
    /// <param name="height">The height in px units of the frame data.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeImageWithTiles<TPixel>(ExifProfile tags, ImageFrame<TPixel> frame, int width, int height, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> pixels = frame.PixelBuffer;

        if (!tags.TryGetValue(ExifTag.TileWidth, out IExifValue<Number> valueWidth))
        {
            ArgumentNullException.ThrowIfNull(valueWidth);
        }

        if (!tags.TryGetValue(ExifTag.TileLength, out IExifValue<Number> valueLength))
        {
            ArgumentNullException.ThrowIfNull(valueLength);
        }

        int tileWidth = (int)valueWidth.Value;
        int tileLength = (int)valueLength.Value;
        int tilesAcross = (width + tileWidth - 1) / tileWidth;
        int tilesDown = (height + tileLength - 1) / tileLength;

        Array tilesOffsetsArray;
        Array tilesByteCountsArray;
        IExifValue tilesOffsetsExifValue = tags.GetValueInternal(ExifTag.TileOffsets);
        IExifValue tilesByteCountsExifValue = tags.GetValueInternal(ExifTag.TileByteCounts);
        if (tilesOffsetsExifValue is null)
        {
            // Note: This is against the spec, but libTiff seems to handle it this way.
            // TIFF 6.0 says: "Do not use both strip- oriented and tile-oriented fields in the same TIFF file".
            tilesOffsetsExifValue = tags.GetValueInternal(ExifTag.StripOffsets);
            tilesByteCountsExifValue = tags.GetValueInternal(ExifTag.StripByteCounts);
            tilesOffsetsArray = (Array)tilesOffsetsExifValue.GetValue();
            tilesByteCountsArray = (Array)tilesByteCountsExifValue.GetValue();
        }
        else
        {
            tilesOffsetsArray = (Array)tilesOffsetsExifValue.GetValue();
            tilesByteCountsArray = (Array)tilesByteCountsExifValue.GetValue();
        }

        using IMemoryOwner<ulong> tileOffsetsMemory = this.ConvertNumbers(tilesOffsetsArray, out Span<ulong> tileOffsets);
        using IMemoryOwner<ulong> tileByteCountsMemory = this.ConvertNumbers(tilesByteCountsArray, out Span<ulong> tileByteCounts);

        if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
        {
            this.DecodeTilesPlanar(frame, tileWidth, tileLength, tilesAcross, tilesDown, tileOffsets, tileByteCounts, cancellationToken);
        }
        else
        {
            this.DecodeTilesChunky(frame, tileWidth, tileLength, tilesAcross, tilesDown, tileOffsets, tileByteCounts, cancellationToken);
        }
    }

    /// <summary>
    /// Decodes the image data for planar encoded pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to decode data into.</param>
    /// <param name="width">The width in px units of the frame data.</param>
    /// <param name="height">The height in px units of the frame data.</param>
    /// <param name="rowsPerStrip">The number of rows per strip of data.</param>
    /// <param name="stripOffsets">An array of byte offsets to each strip in the image.</param>
    /// <param name="stripByteCounts">An array of the size of each strip (in bytes).</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeStripsPlanar<TPixel>(
        ImageFrame<TPixel> frame,
        int width,
        int height,
        int rowsPerStrip,
        Span<ulong> stripOffsets,
        Span<ulong> stripByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int stripsPerPixel = this.BitsPerSample.Channels;
        int stripsPerPlane = stripOffsets.Length / stripsPerPixel;
        int bitsPerPixel = this.BitsPerPixel;

        Buffer2D<TPixel> pixels = frame.PixelBuffer;

        IMemoryOwner<byte>[] stripBuffers = new IMemoryOwner<byte>[stripsPerPixel];

        try
        {
            for (int stripIndex = 0; stripIndex < stripBuffers.Length; stripIndex++)
            {
                int uncompressedStripSize = this.CalculateStripBufferSize(width, rowsPerStrip, stripIndex);
                stripBuffers[stripIndex] = this.memoryAllocator.Allocate<byte>(uncompressedStripSize);
            }

            using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, bitsPerPixel, frame.Metadata);
            TiffBasePlanarColorDecoder<TPixel> colorDecoder = this.CreatePlanarColorDecoder<TPixel>();

            for (int i = 0; i < stripsPerPlane; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int stripHeight = i < stripsPerPlane - 1 || height % rowsPerStrip == 0 ? rowsPerStrip : height % rowsPerStrip;

                int stripIndex = i;
                for (int planeIndex = 0; planeIndex < stripsPerPixel; planeIndex++)
                {
                    decompressor.Decompress(
                        this.inputStream,
                        stripOffsets[stripIndex],
                        stripByteCounts[stripIndex],
                        stripHeight,
                        stripBuffers[planeIndex].GetSpan(),
                        cancellationToken);

                    stripIndex += stripsPerPlane;
                }

                colorDecoder.Decode(stripBuffers, pixels, 0, rowsPerStrip * i, width, stripHeight);
            }
        }
        finally
        {
            foreach (IMemoryOwner<byte> buf in stripBuffers)
            {
                buf?.Dispose();
            }
        }
    }

    /// <summary>
    /// Decodes the image data for chunky encoded pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to decode data into.</param>
    /// <param name="width">The width in px units of the frame data.</param>
    /// <param name="height">The height in px units of the frame data.</param>
    /// <param name="rowsPerStrip">The rows per strip.</param>
    /// <param name="stripOffsets">The strip offsets.</param>
    /// <param name="stripByteCounts">The strip byte counts.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeStripsChunky<TPixel>(
        ImageFrame<TPixel> frame,
        int width,
        int height,
        int rowsPerStrip,
        Span<ulong> stripOffsets,
        Span<ulong> stripByteCounts,
        CancellationToken cancellationToken)
       where TPixel : unmanaged, IPixel<TPixel>
    {
        // If the rowsPerStrip has the default value, which is effectively infinity. That is, the entire image is one strip.
        if (rowsPerStrip == TiffConstants.RowsPerStripInfinity)
        {
            rowsPerStrip = height;
        }

        int uncompressedStripSize = this.CalculateStripBufferSize(width, rowsPerStrip);
        int bitsPerPixel = this.BitsPerPixel;

        using IMemoryOwner<byte> stripBuffer = this.memoryAllocator.Allocate<byte>(uncompressedStripSize, AllocationOptions.Clean);
        Span<byte> stripBufferSpan = stripBuffer.GetSpan();
        Buffer2D<TPixel> pixels = frame.PixelBuffer;

        using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, bitsPerPixel, frame.Metadata);
        TiffBaseColorDecoder<TPixel> colorDecoder = this.CreateChunkyColorDecoder<TPixel>();

        for (int stripIndex = 0; stripIndex < stripOffsets.Length; stripIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int stripHeight = stripIndex < stripOffsets.Length - 1 || height % rowsPerStrip == 0
                ? rowsPerStrip
                : height % rowsPerStrip;

            int top = rowsPerStrip * stripIndex;
            if (top + stripHeight > height)
            {
                // Make sure we ignore any strips that are not needed for the image (if too many are present).
                break;
            }

            decompressor.Decompress(
                this.inputStream,
                stripOffsets[stripIndex],
                stripByteCounts[stripIndex],
                stripHeight,
                stripBufferSpan,
                cancellationToken);

            colorDecoder.Decode(stripBufferSpan, pixels, 0, top, width, stripHeight);
        }
    }

    /// <summary>
    /// Decodes the image data for Tiff's which arrange the pixel data in tiles and the planar configuration.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to decode into.</param>
    /// <param name="tileWidth">The width in pixels of the tile.</param>
    /// <param name="tileLength">The height in pixels of the tile.</param>
    /// <param name="tilesAcross">The number of tiles horizontally.</param>
    /// <param name="tilesDown">The number of tiles vertically.</param>
    /// <param name="tileOffsets">The tile offsets.</param>
    /// <param name="tileByteCounts">The tile byte counts.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeTilesPlanar<TPixel>(
        ImageFrame<TPixel> frame,
        int tileWidth,
        int tileLength,
        int tilesAcross,
        int tilesDown,
        Span<ulong> tileOffsets,
        Span<ulong> tileByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        int width = pixels.Width;
        int height = pixels.Height;
        int bitsPerPixel = this.BitsPerPixel;
        int channels = this.BitsPerSample.Channels;
        int tilesPerChannel = tileOffsets.Length / channels;

        IMemoryOwner<byte>[] tilesBuffers = new IMemoryOwner<byte>[channels];

        try
        {
            int bytesPerTileRow = RoundUpToMultipleOfEight(tileWidth * bitsPerPixel);
            int uncompressedTilesSize = bytesPerTileRow * tileLength;
            for (int i = 0; i < tilesBuffers.Length; i++)
            {
                tilesBuffers[i] = this.memoryAllocator.Allocate<byte>(uncompressedTilesSize, AllocationOptions.Clean);
            }

            using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(frame.Width, bitsPerPixel, frame.Metadata);
            TiffBasePlanarColorDecoder<TPixel> colorDecoder = this.CreatePlanarColorDecoder<TPixel>();

            int tileIndex = 0;
            int remainingPixelsInColumn = height;
            for (int tileY = 0; tileY < tilesDown; tileY++)
            {
                int remainingPixelsInRow = width;
                int pixelColumnOffset = tileY * tileLength;
                bool isLastVerticalTile = tileY == tilesDown - 1;
                for (int tileX = 0; tileX < tilesAcross; tileX++)
                {
                    int pixelRowOffset = tileX * tileWidth;
                    bool isLastHorizontalTile = tileX == tilesAcross - 1;
                    int tileIndexForChannel = tileIndex;
                    for (int i = 0; i < channels; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        decompressor.Decompress(
                            this.inputStream,
                            tileOffsets[tileIndexForChannel],
                            tileByteCounts[tileIndexForChannel],
                            tileLength,
                            tilesBuffers[i].GetSpan(),
                            cancellationToken);

                        tileIndexForChannel += tilesPerChannel;
                    }

                    if (isLastHorizontalTile && remainingPixelsInRow < tileWidth)
                    {
                        // Adjust pixel data in the tile buffer to fit the smaller then usual tile width.
                        for (int i = 0; i < channels; i++)
                        {
                            Span<byte> tileBufferSpan = tilesBuffers[i].GetSpan();
                            for (int y = 0; y < tileLength; y++)
                            {
                                int currentRowOffset = y * tileWidth;
                                Span<byte> adjustedRow = tileBufferSpan.Slice(y * remainingPixelsInRow, remainingPixelsInRow);
                                tileBufferSpan.Slice(currentRowOffset, remainingPixelsInRow).CopyTo(adjustedRow);
                            }
                        }
                    }

                    colorDecoder.Decode(
                        tilesBuffers,
                        pixels,
                        pixelRowOffset,
                        pixelColumnOffset,
                        isLastHorizontalTile ? remainingPixelsInRow : tileWidth,
                        isLastVerticalTile ? remainingPixelsInColumn : tileLength);

                    remainingPixelsInRow -= tileWidth;
                    tileIndex++;
                }

                remainingPixelsInColumn -= tileLength;
            }
        }
        finally
        {
            foreach (IMemoryOwner<byte> buf in tilesBuffers)
            {
                buf?.Dispose();
            }
        }
    }

    /// <summary>
    /// Decodes the image data for TIFFs which arrange the pixel data in tiles and the chunky configuration.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to decode into.</param>
    /// <param name="tileWidth">The width in pixels of the tile.</param>
    /// <param name="tileLength">The height in pixels of the tile.</param>
    /// <param name="tilesAcross">The number of tiles horizontally.</param>
    /// <param name="tilesDown">The number of tiles vertically.</param>
    /// <param name="tileOffsets">The tile offsets.</param>
    /// <param name="tileByteCounts">The tile byte counts.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeTilesChunky<TPixel>(
        ImageFrame<TPixel> frame,
        int tileWidth,
        int tileLength,
        int tilesAcross,
        int tilesDown,
        Span<ulong> tileOffsets,
        Span<ulong> tileByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        int width = pixels.Width;
        int height = pixels.Height;
        int bitsPerPixel = this.BitsPerPixel;
        int bytesPerTileRow = RoundUpToMultipleOfEight(tileWidth * bitsPerPixel);

        using IMemoryOwner<byte> tileBuffer = this.memoryAllocator.Allocate<byte>(bytesPerTileRow * tileLength, AllocationOptions.Clean);
        Span<byte> tileBufferSpan = tileBuffer.GetSpan();

        using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(frame.Width, bitsPerPixel, frame.Metadata, true, tileWidth, tileLength);
        TiffBaseColorDecoder<TPixel> colorDecoder = this.CreateChunkyColorDecoder<TPixel>();

        int tileIndex = 0;
        for (int tileY = 0; tileY < tilesDown; tileY++)
        {
            int rowStartY = tileY * tileLength;
            int rowEndY = Math.Min(rowStartY + tileLength, height);

            for (int tileX = 0; tileX < tilesAcross; tileX++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool isLastHorizontalTile = tileX == tilesAcross - 1;
                int remainingPixelsInRow = width - (tileX * tileWidth);

                decompressor.Decompress(
                    this.inputStream,
                    tileOffsets[tileIndex],
                    tileByteCounts[tileIndex],
                    tileLength,
                    tileBufferSpan,
                    cancellationToken);

                int tileBufferOffset = 0;
                int bytesToCopy = isLastHorizontalTile ? RoundUpToMultipleOfEight(bitsPerPixel * remainingPixelsInRow) : bytesPerTileRow;
                int rowWidth = Math.Min(tileWidth, remainingPixelsInRow);
                int left = tileX * tileWidth;

                for (int y = rowStartY; y < rowEndY; y++)
                {
                    // Decode the tile row directly into the pixel buffer.
                    ReadOnlySpan<byte> tileRowSpan = tileBufferSpan.Slice(tileBufferOffset, bytesToCopy);
                    colorDecoder.Decode(tileRowSpan, pixels, left, y, rowWidth, 1);
                    tileBufferOffset += bytesPerTileRow;
                }

                tileIndex++;
            }
        }
    }

    private TiffBaseColorDecoder<TPixel> CreateChunkyColorDecoder<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel> =>
        TiffColorDecoderFactory<TPixel>.Create(
            this.configuration,
            this.memoryAllocator,
            this.ColorType,
            this.BitsPerSample,
            this.ExtraSamplesType,
            this.ColorMap,
            this.ReferenceBlackAndWhite,
            this.YcbcrCoefficients,
            this.YcbcrSubSampling,
            this.CompressionType,
            this.byteOrder);

    private TiffBasePlanarColorDecoder<TPixel> CreatePlanarColorDecoder<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel> =>
        TiffColorDecoderFactory<TPixel>.CreatePlanar(
            this.ColorType,
            this.BitsPerSample,
            this.ExtraSamplesType,
            this.ColorMap,
            this.ReferenceBlackAndWhite,
            this.YcbcrCoefficients,
            this.YcbcrSubSampling,
            this.byteOrder);

    private TiffBaseDecompressor CreateDecompressor<TPixel>(
        int frameWidth,
        int bitsPerPixel,
        ImageFrameMetadata metadata,
        bool isTiled = false,
        int tileWidth = 0,
        int tileHeight = 0)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TiffDecompressorsFactory.Create(
            this.Options,
            this.CompressionType,
            this.memoryAllocator,
            this.PhotometricInterpretation,
            frameWidth,
            bitsPerPixel,
            metadata,
            this.ColorType,
            this.Predictor,
            this.FaxCompressionOptions,
            this.JpegTables,
            this.OldJpegCompressionStartOfImageMarker.GetValueOrDefault(),
            this.FillOrder,
            this.byteOrder,
            isTiled,
            tileWidth,
            tileHeight);

    private IMemoryOwner<ulong> ConvertNumbers(Array array, out Span<ulong> span)
    {
        if (array is Number[] numbers)
        {
            IMemoryOwner<ulong> memory = this.memoryAllocator.Allocate<ulong>(numbers.Length);
            span = memory.GetSpan();
            for (int i = 0; i < numbers.Length; i++)
            {
                span[i] = (uint)numbers[i];
            }

            return memory;
        }

        DebugGuard.IsTrue(array is ulong[], $"Expected {nameof(UInt64)} array.");
        span = (ulong[])array;
        return null;
    }

    /// <summary>
    /// Calculates the size (in bytes) for a pixel buffer using the determined color format.
    /// </summary>
    /// <param name="width">The width for the desired pixel buffer.</param>
    /// <param name="height">The height for the desired pixel buffer.</param>
    /// <param name="plane">The index of the plane for planar image configuration (or zero for chunky).</param>
    /// <returns>The size (in bytes) of the required pixel buffer.</returns>
    private int CalculateStripBufferSize(int width, int height, int plane = -1)
    {
        DebugGuard.MustBeLessThanOrEqualTo(plane, 3, nameof(plane));

        int bitsPerPixel = 0;

        if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
        {
            DebugGuard.IsTrue(plane == -1, "Expected Chunky planar.");
            bitsPerPixel = this.BitsPerPixel;
        }
        else
        {
            switch (plane)
            {
                case 0:
                    bitsPerPixel = this.BitsPerSample.Channel0;
                    break;
                case 1:
                    bitsPerPixel = this.BitsPerSample.Channel1;
                    break;
                case 2:
                    bitsPerPixel = this.BitsPerSample.Channel2;
                    break;
                case 3:
                    bitsPerPixel = this.BitsPerSample.Channel3;
                    break;
                default:
                    TiffThrowHelper.ThrowNotSupported("More then 4 color channels are not supported");
                    break;
            }
        }

        int bytesPerRow = ((width * bitsPerPixel) + 7) / 8;
        return bytesPerRow * height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpToMultipleOfEight(int value) => (int)(((uint)value + 7) / 8);
}

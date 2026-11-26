// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.Readers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
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

    /// <summary>
    /// Gets or sets the width in pixels of a tile. The parser sets it for a tiled frame. It is greater than zero.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    /// Gets or sets the height in pixels of a tile. The parser sets it for a tiled frame. It is greater than zero.
    /// </summary>
    public int TileLength { get; set; }

    /// <summary>
    /// Gets or sets the row count of a strip. The parser sets it for a frame with strips. It is at most the frame height.
    /// </summary>
    public int RowsPerStrip { get; set; }

    /// <summary>
    /// Gets a value indicating whether the compression contains an image whose decoder owns color conversion and pixel packing.
    /// </summary>
    private bool UsesNestedDecoder
        => this.CompressionType is TiffDecoderCompressionType.Jpeg or TiffDecoderCompressionType.OldJpeg or TiffDecoderCompressionType.Webp;

    /// <summary>
    /// Makes sure that each sample buffer of the layout fits in a span. The decompressors and the color readers use each buffer as a span. Thus the length of
    /// each buffer must fit in an int. The check occurs before the decoder allocates the frame.
    /// </summary>
    /// <remarks>
    /// The layout methods use the same size methods to allocate the buffers. Thus each buffer passes this check.
    /// </remarks>
    /// <param name="width">The width of the frame.</param>
    /// <param name="isTiled">Whether the image uses tiles.</param>
    /// <param name="message">The message for a status other than <see cref="TiffDirectoryStatus.Valid"/>, or <see langword="null"/> for
    /// <see cref="TiffDirectoryStatus.Valid"/>.</param>
    /// <returns>The status of the sample buffers.</returns>
    public TiffDirectoryStatus VerifySampleBuffers(int width, bool isTiled, out string message)
    {
        message = null;

        // The nested decoders write pixels. They use no sample buffer. The parser rejects them for planar data.
        if (this.UsesNestedDecoder)
        {
            return TiffDirectoryStatus.Valid;
        }

        ulong size = 0;
        if (isTiled)
        {
            if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
            {
                for (int channel = 0; channel < this.BitsPerSample.Channels; channel++)
                {
                    size = Math.Max(size, this.CalculateStripBufferSize(this.TileWidth, this.TileLength, channel));
                }
            }
            else
            {
                size = GetTileBufferSize(this.TileWidth, this.TileLength, this.BitsPerPixel, out _);
            }
        }
        else
        {
            if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
            {
                int rowsPerRead = this.GetPlanarRowsPerRead(width, this.RowsPerStrip);
                for (int channel = 0; channel < this.BitsPerSample.Channels; channel++)
                {
                    size = Math.Max(size, this.CalculateStripBufferSize(width, rowsPerRead, channel));
                }
            }
            else
            {
                size = this.GetChunkyStripBufferSize(width, this.RowsPerStrip, out _);
            }
        }

        if (size > int.MaxValue)
        {
            message = $"The TIFF {(isTiled ? "tile" : "strip")} buffer exceeds Int32.MaxValue bytes.";
            return TiffDirectoryStatus.Unsupported;
        }

        return TiffDirectoryStatus.Valid;
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.inputStream = stream;
        DirectoryReader reader = new(stream, this.configuration.MemoryAllocator);
        IList<ExifProfile> directories = reader.Read();
        this.byteOrder = reader.ByteOrder;

        List<ImageFrame<TPixel>> frames = [];
        List<ImageFrameMetadata> framesMetadata = [];

        try
        {
            Size? size = null;
            uint frameCount = 0;
            foreach (ExifProfile ifd in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffMetadata) metadata = this.CreateFrameMetadata(ifd);
                TiffDirectoryStatus status = this.VerifyAndParse(ifd, metadata.TiffMetadata, size, out bool isTiled, out string message);
                if (!this.AcceptDirectory(status, message, frames.Count > 0))
                {
                    break;
                }

                if (!size.HasValue)
                {
                    // Record the root size before the allocation. Then the allocation errors include the image dimensions.
                    size = new Size(metadata.TiffMetadata.EncodingWidth, metadata.TiffMetadata.EncodingHeight);
                    this.Dimensions = size.Value;
                }

                ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(ifd, size.Value, metadata.FrameMetadata, metadata.TiffMetadata, isTiled, cancellationToken);
                frames.Add(frame);
                framesMetadata.Add(frame.Metadata);

                if (++frameCount == this.maxFrames)
                {
                    break;
                }
            }

            ImageMetadata imageMetadata = TiffDecoderMetadataCreator.Create(framesMetadata, this.skipMetadata, reader.ByteOrder, reader.IsBigTiff);
            return new Image<TPixel>(this.configuration, imageMetadata, frames);
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
        Size? size = null;

        // Identify does the same checks as Decode on each IFD. Thus both methods give the same frames.
        foreach (ExifProfile ifd in directories)
        {
            (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffMetadata) meta = this.CreateFrameMetadata(ifd);
            TiffDirectoryStatus status = this.VerifyAndParse(ifd, meta.TiffMetadata, size, out _, out string message);
            if (!this.AcceptDirectory(status, message, framesMetadata.Count > 0))
            {
                break;
            }

            size ??= new Size(meta.TiffMetadata.EncodingWidth, meta.TiffMetadata.EncodingHeight);

            if (meta.TiffMetadata.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor)
            {
                // Identify has no destination pixel format; RgbaVector preserves converted colors as unassociated floating-point values.
                meta.TiffMetadata.LocalColorTable = TiffUtilities.DecodeColorMap<RgbaVector>(
                    this.ColorMap,
                    meta.TiffMetadata.BitsPerSample,
                    meta.FrameMetadata.IccProfile,
                    this.memoryAllocator);
            }

            framesMetadata.Add(meta.FrameMetadata);
        }

        ImageMetadata metadata = TiffDecoderMetadataCreator.Create(framesMetadata, this.skipMetadata, reader.ByteOrder, reader.IsBigTiff);

        // The frames after the first frame are not larger than the first frame.
        return new ImageInfo(size ?? default, metadata, framesMetadata);
    }

    /// <summary>
    /// Examines the status of the IFD before the decoder allocates the frame of the IFD.
    /// </summary>
    /// <remarks>
    /// The frames of earlier IFDs exist already. Thus, if an IFD after the first IFD is damaged or uses an unsupported layout, the image data policy applies.
    /// In the IgnoreImageData mode, the decoder keeps the earlier frames and stops at that IFD.
    /// It stops at the first directory that it cannot read. The first IFD has no earlier frame. Thus an error in the first IFD throws an exception in each
    /// mode.
    /// </remarks>
    /// <param name="status">The status of the IFD.</param>
    /// <param name="message">The message for a status other than <see cref="TiffDirectoryStatus.Valid"/>.</param>
    /// <param name="imageExists">Whether the decoder has frames of earlier IFDs.</param>
    /// <returns><see langword="true"/> if the decoder decodes the IFD. <see langword="false"/> if the decoder stops before the IFD.</returns>
    /// <exception cref="NotSupportedException">The IFD uses an unsupported feature, and the decoder keeps no frame.</exception>
    /// <exception cref="InvalidImageContentException">The IFD is damaged, and the decoder keeps no frame.</exception>
    private bool AcceptDirectory(TiffDirectoryStatus status, string message, bool imageExists)
    {
        if (status == TiffDirectoryStatus.Valid)
        {
            return true;
        }

        if (status == TiffDirectoryStatus.Unsupported && !(imageExists && this.Options.SegmentIntegrityHandling.IgnoresImageDataErrors()))
        {
            TiffThrowHelper.ThrowNotSupported(message);
        }

        this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, message);
        return false;
    }

    /// <summary>
    /// Decodes the image data from a specified IFD.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="tags">The IFD tags.</param>
    /// <param name="size">The size of the frame: the size of the first frame.</param>
    /// <param name="frameMetadata">The metadata of the frame.</param>
    /// <param name="tiffMetadata">The TIFF metadata of the frame.</param>
    /// <param name="isTiled">Whether the image uses tiles.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    /// <returns>The tiff frame.</returns>
    private ImageFrame<TPixel> DecodeFrame<TPixel>(
        ExifProfile tags,
        Size size,
        ImageFrameMetadata frameMetadata,
        TiffFrameMetadata tiffMetadata,
        bool isTiled,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // VerifyAndParse checked the IFD. Thus the layout is valid. After the allocation, errors throw an exception in each mode.
        ImageFrame<TPixel> frame = new(this.configuration, size.Width, size.Height, frameMetadata);
        int width = tiffMetadata.EncodingWidth;
        int height = tiffMetadata.EncodingHeight;

        try
        {
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
        catch
        {
            // The caller owns the frame only after decoding succeeds.
            frame.Dispose();
            throw;
        }
    }

    private (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffMetadata) CreateFrameMetadata(ExifProfile tags)
    {
        ImageFrameMetadata imageFrameMetaData = new();
        if (!this.skipMetadata)
        {
            imageFrameMetaData.TrySetExifProfile(tags);
        }

        // ICC is needed for pixel conversion even when optional EXIF metadata is skipped.
        if (tags.TryGetValue(ExifTag.IccProfile, out IExifValue<byte[]> iccProfileBytes))
        {
            try
            {
                // The EXIF tag keeps its array. The reader copies the bounded ICC payload.
                imageFrameMetaData.IccProfile = IccReader.ReadProfile(iccProfileBytes.Value, this.Options.SegmentIntegrityHandling.IgnoresAncillaryErrors());
            }
            catch (InvalidIccProfileException ex)
            {
                // The ICC reader reports a damaged profile. The other modes continue without the profile.
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid TIFF ICC profile.", ex);
            }
        }
        else if (tags.InvalidTags.Contains(ExifTag.IccProfile))
        {
            // Oversized or malformed ICC tags are rejected before EXIF allocates their payload.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid TIFF ICC profile.");
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
        int rowsPerStrip = this.RowsPerStrip;
        Array stripOffsetsArray = (Array)tags.GetValueInternal(ExifTag.StripOffsets).GetValue();
        Array stripByteCountsArray = (Array)tags.GetValueInternal(ExifTag.StripByteCounts).GetValue();

        using IMemoryOwner<ulong> stripOffsetsMemory = this.ConvertNumbers(stripOffsetsArray, out Span<ulong> stripOffsets);
        using IMemoryOwner<ulong> stripByteCountsMemory = this.ConvertNumbers(stripByteCountsArray, out Span<ulong> stripByteCounts);
        PairOffsets(ref stripOffsets, ref stripByteCounts);
        this.CheckBlockCount(stripOffsets.Length, ((long)height + rowsPerStrip - 1) / rowsPerStrip);

        if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
        {
            this.DecodeStripsPlanar(frame, width, height, rowsPerStrip, stripOffsets, stripByteCounts, cancellationToken);
        }
        else
        {
            this.DecodeStripsChunky(frame, width, height, rowsPerStrip, stripOffsets, stripByteCounts, cancellationToken);
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
        int tileWidth = this.TileWidth;
        int tileLength = this.TileLength;

        // Widen the sums because edge tiles may extend beyond the frame.
        int tilesAcross = (int)(((long)width + tileWidth - 1) / tileWidth);
        int tilesDown = (int)(((long)height + tileLength - 1) / tileLength);

        IExifValue tilesOffsetsExifValue = tags.GetValueInternal(ExifTag.TileOffsets);
        IExifValue tilesByteCountsExifValue = tags.GetValueInternal(ExifTag.TileByteCounts);
        if (tilesOffsetsExifValue is null)
        {
            // Note: This is against the spec, but libTiff seems to handle it this way.
            // TIFF 6.0 says: "Do not use both strip- oriented and tile-oriented fields in the same TIFF file".
            tilesOffsetsExifValue = tags.GetValueInternal(ExifTag.StripOffsets);
            tilesByteCountsExifValue = tags.GetValueInternal(ExifTag.StripByteCounts);
        }

        // The parser makes sure that TileByteCounts is present with TileOffsets, and that StripByteCounts is present with StripOffsets.
        Array tilesOffsetsArray = (Array)tilesOffsetsExifValue.GetValue();
        Array tilesByteCountsArray = (Array)tilesByteCountsExifValue.GetValue();

        using IMemoryOwner<ulong> tileOffsetsMemory = this.ConvertNumbers(tilesOffsetsArray, out Span<ulong> tileOffsets);
        using IMemoryOwner<ulong> tileByteCountsMemory = this.ConvertNumbers(tilesByteCountsArray, out Span<ulong> tileByteCounts);
        PairOffsets(ref tileOffsets, ref tileByteCounts);
        this.CheckBlockCount(tileOffsets.Length, (long)tilesAcross * tilesDown);

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
    /// Decompresses each strip's channel planes before the TIFF color reader combines them into pixels.
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
        ReadOnlySpan<ulong> stripOffsets,
        ReadOnlySpan<ulong> stripByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Planar offsets are grouped by plane, StripsPerImage entries for each plane, and StripsPerImage comes from the
        // image height. See TIFF 6.0 specification, section 8, RowsPerStrip and StripOffsets, pages 39-40:
        // https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
        // A file can hold fewer offsets (see CheckBlockCount). The decode stops after the last strip that all planes have. The product uses 64-bit math. The
        // planes before the last plane can hold more strips than an int can count.
        int channels = this.BitsPerSample.Channels;
        int stripsPerPlane = (int)(((long)height + rowsPerStrip - 1) / rowsPerStrip);
        int stripCount = (int)Math.Clamp(stripOffsets.Length - ((long)(channels - 1) * stripsPerPlane), 0, stripsPerPlane);

        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        IMemoryOwner<byte>[] stripBuffers = new IMemoryOwner<byte>[channels];

        try
        {
            // Planar JPEG, OldJPEG and WebP are rejected by the options parser. These buffers always receive raw channel samples.
            using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, this.BitsPerPixel, frame.Metadata);
            using TiffBasePlanarColorDecoder<TPixel> colorDecoder = this.CreatePlanarColorDecoder<TPixel>(frame.Metadata);

            // Uncompressed rows have fixed offsets. If a plane is larger than the single-buffer limit of the allocator, the decoder reads the rows one at a
            // time.
            int rowsPerRead = this.GetPlanarRowsPerRead(width, rowsPerStrip);
            bool readRows = rowsPerRead < rowsPerStrip;

            // Use one buffer for each channel, for all strips. The bit depth of each plane sets the byte count of its buffer. VerifySampleBuffers made sure
            // that each buffer fits in a span.
            for (int channel = 0; channel < channels; channel++)
            {
                stripBuffers[channel] = this.memoryAllocator.Allocate<byte>((int)this.CalculateStripBufferSize(width, rowsPerRead, channel));
            }

            for (int strip = 0; strip < stripCount; strip++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Each strip has RowsPerStrip rows, except the last strip of the image.
                int top = rowsPerStrip * strip;
                int stripHeight = Math.Min(rowsPerStrip, height - top);
                for (int row = 0; row < stripHeight; row += rowsPerRead)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int readHeight = Math.Min(rowsPerRead, stripHeight - row);
                    int stripIndex = strip;

                    // Offsets are grouped by plane. Read all components before combining this spatial row or strip.
                    for (int channel = 0; channel < channels; channel++)
                    {
                        ulong offset = stripOffsets[stripIndex];
                        ulong byteCount = stripByteCounts[stripIndex];
                        if (readRows)
                        {
                            ulong bytesPerRow = this.CalculateStripBufferSize(width, 1, channel);
                            ulong rowStart = (ulong)row * bytesPerRow;
                            offset += rowStart;
                            byteCount = byteCount > rowStart ? Math.Min(byteCount - rowStart, bytesPerRow) : 0;
                        }

                        // The final strip can be shorter than the reused buffer, and chroma expansion needs more storage
                        // than the file data. The decompressor gets the samples of this strip only, so it can find data
                        // that ends early.
                        Span<byte> samples = stripBuffers[channel].GetSpan()[..(int)this.CalculateUncompressedDataSize(width, readHeight, channel)];

                        decompressor.Decompress<TPixel>(this.inputStream, offset, byteCount, readHeight, samples, default, cancellationToken);
                        stripIndex += stripsPerPlane;
                    }

                    colorDecoder.Decode(stripBuffers, pixels, 0, top + row, width, readHeight);
                }
            }
        }
        finally
        {
            // Allocation or decoding can fail after only some channel buffers have been acquired.
            foreach (IMemoryOwner<byte> buffer in stripBuffers)
            {
                buffer?.Dispose();
            }
        }
    }

    /// <summary>
    /// Decodes interleaved strips, using TIFF color readers for raw samples and nested decoders for JPEG and WebP.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to decode into.</param>
    /// <param name="width">The encoded frame width in pixels.</param>
    /// <param name="height">The encoded frame height in pixels.</param>
    /// <param name="rowsPerStrip">The maximum number of rows in each strip.</param>
    /// <param name="stripOffsets">The compressed strips' stream offsets.</param>
    /// <param name="stripByteCounts">The compressed strips' byte counts.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    private void DecodeStripsChunky<TPixel>(
        ImageFrame<TPixel> frame,
        int width,
        int height,
        int rowsPerStrip,
        ReadOnlySpan<ulong> stripOffsets,
        ReadOnlySpan<ulong> stripByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The decode reads the strips that the image needs. A file can hold fewer strips (see CheckBlockCount). Then the rows of the missing strips keep their
        // cleared value. A file can also hold more strips. The decode calculates the strip count from the image height, as libtiff does. Thus it reads no
        // extra strip.
        int stripCount = (int)Math.Min(stripOffsets.Length, ((long)height + rowsPerStrip - 1) / rowsPerStrip);

        using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, this.BitsPerPixel, frame.Metadata);
        bool decodesPixels = decompressor.DecodesPixels;
        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        TiffBaseColorDecoder<TPixel> colorDecoder = null;
        IMemoryOwner<byte> stripBuffer = null;
        bool readRows = false;

        try
        {
            if (!decodesPixels)
            {
                // Raw decompressors return TIFF samples. Allocate their reusable buffer and select the TIFF color reader together.
                // The nested JPEG, OldJPEG and WebP decoders write TPixel directly. They need neither resource. VerifySampleBuffers made sure that the buffer
                // fits in a span.
                colorDecoder = this.CreateChunkyColorDecoder<TPixel>(frame.Metadata);
                ulong bufferSize = this.GetChunkyStripBufferSize(width, rowsPerStrip, out readRows);
                stripBuffer = this.memoryAllocator.Allocate<byte>((int)bufferSize, AllocationOptions.Clean);
            }

            Span<byte> samples = stripBuffer is null ? default : stripBuffer.GetSpan();

            for (int stripIndex = 0; stripIndex < stripCount; stripIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Each strip has RowsPerStrip rows, except the last strip of the image.
                int top = rowsPerStrip * stripIndex;
                int stripHeight = Math.Min(rowsPerStrip, height - top);

                if (readRows)
                {
                    for (int row = 0; row < stripHeight; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ulong rowStart = (ulong)row * (ulong)samples.Length;
                        ulong offset = stripOffsets[stripIndex] + rowStart;
                        ulong available = stripByteCounts[stripIndex];
                        ulong byteCount = available > rowStart ? Math.Min(available - rowStart, (ulong)samples.Length) : 0;

                        // The TIFF color reader packs each raw row; the decompressor does not write pixels on this path.
                        decompressor.Decompress<TPixel>(this.inputStream, offset, byteCount, 1, samples, default, cancellationToken);
                        colorDecoder.Decode(samples, pixels, 0, top + row, width, 1);
                    }
                }
                else
                {
                    // The final strip can be shorter than the reused buffer. The decompressor gets the samples of this
                    // strip only, so it can find data that ends early. Nested decoders get no sample buffer.
                    Span<byte> stripSamples = samples;
                    if (!decodesPixels)
                    {
                        stripSamples = stripSamples[..(int)this.CalculateUncompressedDataSize(width, stripHeight, -1)];
                    }

                    Buffer2DRegion<TPixel> destination = pixels.GetRegion(0, top, width, stripHeight);
                    decompressor.Decompress(
                        this.inputStream,
                        stripOffsets[stripIndex],
                        stripByteCounts[stripIndex],
                        stripHeight,
                        stripSamples,
                        destination,
                        cancellationToken);

                    // Nested decoders already wrote TPixel. Only raw TIFF samples still require color conversion and packing.
                    if (!decodesPixels)
                    {
                        colorDecoder.Decode(stripSamples, pixels, 0, top, width, stripHeight);
                    }
                }
            }

            // Retain the palette used for decoding, including any ICC conversion, for both full-strip and row-by-row reads.
            if (colorDecoder is PaletteTiffColor<TPixel> paletteDecoder)
            {
                frame.Metadata.GetTiffMetadata().LocalColorTable = paletteDecoder.PaletteColors;
            }
        }
        finally
        {
            colorDecoder?.Dispose();
            stripBuffer?.Dispose();
        }
    }

    /// <summary>
    /// Decompresses tiled channel planes and combines their visible samples with the TIFF color reader.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The image frame to decode data into.</param>
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
        ReadOnlySpan<ulong> tileOffsets,
        ReadOnlySpan<ulong> tileByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        Buffer2D<TPixel> pixels = frame.PixelBuffer;

        // Planar offsets are grouped by plane, TilesPerImage entries for each plane, and TilesPerImage is
        // TilesAcross * TilesDown. See TIFF 6.0 specification, section 15, TileWidth and TileOffsets, pages 67-68:
        // https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
        // A file can hold fewer offsets (see CheckBlockCount). The decode stops after the last tile that all planes have. The products use 64-bit math. A large
        // image of small tiles can hold more tiles than an int can count. The offsets are fewer than int.MaxValue. Thus tileCount and each plane index of a
        // decoded tile fit in an int.
        int channels = this.BitsPerSample.Channels;
        long tilesPerChannel = (long)tilesAcross * tilesDown;
        int tileCount = (int)Math.Clamp(tileOffsets.Length - ((channels - 1) * tilesPerChannel), 0, tilesPerChannel);

        IMemoryOwner<byte>[] tilesBuffers = new IMemoryOwner<byte>[channels];

        try
        {
            // The parser excludes nested image codecs from planar decoding. Every channel is decompressed as TIFF samples.
            using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, this.BitsPerPixel, frame.Metadata, true, tileWidth, tileLength);
            using TiffBasePlanarColorDecoder<TPixel> colorDecoder = this.CreatePlanarColorDecoder<TPixel>(frame.Metadata);

            // Tile rows are byte-aligned. Allocate one reusable buffer per plane at that plane's bit depth.
            // VerifySampleBuffers made sure that each buffer fits in a span.
            for (int channel = 0; channel < channels; channel++)
            {
                int bufferSize = (int)this.CalculateStripBufferSize(tileWidth, tileLength, channel);
                tilesBuffers[channel] = this.memoryAllocator.Allocate<byte>(bufferSize, AllocationOptions.Clean);
            }

            int tileIndex = 0;
            for (int tileY = 0; tileY < tilesDown; tileY++)
            {
                int top = tileY * tileLength;
                int visibleHeight = Math.Min(tileLength, height - top);

                for (int tileX = 0; tileX < tilesAcross; tileX++)
                {
                    // The pixels of the missing tiles keep their cleared value.
                    if (tileIndex >= tileCount)
                    {
                        return;
                    }

                    int left = tileX * tileWidth;
                    int visibleWidth = Math.Min(tileWidth, width - left);
                    int planeTileIndex = tileIndex;

                    // Tile offsets are grouped by plane, just like strip offsets.
                    for (int channel = 0; channel < channels; channel++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Chroma expansion needs more storage than the file data. The decompressor gets the samples of
                        // this tile only, so it can find data that ends early.
                        Span<byte> tileBuffer = tilesBuffers[channel].GetSpan()[..(int)this.CalculateUncompressedDataSize(tileWidth, tileLength, channel)];

                        decompressor.Decompress<TPixel>(
                            this.inputStream,
                            tileOffsets[planeTileIndex],
                            tileByteCounts[planeTileIndex],
                            tileLength,
                            tileBuffer,
                            default,
                            cancellationToken);

                        planeTileIndex += (int)tilesPerChannel;
                    }

                    if (visibleWidth < tileWidth)
                    {
                        // Planar readers advance by the requested width. Remove right-edge padding so each row begins where they expect it.
                        // Copy complete byte-aligned rows at each channel's bit depth; this also handles samples wider than eight bits.
                        for (int channel = 0; channel < channels; channel++)
                        {
                            Span<byte> samples = tilesBuffers[channel].GetSpan();
                            int sourceRowBytes = (int)this.CalculateStripBufferSize(tileWidth, 1, channel);
                            int visibleRowBytes = (int)this.CalculateStripBufferSize(visibleWidth, 1, channel);

                            // Forward copies stay before the next unread encoded row and require no second tile buffer.
                            for (int row = 1; row < visibleHeight; row++)
                            {
                                samples.Slice(row * sourceRowBytes, visibleRowBytes).CopyTo(samples.Slice(row * visibleRowBytes, visibleRowBytes));
                            }
                        }
                    }

                    colorDecoder.Decode(tilesBuffers, pixels, left, top, visibleWidth, visibleHeight);
                    tileIndex++;
                }
            }
        }
        finally
        {
            // Dispose every acquired plane even if a later allocation or decode fails.
            foreach (IMemoryOwner<byte> buffer in tilesBuffers)
            {
                buffer?.Dispose();
            }
        }
    }

    /// <summary>
    /// Decodes interleaved tiles and copies only pixels within the TIFF image bounds.
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
        ReadOnlySpan<ulong> tileOffsets,
        ReadOnlySpan<ulong> tileByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = frame.Width;
        int height = frame.Height;
        Buffer2D<TPixel> pixels = frame.PixelBuffer;

        // A file can hold fewer tiles than the image needs (see CheckBlockCount). The decode stops after the last tile. The pixels of the missing tiles keep
        // their cleared value.
        int tileCount = (int)Math.Min(tileOffsets.Length, (long)tilesAcross * tilesDown);

        using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, this.BitsPerPixel, frame.Metadata, true, tileWidth, tileLength);
        bool decodesPixels = decompressor.DecodesPixels;
        TiffBaseColorDecoder<TPixel> colorDecoder = null;
        IMemoryOwner<byte> tileBuffer = null;
        int bytesPerTileRow = 0;

        try
        {
            if (!decodesPixels)
            {
                // Raw tile rows retain their encoded width, including padding beyond the right image edge.
                // Reuse one sample buffer and TIFF color reader for all tiles. Nested decoders need neither resource.
                // VerifySampleBuffers made sure that the buffer fits in a span.
                colorDecoder = this.CreateChunkyColorDecoder<TPixel>(frame.Metadata);
                ulong tileBufferSize = GetTileBufferSize(tileWidth, tileLength, this.BitsPerPixel, out bytesPerTileRow);
                tileBuffer = this.memoryAllocator.Allocate<byte>((int)tileBufferSize, AllocationOptions.Clean);
            }

            Span<byte> samples = tileBuffer is null ? default : tileBuffer.GetSpan();

            // The tiles are in row order: tile index = tileY * tilesAcross + tileX. See TIFF 6.0 specification, section 15, TileOffsets, page 68.
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int tileY = tileIndex / tilesAcross;
                int top = tileY * tileLength;
                int left = (tileIndex - (tileY * tilesAcross)) * tileWidth;
                int visibleHeight = Math.Min(tileLength, height - top);
                int visibleWidth = Math.Min(tileWidth, width - left);

                // Decode the full encoded tile, but give only its visible destination to the nested decoder.
                Buffer2DRegion<TPixel> destination = pixels.GetRegion(left, top, visibleWidth, visibleHeight);
                decompressor.Decompress(
                    this.inputStream,
                    tileOffsets[tileIndex],
                    tileByteCounts[tileIndex],
                    tileLength,
                    samples,
                    destination,
                    cancellationToken);

                if (!decodesPixels)
                {
                    // Convert only the visible samples. Move forward by the full encoded row. Then the decoder skips the edge padding and does not decode it as
                    // pixels.
                    int visibleRowBytes = (int)RoundUpToMultipleOfEight((long)visibleWidth * this.BitsPerPixel);
                    int offset = 0;

                    for (int row = 0; row < visibleHeight; row++)
                    {
                        colorDecoder.Decode(samples.Slice(offset, visibleRowBytes), pixels, left, top + row, visibleWidth, 1);
                        offset += bytesPerTileRow;
                    }
                }
            }

            // Retain the actual lookup used by the TIFF palette reader, including converted colors.
            if (colorDecoder is PaletteTiffColor<TPixel> paletteDecoder)
            {
                frame.Metadata.GetTiffMetadata().LocalColorTable = paletteDecoder.PaletteColors;
            }
        }
        finally
        {
            colorDecoder?.Dispose();
            tileBuffer?.Dispose();
        }
    }

    /// <summary>
    /// Creates a packed-sample reader and configures its ICC conversion before any destination pixels are packed.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="metadata">The frame metadata with its source ICC profile.</param>
    /// <returns>The reader owned by the current strip or tile traversal.</returns>
    private TiffBaseColorDecoder<TPixel> CreateChunkyColorDecoder<TPixel>(ImageFrameMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TiffBaseColorDecoder<TPixel> decoder = TiffColorDecoderFactory<TPixel>.Create(
            metadata,
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

        this.ConfigureColorConversion(decoder, metadata);

        return decoder;
    }

    /// <summary>
    /// Creates a planar-sample reader and configures its ICC conversion before any destination pixels are packed.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="metadata">The frame metadata with its source ICC profile.</param>
    /// <returns>The reader owned by the current strip or tile traversal.</returns>
    private TiffBasePlanarColorDecoder<TPixel> CreatePlanarColorDecoder<TPixel>(ImageFrameMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TiffBasePlanarColorDecoder<TPixel> decoder = TiffColorDecoderFactory<TPixel>.CreatePlanar(
            metadata,
            this.configuration,
            this.memoryAllocator,
            this.ColorType,
            this.BitsPerSample,
            this.ExtraSamplesType,
            this.ColorMap,
            this.ReferenceBlackAndWhite,
            this.YcbcrCoefficients,
            this.YcbcrSubSampling,
            this.byteOrder);

        this.ConfigureColorConversion(decoder, metadata);

        return decoder;
    }

    /// <summary>
    /// Limits the strip or tile offsets and byte counts to the blocks that have both values. A block needs its offset and
    /// its byte count, so a block that has only one of them is not in the file.
    /// </summary>
    /// <param name="offsets">The block offsets.</param>
    /// <param name="byteCounts">The block byte counts.</param>
    private static void PairOffsets(ref Span<ulong> offsets, ref Span<ulong> byteCounts)
    {
        int count = Math.Min(offsets.Length, byteCounts.Length);
        offsets = offsets[..count];
        byteCounts = byteCounts[..count];
    }

    /// <summary>
    /// Reports a layout with fewer strips or tiles than the image needs. The planar offsets have one group of <paramref name="blocksPerPlane"/> entries for
    /// each plane. See TIFF 6.0 specification, section 8, StripOffsets, page 40, and section 15, TileOffsets, page 68.
    /// </summary>
    /// <remarks>
    /// In the IgnoreImageData mode, the decode reads the blocks that are in the file. The pixels of the missing blocks keep their cleared value. In the other
    /// modes, this method throws an exception.
    /// </remarks>
    /// <param name="blockCount">The count of the blocks with an offset and a byte count.</param>
    /// <param name="blocksPerPlane">The count of the blocks that one plane of the image needs.</param>
    private void CheckBlockCount(int blockCount, long blocksPerPlane)
    {
        long required = this.PlanarConfiguration == TiffPlanarConfiguration.Planar ? blocksPerPlane * this.BitsPerSample.Channels : blocksPerPlane;
        if (blockCount < required)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The TIFF image has fewer strips or tiles than its layout needs.");
        }
    }

    /// <summary>
    /// Gets the byte count of the sample buffer of chunky strips.
    /// </summary>
    /// <remarks>
    /// The decoder can read only uncompressed strips by row, because each row has a known byte offset. If a strip is larger than the single-buffer limit of the
    /// allocator, the decoder reads one row at a time. Thus the decoder does not allocate the full strip.
    /// </remarks>
    /// <param name="width">The width of the frame.</param>
    /// <param name="rowsPerStrip">The row count of a strip.</param>
    /// <param name="readRows">The method sets this value to <see langword="true"/> if the decoder reads one row at a time.</param>
    /// <returns>The byte count of the buffer.</returns>
    private ulong GetChunkyStripBufferSize(int width, int rowsPerStrip, out bool readRows)
    {
        ulong size = this.CalculateStripBufferSize(width, rowsPerStrip);
        readRows = this.CompressionType == TiffDecoderCompressionType.None
            && size > (ulong)this.memoryAllocator.SingleBufferAllocationLimitBytes
            && this.CanReadStripRows();

        return readRows ? this.CalculateStripBufferSize(width, 1) : size;
    }

    /// <summary>
    /// Gets the row count that the decoder reads at one time from planar strips.
    /// </summary>
    /// <remarks>
    /// Uncompressed rows have fixed offsets. If a plane of a strip is larger than the single-buffer limit of the allocator, the decoder reads one row at a
    /// time.
    /// </remarks>
    /// <param name="width">The width of the frame.</param>
    /// <param name="rowsPerStrip">The row count of a strip.</param>
    /// <returns>1 if the decoder reads one row at a time. Otherwise, <paramref name="rowsPerStrip"/>.</returns>
    private int GetPlanarRowsPerRead(int width, int rowsPerStrip)
    {
        if (this.CompressionType == TiffDecoderCompressionType.None && this.CanReadStripRows())
        {
            for (int channel = 0; channel < this.BitsPerSample.Channels; channel++)
            {
                if (this.CalculateStripBufferSize(width, rowsPerStrip, channel) > (ulong)this.memoryAllocator.SingleBufferAllocationLimitBytes)
                {
                    return 1;
                }
            }
        }

        return rowsPerStrip;
    }

    /// <summary>
    /// Enables ICC conversion at the ordinary RGB or grayscale reader's floating row boundary.
    /// </summary>
    /// <param name="decoder">The reader that owns the reusable conversion rows.</param>
    /// <param name="metadata">The frame metadata with its source ICC profile.</param>
    private void ConfigureColorConversion(TiffBaseColorDecoder decoder, ImageFrameMetadata metadata)
    {
        bool grayscaleOrRgb = this.PhotometricInterpretation is TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero
            || this.ColorType is > TiffColorType.PaletteColor and <= TiffColorType.Rgba32323232Planar;

        // Palette, Lab, CMYK and YCbCr readers convert their native components themselves. Nested codecs also own ICC conversion.
        // Ordinary RGB and grayscale readers must transform native samples before TPixel can discard precision or color channels.
        if (!this.UsesNestedDecoder && grayscaleOrRgb)
        {
            decoder.ConfigureColorConversion(this.configuration, metadata.IccProfile);
        }
    }

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
    private ulong CalculateStripBufferSize(int width, int height, int plane = -1)
    {
        DebugGuard.MustBeLessThanOrEqualTo(plane, 3, nameof(plane));

        int bitsPerPixel = 0;

        if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
        {
            DebugGuard.IsTrue(plane == -1, "Expected Chunky planar.");

            // Subsampled YCbCr data has its own size. The decompressors fill the sample buffer with the samples of the
            // strip, so the buffer has that size for each compression.
            if (!this.CanReadStripRows())
            {
                return this.CalculateUncompressedDataSize(width, height, plane);
            }

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
                default:
                    // TiffBitsPerSample holds at most four channels, so the plane index is at most 3.
                    bitsPerPixel = this.BitsPerSample.Channel3;
                    break;
            }
        }

        // A row of more than int.MaxValue bytes is more than the span limit (see VerifySampleBuffers). For a smaller row, the product fits in 64 bits.
        ulong bytesPerRow = (((ulong)width * (ulong)bitsPerPixel) + 7) / 8;
        return bytesPerRow > int.MaxValue ? ulong.MaxValue : bytesPerRow * (ulong)height;
    }

    /// <summary>
    /// Calculates the encoded byte count for an uncompressed strip, tile, or individual row.
    /// </summary>
    /// <param name="width">The stored row width, including tile padding when applicable.</param>
    /// <param name="height">The number of stored rows.</param>
    /// <param name="plane">The planar component index, or -1 for chunky data.</param>
    /// <returns>The number of encoded bytes required for these samples.</returns>
    private ulong CalculateUncompressedDataSize(int width, int height, int plane)
    {
        if (this.CanReadStripRows())
        {
            return this.CalculateStripBufferSize(width, height, plane);
        }

        ulong horizontal = this.YcbcrSubSampling[0];
        ulong vertical = this.YcbcrSubSampling[1];
        ulong groupsAcross = ((ulong)width + horizontal - 1) / horizontal;
        ulong groupsDown = ((ulong)height + vertical - 1) / vertical;

        // Chunky YCbCr stores horizontal * vertical luminance samples, then one Cb and one Cr sample per group.
        // Planar YCbCr stores full-resolution luminance and one sample per group in each separate chroma plane.
        // Chroma expansion needs larger plane buffers, but those extra bytes are not required in the file.
        return this.PlanarConfiguration == TiffPlanarConfiguration.Chunky
            ? groupsAcross * groupsDown * ((horizontal * vertical) + 2)
            : plane == 0 ? (ulong)width * (ulong)height : groupsAcross * groupsDown;
    }

    /// <summary>
    /// Determines whether each uncompressed row can be read and decoded separately.
    /// </summary>
    /// <returns><see langword="true"/> if the rows of a strip can be decoded one at a time.</returns>
    private bool CanReadStripRows()
    {
        // Subsampled YCbCr groups can span rows. Read the complete strip before expanding its chroma samples.
        return this.PhotometricInterpretation != TiffPhotometricInterpretation.YCbCr
            || this.YcbcrSubSampling is null
            || (this.YcbcrSubSampling[0] == 1 && this.YcbcrSubSampling[1] == 1);
    }

    /// <summary>
    /// Converts a bit count to the number of bytes needed to store it.
    /// </summary>
    /// <param name="value">The number of bits.</param>
    /// <returns>The number of bytes, rounded up.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long RoundUpToMultipleOfEight(long value) => (value + 7) / 8;

    /// <summary>
    /// Gets the byte count of a tile with byte-aligned rows.
    /// </summary>
    /// <param name="tileWidth">The encoded tile width in pixels.</param>
    /// <param name="tileLength">The encoded tile height in rows.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="bytesPerTileRow">The encoded row width in bytes, including padding.</param>
    /// <returns>The required tile buffer length in bytes.</returns>
    private static ulong GetTileBufferSize(int tileWidth, int tileLength, int bitsPerPixel, out int bytesPerTileRow)
    {
        long rowBytes = RoundUpToMultipleOfEight((long)tileWidth * bitsPerPixel);

        // A row of more than int.MaxValue bytes is more than the span limit (see VerifySampleBuffers). For a smaller row, the product fits in 64 bits.
        bytesPerTileRow = (int)Math.Min(rowBytes, int.MaxValue);
        return rowBytes > int.MaxValue ? ulong.MaxValue : (ulong)rowBytes * (ulong)tileLength;
    }
}

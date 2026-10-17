// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
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

    /// <summary>
    /// Gets a value indicating whether the compression contains an image whose decoder owns color conversion and pixel packing.
    /// </summary>
    private bool UsesNestedDecoder
        => this.CompressionType is TiffDecoderCompressionType.Jpeg or TiffDecoderCompressionType.OldJpeg or TiffDecoderCompressionType.Webp;

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

            if (meta.TiffMetadata.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor)
            {
                if (!directories[i].TryGetValue(ExifTag.ColorMap, out IExifValue<ushort[]> colorMap))
                {
                    TiffThrowHelper.ThrowNotSupported("The TIFF ColorMap entry is missing for a palette color image.");
                }

                // Identify has no destination pixel format; RgbaVector preserves converted colors as unassociated floating-point values.
                Color[] palette = TiffUtilities.DecodeColorMap<RgbaVector>(
                    colorMap.Value,
                    meta.TiffMetadata.BitsPerSample,
                    meta.FrameMetadata.IccProfile,
                    this.memoryAllocator);

                meta.TiffMetadata.LocalColorTable = palette;
            }

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

        bool grayscaleOrRgb = this.PhotometricInterpretation is TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero
            || this.ColorType is > TiffColorType.PaletteColor and <= TiffColorType.Rgba32323232Planar;

        // JPEG, OldJPEG and WebP already apply ICC conversion.
        // TIFF applies it here for its own grayscale and RGB decoding.
        if (!this.UsesNestedDecoder && grayscaleOrRgb)
        {
            _ = TryConvertIccProfile(frame);
        }

        return frame;
    }

    private (ImageFrameMetadata FrameMetadata, TiffFrameMetadata TiffMetadata) CreateFrameMetadata(ExifProfile tags)
    {
        ImageFrameMetadata imageFrameMetaData = new();
        if (!this.skipMetadata)
        {
            imageFrameMetaData.ExifProfile = tags;
        }

        // ICC is needed for pixel conversion even when optional EXIF metadata is skipped.
        if (tags.TryGetValue(ExifTag.IccProfile, out IExifValue<byte[]> iccProfileBytes))
        {
            this.ExecuteAncillarySegmentAction(
                () =>
                {
                    using MemoryStream profileStream = new(iccProfileBytes.Value);
                    IccReader reader = new();

                    reader.Read(profileStream, iccProfileBytes.Value.Length);

                    imageFrameMetaData.IccProfile = reader.ToProfile();
                });
        }
        else if (tags.InvalidTags.Contains(ExifTag.IccProfile))
        {
            // Oversized or malformed ICC tags are rejected before EXIF allocates their payload.
            this.ExecuteAncillarySegmentAction(() => throw new InvalidIccProfileException("Invalid TIFF ICC profile."));
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
        // The default RowsPerStrip value denotes one strip per plane, not an allocation of that many rows.
        if (rowsPerStrip == TiffConstants.RowsPerStripInfinity)
        {
            rowsPerStrip = height;
        }

        int channels = this.BitsPerSample.Channels;
        int stripsPerPlane = stripOffsets.Length / channels;
        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        IMemoryOwner<byte>[] stripBuffers = new IMemoryOwner<byte>[channels];

        try
        {
            // Planar JPEG, OldJPEG and WebP are rejected by the options parser. These buffers always receive raw channel samples.
            using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, this.BitsPerPixel, frame.Metadata);
            TiffBasePlanarColorDecoder<TPixel> colorDecoder = this.CreatePlanarColorDecoder<TPixel>(frame.Metadata);

            // Reuse one buffer per channel. Each plane's own bit depth determines its byte count.
            for (int channel = 0; channel < channels; channel++)
            {
                ulong bufferSize = this.CalculateStripBufferSize(width, rowsPerStrip, channel);

                // Each channel buffer is exposed as a Span<byte>, so its length must fit in an int.
                if (bufferSize > int.MaxValue)
                {
                    TiffThrowHelper.ThrowNotSupported("The TIFF sample buffer exceeds Int32.MaxValue bytes.");
                }

                stripBuffers[channel] = this.memoryAllocator.Allocate<byte>((int)bufferSize);
            }

            for (int strip = 0; strip < stripsPerPlane; strip++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int stripHeight = strip < stripsPerPlane - 1 || height % rowsPerStrip == 0 ? rowsPerStrip : height % rowsPerStrip;
                int stripIndex = strip;

                // TIFF groups offsets by plane. The same spatial strip in the next channel is stripsPerPlane entries later.
                for (int channel = 0; channel < channels; channel++)
                {
                    decompressor.Decompress<TPixel>(
                        this.inputStream,
                        stripOffsets[stripIndex],
                        stripByteCounts[stripIndex],
                        stripHeight,
                        stripBuffers[channel].GetSpan(),
                        default,
                        cancellationToken);

                    stripIndex += stripsPerPlane;
                }

                // All channel planes are available now; the TIFF reader owns their color conversion and TPixel packing.
                colorDecoder.Decode(stripBuffers, pixels, 0, rowsPerStrip * strip, width, stripHeight);
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
    /// <param name="frame">The image frame to decode data into.</param>
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
        // An omitted RowsPerStrip tag means that one strip contains the entire image.
        if (rowsPerStrip == TiffConstants.RowsPerStripInfinity)
        {
            rowsPerStrip = height;
        }

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
                // Nested JPEG, OldJPEG and WebP decoders write TPixel directly and need neither resource.
                colorDecoder = this.CreateChunkyColorDecoder<TPixel>(frame.Metadata);
                ulong bufferSize = this.CalculateStripBufferSize(width, rowsPerStrip);

                // Only uncompressed strips can be read by row: each row has a known byte offset.
                // This preserves support for strips larger than a Span can hold without allocating the entire strip.
                readRows = decompressor is NoneTiffCompression && bufferSize > int.MaxValue;
                if (readRows)
                {
                    bufferSize = this.CalculateStripBufferSize(width, 1);
                }

                // A raw sample buffer must fit in Span<byte>, whose length is an int. Nested pixel buffers do not use this limit.
                if (bufferSize > int.MaxValue)
                {
                    TiffThrowHelper.ThrowNotSupported("The TIFF sample buffer exceeds Int32.MaxValue bytes.");
                }

                stripBuffer = this.memoryAllocator.Allocate<byte>((int)bufferSize, AllocationOptions.Clean);
            }

            Span<byte> samples = stripBuffer is null ? default : stripBuffer.GetSpan();

            for (int stripIndex = 0; stripIndex < stripOffsets.Length; stripIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int stripHeight = stripIndex < stripOffsets.Length - 1 || height % rowsPerStrip == 0
                    ? rowsPerStrip
                    : height % rowsPerStrip;

                int top = rowsPerStrip * stripIndex;
                if (top + stripHeight > height)
                {
                    // Ignore surplus strips once their rows fall outside this frame.
                    break;
                }

                if (readRows)
                {
                    // A short uncompressed strip must not read into the following strip. Retain the existing partial-image behavior.
                    if (stripByteCounts[stripIndex] < (ulong)samples.Length * (ulong)stripHeight)
                    {
                        break;
                    }

                    for (int row = 0; row < stripHeight; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ulong offset = stripOffsets[stripIndex] + ((ulong)row * (ulong)samples.Length);

                        // The TIFF color reader packs each raw row; the decompressor does not write pixels on this path.
                        decompressor.Decompress<TPixel>(this.inputStream, offset, (ulong)samples.Length, 1, samples, default, cancellationToken);
                        colorDecoder.Decode(samples, pixels, 0, top + row, width, 1);
                    }
                }
                else
                {
                    Buffer2DRegion<TPixel> destination = pixels.GetRegion(0, top, width, stripHeight);
                    decompressor.Decompress(
                        this.inputStream, stripOffsets[stripIndex], stripByteCounts[stripIndex], stripHeight, samples, destination, cancellationToken);

                    // Nested decoders already wrote TPixel. Only raw TIFF samples still require color conversion and packing.
                    if (!decodesPixels)
                    {
                        colorDecoder.Decode(samples, pixels, 0, top, width, stripHeight);
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
            stripBuffer?.Dispose();
        }
    }

    /// <summary>
    /// Decompresses tiled channel planes and combines their visible samples with the TIFF color reader.
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
        ReadOnlySpan<ulong> tileOffsets,
        ReadOnlySpan<ulong> tileByteCounts,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        int width = pixels.Width;
        int height = pixels.Height;

        int channels = this.BitsPerSample.Channels;
        int tilesPerChannel = tileOffsets.Length / channels;
        IMemoryOwner<byte>[] tilesBuffers = new IMemoryOwner<byte>[channels];

        try
        {
            // The parser excludes nested image codecs from planar decoding. Every channel is decompressed as TIFF samples.
            using TiffBaseDecompressor decompressor = this.CreateDecompressor<TPixel>(width, this.BitsPerPixel, frame.Metadata, true, tileWidth, tileLength);
            TiffBasePlanarColorDecoder<TPixel> colorDecoder = this.CreatePlanarColorDecoder<TPixel>(frame.Metadata);

            // Tile rows are byte-aligned. Allocate one reusable buffer per plane at that plane's bit depth.
            for (int channel = 0; channel < channels; channel++)
            {
                ulong bufferSize = this.CalculateStripBufferSize(tileWidth, tileLength, channel);

                // Each plane must fit in the Span<byte> consumed by the decompressor and color reader.
                if (bufferSize > int.MaxValue)
                {
                    TiffThrowHelper.ThrowNotSupported("The TIFF sample buffer exceeds Int32.MaxValue bytes.");
                }

                tilesBuffers[channel] = this.memoryAllocator.Allocate<byte>((int)bufferSize, AllocationOptions.Clean);
            }

            int tileIndex = 0;
            for (int tileY = 0; tileY < tilesDown; tileY++)
            {
                int top = tileY * tileLength;
                int visibleHeight = Math.Min(tileLength, height - top);

                for (int tileX = 0; tileX < tilesAcross; tileX++)
                {
                    int left = tileX * tileWidth;
                    int visibleWidth = Math.Min(tileWidth, width - left);
                    int planeTileIndex = tileIndex;

                    // Tile offsets are grouped by plane, just like strip offsets.
                    for (int channel = 0; channel < channels; channel++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        decompressor.Decompress<TPixel>(
                            this.inputStream,
                            tileOffsets[planeTileIndex],
                            tileByteCounts[planeTileIndex],
                            tileLength,
                            tilesBuffers[channel].GetSpan(),
                            default,
                            cancellationToken);

                        planeTileIndex += tilesPerChannel;
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
        Buffer2D<TPixel> pixels = frame.PixelBuffer;
        int width = pixels.Width;
        int height = pixels.Height;

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
                colorDecoder = this.CreateChunkyColorDecoder<TPixel>(frame.Metadata);
                bytesPerTileRow = RoundUpToMultipleOfEight(tileWidth * this.BitsPerPixel);
                tileBuffer = this.memoryAllocator.Allocate<byte>(bytesPerTileRow * tileLength, AllocationOptions.Clean);
            }

            Span<byte> samples = tileBuffer is null ? default : tileBuffer.GetSpan();
            int tileIndex = 0;

            for (int tileY = 0; tileY < tilesDown; tileY++)
            {
                int top = tileY * tileLength;
                int visibleHeight = Math.Min(tileLength, height - top);

                for (int tileX = 0; tileX < tilesAcross; tileX++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int left = tileX * tileWidth;
                    int visibleWidth = Math.Min(tileWidth, width - left);

                    // Decode the full encoded tile, but expose only its visible destination to the nested decoder.
                    Buffer2DRegion<TPixel> destination = pixels.GetRegion(left, top, visibleWidth, visibleHeight);
                    decompressor.Decompress(
                        this.inputStream, tileOffsets[tileIndex], tileByteCounts[tileIndex], tileLength, samples, destination, cancellationToken);

                    if (!decodesPixels)
                    {
                        // Convert visible samples only. Advance by the full encoded row so edge padding is skipped, not decoded as pixels.
                        int visibleRowBytes = RoundUpToMultipleOfEight(visibleWidth * this.BitsPerPixel);
                        int offset = 0;

                        for (int row = 0; row < visibleHeight; row++)
                        {
                            colorDecoder.Decode(samples.Slice(offset, visibleRowBytes), pixels, left, top + row, visibleWidth, 1);
                            offset += bytesPerTileRow;
                        }
                    }

                    tileIndex++;
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
            tileBuffer?.Dispose();
        }
    }

    private TiffBaseColorDecoder<TPixel> CreateChunkyColorDecoder<TPixel>(ImageFrameMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TiffColorDecoderFactory<TPixel>.Create(
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

    private TiffBasePlanarColorDecoder<TPixel> CreatePlanarColorDecoder<TPixel>(ImageFrameMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel> =>
        TiffColorDecoderFactory<TPixel>.CreatePlanar(
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
    private ulong CalculateStripBufferSize(int width, int height, int plane = -1)
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

        ulong bytesPerRow = (((ulong)width * (ulong)bitsPerPixel) + 7) / 8;
        return bytesPerRow * (ulong)height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundUpToMultipleOfEight(int value) => (int)(((uint)value + 7) / 8);
}

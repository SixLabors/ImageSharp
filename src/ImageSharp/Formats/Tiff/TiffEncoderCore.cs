// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.Writers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Performs the TIFF encoding operation.
/// </summary>
internal sealed class TiffEncoderCore
{
    private static readonly ushort ByteOrderMarker = BitConverter.IsLittleEndian
            ? TiffConstants.ByteOrderLittleEndianShort
            : TiffConstants.ByteOrderBigEndianShort;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private Configuration configuration;

    /// <summary>
    /// The quantizer for creating color palette images.
    /// </summary>
    private readonly IQuantizer quantizer;

    /// <summary>
    /// The pixel sampling strategy for quantization.
    /// </summary>
    private readonly IPixelSamplingStrategy pixelSamplingStrategy;

    /// <summary>
    /// Sets the deflate compression level.
    /// </summary>
    private readonly DeflateCompressionLevel compressionLevel;

    /// <summary>
    /// The default predictor is None.
    /// </summary>
    private const TiffPredictor DefaultPredictor = TiffPredictor.None;

    /// <summary>
    /// The default bits per pixel is Bit24.
    /// </summary>
    private const TiffBitsPerPixel DefaultBitsPerPixel = TiffBitsPerPixel.Bit24;

    /// <summary>
    /// The default compression is None.
    /// </summary>
    private const TiffCompression DefaultCompression = TiffCompression.None;

    /// <summary>
    /// The default photometric interpretation is Rgb.
    /// </summary>
    private const TiffPhotometricInterpretation DefaultPhotometricInterpretation = TiffPhotometricInterpretation.Rgb;

    /// <summary>
    /// Whether to skip metadata during encoding.
    /// </summary>
    private readonly bool skipMetadata;

    private readonly List<(long, uint)> frameMarkers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
    /// </summary>
    /// <param name="options">The options for the encoder.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    public TiffEncoderCore(TiffEncoder options, MemoryAllocator memoryAllocator)
    {
        this.memoryAllocator = memoryAllocator;
        this.PhotometricInterpretation = options.PhotometricInterpretation;
        this.quantizer = options.Quantizer ?? KnownQuantizers.Octree;
        this.pixelSamplingStrategy = options.PixelSamplingStrategy;
        this.BitsPerPixel = options.BitsPerPixel;
        this.HorizontalPredictor = options.HorizontalPredictor;
        this.CompressionType = options.Compression;
        this.compressionLevel = options.CompressionLevel ?? DeflateCompressionLevel.DefaultCompression;
        this.skipMetadata = options.SkipMetadata;
    }

    /// <summary>
    /// Gets the photometric interpretation implementation to use when encoding the image.
    /// </summary>
    internal TiffPhotometricInterpretation? PhotometricInterpretation { get; private set; }

    /// <summary>
    /// Gets or sets the compression implementation to use when encoding the image.
    /// </summary>
    internal TiffCompression? CompressionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating which horizontal predictor to use. This can improve the compression ratio with deflate compression.
    /// </summary>
    internal TiffPredictor? HorizontalPredictor { get; set; }

    /// <summary>
    /// Gets the bits per pixel.
    /// </summary>
    internal TiffBitsPerPixel? BitsPerPixel { get; private set; }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        this.configuration = image.Configuration;

        ImageFrameMetadata rootFrameMetaData = image.Frames.RootFrame.Metadata;
        TiffFrameMetadata rootFrameTiffMetaData = rootFrameMetaData.GetTiffMetadata();

        // Determine the correct values to encode with.
        // EncoderOptions > Metadata > Default.
        TiffBitsPerPixel? bitsPerPixel = this.BitsPerPixel ?? rootFrameTiffMetaData.BitsPerPixel;

        TiffPhotometricInterpretation? photometricInterpretation = this.PhotometricInterpretation ?? rootFrameTiffMetaData.PhotometricInterpretation;

        TiffPredictor predictor =
            this.HorizontalPredictor
            ?? rootFrameTiffMetaData.Predictor
            ?? DefaultPredictor;

        TiffCompression compression =
            this.CompressionType
            ?? rootFrameTiffMetaData.Compression
            ?? DefaultCompression;

        // Make sure, the Encoder options makes sense in combination with each other.
        this.SanitizeAndSetEncoderOptions(bitsPerPixel, image.PixelType.BitsPerPixel, photometricInterpretation, compression, predictor);

        using TiffStreamWriter writer = new(stream);
        Span<byte> buffer = stackalloc byte[4];

        long ifdMarker = WriteHeader(writer, buffer);

        Image<TPixel> metadataImage = image;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ifdMarker = this.WriteFrame(writer, frame, image.Metadata, metadataImage, ifdMarker);
            metadataImage = null;
        }

        long currentOffset = writer.BaseStream.Position;
        foreach ((long, uint) marker in this.frameMarkers)
        {
            writer.WriteMarkerFast(marker.Item1, marker.Item2, buffer);
        }

        writer.BaseStream.Seek(currentOffset, SeekOrigin.Begin);
    }

    /// <summary>
    /// Writes the TIFF file header.
    /// </summary>
    /// <param name="writer">The <see cref="TiffStreamWriter" /> to write data to.</param>
    /// <param name="buffer">Scratch buffer with minimum size of 2.</param>
    /// <returns>
    /// The marker to write the first IFD offset.
    /// </returns>
    public static long WriteHeader(TiffStreamWriter writer, Span<byte> buffer)
    {
        writer.Write(ByteOrderMarker, buffer);
        writer.Write(TiffConstants.HeaderMagicNumber, buffer);
        return writer.PlaceMarker(buffer);
    }

    /// <summary>
    /// Writes all data required to define an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="writer">The <see cref="BinaryWriter" /> to write data to.</param>
    /// <param name="frame">The tiff frame.</param>
    /// <param name="imageMetadata">The image metadata (resolution values for each frame).</param>
    /// <param name="image">The image (common metadata for root frame).</param>
    /// <param name="ifdOffset">The marker to write this IFD offset.</param>
    /// <returns>
    /// The next IFD offset value.
    /// </returns>
    private long WriteFrame<TPixel>(
        TiffStreamWriter writer,
        ImageFrame<TPixel> frame,
        ImageMetadata imageMetadata,
        Image<TPixel> image,
        long ifdOffset)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        TiffEncoderEntriesCollector entriesCollector = new();
        using TiffBaseColorWriter<TPixel> colorWriter = TiffColorWriterFactory.Create(
            this.PhotometricInterpretation,
            frame,
            this.quantizer,
            this.pixelSamplingStrategy,
            this.memoryAllocator,
            this.configuration,
            entriesCollector,
            (int)this.BitsPerPixel);

        using TiffBaseCompressor compressor = TiffCompressorFactory.Create(
            this.CompressionType ?? TiffCompression.None,
            writer.BaseStream,
            this.memoryAllocator,
            frame.Width,
            colorWriter.BitsPerPixel,
            this.compressionLevel,
            this.HorizontalPredictor == TiffPredictor.Horizontal ? this.HorizontalPredictor.Value : TiffPredictor.None);

        int rowsPerStrip = CalcRowsPerStrip(frame.Height, colorWriter.BytesPerRow, this.CompressionType);

        colorWriter.Write(compressor, rowsPerStrip);

        if (image != null)
        {
            // Write the metadata for the root image
            entriesCollector.ProcessMetadata(image, this.skipMetadata);
        }

        // Write the metadata for the frame
        entriesCollector.ProcessMetadata(frame, this.skipMetadata);

        entriesCollector.ProcessFrameInfo(frame, imageMetadata);
        entriesCollector.ProcessImageFormat(this);

        if (writer.Position % 2 != 0)
        {
            // Write padding byte, because the tiff spec requires ifd offset to begin on a word boundary.
            writer.Write(0);
        }

        this.frameMarkers.Add((ifdOffset, (uint)writer.Position));

        return this.WriteIfd(writer, entriesCollector.Entries);
    }

    /// <summary>
    /// Calculates the number of rows written per strip.
    /// </summary>
    /// <param name="height">The height of the image.</param>
    /// <param name="bytesPerRow">The number of bytes per row.</param>
    /// <param name="compression">The compression used.</param>
    /// <returns>Number of rows per strip.</returns>
    private static int CalcRowsPerStrip(int height, int bytesPerRow, TiffCompression? compression)
    {
        DebugGuard.MustBeGreaterThan(height, 0, nameof(height));
        DebugGuard.MustBeGreaterThan(bytesPerRow, 0, nameof(bytesPerRow));

        // Jpeg compressed images should be written in one strip.
        if (compression is TiffCompression.Jpeg)
        {
            return height;
        }

        // If compression is used, change stripSizeInBytes heuristically to a larger value to not write to many strips.
        int stripSizeInBytes = compression is TiffCompression.Deflate || compression is TiffCompression.Lzw ? TiffConstants.DefaultStripSize * 2 : TiffConstants.DefaultStripSize;
        int rowsPerStrip = stripSizeInBytes / bytesPerRow;

        if (rowsPerStrip > 0)
        {
            if (rowsPerStrip < height)
            {
                return rowsPerStrip;
            }

            return height;
        }

        return 1;
    }

    /// <summary>
    /// Writes a TIFF IFD block.
    /// </summary>
    /// <param name="writer">The <see cref="BinaryWriter"/> to write data to.</param>
    /// <param name="entries">The IFD entries to write to the file.</param>
    /// <returns>The marker to write the next IFD offset (if present).</returns>
    private long WriteIfd(TiffStreamWriter writer, List<IExifValue> entries)
    {
        if (entries.Count == 0)
        {
            TiffThrowHelper.ThrowArgumentException("There must be at least one entry per IFD.");
        }

        uint dataOffset = (uint)writer.Position + (uint)(6 + (entries.Count * 12));
        List<byte[]> largeDataBlocks = new();

        entries.Sort((a, b) => (ushort)a.Tag - (ushort)b.Tag);

        Span<byte> buffer = stackalloc byte[4];

        writer.Write((ushort)entries.Count, buffer);

        foreach (IExifValue entry in entries)
        {
            writer.Write((ushort)entry.Tag, buffer);
            writer.Write((ushort)entry.DataType, buffer);
            writer.Write(ExifWriter.GetNumberOfComponents(entry), buffer);

            uint length = ExifWriter.GetLength(entry);
            if (length <= 4)
            {
                int sz = ExifWriter.WriteValue(entry, buffer, 0);
                DebugGuard.IsTrue(sz == length, "Incorrect number of bytes written");
                writer.WritePadded(buffer[..sz]);
            }
            else
            {
                byte[] raw = new byte[length];
                int sz = ExifWriter.WriteValue(entry, raw, 0);
                DebugGuard.IsTrue(sz == raw.Length, "Incorrect number of bytes written");
                largeDataBlocks.Add(raw);
                writer.Write(dataOffset, buffer);
                dataOffset += (uint)(raw.Length + (raw.Length % 2));
            }
        }

        long nextIfdMarker = writer.PlaceMarker(buffer);

        foreach (byte[] dataBlock in largeDataBlocks)
        {
            writer.Write(dataBlock);

            if (dataBlock.Length % 2 == 1)
            {
                writer.Write(0);
            }
        }

        return nextIfdMarker;
    }

    private void SanitizeAndSetEncoderOptions(
        TiffBitsPerPixel? bitsPerPixel,
        int inputBitsPerPixel,
        TiffPhotometricInterpretation? photometricInterpretation,
        TiffCompression compression,
        TiffPredictor predictor)
    {
        // BitsPerPixel should be the primary source of truth for the encoder options.
        if (bitsPerPixel.HasValue)
        {
            switch (bitsPerPixel)
            {
                case TiffBitsPerPixel.Bit1:
                    if (IsOneBitCompression(compression))
                    {
                        // The “normal” PhotometricInterpretation for bilevel CCITT compressed data is WhiteIsZero.
                        this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.WhiteIsZero, compression, TiffPredictor.None);
                        break;
                    }

                    this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.BlackIsZero, compression, TiffPredictor.None);
                    break;
                case TiffBitsPerPixel.Bit4:
                    this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.PaletteColor, compression, TiffPredictor.None);
                    break;
                case TiffBitsPerPixel.Bit8:
                    this.SetEncoderOptions(bitsPerPixel, photometricInterpretation ?? TiffPhotometricInterpretation.BlackIsZero, compression, predictor);
                    break;
                case TiffBitsPerPixel.Bit16:
                    // Assume desire to encode as L16 grayscale
                    this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.BlackIsZero, compression, predictor);
                    break;
                case TiffBitsPerPixel.Bit6:
                case TiffBitsPerPixel.Bit10:
                case TiffBitsPerPixel.Bit12:
                case TiffBitsPerPixel.Bit14:
                case TiffBitsPerPixel.Bit30:
                case TiffBitsPerPixel.Bit36:
                case TiffBitsPerPixel.Bit42:
                case TiffBitsPerPixel.Bit48:
                    // Encoding not yet supported bits per pixel will default to 24 bits.
                    this.SetEncoderOptions(TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, compression, TiffPredictor.None);
                    break;
                case TiffBitsPerPixel.Bit64:
                    // Encoding not yet supported bits per pixel will default to 32 bits.
                    this.SetEncoderOptions(TiffBitsPerPixel.Bit32, TiffPhotometricInterpretation.Rgb, compression, TiffPredictor.None);
                    break;
                default:
                    this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.Rgb, compression, predictor);
                    break;
            }

            // Make sure 1 Bit compression is only used with 1 bit pixel type.
            if (IsOneBitCompression(this.CompressionType) && this.BitsPerPixel != TiffBitsPerPixel.Bit1)
            {
                // Invalid compression / bits per pixel combination, fallback to no compression.
                this.CompressionType = DefaultCompression;
            }

            return;
        }

        // If no photometric interpretation was chosen, the input image bit per pixel should be preserved.
        if (!photometricInterpretation.HasValue)
        {
            if (IsOneBitCompression(this.CompressionType))
            {
                // We need to make sure bits per pixel is set to Bit1 now. WhiteIsZero is set because its the default for bilevel compressed data.
                this.SetEncoderOptions(TiffBitsPerPixel.Bit1, TiffPhotometricInterpretation.WhiteIsZero, compression, TiffPredictor.None);
                return;
            }

            // At the moment only 8, 16 and 32 bits per pixel can be preserved by the tiff encoder.
            if (inputBitsPerPixel == 8)
            {
                this.SetEncoderOptions(TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, compression, predictor);
                return;
            }

            if (inputBitsPerPixel == 16)
            {
                // Assume desire to encode as L16 grayscale
                this.SetEncoderOptions(TiffBitsPerPixel.Bit16, TiffPhotometricInterpretation.BlackIsZero, compression, predictor);
                return;
            }

            this.SetEncoderOptions(TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, compression, predictor);
            return;
        }

        switch (photometricInterpretation)
        {
            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
                if (IsOneBitCompression(this.CompressionType))
                {
                    this.SetEncoderOptions(TiffBitsPerPixel.Bit1, photometricInterpretation, compression, TiffPredictor.None);
                    return;
                }

                if (inputBitsPerPixel == 16)
                {
                    this.SetEncoderOptions(TiffBitsPerPixel.Bit16, photometricInterpretation, compression, predictor);
                    return;
                }

                this.SetEncoderOptions(TiffBitsPerPixel.Bit8, photometricInterpretation, compression, predictor);
                return;

            case TiffPhotometricInterpretation.PaletteColor:
                this.SetEncoderOptions(TiffBitsPerPixel.Bit8, photometricInterpretation, compression, predictor);
                return;

            case TiffPhotometricInterpretation.Rgb:
                // Make sure 1 Bit compression is only used with 1 bit pixel type.
                if (IsOneBitCompression(this.CompressionType))
                {
                    // Invalid compression / bits per pixel combination, fallback to no compression.
                    compression = DefaultCompression;
                }

                this.SetEncoderOptions(TiffBitsPerPixel.Bit24, photometricInterpretation, compression, predictor);
                return;
        }

        this.SetEncoderOptions(DefaultBitsPerPixel, DefaultPhotometricInterpretation, compression, predictor);
    }

    private void SetEncoderOptions(TiffBitsPerPixel? bitsPerPixel, TiffPhotometricInterpretation? photometricInterpretation, TiffCompression compression, TiffPredictor predictor)
    {
        this.BitsPerPixel = bitsPerPixel;
        this.PhotometricInterpretation = photometricInterpretation;
        this.CompressionType = compression;
        this.HorizontalPredictor = predictor;
    }

    public static bool IsOneBitCompression(TiffCompression? compression)
        => compression is TiffCompression.Ccitt1D or TiffCompression.CcittGroup3Fax or TiffCompression.CcittGroup4Fax;
}

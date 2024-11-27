// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
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
    /// The transparent color mode to use when encoding.
    /// </summary>
    private readonly TransparentColorMode transparentColorMode;

    /// <summary>
    /// Whether to skip metadata during encoding.
    /// </summary>
    private readonly bool skipMetadata;

    private readonly List<(long, uint)> frameMarkers = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The options for the encoder.</param>
    /// <param name="configuration">The global configuration.</param>
    public TiffEncoderCore(TiffEncoder encoder, Configuration configuration)
    {
        this.configuration = configuration;
        this.memoryAllocator = configuration.MemoryAllocator;
        this.PhotometricInterpretation = encoder.PhotometricInterpretation;
        this.quantizer = encoder.Quantizer ?? KnownQuantizers.Octree;
        this.pixelSamplingStrategy = encoder.PixelSamplingStrategy;
        this.BitsPerPixel = encoder.BitsPerPixel;
        this.HorizontalPredictor = encoder.HorizontalPredictor;
        this.CompressionType = encoder.Compression;
        this.compressionLevel = encoder.CompressionLevel ?? DeflateCompressionLevel.DefaultCompression;
        this.skipMetadata = encoder.SkipMetadata;
        this.transparentColorMode = encoder.TransparentColorMode;
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
        TiffBitsPerPixel bitsPerPixel = this.BitsPerPixel ?? rootFrameTiffMetaData.BitsPerPixel;

        TiffPhotometricInterpretation photometricInterpretation = this.PhotometricInterpretation ?? rootFrameTiffMetaData.PhotometricInterpretation;

        TiffPredictor predictor = this.HorizontalPredictor ?? rootFrameTiffMetaData.Predictor;

        TiffCompression compression = this.CompressionType ?? rootFrameTiffMetaData.Compression;

        // Make sure the Encoder options makes sense in combination with each other.
        this.SanitizeAndSetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, predictor);

        using TiffStreamWriter writer = new(stream);
        Span<byte> buffer = stackalloc byte[4];

        long ifdMarker = WriteHeader(writer, buffer);

        Image<TPixel>? imageMetadata = image;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            ImageFrame<TPixel>? clonedFrame = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (EncodingUtilities.ShouldClearTransparentPixels<TPixel>(this.transparentColorMode))
                {
                    clonedFrame = frame.Clone();
                    EncodingUtilities.ClearTransparentPixels(clonedFrame, Color.Transparent);
                }

                ImageFrame<TPixel> encodingFrame = clonedFrame ?? frame;

                ifdMarker = this.WriteFrame(writer, encodingFrame, image.Metadata, imageMetadata, this.BitsPerPixel.Value, this.CompressionType.Value, ifdMarker);
                imageMetadata = null;
            }
            finally
            {
                clonedFrame?.Dispose();
            }
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
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="compression">The compression type.</param>
    /// <param name="ifdOffset">The marker to write this IFD offset.</param>
    /// <returns>
    /// The next IFD offset value.
    /// </returns>
    private long WriteFrame<TPixel>(
        TiffStreamWriter writer,
        ImageFrame<TPixel> frame,
        ImageMetadata imageMetadata,
        Image<TPixel>? image,
        TiffBitsPerPixel bitsPerPixel,
        TiffCompression compression,
        long ifdOffset)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Get the width and height of the frame.
        // This can differ from the frame bounds in-memory if the image represents only
        // a subregion.
        TiffFrameMetadata frameMetaData = frame.Metadata.GetTiffMetadata();
        int width = frameMetaData.EncodingWidth > 0 ? frameMetaData.EncodingWidth : frame.Width;
        int height = frameMetaData.EncodingHeight > 0 ? frameMetaData.EncodingHeight : frame.Height;

        width = Math.Min(width, frame.Width);
        height = Math.Min(height, frame.Height);
        Size encodingSize = new(width, height);

        using TiffBaseCompressor compressor = TiffCompressorFactory.Create(
            compression,
            writer.BaseStream,
            this.memoryAllocator,
            width,
            (int)bitsPerPixel,
            this.compressionLevel,
            this.HorizontalPredictor == TiffPredictor.Horizontal ? this.HorizontalPredictor.Value : TiffPredictor.None);

        TiffEncoderEntriesCollector entriesCollector = new();
        using TiffBaseColorWriter<TPixel> colorWriter = TiffColorWriterFactory.Create(
            this.PhotometricInterpretation,
            frame,
            encodingSize,
            this.quantizer,
            this.pixelSamplingStrategy,
            this.memoryAllocator,
            this.configuration,
            entriesCollector,
            (int)bitsPerPixel);

        int rowsPerStrip = CalcRowsPerStrip(height, colorWriter.BytesPerRow, this.CompressionType);

        colorWriter.Write(compressor, rowsPerStrip);

        if (image != null)
        {
            // Write the metadata for the root image
            entriesCollector.ProcessMetadata(image, this.skipMetadata);
        }

        // Write the metadata for the frame
        entriesCollector.ProcessMetadata(frame, this.skipMetadata);

        entriesCollector.ProcessFrameInfo(frame, encodingSize, imageMetadata);
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
        List<byte[]> largeDataBlocks = [];

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

    [MemberNotNull(nameof(BitsPerPixel), nameof(PhotometricInterpretation), nameof(CompressionType), nameof(HorizontalPredictor))]
    private void SanitizeAndSetEncoderOptions(
        TiffBitsPerPixel bitsPerPixel,
        TiffPhotometricInterpretation photometricInterpretation,
        TiffCompression compression,
        TiffPredictor predictor)
    {
        // Ensure 1 Bit compression is only used with 1 bit pixel type.
        // Choose a sensible default based on the bits per pixel.
        if (IsOneBitCompression(compression) && bitsPerPixel != TiffBitsPerPixel.Bit1)
        {
            compression = bitsPerPixel switch
            {
                < TiffBitsPerPixel.Bit8 => TiffCompression.None,
                _ => TiffCompression.Deflate,
            };
        }

        // Ensure predictor is only used with compression that supports it.
        predictor = HasPredictor(compression) ? predictor : TiffPredictor.None;

        // BitsPerPixel should be the primary source of truth for the encoder options.
        switch (bitsPerPixel)
        {
            case TiffBitsPerPixel.Bit1:
                if (IsOneBitCompression(compression))
                {
                    // The “normal” PhotometricInterpretation for bilevel CCITT compressed data is WhiteIsZero.
                    this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.WhiteIsZero, compression, predictor);
                    break;
                }

                this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.BlackIsZero, compression, predictor);
                break;
            case TiffBitsPerPixel.Bit4:
                this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.PaletteColor, compression, predictor);
                break;
            case TiffBitsPerPixel.Bit8:

                // Allow any combination of the below for 8 bit images.
                if (photometricInterpretation is TiffPhotometricInterpretation.BlackIsZero
                    or TiffPhotometricInterpretation.WhiteIsZero
                    or TiffPhotometricInterpretation.PaletteColor)
                {
                    this.SetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, predictor);
                    break;
                }

                this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.PaletteColor, compression, predictor);
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
                this.SetEncoderOptions(TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, compression, predictor);
                break;
            case TiffBitsPerPixel.Bit64:
                // Encoding not yet supported bits per pixel will default to 32 bits.
                this.SetEncoderOptions(TiffBitsPerPixel.Bit32, TiffPhotometricInterpretation.Rgb, compression, predictor);
                break;
            default:
                this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.Rgb, compression, predictor);
                break;
        }
    }

    [MemberNotNull(nameof(BitsPerPixel), nameof(PhotometricInterpretation), nameof(CompressionType), nameof(HorizontalPredictor))]
    private void SetEncoderOptions(
        TiffBitsPerPixel bitsPerPixel,
        TiffPhotometricInterpretation photometricInterpretation,
        TiffCompression compression,
        TiffPredictor predictor)
    {
        this.BitsPerPixel = bitsPerPixel;
        this.PhotometricInterpretation = photometricInterpretation;
        this.CompressionType = compression;
        this.HorizontalPredictor = predictor;
    }

    public static bool IsOneBitCompression(TiffCompression? compression)
        => compression is TiffCompression.Ccitt1D or TiffCompression.CcittGroup3Fax or TiffCompression.CcittGroup4Fax;

    public static bool HasPredictor(TiffCompression? compression)
        => compression is TiffCompression.Deflate or TiffCompression.Lzw;
}

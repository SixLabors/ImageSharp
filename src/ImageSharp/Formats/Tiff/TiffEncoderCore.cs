// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Ani;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.Writers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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
    /// The original encoder options. Each frame resolves unset options from its own TIFF metadata.
    /// </summary>
    private readonly TiffEncoder encoder;

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
        this.encoder = encoder;
        this.configuration = configuration;
        this.memoryAllocator = configuration.MemoryAllocator;
        this.PhotometricInterpretation = encoder.PhotometricInterpretation;
        this.quantizer = encoder.Quantizer ?? KnownQuantizers.Hexadecatree;
        this.pixelSamplingStrategy = encoder.PixelSamplingStrategy;
        this.BitsPerPixel = encoder.BitsPerPixel;
        this.SampleFormat = encoder.SampleFormat;
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
    /// Gets the selected format of the written samples.
    /// </summary>
    internal TiffSampleFormat? SampleFormat { get; private set; }

    /// <summary>
    /// Gets the source pixel's alpha representation for the floating-point extra sample tag.
    /// </summary>
    internal PixelAlphaRepresentation FloatAlphaRepresentation { get; private set; }

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

        using TiffStreamWriter writer = new(stream);
        Span<byte> buffer = stackalloc byte[4];

        long ifdMarker = WriteHeader(writer, buffer);

        Image<TPixel>? imageMetadata = image;

        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Start from the original options for every frame. Resolved values from the previous frame must not become overrides.
            this.ResolveFrameOptions<TPixel>(frame.Metadata.GetTiffMetadata());

            ifdMarker = this.WriteFrame(
                writer,
                frame,
                image.Metadata,
                imageMetadata,
                this.BitsPerPixel.Value,
                this.CompressionType.Value,
                this.SampleFormat.Value,
                this.PhotometricInterpretation.Value,
                ifdMarker,
                cancellationToken);

            imageMetadata = null;
        }

        long currentOffset = writer.BaseStream.Position;
        foreach ((long, uint) marker in this.frameMarkers)
        {
            writer.WriteMarkerFast(marker.Item1, marker.Item2, buffer);
        }

        writer.BaseStream.Seek(currentOffset, SeekOrigin.Begin);
    }

    /// <summary>
    /// Resolves the current frame's TIFF options and selects a supported combination.
    /// </summary>
    /// <remarks>
    /// Explicit encoder options override frame metadata. This method always reads the original options to prevent values leaking between frames.
    /// Floating-point output selects a depth from TPixel when neither the encoder nor the frame specifies a floating-point depth.
    /// </remarks>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="frameMetadata">The current frame's TIFF metadata.</param>
    [MemberNotNull(nameof(BitsPerPixel), nameof(PhotometricInterpretation), nameof(CompressionType), nameof(HorizontalPredictor), nameof(SampleFormat))]
    private void ResolveFrameOptions<TPixel>(TiffFrameMetadata frameMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PixelTypeInfo pixelInfo = TPixel.GetPixelTypeInfo();
        TiffSampleFormat sampleFormat = this.encoder.SampleFormat ?? frameMetadata.SampleFormat;

        // Signed integer and undefined samples have no writer. Use the unsigned integer writer for those metadata values.
        if (sampleFormat is not TiffSampleFormat.Float and not TiffSampleFormat.UnsignedInteger)
        {
            sampleFormat = TiffSampleFormat.UnsignedInteger;
        }

        // Explicit options override frame metadata. TiffFrameMetadata supplies the format defaults for newly created frames.
        TiffBitsPerPixel bitsPerPixel = this.encoder.BitsPerPixel ?? frameMetadata.BitsPerPixel;

        TiffPhotometricInterpretation photometricInterpretation = this.encoder.PhotometricInterpretation ?? frameMetadata.PhotometricInterpretation;

        TiffPredictor predictor = this.encoder.HorizontalPredictor ?? frameMetadata.Predictor;

        TiffCompression compression = this.encoder.Compression ?? frameMetadata.Compression;

        // Resolve sample depth and color layout before applying the compression and predictor restrictions.
        if (sampleFormat == TiffSampleFormat.Float)
        {
            // Integer metadata depths do not describe float samples. Without an explicit depth, use the source's color and alpha information.
            // RGB uses 96 bits, RGBA uses 128 bits, and grayscale uses 32 bits.
            if (this.encoder.BitsPerPixel is null && frameMetadata.SampleFormat != TiffSampleFormat.Float)
            {
                bool hasColorComponents = (pixelInfo.ColorType & PixelColorType.RGB) == PixelColorType.RGB
                    || (pixelInfo.ColorType & PixelColorType.BGR) == PixelColorType.BGR;

                bitsPerPixel = hasColorComponents
                    ? pixelInfo.AlphaRepresentation == PixelAlphaRepresentation.None ? TiffBitsPerPixel.Bit96 : TiffBitsPerPixel.Bit128
                    : TiffBitsPerPixel.Bit32;
            }

            if (this.encoder.PhotometricInterpretation is null && frameMetadata.SampleFormat != TiffSampleFormat.Float)
            {
                photometricInterpretation = bitsPerPixel == TiffBitsPerPixel.Bit32
                    ? TiffPhotometricInterpretation.BlackIsZero
                    : TiffPhotometricInterpretation.Rgb;
            }

            this.FloatAlphaRepresentation = pixelInfo.AlphaRepresentation;
        }
        else
        {
            // An explicit change from floating-point samples must not inherit their 32-bit
            // component depth when the caller did not choose an integer output depth.
            if (this.encoder.BitsPerPixel is null && frameMetadata.SampleFormat == TiffSampleFormat.Float)
            {
                bitsPerPixel = TiffConstants.DefaultBitsPerPixel;
            }
        }

        this.SanitizeAndSetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, predictor, sampleFormat);
        this.SampleFormat = sampleFormat;
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
    /// Writes one frame's sample data and TIFF directory with its resolved options and compatible ICC profile.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="writer">The <see cref="BinaryWriter" /> to write data to.</param>
    /// <param name="frame">The tiff frame.</param>
    /// <param name="imageMetadata">The image metadata (resolution values for each frame).</param>
    /// <param name="image">The image (common metadata for root frame).</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="compression">The compression type.</param>
    /// <param name="sampleFormat">The sample format.</param>
    /// <param name="photometricInterpretation">The resolved grayscale, palette, RGB, or JPEG CMYK sample interpretation.</param>
    /// <param name="ifdOffset">The marker to write this IFD offset.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
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
        TiffSampleFormat sampleFormat,
        TiffPhotometricInterpretation photometricInterpretation,
        long ifdOffset,
        CancellationToken cancellationToken)
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

        TiffEncoderEntriesCollector entriesCollector = new();

        // Prefer the frame's profile. TIFF, ICO, CUR and ANI store profiles per frame or resource.
        // Their image profile copies the first frame's profile and must not be inherited by a later unprofiled frame.
        // Other formats can define one image-wide profile, which applies when the current frame has no profile of its own.
        IImageFormat? sourceFormat = frame.Metadata.DecodedImageFormat ?? imageMetadata.DecodedImageFormat;
        IccProfile? sourceProfile = frame.Metadata.IccProfile;
        if (sourceProfile is null && sourceFormat is not (TiffFormat or IcoFormat or CurFormat or AniFormat))
        {
            sourceProfile = imageMetadata.IccProfile;
        }

        // Byte compressors receive samples already packed by TIFF. Profile compatibility therefore follows the TIFF sample layout.
        // Indexed output checks the palette's RGB space through the shared helper; indices themselves are not color components.
        PixelColorType sampleColorType = photometricInterpretation switch
        {
            TiffPhotometricInterpretation.PaletteColor => PixelColorType.Indexed,
            TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero => PixelColorType.Luminance,
            TiffPhotometricInterpretation.Separated => PixelColorType.CMYK,
            _ => PixelColorType.RGB
        };

        IccProfile? outputProfile = null;
        ColorProfileConverter? converter = null;
        if (compression != TiffCompression.Jpeg)
        {
            // The helper compares the retained profile's data color space with sampleColorType.
            // A match retains that same profile and supplies an sRGB-to-profile converter when needed. A mismatch omits the profile.
            outputProfile = IccProfile.GetIccProfileForEncoding(sourceProfile, sampleColorType, this.memoryAllocator, out converter);
        }
        else
        {
            // Select the profile before encoding so TIFF and JPEG write the same profile.
            // JPEG creates and owns its converter. This shared compatibility check allocates no unused TIFF converter.
            outputProfile = IccProfile.GetIccProfileForEncoding(sourceProfile, sampleColorType);
        }

        // TIFF has no separate JPEG color-type option. Its resolved photometric interpretation selects the nested JPEG color type.
        // JPEG receives the original TPixel region and performs its own ICC conversion with the supplied profile.
        JpegColorType jpegColorType = photometricInterpretation switch
        {
            TiffPhotometricInterpretation.Separated => JpegColorType.Cmyk,
            TiffPhotometricInterpretation.BlackIsZero => JpegColorType.Luminance,
            _ => JpegColorType.Rgb
        };

        using TiffBaseColorWriter<TPixel> colorWriter = compression == TiffCompression.Jpeg
            ? new TiffJpegWriter<TPixel>(
                frame,
                encodingSize,
                this.memoryAllocator,
                this.configuration,
                entriesCollector,
                outputProfile,
                jpegColorType,
                EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(this.transparentColorMode),
                cancellationToken)
            : TiffColorWriterFactory.Create(
                photometricInterpretation,
                frame,
                encodingSize,
                this.quantizer,
                this.pixelSamplingStrategy,
                this.memoryAllocator,
                this.configuration,
                entriesCollector,
                (int)bitsPerPixel,
                sampleFormat,
                EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(this.transparentColorMode),
                converter);

        using TiffBaseCompressor compressor = TiffCompressorFactory.Create(
            compression,
            writer.BaseStream,
            this.memoryAllocator,
            width,
            colorWriter.BitsPerPixel,
            this.compressionLevel,
            this.HorizontalPredictor == TiffPredictor.Horizontal ? this.HorizontalPredictor.Value : TiffPredictor.None);

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

        // Write the same profile that describes the samples, including when JPEG encoded the original pixel region.
        // The TIFF IccProfile directory entry is required even when SkipMetadata omits optional metadata.
        entriesCollector.ProcessIccProfile(outputProfile);

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
        int stripSizeInBytes = compression is TiffCompression.Deflate || compression is TiffCompression.Lzw
            ? TiffConstants.DefaultStripSize * 2
            : TiffConstants.DefaultStripSize;
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
                byte[] raw;
                if (entry.GetValue() is byte[] bytes)
                {
                    // Byte and Undefined entries already hold their encoded payload. Keep that array until the IFD is complete;
                    // writing it afterward only reads it, so a second profile-sized array and copy serve no purpose.
                    raw = bytes;
                }
                else
                {
                    // Numeric arrays and strings still need byte-order or text encoding before their out-of-line data is written.
                    raw = new byte[length];
                    int sz = ExifWriter.WriteValue(entry, raw, 0);
                    DebugGuard.IsTrue(sz == raw.Length, "Incorrect number of bytes written");
                }

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

    /// <summary>
    /// Selects a supported layout and compression from the current frame's requested settings.
    /// </summary>
    /// <param name="bitsPerPixel">The requested pixel depth.</param>
    /// <param name="photometricInterpretation">The requested sample interpretation.</param>
    /// <param name="compression">The requested compression.</param>
    /// <param name="predictor">The requested predictor.</param>
    /// <param name="sampleFormat">The requested sample format.</param>
    [MemberNotNull(nameof(BitsPerPixel), nameof(PhotometricInterpretation), nameof(CompressionType), nameof(HorizontalPredictor))]
    private void SanitizeAndSetEncoderOptions(
        TiffBitsPerPixel bitsPerPixel,
        TiffPhotometricInterpretation photometricInterpretation,
        TiffCompression compression,
        TiffPredictor predictor,
        TiffSampleFormat sampleFormat)
    {
        if (sampleFormat == TiffSampleFormat.Float)
        {
            // Preserve supported float depths. For an unsupported depth, select a layout
            // from the requested color interpretation and the source alpha representation.
            if (bitsPerPixel is not (TiffBitsPerPixel.Bit32 or TiffBitsPerPixel.Bit96 or TiffBitsPerPixel.Bit128))
            {
                bitsPerPixel = photometricInterpretation is TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero
                    ? TiffBitsPerPixel.Bit32
                    : this.FloatAlphaRepresentation == PixelAlphaRepresentation.None ? TiffBitsPerPixel.Bit96 : TiffBitsPerPixel.Bit128;
            }

            if (bitsPerPixel == TiffBitsPerPixel.Bit32)
            {
                photometricInterpretation = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero
                    ? TiffPhotometricInterpretation.WhiteIsZero
                    : TiffPhotometricInterpretation.BlackIsZero;
            }
            else
            {
                photometricInterpretation = TiffPhotometricInterpretation.Rgb;
            }

            // Fax and JPEG compressors cannot write these sample layouts. Deflate is the
            // existing TIFF fallback for a non-bilevel image with incompatible compression.
            if (compression is not (TiffCompression.None or TiffCompression.PackBits or TiffCompression.Deflate or TiffCompression.Lzw))
            {
                compression = TiffCompression.Deflate;
            }

            // The TIFF floating-point predictor is not implemented, so write samples directly.
            this.SetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, TiffPredictor.None);
            return;
        }

        // The packed integer depths still use the existing 16-bit color fallback.
        bitsPerPixel = bitsPerPixel switch
        {
            TiffBitsPerPixel.Bit30 or TiffBitsPerPixel.Bit36 or TiffBitsPerPixel.Bit42 => TiffBitsPerPixel.Bit48,
            _ => bitsPerPixel
        };

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

        // JPEG supports one 8-bit gray sample or four 8-bit CMYK samples in these TIFF layouts.
        // Keep these combinations before the general integer fallback, which would select palette or RGB instead.
        if (compression == TiffCompression.Jpeg
            && ((bitsPerPixel == TiffBitsPerPixel.Bit8 && photometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                || (bitsPerPixel == TiffBitsPerPixel.Bit32 && photometricInterpretation == TiffPhotometricInterpretation.Separated)))
        {
            this.SetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, predictor);
            return;
        }

        // The remaining JPEG layout is eight-bit RGB. Unsupported layouts use the existing Deflate fallback.
        if (compression == TiffCompression.Jpeg && bitsPerPixel != TiffBitsPerPixel.Bit24)
        {
            compression = TiffCompression.Deflate;
        }

        // CMYK uses four ink samples, with no alpha channel. Keep its 8- or 16-bit component depth
        // before the RGB fallback below. JPEG's unsupported 16-bit layout has already selected Deflate.
        if (photometricInterpretation == TiffPhotometricInterpretation.Separated
            && bitsPerPixel is TiffBitsPerPixel.Bit32 or TiffBitsPerPixel.Bit64)
        {
            this.SetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, predictor);
            return;
        }

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
                photometricInterpretation = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero
                    ? TiffPhotometricInterpretation.WhiteIsZero
                    : TiffPhotometricInterpretation.BlackIsZero;

                this.SetEncoderOptions(bitsPerPixel, photometricInterpretation, compression, predictor);
                break;
            case TiffBitsPerPixel.Bit6:
            case TiffBitsPerPixel.Bit10:
            case TiffBitsPerPixel.Bit12:
            case TiffBitsPerPixel.Bit14:
                // Encoding not yet supported bits per pixel will default to 24 bits.
                this.SetEncoderOptions(TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, compression, predictor);
                break;
            case TiffBitsPerPixel.Bit48:
            case TiffBitsPerPixel.Bit64:
                this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.Rgb, compression, predictor);
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

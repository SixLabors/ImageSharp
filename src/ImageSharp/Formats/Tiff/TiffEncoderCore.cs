// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using SixLabors.ImageSharp.Advanced;
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

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Performs the TIFF encoding operation.
    /// </summary>
    internal sealed class TiffEncoderCore : IImageEncoderInternals
    {
        private static readonly ushort ByteOrderMarker = BitConverter.IsLittleEndian
                ? TiffConstants.ByteOrderLittleEndianShort
                : TiffConstants.ByteOrderBigEndianShort;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// A scratch buffer to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// The quantizer for creating color palette image.
        /// </summary>
        private readonly IQuantizer quantizer;

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
        /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        public TiffEncoderCore(ITiffEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.PhotometricInterpretation = options.PhotometricInterpretation;
            this.quantizer = options.Quantizer ?? KnownQuantizers.Octree;
            this.BitsPerPixel = options.BitsPerPixel;
            this.HorizontalPredictor = options.HorizontalPredictor;
            this.CompressionType = options.Compression;
            this.compressionLevel = options.CompressionLevel ?? DeflateCompressionLevel.DefaultCompression;
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

            this.configuration = image.GetConfiguration();

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

            using (var writer = new TiffStreamWriter(stream))
            {
                long firstIfdMarker = this.WriteHeader(writer);

                // TODO: multiframing is not supported
                this.WriteImage(writer, image, firstIfdMarker);
            }
        }

        /// <summary>
        /// Writes the TIFF file header.
        /// </summary>
        /// <param name="writer">The <see cref="TiffStreamWriter" /> to write data to.</param>
        /// <returns>
        /// The marker to write the first IFD offset.
        /// </returns>
        public long WriteHeader(TiffStreamWriter writer)
        {
            writer.Write(ByteOrderMarker);
            writer.Write(TiffConstants.HeaderMagicNumber);
            return writer.PlaceMarker();
        }

        /// <summary>
        /// Writes all data required to define an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="writer">The <see cref="BinaryWriter"/> to write data to.</param>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="ifdOffset">The marker to write this IFD offset.</param>
        private void WriteImage<TPixel>(TiffStreamWriter writer, Image<TPixel> image, long ifdOffset)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var entriesCollector = new TiffEncoderEntriesCollector();

            using TiffBaseCompressor compressor = TiffCompressorFactory.Create(
                this.CompressionType ?? TiffCompression.None,
                writer.BaseStream,
                this.memoryAllocator,
                image.Width,
                (int)this.BitsPerPixel,
                this.compressionLevel,
                this.HorizontalPredictor == TiffPredictor.Horizontal ? this.HorizontalPredictor.Value : TiffPredictor.None);

            using TiffBaseColorWriter<TPixel> colorWriter = TiffColorWriterFactory.Create(
                this.PhotometricInterpretation,
                image.Frames.RootFrame,
                this.quantizer,
                this.memoryAllocator,
                this.configuration,
                entriesCollector,
                (int)this.BitsPerPixel);

            int rowsPerStrip = this.CalcRowsPerStrip(image.Frames.RootFrame.Height, colorWriter.BytesPerRow);

            colorWriter.Write(compressor, rowsPerStrip);

            entriesCollector.ProcessImageFormat(this);
            entriesCollector.ProcessGeneral(image);

            writer.WriteMarker(ifdOffset, (uint)writer.Position);
            long nextIfdMarker = this.WriteIfd(writer, entriesCollector.Entries);
        }

        /// <summary>
        /// Calculates the number of rows written per strip.
        /// </summary>
        /// <param name="height">The height of the image.</param>
        /// <param name="bytesPerRow">The number of bytes per row.</param>
        /// <returns>Number of rows per strip.</returns>
        private int CalcRowsPerStrip(int height, int bytesPerRow)
        {
            DebugGuard.MustBeGreaterThan(height, 0, nameof(height));
            DebugGuard.MustBeGreaterThan(bytesPerRow, 0, nameof(bytesPerRow));

            int rowsPerStrip = TiffConstants.DefaultStripSize / bytesPerRow;

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
            var largeDataBlocks = new List<byte[]>();

            entries.Sort((a, b) => (ushort)a.Tag - (ushort)b.Tag);

            writer.Write((ushort)entries.Count);

            foreach (IExifValue entry in entries)
            {
                writer.Write((ushort)entry.Tag);
                writer.Write((ushort)entry.DataType);
                writer.Write(ExifWriter.GetNumberOfComponents(entry));

                uint length = ExifWriter.GetLength(entry);
                if (length <= 4)
                {
                    int sz = ExifWriter.WriteValue(entry, this.buffer, 0);
                    DebugGuard.IsTrue(sz == length, "Incorrect number of bytes written");
                    writer.WritePadded(this.buffer.AsSpan(0, sz));
                }
                else
                {
                    var raw = new byte[length];
                    int sz = ExifWriter.WriteValue(entry, raw, 0);
                    DebugGuard.IsTrue(sz == raw.Length, "Incorrect number of bytes written");
                    largeDataBlocks.Add(raw);
                    writer.Write(dataOffset);
                    dataOffset += (uint)(raw.Length + (raw.Length % 2));
                }
            }

            long nextIfdMarker = writer.PlaceMarker();

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

        private void SanitizeAndSetEncoderOptions(TiffBitsPerPixel? bitsPerPixel, int inputBitsPerPixel, TiffPhotometricInterpretation? photometricInterpretation, TiffCompression compression, TiffPredictor predictor)
        {
            // BitsPerPixel should be the primary source of truth for the encoder options.
            if (bitsPerPixel.HasValue)
            {
                switch (bitsPerPixel)
                {
                    case TiffBitsPerPixel.Bit1:
                        if (compression == TiffCompression.Ccitt1D || compression == TiffCompression.CcittGroup3Fax || compression == TiffCompression.CcittGroup4Fax)
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
                    case TiffBitsPerPixel.Bit6:
                    case TiffBitsPerPixel.Bit12:
                        // Encoding 12 and 6 bits per pixel is not yet supported. Default to 24 bits.
                        this.SetEncoderOptions(TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, compression, TiffPredictor.None);
                        break;
                    default:
                        this.SetEncoderOptions(bitsPerPixel, TiffPhotometricInterpretation.Rgb, compression, predictor);
                        break;
                }

                return;
            }

            // If no photometric interpretation was chosen, the input image bit per pixel should be preserved.
            if (!photometricInterpretation.HasValue)
            {
                // At the moment only 8 and 32 bits per pixel can be preserved by the tiff encoder.
                if (inputBitsPerPixel == 8)
                {
                    this.SetEncoderOptions(TiffBitsPerPixel.Bit8, TiffPhotometricInterpretation.BlackIsZero, compression, predictor);
                    return;
                }

                this.SetEncoderOptions(TiffBitsPerPixel.Bit24, TiffPhotometricInterpretation.Rgb, compression, predictor);
                return;
            }

            switch (photometricInterpretation)
            {
                case TiffPhotometricInterpretation.BlackIsZero:
                case TiffPhotometricInterpretation.WhiteIsZero:
                    if (this.CompressionType == TiffCompression.Ccitt1D ||
                        this.CompressionType == TiffCompression.CcittGroup3Fax ||
                        this.CompressionType == TiffCompression.CcittGroup4Fax)
                    {
                        this.SetEncoderOptions(TiffBitsPerPixel.Bit1, photometricInterpretation, compression, TiffPredictor.None);
                        return;
                    }
                    else
                    {
                        this.SetEncoderOptions(TiffBitsPerPixel.Bit8, photometricInterpretation, compression, predictor);
                        return;
                    }

                case TiffPhotometricInterpretation.PaletteColor:
                    this.SetEncoderOptions(TiffBitsPerPixel.Bit8, photometricInterpretation, compression, predictor);
                    return;

                case TiffPhotometricInterpretation.Rgb:
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
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Performs the TIFF encoding operation.
    /// </summary>
    internal sealed class TiffEncoderCore : IImageEncoderInternals
    {
        public const int DefaultStripSize = 8 * 1024;

        public static readonly ByteOrder ByteOrder = BitConverter.IsLittleEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

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
        /// The color depth, in number of bits per pixel.
        /// </summary>
        private TiffBitsPerPixel bitsPerPixel;

        /// <summary>
        /// The quantizer for creating color palette image.
        /// </summary>
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Sets the deflate compression level.
        /// </summary>
        private readonly DeflateCompressionLevel compressionLevel;

        private readonly int maxStripBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        public TiffEncoderCore(ITiffEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.CompressionType = options.Compression;
            this.Mode = options.Mode;
            this.quantizer = options.Quantizer ?? KnownQuantizers.Octree;
            this.UseHorizontalPredictor = options.UseHorizontalPredictor;
            this.compressionLevel = options.CompressionLevel;
            this.maxStripBytes = options.MaxStripBytes;
        }

        /// <summary>
        /// Gets the photometric interpretation implementation to use when encoding the image.
        /// </summary>
        internal TiffPhotometricInterpretation PhotometricInterpretation { get; private set; }

        /// <summary>
        /// Gets the compression implementation to use when encoding the image.
        /// </summary>
        internal TiffEncoderCompression CompressionType { get; }

        /// <summary>
        /// Gets the encoding mode to use. RGB, RGB with color palette or gray.
        /// If no mode is specified in the options, RGB will be used.
        /// </summary>
        internal TiffEncodingMode Mode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to use horizontal prediction. This can improve the compression ratio with deflate compression.
        /// </summary>
        internal bool UseHorizontalPredictor { get; }

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

            this.SetMode(image);
            this.SetPhotometricInterpretation();

            using (var writer = new TiffStreamWriter(stream))
            {
                long firstIfdMarker = this.WriteHeader(writer);

                // TODO: multiframing is not support
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
            long firstIfdMarker = writer.PlaceMarker();

            return firstIfdMarker;
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

            // Write the image bytes to the steam.
            var imageDataStart = (uint)writer.Position;

            using TiffBaseCompressor compressor = TiffCompressorFactory.Create(
                     this.CompressionType,
                     writer.BaseStream,
                     this.memoryAllocator,
                     image.Width,
                     (int)this.bitsPerPixel,
                     this.compressionLevel,
                     this.UseHorizontalPredictor ? TiffPredictor.Horizontal : TiffPredictor.None);

            using TiffBaseColorWriter<TPixel> colorWriter = TiffColorWriterFactory.Create(this.Mode, image.Frames.RootFrame, this.quantizer, this.memoryAllocator, this.configuration, entriesCollector);

            int rowsPerStrip = this.CalcRowsPerStrip(image.Frames.RootFrame, colorWriter.BytesPerRow);

            colorWriter.Write(compressor, rowsPerStrip);

            entriesCollector.ProcessImageFormat(this);
            entriesCollector.ProcessGeneral(image);

            writer.WriteMarker(ifdOffset, (uint)writer.Position);
            long nextIfdMarker = this.WriteIfd(writer, entriesCollector.Entries);
        }

        private int CalcRowsPerStrip(ImageFrame image, int bytesPerRow)
        {
            int sz = this.maxStripBytes > 0 ? this.maxStripBytes : DefaultStripSize;
            int height = sz / bytesPerRow;

            return height > 0 ? (height < image.Height ? height : image.Height) : 1;
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
                throw new ArgumentException("There must be at least one entry per IFD.", nameof(entries));
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
                var raw = new byte[length];
                int sz = ExifWriter.WriteValue(entry, raw, 0);
                DebugGuard.IsTrue(sz == raw.Length, "Incorrect number of bytes written");
                if (raw.Length <= 4)
                {
                    writer.WritePadded(raw);
                }
                else
                {
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

        private void SetMode(Image image)
        {
            if (this.CompressionType == TiffEncoderCompression.CcittGroup3Fax || this.CompressionType == TiffEncoderCompression.ModifiedHuffman)
            {
                if (this.Mode == TiffEncodingMode.Default)
                {
                    this.Mode = TiffEncodingMode.BiColor;
                    this.bitsPerPixel = TiffBitsPerPixel.Pixel1;
                    return;
                }
                else if (this.Mode != TiffEncodingMode.BiColor)
                {
                    TiffThrowHelper.ThrowImageFormatException($"The {this.CompressionType} compression and {this.Mode} aren't compatible. Please use {this.CompressionType} only with {TiffEncodingMode.BiColor} or {TiffEncodingMode.Default} mode.");
                }
            }

            if (this.Mode == TiffEncodingMode.Default)
            {
                // Preserve input bits per pixel, if no mode was specified.
                TiffMetadata tiffMetadata = image.Metadata.GetTiffMetadata();
                switch (tiffMetadata.BitsPerPixel)
                {
                    case TiffBitsPerPixel.Pixel1:
                        this.Mode = TiffEncodingMode.BiColor;
                        break;
                    case TiffBitsPerPixel.Pixel8:
                        // todo: can gray or palette
                        this.Mode = TiffEncodingMode.Gray;
                        break;
                    default:
                        this.Mode = TiffEncodingMode.Rgb;
                        break;
                }
            }

            switch (this.Mode)
            {
                case TiffEncodingMode.BiColor:
                    this.bitsPerPixel = TiffBitsPerPixel.Pixel1;
                    break;
                case TiffEncodingMode.ColorPalette:
                case TiffEncodingMode.Gray:
                    this.bitsPerPixel = TiffBitsPerPixel.Pixel8;
                    break;
                case TiffEncodingMode.Rgb:
                    this.bitsPerPixel = TiffBitsPerPixel.Pixel24;
                    break;
                default:
                    this.Mode = TiffEncodingMode.Rgb;
                    this.bitsPerPixel = TiffBitsPerPixel.Pixel24;
                    break;
            }
        }

        private void SetPhotometricInterpretation()
        {
            switch (this.Mode)
            {
                case TiffEncodingMode.ColorPalette:
                    this.PhotometricInterpretation = TiffPhotometricInterpretation.PaletteColor;
                    break;
                case TiffEncodingMode.BiColor:
                    if (this.CompressionType == TiffEncoderCompression.CcittGroup3Fax || this.CompressionType == TiffEncoderCompression.ModifiedHuffman)
                    {
                        // The “normal” PhotometricInterpretation for bilevel CCITT compressed data is WhiteIsZero.
                        this.PhotometricInterpretation = TiffPhotometricInterpretation.WhiteIsZero;
                    }
                    else
                    {
                        this.PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero;
                    }

                    break;

                case TiffEncodingMode.Gray:
                    this.PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero;
                    break;
                default:
                    this.PhotometricInterpretation = TiffPhotometricInterpretation.Rgb;
                    break;
            }
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils;
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
        private TiffBitsPerPixel? bitsPerPixel;

        /// <summary>
        /// The quantizer for creating color palette image.
        /// </summary>
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Indicating whether to use horizontal prediction. This can improve the compression ratio with deflate compression.
        /// </summary>
        private readonly bool useHorizontalPredictor;

        /// <summary>
        /// Sets the deflate compression level.
        /// </summary>
        private readonly DeflateCompressionLevel compressionLevel;

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
            this.useHorizontalPredictor = options.UseHorizontalPredictor;
            this.compressionLevel = options.CompressionLevel;
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
        /// </summary>
        internal TiffEncodingMode Mode { get; private set; }

        internal bool UseHorizontalPredictor => this.useHorizontalPredictor;

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
            ImageMetadata metadata = image.Metadata;
            TiffMetadata tiffMetadata = metadata.GetTiffMetadata();
            this.bitsPerPixel ??= tiffMetadata.BitsPerPixel;
            if (this.Mode == TiffEncodingMode.Default)
            {
                // Preserve input bits per pixel, if no mode was specified.
                if (this.bitsPerPixel == TiffBitsPerPixel.Pixel8)
                {
                    this.Mode = TiffEncodingMode.Gray;
                }
                else if (this.bitsPerPixel == TiffBitsPerPixel.Pixel1)
                {
                    this.Mode = TiffEncodingMode.BiColor;
                }
            }

            this.SetPhotometricInterpretation();

            using (var writer = new TiffWriter(stream, this.memoryAllocator, this.configuration))
            {
                long firstIfdMarker = this.WriteHeader(writer);

                // TODO: multiframing is not support
                long nextIfdMarker = this.WriteImage(writer, image, firstIfdMarker);
            }
        }

        /// <summary>
        /// Writes the TIFF file header.
        /// </summary>
        /// <param name="writer">The <see cref="TiffWriter"/> to write data to.</param>
        /// <returns>The marker to write the first IFD offset.</returns>
        public long WriteHeader(TiffWriter writer)
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
        /// <returns>The marker to write the next IFD offset (if present).</returns>
        public long WriteImage<TPixel>(TiffWriter writer, Image<TPixel> image, long ifdOffset)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var entriesCollector = new TiffEncoderEntriesCollector();

            // Write the image bytes to the steam.
            var imageDataStart = (uint)writer.Position;
            int imageDataBytes;
            switch (this.Mode)
            {
                case TiffEncodingMode.ColorPalette:
                    imageDataBytes = writer.WritePalettedRgb(image, this.quantizer, this.CompressionType, this.compressionLevel, this.useHorizontalPredictor, entriesCollector);
                    break;
                case TiffEncodingMode.Gray:
                    imageDataBytes = writer.WriteGray(image, this.CompressionType, this.compressionLevel, this.useHorizontalPredictor);
                    break;
                case TiffEncodingMode.BiColor:
                    imageDataBytes = writer.WriteBiColor(image, this.CompressionType, this.compressionLevel);
                    break;
                default:
                    imageDataBytes = writer.WriteRgb(image, this.CompressionType, this.compressionLevel, this.useHorizontalPredictor);
                    break;
            }

            this.AddStripTags(image, entriesCollector, imageDataStart, imageDataBytes);
            entriesCollector.ProcessImageFormat(this);
            entriesCollector.ProcessGeneral(image);

            writer.WriteMarker(ifdOffset, (uint)writer.Position);
            long nextIfdMarker = this.WriteIfd(writer, entriesCollector.Entries);

            return nextIfdMarker + imageDataBytes;
        }

        /// <summary>
        /// Writes a TIFF IFD block.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> to write data to.</param>
        /// <param name="entries">The IFD entries to write to the file.</param>
        /// <returns>The marker to write the next IFD offset (if present).</returns>
        public long WriteIfd(TiffWriter writer, List<IExifValue> entries)
        {
            if (entries.Count == 0)
            {
                throw new ArgumentException("There must be at least one entry per IFD.", nameof(entries));
            }

            uint dataOffset = (uint)writer.Position + (uint)(6 + (entries.Count * 12));
            var largeDataBlocks = new List<byte[]>();

            entries.Sort((a, b) => (ushort)a.Tag - (ushort)b.Tag);

            writer.Write((ushort)entries.Count);

            foreach (ExifValue entry in entries)
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

        /// <summary>
        /// Adds image format information to the specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}" /> to encode from.</param>
        /// <param name="entriesCollector">The entries collector.</param>
        /// <param name="imageDataStartOffset">The start of the image data in the stream.</param>
        /// <param name="imageDataBytes">The image data in bytes to write.</param>
        public void AddStripTags<TPixel>(Image<TPixel> image, TiffEncoderEntriesCollector entriesCollector, uint imageDataStartOffset, int imageDataBytes)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var stripOffsets = new ExifLongArray(ExifTagValue.StripOffsets)
            {
                // TODO: we only write one image strip for the start.
                Value = new[] { imageDataStartOffset }
            };

            var rowsPerStrip = new ExifLong(ExifTagValue.RowsPerStrip)
            {
                // All rows in one strip.
                Value = (uint)image.Height
            };

            var stripByteCounts = new ExifLongArray(ExifTagValue.StripByteCounts)
            {
                Value = new[] { (uint)imageDataBytes }
            };

            entriesCollector.Add(stripOffsets);
            entriesCollector.Add(rowsPerStrip);
            entriesCollector.Add(stripByteCounts);
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

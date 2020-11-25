// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Performs the TIFF encoding operation.
    /// </summary>
    internal sealed class TiffEncoderCore : IImageEncoderInternals
    {
        /// <summary>
        /// The amount to pad each row by in bytes.
        /// </summary>
        private int padding;

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
        /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        public TiffEncoderCore(ITiffEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;

            if (options.BitsPerPixel == TiffBitsPerPixel.Pixel8)
            {
                this.PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero;
            }
            else
            {
                this.PhotometricInterpretation = TiffPhotometricInterpretation.Rgb;
            }
        }

        /// <summary>
        /// Gets the photometric interpretation implementation to use when encoding the image.
        /// </summary>
        private TiffPhotometricInterpretation PhotometricInterpretation { get; }

        /// <summary>
        /// Gets or sets the compression implementation to use when encoding the image.
        /// </summary>
        public TiffCompressionType CompressionType { get; set; }

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

            short bpp = (short)this.bitsPerPixel;
            int bytesPerLine = 4 * (((image.Width * bpp) + 31) / 32);
            this.padding = bytesPerLine - (int)(image.Width * (bpp / 8F));

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
            ushort byteOrderMarker = BitConverter.IsLittleEndian
                ? TiffConstants.ByteOrderLittleEndianShort
                : TiffConstants.ByteOrderBigEndianShort;

            writer.Write(byteOrderMarker);
            writer.Write((ushort)42);
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
            var ifdEntries = new List<IExifValue>();

            // Write the image bytes to the steam.
            var imageDataStart = (uint)writer.Position;
            int imageDataBytes;
            if (this.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb)
            {
                imageDataBytes = writer.WriteRgbImageData(image, this.padding);
            }
            else
            {
                imageDataBytes = writer.WriteGrayImageData(image, this.padding);
            }

            // Write info's about the image to the stream.
            this.AddImageFormat(image, ifdEntries, imageDataStart, imageDataBytes);
            writer.WriteMarker(ifdOffset, (uint)writer.Position);
            long nextIfdMarker = this.WriteIfd(writer, ifdEntries);

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
                    writer.Write((byte)0);
                }
            }

            return nextIfdMarker;
        }

        /// <summary>
        /// Adds image format information to the specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="ifdEntries">The image format entries to add to the IFD.</param>
        /// <param name="imageDataStartOffset">The start of the image data in the stream.</param>
        /// <param name="imageDataBytes">The image data in bytes to write.</param>
        public void AddImageFormat<TPixel>(Image<TPixel> image, List<IExifValue> ifdEntries, uint imageDataStartOffset, int imageDataBytes)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            var width = new ExifLong(ExifTagValue.ImageWidth)
            {
                Value = (uint)image.Width
            };

            var height = new ExifLong(ExifTagValue.ImageLength)
            {
                Value = (uint)image.Height
            };

            ushort[] bitsPerSampleValue = this.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb ? new ushort[] { 8, 8, 8 } : new ushort[] { 8 };
            var bitPerSample = new ExifShortArray(ExifTagValue.BitsPerSample)
            {
                Value = bitsPerSampleValue
            };

            var compression = new ExifShort(ExifTagValue.Compression)
            {
                // TODO: for the start, no compression is used.
                Value = (ushort)TiffCompression.None
            };

            var photometricInterpretation = new ExifShort(ExifTagValue.PhotometricInterpretation)
            {
                Value = (ushort)this.PhotometricInterpretation
            };

            var stripOffsets = new ExifLongArray(ExifTagValue.StripOffsets)
            {
                // TODO: we only write one image strip for the start.
                Value = new[] { imageDataStartOffset }
            };

            var samplesPerPixel = new ExifLong(ExifTagValue.SamplesPerPixel)
            {
                Value = 3
            };

            var rowsPerStrip = new ExifLong(ExifTagValue.RowsPerStrip)
            {
                // TODO: all rows in one strip for the start
                Value = (uint)image.Height
            };

            var stripByteCounts = new ExifLongArray(ExifTagValue.StripByteCounts)
            {
                Value = new[] { (uint)imageDataBytes }
            };

            var xResolution = new ExifRational(ExifTagValue.XResolution)
            {
                // TODO: what to use here as a default?
                Value = Rational.FromDouble(1.0d)
            };

            var yResolution = new ExifRational(ExifTagValue.YResolution)
            {
                // TODO: what to use here as a default?
                Value = Rational.FromDouble(1.0d)
            };

            var resolutionUnit = new ExifShort(ExifTagValue.ResolutionUnit)
            {
                // TODO: what to use here as default?
                Value = 0
            };

            var software = new ExifString(ExifTagValue.Software)
            {
                Value = "ImageSharp"
            };

            ifdEntries.Add(width);
            ifdEntries.Add(height);
            ifdEntries.Add(bitPerSample);
            ifdEntries.Add(compression);
            ifdEntries.Add(photometricInterpretation);
            ifdEntries.Add(stripOffsets);
            ifdEntries.Add(samplesPerPixel);
            ifdEntries.Add(rowsPerStrip);
            ifdEntries.Add(stripByteCounts);
            ifdEntries.Add(xResolution);
            ifdEntries.Add(yResolution);
            ifdEntries.Add(resolutionUnit);
            ifdEntries.Add(software);
        }
    }
}

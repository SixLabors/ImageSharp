// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using SixLabors.ImageSharp.Advanced;
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
        /// The quantizer for creating color palette image.
        /// </summary>
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Indicating whether to use horizontal prediction. This can improve the compression ratio with deflate compression.
        /// </summary>
        private bool useHorizontalPredictor;

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
        }

        /// <summary>
        /// Gets or sets the photometric interpretation implementation to use when encoding the image.
        /// </summary>
        private TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <summary>
        /// Gets the compression implementation to use when encoding the image.
        /// </summary>
        private TiffEncoderCompression CompressionType { get; }

        /// <summary>
        /// Gets or sets the encoding mode to use. RGB, RGB with color palette or gray.
        /// </summary>
        private TiffEncodingMode Mode { get; set; }

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
            IExifValue colorMap = null;
            var ifdEntries = new List<IExifValue>();

            // Write the image bytes to the steam.
            var imageDataStart = (uint)writer.Position;
            int imageDataBytes;
            switch (this.Mode)
            {
                case TiffEncodingMode.ColorPalette:
                    imageDataBytes = writer.WritePalettedRgb(image, this.quantizer, this.padding, this.CompressionType, this.useHorizontalPredictor, out colorMap);
                    break;
                case TiffEncodingMode.Gray:
                    imageDataBytes = writer.WriteGray(image, this.padding, this.CompressionType, this.useHorizontalPredictor);
                    break;
                case TiffEncodingMode.BiColor:
                    imageDataBytes = writer.WriteBiColor(image, this.CompressionType);
                    break;
                default:
                    imageDataBytes = writer.WriteRgb(image, this.padding, this.CompressionType, this.useHorizontalPredictor);
                    break;
            }

            // Write info's about the image to the stream.
            this.AddImageFormat(image, ifdEntries, imageDataStart, imageDataBytes);
            if (this.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor)
            {
                ifdEntries.Add(colorMap);
            }

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

            ushort[] bitsPerSampleValue = this.GetBitsPerSampleValue();
            var bitPerSample = new ExifShortArray(ExifTagValue.BitsPerSample)
            {
                Value = bitsPerSampleValue
            };

            ushort compressionType = this.GetCompressionType();
            var compression = new ExifShort(ExifTagValue.Compression)
            {
                Value = compressionType
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
                Value = this.GetSamplesPerPixel()
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
                Value = 3 // 3 is centimeter.
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

            if (this.useHorizontalPredictor)
            {
                if (this.Mode == TiffEncodingMode.Rgb || this.Mode == TiffEncodingMode.Gray || this.Mode == TiffEncodingMode.ColorPalette)
                {
                    var predictor = new ExifShort(ExifTagValue.Predictor) { Value = (ushort)TiffPredictor.Horizontal };

                    ifdEntries.Add(predictor);
                }
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

        private uint GetSamplesPerPixel()
        {
            switch (this.PhotometricInterpretation)
            {
                case TiffPhotometricInterpretation.Rgb:
                    return 3;
                case TiffPhotometricInterpretation.PaletteColor:
                case TiffPhotometricInterpretation.BlackIsZero:
                case TiffPhotometricInterpretation.WhiteIsZero:
                    return 1;
                default:
                    return 3;
            }
        }

        private ushort[] GetBitsPerSampleValue()
        {
            switch (this.PhotometricInterpretation)
            {
                case TiffPhotometricInterpretation.PaletteColor:
                    return new ushort[] { 8 };
                case TiffPhotometricInterpretation.Rgb:
                    return new ushort[] { 8, 8, 8 };
                case TiffPhotometricInterpretation.WhiteIsZero:
                    if (this.Mode == TiffEncodingMode.BiColor)
                    {
                        return new ushort[] { 1 };
                    }

                    return new ushort[] { 8 };
                case TiffPhotometricInterpretation.BlackIsZero:
                    if (this.Mode == TiffEncodingMode.BiColor)
                    {
                        return new ushort[] { 1 };
                    }

                    return new ushort[] { 8 };
                default:
                    return new ushort[] { 8, 8, 8 };
            }
        }

        private ushort GetCompressionType()
        {
            if (this.CompressionType == TiffEncoderCompression.Deflate && this.Mode == TiffEncodingMode.Rgb)
            {
                return (ushort)TiffCompression.Deflate;
            }

            if (this.CompressionType == TiffEncoderCompression.Lzw && this.Mode == TiffEncodingMode.Rgb)
            {
                return (ushort)TiffCompression.Lzw;
            }

            if (this.CompressionType == TiffEncoderCompression.PackBits && this.Mode == TiffEncodingMode.Rgb)
            {
                return (ushort)TiffCompression.PackBits;
            }

            if (this.CompressionType == TiffEncoderCompression.Deflate && this.Mode == TiffEncodingMode.Gray)
            {
                return (ushort)TiffCompression.Deflate;
            }

            if (this.CompressionType == TiffEncoderCompression.Lzw && this.Mode == TiffEncodingMode.Gray)
            {
                return (ushort)TiffCompression.Lzw;
            }

            if (this.CompressionType == TiffEncoderCompression.PackBits && this.Mode == TiffEncodingMode.Gray)
            {
                return (ushort)TiffCompression.PackBits;
            }

            if (this.CompressionType == TiffEncoderCompression.Deflate && this.Mode == TiffEncodingMode.ColorPalette)
            {
                return (ushort)TiffCompression.Deflate;
            }

            if (this.CompressionType == TiffEncoderCompression.PackBits && this.Mode == TiffEncodingMode.ColorPalette)
            {
                return (ushort)TiffCompression.PackBits;
            }

            if (this.CompressionType == TiffEncoderCompression.Deflate && this.Mode == TiffEncodingMode.BiColor)
            {
                return (ushort)TiffCompression.Deflate;
            }

            if (this.CompressionType == TiffEncoderCompression.PackBits && this.Mode == TiffEncodingMode.BiColor)
            {
                return (ushort)TiffCompression.PackBits;
            }

            if (this.CompressionType == TiffEncoderCompression.CcittGroup3Fax && this.Mode == TiffEncodingMode.BiColor)
            {
                return (ushort)TiffCompression.CcittGroup3Fax;
            }

            if (this.CompressionType == TiffEncoderCompression.ModifiedHuffman && this.Mode == TiffEncodingMode.BiColor)
            {
                return (ushort)TiffCompression.Ccitt1D;
            }

            return (ushort)TiffCompression.None;
        }
    }
}

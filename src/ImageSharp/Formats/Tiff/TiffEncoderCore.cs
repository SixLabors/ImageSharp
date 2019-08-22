// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Performs the TIFF encoding operation.
    /// </summary>
    internal sealed class TiffEncoderCore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        public TiffEncoderCore(ITiffEncoderOptions options)
        {
            options = options ?? new TiffEncoder();
        }

        /// <summary>
        /// Gets or sets the photometric interpretation implementation to use when encoding the image.
        /// </summary>
        public TiffColorType ColorType { get; set; }

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
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            using (TiffWriter writer = new TiffWriter(stream))
            {
                long firstIfdMarker = this.WriteHeader(writer);
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
            ushort byteOrderMarker = BitConverter.IsLittleEndian ? TiffConstants.ByteOrderLittleEndianShort
                                                                 : TiffConstants.ByteOrderBigEndianShort;

            writer.Write(byteOrderMarker);
            writer.Write((ushort)42);
            long firstIfdMarker = writer.PlaceMarker();

            return firstIfdMarker;
        }

        /// <summary>
        /// Writes a TIFF IFD block.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> to write data to.</param>
        /// <param name="entries">The IFD entries to write to the file.</param>
        /// <returns>The marker to write the next IFD offset (if present).</returns>
        public long WriteIfd(TiffWriter writer, List<TiffIfdEntry> entries)
        {
            if (entries.Count == 0)
            {
                throw new ArgumentException("There must be at least one entry per IFD.", nameof(entries));
            }

            uint dataOffset = (uint)writer.Position + (uint)(6 + (entries.Count * 12));
            List<byte[]> largeDataBlocks = new List<byte[]>();

            entries.Sort((a, b) => a.Tag - b.Tag);

            writer.Write((ushort)entries.Count);

            foreach (TiffIfdEntry entry in entries)
            {
                writer.Write(entry.Tag);
                writer.Write((ushort)entry.Type);
                writer.Write(entry.Count);

                if (entry.Value.Length <= 4)
                {
                    writer.WritePadded(entry.Value);
                }
                else
                {
                    largeDataBlocks.Add(entry.Value);
                    writer.Write(dataOffset);
                    dataOffset += (uint)(entry.Value.Length + (entry.Value.Length % 2));
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
        /// Writes all data required to define an image
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="writer">The <see cref="BinaryWriter"/> to write data to.</param>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="ifdOffset">The marker to write this IFD offset.</param>
        /// <returns>The marker to write the next IFD offset (if present).</returns>
        public long WriteImage<TPixel>(TiffWriter writer, Image<TPixel> image, long ifdOffset)
            where TPixel : struct, IPixel<TPixel>
        {
            List<TiffIfdEntry> ifdEntries = new List<TiffIfdEntry>();

            this.AddImageFormat(image, ifdEntries);
            this.AddMetadata(image, ifdEntries);

            writer.WriteMarker(ifdOffset, (uint)writer.Position);
            long nextIfdMarker = this.WriteIfd(writer, ifdEntries);

            return nextIfdMarker;
        }

        /// <summary>
        /// Adds image metadata to the specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="ifdEntries">The metadata entries to add to the IFD.</param>
        public void AddMetadata<TPixel>(Image<TPixel> image, List<TiffIfdEntry> ifdEntries)
            where TPixel : struct, IPixel<TPixel>
        {
            ifdEntries.AddUnsignedRational(TiffTags.XResolution, new Rational(image.Metadata.HorizontalResolution));
            ifdEntries.AddUnsignedRational(TiffTags.YResolution, new Rational(image.Metadata.VerticalResolution));
            ifdEntries.AddUnsignedShort(TiffTags.ResolutionUnit, (uint)TiffResolutionUnit.Inch);

            TiffMetaData tiffMetadata = image.Metadata.GetFormatMetadata(TiffFormat.Instance);
            foreach (TiffMetadataTag metadata in tiffMetadata.TextTags)
            {
                switch (metadata.Name)
                {
                    case TiffMetadataNames.Artist:
                    {
                        ifdEntries.AddAscii(TiffTags.Artist, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.Copyright:
                    {
                        ifdEntries.AddAscii(TiffTags.Copyright, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.DateTime:
                    {
                        ifdEntries.AddAscii(TiffTags.DateTime, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.HostComputer:
                    {
                        ifdEntries.AddAscii(TiffTags.HostComputer, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.ImageDescription:
                    {
                        ifdEntries.AddAscii(TiffTags.ImageDescription, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.Make:
                    {
                        ifdEntries.AddAscii(TiffTags.Make, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.Model:
                    {
                        ifdEntries.AddAscii(TiffTags.Model, metadata.Value);
                        break;
                    }

                    case TiffMetadataNames.Software:
                    {
                        ifdEntries.AddAscii(TiffTags.Software, metadata.Value);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds image format information to the specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="ifdEntries">The image format entries to add to the IFD.</param>
        public void AddImageFormat<TPixel>(Image<TPixel> image, List<TiffIfdEntry> ifdEntries)
        where TPixel : struct, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }
    }
}

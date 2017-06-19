// <copyright file="TiffEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using ImageSharp.Formats.Tiff;
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    using Quantizers;

    using static ComparableExtensions;

    /// <summary>
    /// Performs the TIFF encoding operation.
    /// </summary>
    internal sealed class TiffEncoderCore
    {
        /// <summary>
        /// The options for the encoder.
        /// </summary>
        private readonly ITiffEncoderOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        public TiffEncoderCore(ITiffEncoderOptions options)
        {
            this.options = options ?? new TiffEncoderOptions();
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TPixel}"/> to encode from.</param>
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
        /// <param name="image">The <see cref="ImageBase{TPixel}"/> to encode from.</param>
        /// <param name="ifdOffset">The marker to write this IFD offset.</param>
        /// <returns>The marker to write the next IFD offset (if present).</returns>
        public long WriteImage<TPixel>(TiffWriter writer, Image<TPixel> image, long ifdOffset)
            where TPixel : struct, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }
    }
}
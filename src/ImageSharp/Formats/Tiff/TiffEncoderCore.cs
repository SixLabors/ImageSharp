// <copyright file="TiffEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Buffers;
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

            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                this.WriteHeader(writer, 0);
            }
        }

        /// <summary>
        /// Writes the TIFF file header.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> to write data to.</param>
        /// <param name="firstIfdOffset">The byte offset to the first IFD in the file.</param>
        public void WriteHeader(BinaryWriter writer, uint firstIfdOffset)
        {
            if (firstIfdOffset == 0 || firstIfdOffset % TiffConstants.SizeOfWordBoundary != 0)
            {
                throw new ArgumentException("IFD offsets must be non-zero and on a word boundary.", nameof(firstIfdOffset));
            }

            ushort byteOrderMarker = BitConverter.IsLittleEndian ? TiffConstants.ByteOrderLittleEndianShort
                                                                 : TiffConstants.ByteOrderBigEndianShort;

            writer.Write(byteOrderMarker);
            writer.Write((ushort)42);
            writer.Write(firstIfdOffset);
        }
    }
}
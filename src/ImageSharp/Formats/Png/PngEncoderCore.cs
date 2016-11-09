﻿// <copyright file="PngEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Quantizers;

    /// <summary>
    /// Performs the png encoding operation.
    /// TODO: Perf. There's lots of array parsing and copying going on here. This should be unmanaged.
    /// </summary>
    internal sealed class PngEncoderCore
    {
        /// <summary>
        /// The maximum block size, defaults at 64k for uncompressed blocks.
        /// </summary>
        private const int MaxBlockSize = 65535;

        /// <summary>
        /// Reusable buffer for writing chunk types.
        /// </summary>
        private readonly byte[] chunkTypeBuffer = new byte[4];

        /// <summary>
        /// Reusable buffer for writing chunk data.
        /// </summary>
        private readonly byte[] chunkDataBuffer = new byte[16];


        /// <summary>
        /// Contains the raw pixel data from the image.
        /// </summary>
        private byte[] pixelData;

        /// <summary>
        /// The image width.
        /// </summary>
        private int width;

        /// <summary>
        /// The image height.
        /// </summary>
        private int height;

        /// <summary>
        /// The number of bits required to encode the colors in the png.
        /// </summary>
        private byte bitDepth;

        /// <summary>
        /// The number of bytes per pixel.
        /// </summary>
        private int bytesPerPixel;

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        public PngColorType PngColorType { get; set; }

        /// <summary>
        /// Gets or sets the compression level 1-9.
        /// <remarks>Defaults to 6.</remarks>
        /// </summary>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// Gets or sets a value indicating whether this instance should write
        /// gamma information to the stream. The default value is false.
        /// </summary>
        public bool WriteGamma { get; set; }

        /// <summary>
        /// Gets or sets the gamma value, that will be written
        /// the the stream, when the <see cref="WriteGamma"/> property
        /// is set to true. The default value is 2.2F.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        public float Gamma { get; set; } = 2.2F;

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageBase{TColor, TPacked}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor, TPacked}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TColor, TPacked>(ImageBase<TColor, TPacked> image, Stream stream)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.width = image.Width;
            this.height = image.Height;

            // Write the png header.
            stream.Write(
                new byte[]
                    {
                    0x89, // Set the high bit.
                    0x50, // P
                    0x4E, // N
                    0x47, // G
                    0x0D, // Line ending CRLF
                    0x0A, // Line ending CRLF
                    0x1A, // EOF
                    0x0A  // LF
                    },
                0,
                8);

            // Ensure that quality can be set but has a fallback.
            int quality = this.Quality > 0 ? this.Quality : image.Quality;
            this.Quality = quality > 0 ? quality.Clamp(1, int.MaxValue) : int.MaxValue;

            // Set correct color type if the color count is 256 or less.
            if (this.Quality <= 256)
            {
                this.PngColorType = PngColorType.Palette;
            }

            // Set correct bit depth.
            this.bitDepth = this.Quality <= 256
                               ? (byte)ImageMaths.GetBitsNeededForColorDepth(this.Quality).Clamp(1, 8)
                               : (byte)8;

            // Png only supports in four pixel depths: 1, 2, 4, and 8 bits when using the PLTE chunk
            if (this.bitDepth == 3)
            {
                this.bitDepth = 4;
            }
            else if (this.bitDepth >= 5 || this.bitDepth <= 7)
            {
                this.bitDepth = 8;
            }

            this.bytesPerPixel = this.CalculateBytesPerPixel();

            PngHeader header = new PngHeader
            {
                Width = image.Width,
                Height = image.Height,
                ColorType = (byte)this.PngColorType,
                BitDepth = this.bitDepth,
                FilterMethod = 0, // None
                CompressionMethod = 0,
                InterlaceMethod = 0
            };

            this.WriteHeaderChunk(stream, header);

            // Collect the pixel data
            // TODO: Avoid doing this all at once and try row by row.
            if (this.PngColorType == PngColorType.Palette)
            {
                this.CollectIndexedBytes(image, stream, header);
            }
            else if (this.PngColorType == PngColorType.Grayscale || this.PngColorType == PngColorType.GrayscaleWithAlpha)
            {
                this.CollectGrayscaleBytes(image);
            }
            else
            {
                // this.CollectColorBytes(image);
            }

            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);
            this.WriteDataChunks(image, stream);
            this.WriteEndChunk(stream);
            stream.Flush();
        }

        /// <summary>
        /// Writes an integer to the byte array.
        /// </summary>
        /// <param name="data">The <see cref="T:byte[]"/> containing image data.</param>
        /// <param name="offset">The amount to offset by.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteInteger(byte[] data, int offset, int value)
        {
            byte[] buffer = BitConverter.GetBytes(value);

            buffer.ReverseBytes();
            Buffer.BlockCopy(buffer, 0, data, offset, 4);
        }

        /// <summary>
        /// Writes an integer to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteInteger(Stream stream, int value)
        {
            byte[] buffer = BitConverter.GetBytes(value);

            buffer.ReverseBytes();
            stream.Write(buffer, 0, 4);
        }

        /// <summary>
        /// Writes an unsigned integer to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteInteger(Stream stream, uint value)
        {
            byte[] buffer = BitConverter.GetBytes(value);

            buffer.ReverseBytes();
            stream.Write(buffer, 0, 4);
        }

        /// <summary>
        /// Collects the indexed pixel data.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        private void CollectIndexedBytes<TColor, TPacked>(ImageBase<TColor, TPacked> image, Stream stream, PngHeader header)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            // Quatize the image and get the pixels
            QuantizedImage<TColor, TPacked> quantized = this.WritePaletteChunk(stream, header, image);
            this.pixelData = quantized.Pixels;
        }

        /// <summary>
        /// Collects the grayscale pixel data.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to encode.</param>
        private void CollectGrayscaleBytes<TColor, TPacked>(ImageBase<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            // Copy the pixels across from the image.
            this.pixelData = new byte[this.width * this.height * this.bytesPerPixel];
            int stride = this.width * this.bytesPerPixel;
            byte[] bytes = new byte[4];
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                for (int y = 0; y < this.height; y++)
                {
                    for (int x = 0; x < this.width; x++)
                    {
                        // Convert the color to YCbCr and store the luminance
                        // Optionally store the original color alpha.
                        int dataOffset = (y * stride) + (x * this.bytesPerPixel);
                        pixels[x, y].ToBytes(bytes, 0, ComponentOrder.XYZW);
                        YCbCr luminance = new Color(bytes[0], bytes[1], bytes[2], bytes[3]);
                        for (int i = 0; i < this.bytesPerPixel; i++)
                        {
                            if (i == 0)
                            {
                                this.pixelData[dataOffset] = (byte)luminance.Y;
                            }
                            else
                            {
                                this.pixelData[dataOffset + i] = bytes[3];
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Collects the true color pixel data.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="row">The row index.</param>
        /// <param name="rawScanline">The raw Scanline.</param>
        private void CollectColorBytes<TColor, TPacked>(ImageBase<TColor, TPacked> image, int row, byte[] rawScanline)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                int bpp = this.bytesPerPixel;
                for (int x = 0; x < this.width; x++)
                {
                    pixels[x, row].ToBytes(rawScanline, x * this.bytesPerPixel, bpp == 4 ? ComponentOrder.XYZW : ComponentOrder.XYZ);
                }

                if (rawScanline.Any(x => x > 0))
                {
                    var t = rawScanline;
                }
            }
        }

        /// <summary>
        /// Encodes the pixel data line by line.
        /// Each scanline is encoded in the most optimal manner to improve compression.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="row">The row.</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="rawScanline">The raw scanline.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private byte[] EncodePixelRow<TColor, TPacked>(ImageBase<TColor, TPacked> image, int row, byte[] previousScanline, byte[] rawScanline, int bytesPerScanline)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            if (this.PngColorType == PngColorType.Palette || this.PngColorType == PngColorType.Grayscale || this.PngColorType == PngColorType.GrayscaleWithAlpha)
            {
                Buffer.BlockCopy(this.pixelData, row * bytesPerScanline, rawScanline, 0, bytesPerScanline);
            }
            else
            {
                this.CollectColorBytes(image, row, rawScanline);
            }

            byte[] filteredScanline = this.GetOptimalFilteredScanline(rawScanline, previousScanline, bytesPerScanline, this.bytesPerPixel);

            return filteredScanline;
        }

        /// <summary>
        /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
        /// to be most compressible, using lowest total variation as proxy for compressibility.
        /// </summary>
        /// <param name="rawScanline">The raw scanline</param>
        /// <param name="previousScanline">The previous scanline</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <param name="bytesPerPixel">The number of bytes per pixel</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private byte[] GetOptimalFilteredScanline(byte[] rawScanline, byte[] previousScanline, int bytesPerScanline, int bytesPerPixel)
        {
            Tuple<byte[], int>[] candidates = new Tuple<byte[], int>[4];

            byte[] sub = SubFilter.Encode(rawScanline, bytesPerPixel, bytesPerScanline);
            candidates[0] = new Tuple<byte[], int>(sub, this.CalculateTotalVariation(sub));

            byte[] up = UpFilter.Encode(rawScanline, bytesPerScanline, previousScanline);
            candidates[1] = new Tuple<byte[], int>(up, this.CalculateTotalVariation(up));

            byte[] average = AverageFilter.Encode(rawScanline, previousScanline, bytesPerPixel, bytesPerScanline);
            candidates[2] = new Tuple<byte[], int>(average, this.CalculateTotalVariation(average));

            byte[] paeth = PaethFilter.Encode(rawScanline, previousScanline, bytesPerPixel, bytesPerScanline);
            candidates[3] = new Tuple<byte[], int>(paeth, this.CalculateTotalVariation(paeth));

            int lowestTotalVariation = int.MaxValue;
            int lowestTotalVariationIndex = 0;

            for (int i = 0; i < 4; i++)
            {
                if (candidates[i].Item2 < lowestTotalVariation)
                {
                    lowestTotalVariationIndex = i;
                    lowestTotalVariation = candidates[i].Item2;
                }
            }

            return candidates[lowestTotalVariationIndex].Item1;
        }

        /// <summary>
        /// Calculates the total variation of given byte array. Total variation is the sum of the absolute values of
        /// neighbor differences.
        /// </summary>
        /// <param name="input">The scanline bytes</param>
        /// <returns>The <see cref="int"/></returns>
        private int CalculateTotalVariation(byte[] input)
        {
            int totalVariation = 0;

            for (int i = 1; i < input.Length; i++)
            {
                totalVariation += Math.Abs(input[i] - input[i - 1]);
            }

            return totalVariation;
        }

        /// <summary>
        /// Calculates the correct number of bytes per pixel for the given color type.
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        private int CalculateBytesPerPixel()
        {
            switch (this.PngColorType)
            {
                case PngColorType.Grayscale:
                    return 1;

                case PngColorType.GrayscaleWithAlpha:
                    return 2;

                case PngColorType.Palette:
                    return 1;

                case PngColorType.Rgb:
                    return 3;

                // PngColorType.RgbWithAlpha
                // TODO: Maybe figure out a way to detect if there are any transparent
                // pixels and encode RGB if none.
                default:
                    return 4;
            }
        }

        /// <summary>
        /// Writes the header chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        private void WriteHeaderChunk(Stream stream, PngHeader header)
        {
            WriteInteger(this.chunkDataBuffer, 0, header.Width);
            WriteInteger(this.chunkDataBuffer, 4, header.Height);

            this.chunkDataBuffer[8] = header.BitDepth;
            this.chunkDataBuffer[9] = header.ColorType;
            this.chunkDataBuffer[10] = header.CompressionMethod;
            this.chunkDataBuffer[11] = header.FilterMethod;
            this.chunkDataBuffer[12] = header.InterlaceMethod;

            this.WriteChunk(stream, PngChunkTypes.Header, this.chunkDataBuffer, 0, 13);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        /// <param name="image">The image to encode.</param>
        /// <returns>The <see cref="QuantizedImage{TColor, TPacked}"/></returns>
        private QuantizedImage<TColor, TPacked> WritePaletteChunk<TColor, TPacked>(Stream stream, PngHeader header, ImageBase<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            if (this.Quality > 256)
            {
                return null;
            }

            if (this.Quantizer == null)
            {
                this.Quantizer = new WuQuantizer<TColor, TPacked>();
            }

            // Quantize the image returning a palette. This boxing is icky.
            QuantizedImage<TColor, TPacked> quantized = ((IQuantizer<TColor, TPacked>)this.Quantizer).Quantize(image, this.Quality);

            // Grab the palette and write it to the stream.
            TColor[] palette = quantized.Palette;
            int pixelCount = palette.Length;
            List<byte> transparentPixels = new List<byte>();

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, header.BitDepth) * 3;
            byte[] colorTable = new byte[colorTableLength];

            // TODO: Optimize this.
            Parallel.For(
                0,
                pixelCount,
                Bootstrapper.Instance.ParallelOptions,
                i =>
                {
                    int offset = i * 3;
                    Color color = new Color(palette[i].ToVector4());
                    int alpha = color.A;

                    // Premultiply the color. This helps prevent banding.
                    if (alpha < 255 && alpha > this.Threshold)
                    {
                        color = Color.Multiply(color, new Color(alpha, alpha, alpha, 255));
                    }

                    colorTable[offset] = color.R;
                    colorTable[offset + 1] = color.G;
                    colorTable[offset + 2] = color.B;

                    if (alpha <= this.Threshold)
                    {
                        transparentPixels.Add((byte)offset);
                    }
                });

            this.WriteChunk(stream, PngChunkTypes.Palette, colorTable);

            // Write the transparency data
            if (transparentPixels.Any())
            {
                this.WriteChunk(stream, PngChunkTypes.PaletteAlpha, transparentPixels.ToArray());
            }

            return quantized;
        }

        /// <summary>
        /// Writes the physical dimension information to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="imageBase">The image base.</param>
        private void WritePhysicalChunk<TColor, TPacked>(Stream stream, ImageBase<TColor, TPacked> imageBase)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            Image<TColor, TPacked> image = imageBase as Image<TColor, TPacked>;
            if (image != null && image.HorizontalResolution > 0 && image.VerticalResolution > 0)
            {
                // 39.3700787 = inches in a meter.
                int dpmX = (int)Math.Round(image.HorizontalResolution * 39.3700787D);
                int dpmY = (int)Math.Round(image.VerticalResolution * 39.3700787D);

                WriteInteger(this.chunkDataBuffer, 0, dpmX);
                WriteInteger(this.chunkDataBuffer, 4, dpmY);

                this.chunkDataBuffer[8] = 1;

                this.WriteChunk(stream, PngChunkTypes.Physical, this.chunkDataBuffer, 0, 9);
            }
        }

        /// <summary>
        /// Writes the gamma information to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void WriteGammaChunk(Stream stream)
        {
            if (this.WriteGamma)
            {
                int gammaValue = (int)(this.Gamma * 100000F);

                byte[] size = BitConverter.GetBytes(gammaValue);

                this.chunkDataBuffer[0] = size[3];
                this.chunkDataBuffer[1] = size[2];
                this.chunkDataBuffer[2] = size[1];
                this.chunkDataBuffer[3] = size[0];

                this.WriteChunk(stream, PngChunkTypes.Gamma, this.chunkDataBuffer, 0, 4);
            }
        }

        /// <summary>
        /// Writes the pixel information to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="stream">The stream.</param>
        private void WriteDataChunks<TColor, TPacked>(ImageBase<TColor, TPacked> image, Stream stream)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            int bytesPerScanline = this.width * this.bytesPerPixel;

            // TODO: These could be rented
            byte[] previousScanline = new byte[bytesPerScanline];
            byte[] rawScanline = new byte[bytesPerScanline];

            byte[] buffer;
            int bufferLength;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (ZlibDeflateStream deflateStream = new ZlibDeflateStream(memoryStream, this.CompressionLevel))
                {
                    for (int y = 0; y < this.height; y++)
                    {
                        byte[] data = this.EncodePixelRow(image, y, previousScanline, rawScanline, bytesPerScanline);
                        deflateStream.Write(data, 0, data.Length);
                        deflateStream.Flush();

                        // Do a bit of shuffling;
                        byte[] tmp = rawScanline;
                        rawScanline = previousScanline;
                        previousScanline = tmp;
                    }

                    bufferLength = (int)memoryStream.Length;
                    buffer = memoryStream.ToArray();
                }
            }
            finally
            {
                memoryStream?.Dispose();
            }

            int numChunks = bufferLength / MaxBlockSize;

            if (bufferLength % MaxBlockSize != 0)
            {
                numChunks++;
            }

            for (int i = 0; i < numChunks; i++)
            {
                int length = bufferLength - (i * MaxBlockSize);

                if (length > MaxBlockSize)
                {
                    length = MaxBlockSize;
                }

                this.WriteChunk(stream, PngChunkTypes.Data, buffer, i * MaxBlockSize, length);
            }
        }

        /// <summary>
        /// Writes the chunk end to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void WriteEndChunk(Stream stream)
        {
            this.WriteChunk(stream, PngChunkTypes.End, null);
        }

        /// <summary>
        /// Writes a chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="type">The type of chunk to write.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing data.</param>
        private void WriteChunk(Stream stream, string type, byte[] data)
        {
            this.WriteChunk(stream, type, data, 0, data?.Length ?? 0);
        }

        /// <summary>
        /// Writes a chunk of a specified length to the stream at the given offset.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="type">The type of chunk to write.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing data.</param>
        /// <param name="offset">The position to offset the data at.</param>
        /// <param name="length">The of the data to write.</param>
        private void WriteChunk(Stream stream, string type, byte[] data, int offset, int length)
        {
            // Chunk length
            WriteInteger(stream, length);

            // Chunk type
            this.chunkTypeBuffer[0] = (byte)type[0];
            this.chunkTypeBuffer[1] = (byte)type[1];
            this.chunkTypeBuffer[2] = (byte)type[2];
            this.chunkTypeBuffer[3] = (byte)type[3];

            stream.Write(this.chunkTypeBuffer, 0, 4);

            Crc32 crc32 = new Crc32();
            crc32.Update(this.chunkTypeBuffer);

            // Chunk data
            if (data != null)
            {
                stream.Write(data, offset, length);
                crc32.Update(data, offset, length);
            }

            WriteInteger(stream, (uint)crc32.Value);
        }
    }
}

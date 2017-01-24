// <copyright file="PngEncoderCore.cs" company="James Jackson-South">
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

    using Quantizers;

    using static ComparableExtensions;

    /// <summary>
    /// Performs the png encoding operation.
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
        /// Reusable crc for validating chunks.
        /// </summary>
        private readonly Crc32 crc = new Crc32();

        /// <summary>
        /// Contains the raw pixel data from an indexed image.
        /// </summary>
        private byte[] palettePixelData;

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
        /// The buffer for the sub filter
        /// </summary>
        private byte[] sub;

        /// <summary>
        /// The buffer for the up filter
        /// </summary>
        private byte[] up;

        /// <summary>
        /// The buffer for the average filter
        /// </summary>
        private byte[] average;

        /// <summary>
        /// The buffer for the paeth filter
        /// </summary>
        private byte[] paeth;

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
        /// Encodes the image to the specified stream from the <see cref="ImageBase{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TColor>(ImageBase<TColor> image, Stream stream)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.width = image.Width;
            this.height = image.Height;

            // Write the png header.
            this.chunkDataBuffer[0] = 0x89; // Set the high bit.
            this.chunkDataBuffer[1] = 0x50; // P
            this.chunkDataBuffer[2] = 0x4E; // N
            this.chunkDataBuffer[3] = 0x47; // G
            this.chunkDataBuffer[4] = 0x0D; // Line ending CRLF
            this.chunkDataBuffer[5] = 0x0A; // Line ending CRLF
            this.chunkDataBuffer[6] = 0x1A; // EOF
            this.chunkDataBuffer[7] = 0x0A; // LF

            stream.Write(this.chunkDataBuffer, 0, 8);

            // Ensure that quality can be set but has a fallback.
            int quality = this.Quality > 0 ? this.Quality : image.Quality;
            this.Quality = quality > 0 ? quality.Clamp(1, int.MaxValue) : int.MaxValue;

            // Set correct color type if the color count is 256 or less.
            if (this.Quality <= 256)
            {
                this.PngColorType = PngColorType.Palette;
            }

            if (this.PngColorType == PngColorType.Palette && this.Quality > 256)
            {
                this.Quality = 256;
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

            // Collect the indexed pixel data
            if (this.PngColorType == PngColorType.Palette)
            {
                this.CollectIndexedBytes(image, stream, header);
            }

            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                this.WriteDataChunks(pixels, stream);
            }

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
        /// <param name="image">The image to encode.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        private void CollectIndexedBytes<TColor>(ImageBase<TColor> image, Stream stream, PngHeader header)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // Quantize the image and get the pixels.
            QuantizedImage<TColor> quantized = this.WritePaletteChunk(stream, header, image);
            this.palettePixelData = quantized.Pixels;
        }

        /// <summary>
        /// Collects a row of grayscale pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The image pixels accessor.</param>
        /// <param name="row">The row index.</param>
        /// <param name="rawScanline">The raw scanline.</param>
        private void CollectGrayscaleBytes<TColor>(PixelAccessor<TColor> pixels, int row, byte[] rawScanline)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // Copy the pixels across from the image.
            // Reuse the chunk type buffer.
            for (int x = 0; x < this.width; x++)
            {
                // Convert the color to YCbCr and store the luminance
                // Optionally store the original color alpha.
                int offset = x * this.bytesPerPixel;
                pixels[x, row].ToXyzwBytes(this.chunkTypeBuffer, 0);
                byte luminance = (byte)((0.299F * this.chunkTypeBuffer[0]) + (0.587F * this.chunkTypeBuffer[1]) + (0.114F * this.chunkTypeBuffer[2]));

                for (int i = 0; i < this.bytesPerPixel; i++)
                {
                    if (i == 0)
                    {
                        rawScanline[offset] = luminance;
                    }
                    else
                    {
                        rawScanline[offset + i] = this.chunkTypeBuffer[3];
                    }
                }
            }
        }

        /// <summary>
        /// Collects a row of true color pixel data.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The image pixel accessor.</param>
        /// <param name="row">The row index.</param>
        /// <param name="rawScanline">The raw scanline.</param>
        private void CollectColorBytes<TColor>(PixelAccessor<TColor> pixels, int row, byte[] rawScanline)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // We can use the optimized PixelAccessor here and copy the bytes in unmanaged memory.
            using (PixelArea<TColor> pixelRow = new PixelArea<TColor>(this.width, rawScanline, this.bytesPerPixel == 4 ? ComponentOrder.Xyzw : ComponentOrder.Xyz))
            {
                pixels.CopyTo(pixelRow, row);
            }
        }

        /// <summary>
        /// Encodes the pixel data line by line.
        /// Each scanline is encoded in the most optimal manner to improve compression.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="pixels">The image pixel accessor.</param>
        /// <param name="row">The row.</param>
        /// <param name="previousScanline">The previous scanline.</param>
        /// <param name="rawScanline">The raw scanline.</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private byte[] EncodePixelRow<TColor>(PixelAccessor<TColor> pixels, int row, byte[] previousScanline, byte[] rawScanline, byte[] result)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            switch (this.PngColorType)
            {
                case PngColorType.Palette:
                    Buffer.BlockCopy(this.palettePixelData, row * rawScanline.Length, rawScanline, 0, rawScanline.Length);
                    break;
                case PngColorType.Grayscale:
                case PngColorType.GrayscaleWithAlpha:
                    this.CollectGrayscaleBytes(pixels, row, rawScanline);
                    break;
                default:
                    this.CollectColorBytes(pixels, row, rawScanline);
                    break;
            }

            return this.GetOptimalFilteredScanline(rawScanline, previousScanline, result);
        }

        /// <summary>
        /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
        /// to be most compressible, using lowest total variation as proxy for compressibility.
        /// </summary>
        /// <param name="rawScanline">The raw scanline</param>
        /// <param name="previousScanline">The previous scanline</param>
        /// <param name="result">The filtered scanline result.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private byte[] GetOptimalFilteredScanline(byte[] rawScanline, byte[] previousScanline, byte[] result)
        {
            // Palette images don't compress well with adaptive filtering.
            if (this.PngColorType == PngColorType.Palette || this.bitDepth < 8)
            {
                NoneFilter.Encode(rawScanline, result);
                return result;
            }

            // This order, while different to the enumerated order is more likely to produce a smaller sum
            // early on which shaves a couple of milliseconds off the processing time.
            UpFilter.Encode(rawScanline, previousScanline, this.up);
            int currentSum = this.CalculateTotalVariation(this.up, int.MaxValue);
            int lowestSum = currentSum;
            result = this.up;

            PaethFilter.Encode(rawScanline, previousScanline, this.paeth, this.bytesPerPixel);
            currentSum = this.CalculateTotalVariation(this.paeth, currentSum);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                result = this.paeth;
            }

            SubFilter.Encode(rawScanline, this.sub, this.bytesPerPixel);
            currentSum = this.CalculateTotalVariation(this.sub, int.MaxValue);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                result = this.sub;
            }

            AverageFilter.Encode(rawScanline, previousScanline, this.average, this.bytesPerPixel);
            currentSum = this.CalculateTotalVariation(this.average, currentSum);

            if (currentSum < lowestSum)
            {
                result = this.average;
            }

            return result;
        }

        /// <summary>
        /// Calculates the total variation of given byte array. Total variation is the sum of the absolute values of
        /// neighbor differences.
        /// </summary>
        /// <param name="scanline">The scanline bytes</param>
        /// <param name="lastSum">The last variation sum</param>
        /// <returns>The <see cref="int"/></returns>
        private int CalculateTotalVariation(byte[] scanline, int lastSum)
        {
            int sum = 0;

            for (int i = 1; i < scanline.Length; i++)
            {
                byte v = scanline[i];
                sum += v < 128 ? v : 256 - v;

                // No point continuing if we are larger.
                if (sum > lastSum)
                {
                    break;
                }
            }

            return sum;
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
            this.chunkDataBuffer[12] = (byte)header.InterlaceMethod;

            this.WriteChunk(stream, PngChunkTypes.Header, this.chunkDataBuffer, 0, 13);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        /// <param name="image">The image to encode.</param>
        /// <returns>The <see cref="QuantizedImage{TColor}"/></returns>
        private QuantizedImage<TColor> WritePaletteChunk<TColor>(Stream stream, PngHeader header, ImageBase<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (this.Quality > 256)
            {
                return null;
            }

            if (this.Quantizer == null)
            {
                this.Quantizer = new WuQuantizer<TColor>();
            }

            // Quantize the image returning a palette. This boxing is icky.
            QuantizedImage<TColor> quantized = ((IQuantizer<TColor>)this.Quantizer).Quantize(image, this.Quality);

            // Grab the palette and write it to the stream.
            TColor[] palette = quantized.Palette;
            int pixelCount = palette.Length;
            List<byte> transparentPixels = new List<byte>();

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, header.BitDepth) * 3;
            byte[] colorTable = ArrayPool<byte>.Shared.Rent(colorTableLength);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(4);

            try
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int offset = i * 3;
                    palette[i].ToXyzwBytes(bytes, 0);

                    int alpha = bytes[3];

                    colorTable[offset] = bytes[0];
                    colorTable[offset + 1] = bytes[1];
                    colorTable[offset + 2] = bytes[2];

                    if (alpha <= this.Threshold)
                    {
                        transparentPixels.Add((byte)offset);
                    }
                }

                this.WriteChunk(stream, PngChunkTypes.Palette, colorTable, 0, colorTableLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(colorTable);
                ArrayPool<byte>.Shared.Return(bytes);
            }

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
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="imageBase">The image base.</param>
        private void WritePhysicalChunk<TColor>(Stream stream, ImageBase<TColor> imageBase)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Image<TColor> image = imageBase as Image<TColor>;
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
        /// <param name="pixels">The pixel accessor.</param>
        /// <param name="stream">The stream.</param>
        private void WriteDataChunks<TColor>(PixelAccessor<TColor> pixels, Stream stream)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int bytesPerScanline = this.width * this.bytesPerPixel;
            byte[] previousScanline = new byte[bytesPerScanline];
            byte[] rawScanline = new byte[bytesPerScanline];
            int resultLength = bytesPerScanline + 1;
            byte[] result = new byte[resultLength];

            if (this.PngColorType != PngColorType.Palette)
            {
                this.sub = new byte[resultLength];
                this.up = new byte[resultLength];
                this.average = new byte[resultLength];
                this.paeth = new byte[resultLength];
            }

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
                        deflateStream.Write(this.EncodePixelRow(pixels, y, previousScanline, rawScanline, result), 0, resultLength);

                        Swap(ref rawScanline, ref previousScanline);
                    }
                }

                buffer = memoryStream.ToArray();
                bufferLength = buffer.Length;
            }
            finally
            {
                memoryStream?.Dispose();
            }

            // Store the chunks in repeated 64k blocks.
            // This reduces the memory load for decoding the image for many decoders.
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
            WriteInteger(stream, length);

            this.chunkTypeBuffer[0] = (byte)type[0];
            this.chunkTypeBuffer[1] = (byte)type[1];
            this.chunkTypeBuffer[2] = (byte)type[2];
            this.chunkTypeBuffer[3] = (byte)type[3];

            stream.Write(this.chunkTypeBuffer, 0, 4);

            if (data != null)
            {
                stream.Write(data, offset, length);
            }

            this.crc.Reset();
            this.crc.Update(this.chunkTypeBuffer);

            if (data != null && length > 0)
            {
                this.crc.Update(data, offset, length);
            }

            WriteInteger(stream, (uint)this.crc.Value);
        }
    }
}
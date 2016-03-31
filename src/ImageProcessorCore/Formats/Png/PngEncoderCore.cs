// <copyright file="PngEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using ImageProcessorCore.Quantizers;

    /// <summary>
    /// Performs the png encoding operation.
    /// TODO: Perf. There's lots of array parsing going on here. This should be unmanaged.
    /// </summary>
    internal class PngEncoderCore
    {
        /// <summary>
        /// The maximum block size, defaults at 64k for uncompressed blocks.
        /// </summary>
        private const int MaxBlockSize = 65535;

        /// <summary>
        /// The number of bits required to encode the colors in the png.
        /// </summary>
        private byte bitDepth;

        /// <summary>
        /// The quantized image result.
        /// </summary>
        private QuantizedImage quantized;

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        public int Quality { get; set; } = int.MaxValue;

        /// <summary>
        /// The compression level 1-9. 
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
        /// The quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = 128;

        /// <inheritdoc/>
        public void Encode(ImageBase image, Stream stream)
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

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

            this.Quality = image.Quality.Clamp(1, int.MaxValue);

            this.bitDepth = this.Quality <= 256
                               ? (byte)(this.GetBitsNeededForColorDepth(this.Quality).Clamp(1, 8))
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

            // TODO: Add more color options here.
            PngHeader header = new PngHeader
            {
                Width = image.Width,
                Height = image.Height,
                ColorType = (byte)(this.Quality <= 256 ? 3 : 6), // 3 = indexed, 6= Each pixel is an R,G,B triple, followed by an alpha sample.
                BitDepth = this.bitDepth,
                FilterMethod = 0, // None
                CompressionMethod = 0,
                InterlaceMethod = 0
            };

            this.WriteHeaderChunk(stream, header);
            this.WritePaletteChunk(stream, header, image);
            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);
            this.WriteDataChunks(stream, image);
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

            Array.Reverse(buffer);
            Array.Copy(buffer, 0, data, offset, 4);
        }

        /// <summary>
        /// Writes an integer to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteInteger(Stream stream, int value)
        {
            byte[] buffer = BitConverter.GetBytes(value);

            Array.Reverse(buffer);

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

            Array.Reverse(buffer);

            stream.Write(buffer, 0, 4);
        }

        /// <summary>
        /// Writes the header chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        private void WriteHeaderChunk(Stream stream, PngHeader header)
        {
            byte[] chunkData = new byte[13];

            WriteInteger(chunkData, 0, header.Width);
            WriteInteger(chunkData, 4, header.Height);

            chunkData[8] = header.BitDepth;
            chunkData[9] = header.ColorType;
            chunkData[10] = header.CompressionMethod;
            chunkData[11] = header.FilterMethod;
            chunkData[12] = header.InterlaceMethod;

            this.WriteChunk(stream, PngChunkTypes.Header, chunkData);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        /// <param name="image">The image to encode.</param>
        private void WritePaletteChunk(Stream stream, PngHeader header, ImageBase image)
        {
            if (this.Quality > 256)
            {
                return;
            }

            if (this.Quantizer == null)
            {
                this.Quantizer = new WuQuantizer { Threshold = this.Threshold };
            }

            // Quantize the image returning a palette.
            this.quantized = this.Quantizer.Quantize(image, this.Quality);

            // Grab the palette and write it to the stream.
            Bgra32[] palette = this.quantized.Palette;
            int pixelCount = palette.Length;

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, header.BitDepth) * 3;
            byte[] colorTable = new byte[colorTableLength];

            Parallel.For(0, pixelCount,
                i =>
                {
                    int offset = i * 3;
                    Bgra32 color = palette[i];

                    colorTable[offset] = color.R;
                    colorTable[offset + 1] = color.G;
                    colorTable[offset + 2] = color.B;
                });

            this.WriteChunk(stream, PngChunkTypes.Palette, colorTable);

            // Write the transparency data
            if (this.quantized.TransparentIndex > -1)
            {
                byte[] buffer = BitConverter.GetBytes(this.quantized.TransparentIndex);

                Array.Reverse(buffer);

                this.WriteChunk(stream, PngChunkTypes.PaletteAlpha, buffer);
            }
        }

        /// <summary>
        /// Writes the physical dimension information to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="imageBase">The image base.</param>
        private void WritePhysicalChunk(Stream stream, ImageBase imageBase)
        {
            Image image = imageBase as Image;
            if (image != null && image.HorizontalResolution > 0 && image.VerticalResolution > 0)
            {
                // 39.3700787 = inches in a meter.
                int dpmX = (int)Math.Round(image.HorizontalResolution * 39.3700787d);
                int dpmY = (int)Math.Round(image.VerticalResolution * 39.3700787d);

                byte[] chunkData = new byte[9];

                WriteInteger(chunkData, 0, dpmX);
                WriteInteger(chunkData, 4, dpmY);

                chunkData[8] = 1;

                this.WriteChunk(stream, PngChunkTypes.Physical, chunkData);
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
                int gammaValue = (int)(this.Gamma * 100000f);

                byte[] fourByteData = new byte[4];

                byte[] size = BitConverter.GetBytes(gammaValue);

                fourByteData[0] = size[3];
                fourByteData[1] = size[2];
                fourByteData[2] = size[1];
                fourByteData[3] = size[0];

                this.WriteChunk(stream, PngChunkTypes.Gamma, fourByteData);
            }
        }

        /// <summary>
        /// Writes the pixel information to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="image">The image base.</param>
        private void WriteDataChunks(Stream stream, ImageBase image)
        {
            byte[] data;
            int imageWidth = image.Width;
            int imageHeight = image.Height;

            // Indexed image.
            if (this.Quality <= 256)
            {
                int rowLength = imageWidth + 1;
                data = new byte[rowLength * imageHeight];

                Parallel.For(0, imageHeight, y =>
                {
                    int dataOffset = (y * rowLength);
                    byte compression = 0;
                    if (y > 0)
                    {
                        compression = 2;
                    }
                    data[dataOffset++] = compression;
                    for (int x = 0; x < imageWidth; x++)
                    {
                        data[dataOffset++] = this.quantized.Pixels[(y * imageWidth) + x];
                        if (y > 0)
                        {
                            data[dataOffset - 1] -= this.quantized.Pixels[((y - 1) * imageWidth) + x];
                        }
                    }
                });
            }
            else
            {
                // TrueColor image.
                data = new byte[(imageWidth * imageHeight * 4) + image.Height];

                int rowLength = (imageWidth * 4) + 1;

                Parallel.For(0, imageHeight, y =>
                {
                    byte compression = 0;
                    if (y > 0)
                    {
                        compression = 2;
                    }

                    data[y * rowLength] = compression;

                    for (int x = 0; x < imageWidth; x++)
                    {
                        Bgra32 color = Color.ToNonPremultiplied(image[x, y]);

                        // Calculate the offset for the new array.
                        int dataOffset = (y * rowLength) + (x * 4) + 1;
                        data[dataOffset] = color.R;
                        data[dataOffset + 1] = color.G;
                        data[dataOffset + 2] = color.B;
                        data[dataOffset + 3] = color.A;

                        if (y > 0)
                        {
                            color = Color.ToNonPremultiplied(image[x, y - 1]);

                            data[dataOffset] -= color.R;
                            data[dataOffset + 1] -= color.G;
                            data[dataOffset + 2] -= color.B;
                            data[dataOffset + 3] -= color.A;
                        }
                    }
                });
            }

            byte[] buffer;
            int bufferLength;

            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();

                using (ZlibDeflateStream deflateStream = new ZlibDeflateStream(memoryStream, this.CompressionLevel))
                {
                    deflateStream.Write(data, 0, data.Length);
                }

                bufferLength = (int)memoryStream.Length;
                buffer = memoryStream.ToArray();
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
        /// Writes a chunk  of a specified length to the stream at the given offset.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="type">The type of chunk to write.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing data.</param>
        /// <param name="offset">The position to offset the data at.</param>
        /// <param name="length">The of the data to write.</param>
        private void WriteChunk(Stream stream, string type, byte[] data, int offset, int length)
        {
            WriteInteger(stream, length);

            byte[] typeArray = new byte[4];
            typeArray[0] = (byte)type[0];
            typeArray[1] = (byte)type[1];
            typeArray[2] = (byte)type[2];
            typeArray[3] = (byte)type[3];

            stream.Write(typeArray, 0, 4);

            if (data != null)
            {
                stream.Write(data, offset, length);
            }

            Crc32 crc32 = new Crc32();
            crc32.Update(typeArray);

            if (data != null)
            {
                crc32.Update(data, offset, length);
            }

            WriteInteger(stream, (uint)crc32.Value);
        }

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colors">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        private int GetBitsNeededForColorDepth(int colors)
        {
            return (int)Math.Ceiling(Math.Log(colors, 2));
        }
    }
}

// <copyright file="PngEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Image encoder for writing image data to a stream in png format.
    /// </summary>
    public class PngEncoder : IImageEncoder
    {
        /// <summary>
        /// The maximum block size.
        /// </summary>
        private const int MaxBlockSize = 0xFFFF;

        /// <summary>
        /// The the transparency threshold.
        /// </summary>
        private int threshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoder"/> class.
        /// </summary>
        public PngEncoder()
        {
            this.Gamma = 2.2f;
        }

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>Png is a lossless format so this is not used in this encoder.</remarks>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public int Threshold
        {
            get { return this.threshold; }
            set { this.threshold = value.Clamp(0, 255); }
        }

        /// <inheritdoc/>
        public string MimeType => "image/png";

        /// <inheritdoc/>
        public string Extension => "png";

        /// <summary>
        /// Gets or sets a value indicating whether this encoder
        /// will write the image uncompressed the stream.
        /// </summary>
        /// <value>
        /// <c>true</c> if the image should be written uncompressed to
        /// the stream; otherwise, <c>false</c>.
        /// </value>
        // TODO: We can't quickly return a color to non-premultiplied with this method.
        // Should we remove?
        //public bool IsWritingUncompressed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is writing
        /// gamma information to the stream. The default value is false.
        /// </summary>
        /// <value>
        /// <c>True</c> if this instance is writing gamma
        /// information to the stream.; otherwise, <c>false</c>.
        /// </value>
        public bool IsWritingGamma { get; set; }

        /// <summary>
        /// Gets or sets the gamma value, that will be written
        /// the the stream, when the <see cref="IsWritingGamma"/> property
        /// is set to true. The default value is 2.2f.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        public double Gamma { get; set; }

        /// <inheritdoc/>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, nameof(extension));

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;

            return extension.Equals(this.Extension, StringComparison.OrdinalIgnoreCase);
        }

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
                    0x0A // LF
                    },
                0,
                8);

            PngHeader header = new PngHeader
            {
                Width = image.Width,
                Height = image.Height,
                ColorType = 6,
                BitDepth = 8,
                FilterMethod = 0,
                CompressionMethod = 0,
                InterlaceMethod = 0
            };

            this.WriteHeaderChunk(stream, header);
            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);

            //if (this.IsWritingUncompressed)
            //{
            //    this.WriteDataChunksFast(stream, image);
            //}
            //else
            //{
            this.WriteDataChunks(stream, image);
            //}

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
        /// Writes the physical dimension information to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="imageBase">The image base.</param>
        private void WritePhysicalChunk(Stream stream, ImageBase imageBase)
        {
            Image image = imageBase as Image;
            if (image != null && image.HorizontalResolution > 0 && image.VerticalResolution > 0)
            {
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
            if (this.IsWritingGamma)
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
        /// <param name="imageBase">The image base.</param>
        private void WriteDataChunksFast(Stream stream, ImageBase imageBase)
        {
            float[] pixels = imageBase.Pixels;

            // Convert the pixel array to a new array for adding
            // the filter byte.
            byte[] data = new byte[(imageBase.Width * imageBase.Height * 4) + imageBase.Height];

            int rowLength = (imageBase.Width * 4) + 1;

            for (int y = 0; y < imageBase.Height; y++)
            {
                data[y * rowLength] = 0;
                Array.Copy(pixels, y * imageBase.Width * 4, data, (y * rowLength) + 1, imageBase.Width * 4);
            }

            Adler32 adler32 = new Adler32();
            adler32.Update(data);

            using (MemoryStream tempStream = new MemoryStream())
            {
                int remainder = data.Length;

                int blockCount;
                if ((data.Length % MaxBlockSize) == 0)
                {
                    blockCount = data.Length / MaxBlockSize;
                }
                else
                {
                    blockCount = (data.Length / MaxBlockSize) + 1;
                }

                // Write headers
                tempStream.WriteByte(0x78);
                tempStream.WriteByte(0xDA);

                for (int i = 0; i < blockCount; i++)
                {
                    // Write the length
                    ushort length = (ushort)((remainder < MaxBlockSize) ? remainder : MaxBlockSize);

                    tempStream.WriteByte(length == remainder ? (byte)0x01 : (byte)0x00);

                    tempStream.Write(BitConverter.GetBytes(length), 0, 2);

                    // Write one's compliment of length
                    tempStream.Write(BitConverter.GetBytes((ushort)~length), 0, 2);

                    // Write blocks
                    tempStream.Write(data, i * MaxBlockSize, length);

                    // Next block
                    remainder -= MaxBlockSize;
                }

                WriteInteger(tempStream, (int)adler32.Value);

                tempStream.Seek(0, SeekOrigin.Begin);

                byte[] zipData = new byte[tempStream.Length];
                tempStream.Read(zipData, 0, (int)tempStream.Length);

                this.WriteChunk(stream, PngChunkTypes.Data, zipData);
            }
        }

        /// <summary>
        /// Writes the pixel information to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="imageBase">The image base.</param>
        private void WriteDataChunks(Stream stream, ImageBase imageBase)
        {
            float[] pixels = imageBase.Pixels;

            byte[] data = new byte[(imageBase.Width * imageBase.Height * 4) + imageBase.Height];

            int rowLength = (imageBase.Width * 4) + 1;

            for (int y = 0; y < imageBase.Height; y++)
            {
                byte compression = 0;
                if (y > 0)
                {
                    compression = 2;
                }

                data[y * rowLength] = compression;

                for (int x = 0; x < imageBase.Width; x++)
                {
                    // Calculate the offset for the new array.
                    int dataOffset = (y * rowLength) + (x * 4) + 1;

                    // Calculate the offset for the original pixel array.
                    int pixelOffset = ((y * imageBase.Width) + x) * 4;

                    // Convert to non-premultiplied color.
                    float r = pixels[pixelOffset];
                    float g = pixels[pixelOffset + 1];
                    float b = pixels[pixelOffset + 2];
                    float a = pixels[pixelOffset + 3];

                    // Implicit cast to Bgra32 handles premultiplication conversion.
                    Bgra32 color = new Color(r, g, b, a);

                    if (color.A < this.Threshold)
                    {
                        color = new Bgra32(0, 0, 0, 0);
                    }

                    data[dataOffset] = color.R;
                    data[dataOffset + 1] = color.G;
                    data[dataOffset + 2] = color.B;
                    data[dataOffset + 3] = color.A;

                    if (y > 0)
                    {
                        int lastOffset = (((y - 1) * imageBase.Width) + x) * 4;

                        r = pixels[lastOffset];
                        g = pixels[lastOffset + 1];
                        b = pixels[lastOffset + 2];
                        a = pixels[lastOffset + 3];

                        color = new Color(r, g, b, a);

                        data[dataOffset] -= color.R;
                        data[dataOffset + 1] -= color.G;
                        data[dataOffset + 2] -= color.B;
                        data[dataOffset + 3] -= color.A;
                    }
                }
            }

            byte[] buffer;
            int bufferLength;

            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();

                using (DeflaterOutputStream outputStream = new DeflaterOutputStream(memoryStream))
                {
                    outputStream.Write(data, 0, data.Length);
                    outputStream.Flush();
                    outputStream.Finish();

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
    }
}

// <copyright file="PngEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using Quantizers;

    /// <summary>
    /// Performs the png encoding operation.
    /// TODO: Perf. There's lots of array parsing going on here. This should be unmanaged.
    /// </summary>
    internal sealed class PngEncoderCore
    {
        /// <summary>
        /// The maximum block size, defaults at 64k for uncompressed blocks.
        /// </summary>
        private const int MaxBlockSize = 65535;

        /// <summary>
        /// Contains the raw pixel data from the image.
        /// </summary>
        byte[] pixelData;

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
        /// Gets or sets the quality of output for images.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        public PngColorType PngColorType { get; set; }

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

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageBase{T,TP}"/>.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="image">The <see cref="ImageBase{T,TP}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<T, TP>(ImageBase<T, TP> image, Stream stream)
            where T : IPackedVector<TP>
            where TP : struct
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

            // Set correct color type.
            if (Quality <= 256)
            {
                this.PngColorType = PngColorType.Palette;
            }

            // Set correct bit depth.
            this.bitDepth = this.Quality <= 256
                               ? (byte)(ImageMaths.GetBitsNeededForColorDepth(this.Quality).Clamp(1, 8))
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

            if (this.Quality <= 256)
            {
                // Quatize the image and get the pixels
                QuantizedImage<T, TP> quantized = this.WritePaletteChunk(stream, header, image);
                pixelData = quantized.Pixels;
            }
            else
            {
                // Copy the pixels across from the image.
                // TODO: This should vary by bytes per pixel.
                this.pixelData = new byte[this.width * this.height * 4];
                int stride = this.width * 4;
                using (IPixelAccessor<T, TP> pixels = image.Lock())
                {
                    for (int y = 0; y < this.height; y++)
                    {
                        for (int x = 0; x < this.width; x++)
                        {
                            int dataOffset = (y * stride) + (x * 4);
                            byte[] source = pixels[x, y].ToBytes();

                            // r -> g -> b -> a
                            this.pixelData[dataOffset] = source[0];
                            this.pixelData[dataOffset + 1] = source[1];
                            this.pixelData[dataOffset + 2] = source[2];
                            this.pixelData[dataOffset + 3] = source[3];
                        }
                    }
                }
            }

            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);

            //using (IPixelAccessor<T, TP> pixels = image.Lock())
            //{
            //    this.WriteDataChunks(stream, pixels, quantized);
            //}
            this.WriteDataChunks(stream);

            this.WriteEndChunk(stream);
            stream.Flush();
        }

        private byte[] EncodePixelData()
        {
            List<byte[]> filteredScanlines = new List<byte[]>();

            int bytesPerPixel = CalculateBytesPerPixel();
            byte[] previousScanline = new byte[width * bytesPerPixel];

            for (int y = 0; y < height; y++)
            {
                byte[] rawScanline = GetRawScanline(y);
                byte[] filteredScanline = GetOptimalFilteredScanline(rawScanline, previousScanline, bytesPerPixel);

                filteredScanlines.Add(filteredScanline);

                previousScanline = rawScanline;
            }

            List<byte> result = new List<byte>();

            foreach (var encodedScanline in filteredScanlines)
            {
                result.AddRange(encodedScanline);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
        /// to be most compressible, using lowest total variation as proxy for compressibility.
        /// </summary>
        /// <param name="rawScanline"></param>
        /// <param name="previousScanline"></param>
        /// <param name="bytesPerPixel"></param>
        /// <returns></returns>
        private byte[] GetOptimalFilteredScanline(byte[] rawScanline, byte[] previousScanline, int bytesPerPixel)
        {
            List<Tuple<byte[], int>> candidates = new List<Tuple<byte[], int>>();

            byte[] sub = SubFilter.Encode(rawScanline, bytesPerPixel);
            candidates.Add(new Tuple<byte[], int>(sub, CalculateTotalVariation(sub)));

            byte[] up = UpFilter.Encode(rawScanline, previousScanline);
            candidates.Add(new Tuple<byte[], int>(up, CalculateTotalVariation(up)));

            byte[] average = AverageFilter.Encode(rawScanline, previousScanline, bytesPerPixel);
            candidates.Add(new Tuple<byte[], int>(average, CalculateTotalVariation(average)));

            byte[] paeth = PaethFilter.Encode(rawScanline, previousScanline, bytesPerPixel);
            candidates.Add(new Tuple<byte[], int>(paeth, CalculateTotalVariation(paeth)));

            int lowestTotalVariation = int.MaxValue;
            int lowestTotalVariationIndex = 0;

            for (int i = 0; i < candidates.Count; i++)
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
        /// Calculates the total variation of given byte array.  Total variation is the sum of the absolute values of
        /// neighbour differences.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private int CalculateTotalVariation(byte[] input)
        {
            int totalVariation = 0;

            for (int i = 1; i < input.Length; i++)
            {
                totalVariation += Math.Abs(input[i] - input[i - 1]);
            }

            return totalVariation;
        }

        private byte[] GetRawScanline(int y)
        {
            // TODO: This should vary by bytes per pixel.
            int stride = (this.PngColorType == PngColorType.Palette ? 1 : 4) * this.width;
            byte[] rawScanline = new byte[stride];
            Array.Copy(this.pixelData, y * stride, rawScanline, 0, stride);
            return rawScanline;
        }

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
                default:
                    return 4;
            }
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
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        /// <param name="image">The image to encode.</param>
        private QuantizedImage<T, TP> WritePaletteChunk<T, TP>(Stream stream, PngHeader header, ImageBase<T, TP> image)
            where T : IPackedVector<TP>
            where TP : struct
        {
            if (this.Quality > 256)
            {
                return null;
            }

            if (this.Quantizer == null)
            {
                this.Quantizer = new WuQuantizer<T, TP> { Threshold = this.Threshold };
            }

            // Quantize the image returning a palette. This boxing is icky.
            QuantizedImage<T, TP> quantized = ((IQuantizer<T, TP>)this.Quantizer).Quantize(image, this.Quality);

            // Grab the palette and write it to the stream.
            T[] palette = quantized.Palette;
            int pixelCount = palette.Length;

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, header.BitDepth) * 3;
            byte[] colorTable = new byte[colorTableLength];

            Parallel.For(
                0,
                pixelCount,
                Bootstrapper.Instance.ParallelOptions,
                i =>
                {
                    int offset = i * 3;
                    byte[] color = palette[i].ToBytes();

                    // Expected format r->g->b
                    colorTable[offset] = color[0];
                    colorTable[offset + 1] = color[1];
                    colorTable[offset + 2] = color[2];
                });

            this.WriteChunk(stream, PngChunkTypes.Palette, colorTable);

            // Write the transparency data
            if (quantized.TransparentIndex > -1)
            {
                this.WriteChunk(stream, PngChunkTypes.PaletteAlpha, new[] { (byte)quantized.TransparentIndex });
            }

            return quantized;
        }

        /// <summary>
        /// Writes the physical dimension information to the stream.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="imageBase">The image base.</param>
        private void WritePhysicalChunk<T, TP>(Stream stream, ImageBase<T, TP> imageBase)
            where T : IPackedVector<TP>
            where TP : struct
        {
            Image<T, TP> image = imageBase as Image<T, TP>;
            if (image != null && image.HorizontalResolution > 0 && image.VerticalResolution > 0)
            {
                // 39.3700787 = inches in a meter.
                int dpmX = (int)Math.Round(image.HorizontalResolution * 39.3700787D);
                int dpmY = (int)Math.Round(image.VerticalResolution * 39.3700787D);

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
        private void WriteDataChunks(Stream stream)
        {
            byte[] data = this.EncodePixelData();

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

        ///// <summary>
        ///// Writes the pixel information to the stream.
        ///// </summary>
        ///// <typeparam name="T">The pixel format.</typeparam>
        ///// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        ///// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        ///// <param name="pixels">The image pixels.</param>
        ///// <param name="quantized">The quantized image.</param>
        //private void WriteDataChunks<T, TP>(Stream stream, IPixelAccessor<T, TP> pixels, QuantizedImage<T, TP> quantized)
        //    where T : IPackedVector<TP>
        //    where TP : struct
        //{
        //    byte[] data;
        //    int imageWidth = pixels.Width;
        //    int imageHeight = pixels.Height;

        //    // Indexed image.
        //    if (this.Quality <= 256)
        //    {
        //        int rowLength = imageWidth + 1;
        //        data = new byte[rowLength * imageHeight];

        //        Parallel.For(
        //            0,
        //            imageHeight,
        //            Bootstrapper.Instance.ParallelOptions,
        //            y =>
        //        {
        //            int dataOffset = (y * rowLength);
        //            byte compression = 0;
        //            if (y > 0)
        //            {
        //                compression = 2;
        //            }
        //            data[dataOffset++] = compression;
        //            for (int x = 0; x < imageWidth; x++)
        //            {
        //                data[dataOffset++] = quantized.Pixels[(y * imageWidth) + x];
        //                if (y > 0)
        //                {
        //                    data[dataOffset - 1] -= quantized.Pixels[((y - 1) * imageWidth) + x];
        //                }
        //            }
        //        });
        //    }
        //    else
        //    {
        //        // TrueColor image.
        //        data = new byte[(imageWidth * imageHeight * 4) + pixels.Height];

        //        int rowLength = (imageWidth * 4) + 1;

        //        Parallel.For(
        //            0,
        //            imageHeight,
        //            Bootstrapper.Instance.ParallelOptions,
        //            y =>
        //        {
        //            byte compression = 0;
        //            if (y > 0)
        //            {
        //                compression = 2;
        //            }

        //            data[y * rowLength] = compression;

        //            for (int x = 0; x < imageWidth; x++)
        //            {
        //                byte[] color = pixels[x, y].ToBytes();

        //                // Calculate the offset for the new array.
        //                int dataOffset = (y * rowLength) + (x * 4) + 1;

        //                // Expected format 
        //                data[dataOffset] = color[0];
        //                data[dataOffset + 1] = color[1];
        //                data[dataOffset + 2] = color[2];
        //                data[dataOffset + 3] = color[3];

        //                if (y > 0)
        //                {
        //                    color = pixels[x, y - 1].ToBytes();

        //                    data[dataOffset] -= color[0];
        //                    data[dataOffset + 1] -= color[1];
        //                    data[dataOffset + 2] -= color[2];
        //                    data[dataOffset + 3] -= color[3];
        //                }
        //            }
        //        });
        //    }

        //    byte[] buffer;
        //    int bufferLength;

        //    MemoryStream memoryStream = null;
        //    try
        //    {
        //        memoryStream = new MemoryStream();

        //        using (ZlibDeflateStream deflateStream = new ZlibDeflateStream(memoryStream, this.CompressionLevel))
        //        {
        //            deflateStream.Write(data, 0, data.Length);
        //        }

        //        bufferLength = (int)memoryStream.Length;
        //        buffer = memoryStream.ToArray();
        //    }
        //    finally
        //    {
        //        memoryStream?.Dispose();
        //    }

        //    int numChunks = bufferLength / MaxBlockSize;

        //    if (bufferLength % MaxBlockSize != 0)
        //    {
        //        numChunks++;
        //    }

        //    for (int i = 0; i < numChunks; i++)
        //    {
        //        int length = bufferLength - (i * MaxBlockSize);

        //        if (length > MaxBlockSize)
        //        {
        //            length = MaxBlockSize;
        //        }

        //        this.WriteChunk(stream, PngChunkTypes.Data, buffer, i * MaxBlockSize, length);
        //    }
        //}

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
    }
}

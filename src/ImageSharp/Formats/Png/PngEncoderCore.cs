// <copyright file="PngEncoderCore.cs" company="James Jackson-South">
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

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    using Quantizers;

    using static ComparableExtensions;

    /// <summary>
    /// Performs the png encoding operation.
    /// </summary>
    internal sealed class PngEncoderCore : IDisposable
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
        /// The number of bytes per scanline.
        /// </summary>
        private int bytesPerScanline;

        /// <summary>
        /// The previous scanline.
        /// </summary>
        private Buffer<byte> previousScanline;

        /// <summary>
        /// The raw scanline.
        /// </summary>
        private Buffer<byte> rawScanline;

        /// <summary>
        /// The filtered scanline result.
        /// </summary>
        private Buffer<byte> result;

        /// <summary>
        /// The buffer for the sub filter
        /// </summary>
        private Buffer<byte> sub;

        /// <summary>
        /// The buffer for the up filter
        /// </summary>
        private Buffer<byte> up;

        /// <summary>
        /// The buffer for the average filter
        /// </summary>
        private Buffer<byte> average;

        /// <summary>
        /// The buffer for the paeth filter
        /// </summary>
        private Buffer<byte> paeth;

        /// <summary>
        /// The png color type.
        /// </summary>
        private PngColorType pngColorType;

        /// <summary>
        /// The quantizer for reducing the color count.
        /// </summary>
        private IQuantizer quantizer;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore metadata
        /// </summary>
        private bool ignoreMetadata;

        /// <summary>
        /// Gets or sets the Quality value
        /// </summary>
        private int paletteSize;

        /// <summary>
        /// Gets or sets the CompressionLevel value
        /// </summary>
        private int compressionLevel;

        /// <summary>
        /// Gets or sets the Gamma value
        /// </summary>
        private float gamma;

        /// <summary>
        /// Gets or sets the Threshold value
        /// </summary>
        private byte threshold;

        /// <summary>
        /// Gets or sets a value indicating whether to Write Gamma
        /// </summary>
        private bool writeGamma;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for influancing the encoder</param>
        public PngEncoderCore(IPngEncoderOptions options)
        {
            this.ignoreMetadata = options.IgnoreMetadata;
            this.paletteSize = options.PaletteSize > 0 ? options.PaletteSize.Clamp(1, int.MaxValue) : int.MaxValue;
            this.pngColorType = options.PngColorType;
            this.compressionLevel = options.CompressionLevel;
            this.gamma = options.Gamma;
            this.quantizer = options.Quantizer;
            this.threshold = options.Threshold;
            this.writeGamma = options.WriteGamma;
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

            // Set correct color type if the color count is 256 or less.
            if (this.paletteSize <= 256)
            {
                this.pngColorType = PngColorType.Palette;
            }

            if (this.pngColorType == PngColorType.Palette && this.paletteSize > 256)
            {
                this.paletteSize = 256;
            }

            // Set correct bit depth.
            this.bitDepth = this.paletteSize <= 256
                               ? (byte)ImageMaths.GetBitsNeededForColorDepth(this.paletteSize).Clamp(1, 8)
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

            var header = new PngHeader
            {
                Width = image.Width,
                Height = image.Height,
                ColorType = this.pngColorType,
                BitDepth = this.bitDepth,
                FilterMethod = 0, // None
                CompressionMethod = 0,
                InterlaceMethod = 0
            };

            this.WriteHeaderChunk(stream, header);

            // Collect the indexed pixel data
            if (this.pngColorType == PngColorType.Palette)
            {
                this.CollectIndexedBytes(image, stream, header);
            }

            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);
            this.WriteDataChunks(image, stream);
            this.WriteEndChunk(stream);
            stream.Flush();
        }

        /// <summary>
        /// Disposes PngEncoderCore instance, disposing it's internal buffers.
        /// </summary>
        public void Dispose()
        {
            this.previousScanline?.Dispose();
            this.rawScanline?.Dispose();
            this.result?.Dispose();
            this.sub?.Dispose();
            this.up?.Dispose();
            this.average?.Dispose();
            this.paeth?.Dispose();
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
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        private void CollectIndexedBytes<TPixel>(ImageBase<TPixel> image, Stream stream, PngHeader header)
            where TPixel : struct, IPixel<TPixel>
        {
            // Quantize the image and get the pixels.
            QuantizedImage<TPixel> quantized = this.WritePaletteChunk(stream, header, image);
            this.palettePixelData = quantized.Pixels;
        }

        /// <summary>
        /// Collects a row of grayscale pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The image row span.</param>
        private void CollectGrayscaleBytes<TPixel>(Span<TPixel> rowSpan)
            where TPixel : struct, IPixel<TPixel>
        {
            byte[] rawScanlineArray = this.rawScanline.Array;

            // Copy the pixels across from the image.
            // Reuse the chunk type buffer.
            for (int x = 0; x < this.width; x++)
            {
                // Convert the color to YCbCr and store the luminance
                // Optionally store the original color alpha.
                int offset = x * this.bytesPerPixel;
                rowSpan[x].ToXyzwBytes(this.chunkTypeBuffer, 0);
                byte luminance = (byte)((0.299F * this.chunkTypeBuffer[0]) + (0.587F * this.chunkTypeBuffer[1]) + (0.114F * this.chunkTypeBuffer[2]));

                for (int i = 0; i < this.bytesPerPixel; i++)
                {
                    if (i == 0)
                    {
                        rawScanlineArray[offset] = luminance;
                    }
                    else
                    {
                        rawScanlineArray[offset + i] = this.chunkTypeBuffer[3];
                    }
                }
            }
        }

        /// <summary>
        /// Collects a row of true color pixel data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The row span.</param>
        private void CollecTPixelBytes<TPixel>(Span<TPixel> rowSpan)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.bytesPerPixel == 4)
            {
                PixelOperations<TPixel>.Instance.ToRgba32Bytes(rowSpan, this.rawScanline, this.width);
            }
            else
            {
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(rowSpan, this.rawScanline, this.width);
            }
        }

        /// <summary>
        /// Encodes the pixel data line by line.
        /// Each scanline is encoded in the most optimal manner to improve compression.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The row span.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private Buffer<byte> EncodePixelRow<TPixel>(Span<TPixel> rowSpan, int row)
            where TPixel : struct, IPixel<TPixel>
        {
            switch (this.pngColorType)
            {
                case PngColorType.Palette:
                    Buffer.BlockCopy(this.palettePixelData, row * this.rawScanline.Length, this.rawScanline.Array, 0, this.rawScanline.Length);
                    break;
                case PngColorType.Grayscale:
                case PngColorType.GrayscaleWithAlpha:
                    this.CollectGrayscaleBytes(rowSpan);
                    break;
                default:
                    this.CollecTPixelBytes(rowSpan);
                    break;
            }

            return this.GetOptimalFilteredScanline();
        }

        /// <summary>
        /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
        /// to be most compressible, using lowest total variation as proxy for compressibility.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private Buffer<byte> GetOptimalFilteredScanline()
        {
            Span<byte> scanSpan = this.rawScanline.Span;
            Span<byte> prevSpan = this.previousScanline.Span;

            // Palette images don't compress well with adaptive filtering.
            if (this.pngColorType == PngColorType.Palette || this.bitDepth < 8)
            {
                NoneFilter.Encode(this.rawScanline, this.result);
                return this.result;
            }

            // This order, while different to the enumerated order is more likely to produce a smaller sum
            // early on which shaves a couple of milliseconds off the processing time.
            UpFilter.Encode(scanSpan, prevSpan, this.up);

            int currentSum = this.CalculateTotalVariation(this.up, int.MaxValue);
            int lowestSum = currentSum;
            Buffer<byte> actualResult = this.up;

            PaethFilter.Encode(scanSpan, prevSpan, this.paeth, this.bytesPerPixel);
            currentSum = this.CalculateTotalVariation(this.paeth, currentSum);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                actualResult = this.paeth;
            }

            SubFilter.Encode(scanSpan, this.sub, this.bytesPerPixel);
            currentSum = this.CalculateTotalVariation(this.sub, int.MaxValue);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                actualResult = this.sub;
            }

            AverageFilter.Encode(scanSpan, prevSpan, this.average, this.bytesPerPixel);
            currentSum = this.CalculateTotalVariation(this.average, currentSum);

            if (currentSum < lowestSum)
            {
                actualResult = this.average;
            }

            return actualResult;
        }

        /// <summary>
        /// Calculates the total variation of given byte array. Total variation is the sum of the absolute values of
        /// neighbor differences.
        /// </summary>
        /// <param name="scanline">The scanline bytes</param>
        /// <param name="lastSum">The last variation sum</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateTotalVariation(Span<byte> scanline, int lastSum)
        {
            ref byte scanBaseRef = ref scanline.DangerousGetPinnableReference();
            int sum = 0;

            for (int i = 1; i < this.bytesPerScanline; i++)
            {
                byte v = Unsafe.Add(ref scanBaseRef, i);
                sum += v < 128 ? v : 256 - v;

                // No point continuing if we are larger.
                if (sum >= lastSum)
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
            switch (this.pngColorType)
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
            this.chunkDataBuffer[9] = (byte)header.ColorType;
            this.chunkDataBuffer[10] = header.CompressionMethod;
            this.chunkDataBuffer[11] = header.FilterMethod;
            this.chunkDataBuffer[12] = (byte)header.InterlaceMethod;

            this.WriteChunk(stream, PngChunkTypes.Header, this.chunkDataBuffer, 0, 13);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        /// <param name="image">The image to encode.</param>
        /// <returns>The <see cref="QuantizedImage{TPixel}"/></returns>
        private QuantizedImage<TPixel> WritePaletteChunk<TPixel>(Stream stream, PngHeader header, ImageBase<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.paletteSize > 256)
            {
                return null;
            }

            if (this.quantizer == null)
            {
                this.quantizer = new WuQuantizer<TPixel>();
            }

            // Quantize the image returning a palette. This boxing is icky.
            QuantizedImage<TPixel> quantized = ((IQuantizer<TPixel>)this.quantizer).Quantize(image, this.paletteSize);

            // Grab the palette and write it to the stream.
            TPixel[] palette = quantized.Palette;
            byte pixelCount = palette.Length.ToByte();

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, header.BitDepth) * 3;
            byte[] colorTable = ArrayPool<byte>.Shared.Rent(colorTableLength);
            byte[] alphaTable = ArrayPool<byte>.Shared.Rent(pixelCount);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(4);
            bool anyAlpha = false;
            try
            {
                for (byte i = 0; i < pixelCount; i++)
                {
                    if (quantized.Pixels.Contains(i))
                    {
                        int offset = i * 3;
                        palette[i].ToXyzwBytes(bytes, 0);

                        byte alpha = bytes[3];

                        colorTable[offset] = bytes[0];
                        colorTable[offset + 1] = bytes[1];
                        colorTable[offset + 2] = bytes[2];

                        if (alpha > this.threshold)
                        {
                            alpha = 255;
                        }

                        anyAlpha = anyAlpha || alpha < 255;
                        alphaTable[i] = alpha;
                    }
                }

                this.WriteChunk(stream, PngChunkTypes.Palette, colorTable, 0, colorTableLength);

                // Write the transparency data
                if (anyAlpha)
                {
                    this.WriteChunk(stream, PngChunkTypes.PaletteAlpha, alphaTable, 0, pixelCount);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(colorTable);
                ArrayPool<byte>.Shared.Return(alphaTable);
                ArrayPool<byte>.Shared.Return(bytes);
            }

            return quantized;
        }

        /// <summary>
        /// Writes the physical dimension information to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="image">The image.</param>
        private void WritePhysicalChunk<TPixel>(Stream stream, Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (image.MetaData.HorizontalResolution > 0 && image.MetaData.VerticalResolution > 0)
            {
                // 39.3700787 = inches in a meter.
                int dpmX = (int)Math.Round(image.MetaData.HorizontalResolution * 39.3700787D);
                int dpmY = (int)Math.Round(image.MetaData.VerticalResolution * 39.3700787D);

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
            if (this.writeGamma)
            {
                int gammaValue = (int)(this.gamma * 100000F);

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
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The image.</param>
        /// <param name="stream">The stream.</param>
        private void WriteDataChunks<TPixel>(Image<TPixel> pixels, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            this.bytesPerScanline = this.width * this.bytesPerPixel;
            int resultLength = this.bytesPerScanline + 1;

            this.previousScanline = Buffer<byte>.CreateClean(this.bytesPerScanline);
            this.rawScanline = Buffer<byte>.CreateClean(this.bytesPerScanline);
            this.result = Buffer<byte>.CreateClean(resultLength);

            if (this.pngColorType != PngColorType.Palette)
            {
                this.sub = Buffer<byte>.CreateClean(resultLength);
                this.up = Buffer<byte>.CreateClean(resultLength);
                this.average = Buffer<byte>.CreateClean(resultLength);
                this.paeth = Buffer<byte>.CreateClean(resultLength);
            }

            byte[] buffer;
            int bufferLength;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (var deflateStream = new ZlibDeflateStream(memoryStream, this.compressionLevel))
                {
                    for (int y = 0; y < this.height; y++)
                    {
                        Buffer<byte> r = this.EncodePixelRow(pixels.GetRowSpan(y), y);
                        deflateStream.Write(r.Array, 0, resultLength);

                        Swap(ref this.rawScanline, ref this.previousScanline);
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
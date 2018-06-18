// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Quantization;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Performs the png encoding operation.
    /// </summary>
    internal sealed class PngEncoderCore : IDisposable
    {
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The maximum block size, defaults at 64k for uncompressed blocks.
        /// </summary>
        private const int MaxBlockSize = 65535;

        /// <summary>
        /// Reusable buffer for writing general data.
        /// </summary>
        private readonly byte[] buffer = new byte[8];

        /// <summary>
        /// Reusable buffer for writing chunk data.
        /// </summary>
        private readonly byte[] chunkDataBuffer = new byte[16];

        /// <summary>
        /// Reusable crc for validating chunks.
        /// </summary>
        private readonly Crc32 crc = new Crc32();

        /// <summary>
        /// The png bit depth
        /// </summary>
        private readonly PngBitDepth pngBitDepth;

        /// <summary>
        /// Gets or sets a value indicating whether to use 16 bit encoding for supported color types.
        /// </summary>
        private readonly bool use16Bit;

        /// <summary>
        /// The png color type.
        /// </summary>
        private readonly PngColorType pngColorType;

        /// <summary>
        /// The png filter method.
        /// </summary>
        private readonly PngFilterMethod pngFilterMethod;

        /// <summary>
        /// The quantizer for reducing the color count.
        /// </summary>
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Gets or sets the CompressionLevel value
        /// </summary>
        private readonly int compressionLevel;

        /// <summary>
        /// Gets or sets the Gamma value
        /// </summary>
        private readonly float gamma;

        /// <summary>
        /// Gets or sets the Threshold value
        /// </summary>
        private readonly byte threshold;

        /// <summary>
        /// Gets or sets a value indicating whether to Write Gamma
        /// </summary>
        private readonly bool writeGamma;

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
        private IManagedByteBuffer previousScanline;

        /// <summary>
        /// The raw scanline.
        /// </summary>
        private IManagedByteBuffer rawScanline;

        /// <summary>
        /// The filtered scanline result.
        /// </summary>
        private IManagedByteBuffer result;

        /// <summary>
        /// The buffer for the sub filter
        /// </summary>
        private IManagedByteBuffer sub;

        /// <summary>
        /// The buffer for the up filter
        /// </summary>
        private IManagedByteBuffer up;

        /// <summary>
        /// The buffer for the average filter
        /// </summary>
        private IManagedByteBuffer average;

        /// <summary>
        /// The buffer for the Paeth filter
        /// </summary>
        private IManagedByteBuffer paeth;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderCore"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="options">The options for influencing the encoder</param>
        public PngEncoderCore(MemoryAllocator memoryAllocator, IPngEncoderOptions options)
        {
            this.memoryAllocator = memoryAllocator;
            this.pngBitDepth = options.BitDepth;
            this.use16Bit = this.pngBitDepth.Equals(PngBitDepth.Bit16);
            this.pngColorType = options.ColorType;
            this.pngFilterMethod = options.FilterMethod;
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
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.width = image.Width;
            this.height = image.Height;

            stream.Write(PngConstants.HeaderBytes, 0, PngConstants.HeaderBytes.Length);

            QuantizedFrame<TPixel> quantized = null;
            if (this.pngColorType == PngColorType.Palette)
            {
                // Create quantized frame returning the palette and set the bit depth.
                quantized = this.quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(image.Frames.RootFrame);
                this.palettePixelData = quantized.Pixels;
                byte bits = (byte)ImageMaths.GetBitsNeededForColorDepth(quantized.Palette.Length).Clamp(1, 8);

                // Png only supports in four pixel depths: 1, 2, 4, and 8 bits when using the PLTE chunk
                if (bits == 3)
                {
                    bits = 4;
                }
                else if (bits >= 5 || bits <= 7)
                {
                    bits = 8;
                }

                this.bitDepth = bits;
            }
            else
            {
                this.bitDepth = (byte)(this.use16Bit ? 16 : 8);
            }

            this.bytesPerPixel = this.CalculateBytesPerPixel();

            var header = new PngHeader(
                width: image.Width,
                height: image.Height,
                bitDepth: this.bitDepth,
                colorType: this.pngColorType,
                compressionMethod: 0, // None
                filterMethod: 0,
                interlaceMethod: 0); // TODO: Can't write interlaced yet.

            this.WriteHeaderChunk(stream, header);

            // Collect the indexed pixel data
            if (quantized != null)
            {
                this.WritePaletteChunk(stream, header, quantized);
            }

            this.WritePhysicalChunk(stream, image);
            this.WriteGammaChunk(stream);
            this.WriteDataChunks(image.Frames.RootFrame, stream);
            this.WriteEndChunk(stream);
            stream.Flush();
        }

        /// <inheritdoc />
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
        /// Collects a row of grayscale pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The image row span.</param>
        private void CollectGrayscaleBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan)
            where TPixel : struct, IPixel<TPixel>
        {
            // Use ITU-R recommendation 709 to match libpng.
            const float RX = .2126F;
            const float GX = .7152F;
            const float BX = .0722F;
            Span<byte> rawScanlineSpan = this.rawScanline.GetSpan();

            if (this.pngColorType.Equals(PngColorType.Grayscale))
            {
                // TODO: Realistically we should support 1, 2, 4, 8, and 16 bit grayscale images.
                // we currently do the other types via palette. Maybe RC as I don't understand how the data is packed yet
                // for 1, 2, and 4 bit grayscale images.
                if (this.use16Bit)
                {
                    // 16 bit grayscale
                    Rgb48 rgb = default;
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 2)
                    {
                        rowSpan[x].ToRgb48(ref rgb);
                        ushort luminance = (ushort)((RX * rgb.R) + (GX * rgb.G) + (BX * rgb.B));
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), luminance);
                    }
                }
                else
                {
                    // 8 bit grayscale
                    Rgb24 rgb = default;
                    for (int x = 0; x < rowSpan.Length; x++)
                    {
                        rowSpan[x].ToRgb24(ref rgb);
                        rawScanlineSpan[x] = (byte)((RX * rgb.R) + (GX * rgb.G) + (BX * rgb.B));
                    }
                }
            }
            else
            {
                if (this.use16Bit)
                {
                    // 16 bit grayscale + alpha
                    Rgba64 rgba = default;
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 4)
                    {
                        rowSpan[x].ToRgba64(ref rgba);
                        ushort luminance = (ushort)((RX * rgba.R) + (GX * rgba.G) + (BX * rgba.B));
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), luminance);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgba.A);
                    }
                }
                else
                {
                    // 8 bit grayscale + alpha
                    Rgba32 rgba = default;
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 2)
                    {
                        rowSpan[x].ToRgba32(ref rgba);
                        rawScanlineSpan[o] = (byte)((RX * rgba.R) + (GX * rgba.G) + (BX * rgba.B));
                        rawScanlineSpan[o + 1] = rgba.A;
                    }
                }
            }
        }

        /// <summary>
        /// Collects a row of true color pixel data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The row span.</param>
        private void CollectTPixelBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<byte> rawScanlineSpan = this.rawScanline.GetSpan();

            switch (this.bytesPerPixel)
            {
                case 4:
                    {
                        // 8 bit Rgba
                        PixelOperations<TPixel>.Instance.ToRgba32Bytes(rowSpan, rawScanlineSpan, this.width);
                        break;
                    }

                case 3:
                    {
                        // 8 bit Rgb
                        PixelOperations<TPixel>.Instance.ToRgb24Bytes(rowSpan, rawScanlineSpan, this.width);
                        break;
                    }

                case 8:
                    {
                        // 16 bit Rgba
                        Rgba64 rgba = default;
                        for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 8)
                        {
                            rowSpan[x].ToRgba64(ref rgba);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), rgba.R);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgba.G);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 4, 2), rgba.B);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 6, 2), rgba.A);
                        }

                        break;
                    }

                default:
                    {
                        // 16 bit Rgb
                        Rgb48 rgb = default;
                        for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 6)
                        {
                            rowSpan[x].ToRgb48(ref rgb);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), rgb.R);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgb.G);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 4, 2), rgb.B);
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// Encodes the pixel data line by line.
        /// Each scanline is encoded in the most optimal manner to improve compression.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The row span.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="IManagedByteBuffer"/></returns>
        private IManagedByteBuffer EncodePixelRow<TPixel>(ReadOnlySpan<TPixel> rowSpan, int row)
            where TPixel : struct, IPixel<TPixel>
        {
            switch (this.pngColorType)
            {
                case PngColorType.Palette:
                    Buffer.BlockCopy(this.palettePixelData, row * this.rawScanline.Length(), this.rawScanline.Array, 0, this.rawScanline.Length());
                    break;
                case PngColorType.Grayscale:
                case PngColorType.GrayscaleWithAlpha:
                    this.CollectGrayscaleBytes(rowSpan);
                    break;
                default:
                    this.CollectTPixelBytes(rowSpan);
                    break;
            }

            switch (this.pngFilterMethod)
            {
                case PngFilterMethod.None:
                    NoneFilter.Encode(this.rawScanline.GetSpan(), this.result.GetSpan());
                    return this.result;

                case PngFilterMethod.Sub:
                    SubFilter.Encode(this.rawScanline.GetSpan(), this.sub.GetSpan(), this.bytesPerPixel, out int _);
                    return this.sub;

                case PngFilterMethod.Up:
                    UpFilter.Encode(this.rawScanline.GetSpan(), this.previousScanline.GetSpan(), this.up.GetSpan(), out int _);
                    return this.up;

                case PngFilterMethod.Average:
                    AverageFilter.Encode(this.rawScanline.GetSpan(), this.previousScanline.GetSpan(), this.average.GetSpan(), this.bytesPerPixel, out int _);
                    return this.average;

                case PngFilterMethod.Paeth:
                    PaethFilter.Encode(this.rawScanline.GetSpan(), this.previousScanline.GetSpan(), this.paeth.GetSpan(), this.bytesPerPixel, out int _);
                    return this.paeth;

                default:
                    return this.GetOptimalFilteredScanline();
            }
        }

        /// <summary>
        /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
        /// to be most compressible, using lowest total variation as proxy for compressibility.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private IManagedByteBuffer GetOptimalFilteredScanline()
        {
            // Palette images don't compress well with adaptive filtering.
            if (this.pngColorType == PngColorType.Palette || this.bitDepth < 8)
            {
                NoneFilter.Encode(this.rawScanline.GetSpan(), this.result.GetSpan());
                return this.result;
            }

            Span<byte> scanSpan = this.rawScanline.GetSpan();
            Span<byte> prevSpan = this.previousScanline.GetSpan();

            // This order, while different to the enumerated order is more likely to produce a smaller sum
            // early on which shaves a couple of milliseconds off the processing time.
            UpFilter.Encode(scanSpan, prevSpan, this.up.GetSpan(), out int currentSum);

            // TODO: PERF.. We should be breaking out of the encoding for each line as soon as we hit the sum.
            // That way the above comment would actually be true. It used to be anyway...
            // If we could use SIMD for none branching filters we could really speed it up.
            int lowestSum = currentSum;
            IManagedByteBuffer actualResult = this.up;

            PaethFilter.Encode(scanSpan, prevSpan, this.paeth.GetSpan(), this.bytesPerPixel, out currentSum);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                actualResult = this.paeth;
            }

            SubFilter.Encode(scanSpan, this.sub.GetSpan(), this.bytesPerPixel, out currentSum);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                actualResult = this.sub;
            }

            AverageFilter.Encode(scanSpan, prevSpan, this.average.GetSpan(), this.bytesPerPixel, out currentSum);

            if (currentSum < lowestSum)
            {
                actualResult = this.average;
            }

            return actualResult;
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
                    return this.use16Bit ? 2 : 1;

                case PngColorType.GrayscaleWithAlpha:
                    return this.use16Bit ? 4 : 2;

                case PngColorType.Palette:
                    return 1;

                case PngColorType.Rgb:
                    return this.use16Bit ? 6 : 3;

                // PngColorType.RgbWithAlpha
                default:
                    return this.use16Bit ? 8 : 4;
            }
        }

        /// <summary>
        /// Writes the header chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        private void WriteHeaderChunk(Stream stream, in PngHeader header)
        {
            BinaryPrimitives.WriteInt32BigEndian(this.chunkDataBuffer.AsSpan(0, 4), header.Width);
            BinaryPrimitives.WriteInt32BigEndian(this.chunkDataBuffer.AsSpan(4, 4), header.Height);

            this.chunkDataBuffer[8] = header.BitDepth;
            this.chunkDataBuffer[9] = (byte)header.ColorType;
            this.chunkDataBuffer[10] = header.CompressionMethod;
            this.chunkDataBuffer[11] = header.FilterMethod;
            this.chunkDataBuffer[12] = (byte)header.InterlaceMethod;

            this.WriteChunk(stream, PngChunkType.Header, this.chunkDataBuffer, 0, 13);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="header">The <see cref="PngHeader"/>.</param>
        /// <param name="quantized">The quantized frame.</param>
        private void WritePaletteChunk<TPixel>(Stream stream, in PngHeader header, QuantizedFrame<TPixel> quantized)
            where TPixel : struct, IPixel<TPixel>
        {
            // Grab the palette and write it to the stream.
            TPixel[] palette = quantized.Palette;
            byte pixelCount = palette.Length.ToByte();

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, header.BitDepth) * 3;
            Rgba32 rgba = default;
            bool anyAlpha = false;

            using (IManagedByteBuffer colorTable = this.memoryAllocator.AllocateManagedByteBuffer(colorTableLength))
            using (IManagedByteBuffer alphaTable = this.memoryAllocator.AllocateManagedByteBuffer(pixelCount))
            {
                Span<byte> colorTableSpan = colorTable.GetSpan();
                Span<byte> alphaTableSpan = alphaTable.GetSpan();

                for (byte i = 0; i < pixelCount; i++)
                {
                    if (quantized.Pixels.Contains(i))
                    {
                        int offset = i * 3;
                        palette[i].ToRgba32(ref rgba);

                        byte alpha = rgba.A;

                        colorTableSpan[offset] = rgba.R;
                        colorTableSpan[offset + 1] = rgba.G;
                        colorTableSpan[offset + 2] = rgba.B;

                        if (alpha > this.threshold)
                        {
                            alpha = 255;
                        }

                        anyAlpha = anyAlpha || alpha < 255;
                        alphaTableSpan[i] = alpha;
                    }
                }

                this.WriteChunk(stream, PngChunkType.Palette, colorTable.Array, 0, colorTableLength);

                // Write the transparency data
                if (anyAlpha)
                {
                    this.WriteChunk(stream, PngChunkType.PaletteAlpha, alphaTable.Array, 0, pixelCount);
                }
            }
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

                BinaryPrimitives.WriteInt32BigEndian(this.chunkDataBuffer.AsSpan(0, 4), dpmX);
                BinaryPrimitives.WriteInt32BigEndian(this.chunkDataBuffer.AsSpan(4, 4), dpmY);

                this.chunkDataBuffer[8] = 1;

                this.WriteChunk(stream, PngChunkType.Physical, this.chunkDataBuffer, 0, 9);
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
                // 4-byte unsigned integer of gamma * 100,000.
                uint gammaValue = (uint)(this.gamma * 100_000F);

                BinaryPrimitives.WriteUInt32BigEndian(this.chunkDataBuffer.AsSpan(0, 4), gammaValue);

                this.WriteChunk(stream, PngChunkType.Gamma, this.chunkDataBuffer, 0, 4);
            }
        }

        /// <summary>
        /// Writes the pixel information to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The image.</param>
        /// <param name="stream">The stream.</param>
        private void WriteDataChunks<TPixel>(ImageFrame<TPixel> pixels, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            this.bytesPerScanline = this.width * this.bytesPerPixel;
            int resultLength = this.bytesPerScanline + 1;

            this.previousScanline = this.memoryAllocator.AllocateCleanManagedByteBuffer(this.bytesPerScanline);
            this.rawScanline = this.memoryAllocator.AllocateCleanManagedByteBuffer(this.bytesPerScanline);
            this.result = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);

            switch (this.pngFilterMethod)
            {
                case PngFilterMethod.None:
                    break;

                case PngFilterMethod.Sub:

                    this.sub = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    break;

                case PngFilterMethod.Up:

                    this.up = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    break;

                case PngFilterMethod.Average:

                    this.average = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    break;

                case PngFilterMethod.Paeth:

                    this.paeth = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    break;
                case PngFilterMethod.Adaptive:

                    this.sub = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    this.up = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    this.average = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    this.paeth = this.memoryAllocator.AllocateCleanManagedByteBuffer(resultLength);
                    break;
            }

            byte[] buffer;
            int bufferLength;

            using (var memoryStream = new MemoryStream())
            {
                using (var deflateStream = new ZlibDeflateStream(memoryStream, this.compressionLevel))
                {
                    for (int y = 0; y < this.height; y++)
                    {
                        IManagedByteBuffer r = this.EncodePixelRow((ReadOnlySpan<TPixel>)pixels.GetPixelRowSpan(y), y);
                        deflateStream.Write(r.Array, 0, resultLength);

                        IManagedByteBuffer temp = this.rawScanline;
                        this.rawScanline = this.previousScanline;
                        this.previousScanline = temp;
                    }
                }

                buffer = memoryStream.ToArray();
                bufferLength = buffer.Length;
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

                this.WriteChunk(stream, PngChunkType.Data, buffer, i * MaxBlockSize, length);
            }
        }

        /// <summary>
        /// Writes the chunk end to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void WriteEndChunk(Stream stream)
        {
            this.WriteChunk(stream, PngChunkType.End, null);
        }

        /// <summary>
        /// Writes a chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="type">The type of chunk to write.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing data.</param>
        private void WriteChunk(Stream stream, PngChunkType type, byte[] data)
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
        private void WriteChunk(Stream stream, PngChunkType type, byte[] data, int offset, int length)
        {
            BinaryPrimitives.WriteInt32BigEndian(this.buffer, length);
            BinaryPrimitives.WriteUInt32BigEndian(this.buffer.AsSpan(4, 4), (uint)type);

            stream.Write(this.buffer, 0, 8);

            this.crc.Reset();

            this.crc.Update(this.buffer.AsSpan(4, 4)); // Write the type buffer

            if (data != null && length > 0)
            {
                stream.Write(data, offset, length);

                this.crc.Update(data.AsSpan(offset, length));
            }

            BinaryPrimitives.WriteUInt32BigEndian(this.buffer, (uint)this.crc.Value);

            stream.Write(this.buffer, 0, 4); // write the crc
        }
    }
}
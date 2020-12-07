// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal class TiffWriter : IDisposable
    {
        private readonly Stream output;

        private readonly MemoryAllocator memoryAllocator;

        private readonly Configuration configuration;

        private readonly byte[] paddingBytes = new byte[4];

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffWriter"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="memoryMemoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The configuration.</param>
        public TiffWriter(Stream output, MemoryAllocator memoryMemoryAllocator, Configuration configuration)
        {
            this.output = output;
            this.memoryAllocator = memoryMemoryAllocator;
            this.configuration = configuration;
        }

        /// <summary>
        /// Gets a value indicating whether the architecture is little-endian.
        /// </summary>
        public bool IsLittleEndian => BitConverter.IsLittleEndian;

        /// <summary>
        /// Gets the current position within the stream.
        /// </summary>
        public long Position => this.output.Position;

        /// <summary>
        /// Writes an empty four bytes to the stream, returning the offset to be written later.
        /// </summary>
        /// <returns>The offset to be written later</returns>
        public long PlaceMarker()
        {
            long offset = this.output.Position;
            this.Write(0u);
            return offset;
        }

        /// <summary>
        /// Writes an array of bytes to the current stream.
        /// </summary>
        /// <param name="value">The bytes to write.</param>
        public void Write(byte[] value)
        {
            this.output.Write(value, 0, value.Length);
        }

        /// <summary>
        /// Writes a byte to the current stream.
        /// </summary>
        /// <param name="value">The byte to write.</param>
        public void Write(byte value)
        {
            this.output.Write(new byte[] { value }, 0, 1);
        }

        /// <summary>
        /// Writes a two-byte unsigned integer to the current stream.
        /// </summary>
        /// <param name="value">The two-byte unsigned integer to write.</param>
        public void Write(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            this.output.Write(bytes, 0, 2);
        }

        /// <summary>
        /// Writes a four-byte unsigned integer to the current stream.
        /// </summary>
        /// <param name="value">The four-byte unsigned integer to write.</param>
        public void Write(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            this.output.Write(bytes, 0, 4);
        }

        /// <summary>
        /// Writes an array of bytes to the current stream, padded to four-bytes.
        /// </summary>
        /// <param name="value">The bytes to write.</param>
        public void WritePadded(byte[] value)
        {
            this.output.Write(value, 0, value.Length);

            if (value.Length < 4)
            {
                this.output.Write(this.paddingBytes, 0, 4 - value.Length);
            }
        }

        /// <summary>
        /// Writes a four-byte unsigned integer to the specified marker in the stream.
        /// </summary>
        /// <param name="offset">The offset returned when placing the marker</param>
        /// <param name="value">The four-byte unsigned integer to write.</param>
        public void WriteMarker(long offset, uint value)
        {
            long currentOffset = this.output.Position;
            this.output.Seek(offset, SeekOrigin.Begin);
            this.Write(value);
            this.output.Seek(currentOffset, SeekOrigin.Begin);
        }

        /// <summary>
        /// Writes the image data as RGB to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used with deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteRgb<TPixel>(Image<TPixel> image, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(image.Width * 3);
            Span<byte> rowSpan = row.GetSpan();
            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteDeflateCompressedRgb(image, rowSpan, compressionLevel, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.Lzw)
            {
                return this.WriteLzwCompressedRgb(image, rowSpan, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WriteRgbPackBitsCompressed(image, rowSpan);
            }

            // No compression.
            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                this.output.Write(rowSpan);
                bytesWritten += rowSpan.Length;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as RGB compressed with zlib to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A Span for a pixel row.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used with deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteDeflateCompressedRgb<TPixel>(Image<TPixel> image, Span<byte> rowSpan, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.memoryAllocator, memoryStream, compressionLevel); // TODO: move zlib compression from png to a common place?

            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);

                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction24Bit(rowSpan);
                }

                deflateStream.Write(rowSpan);
            }

            deflateStream.Flush();

            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as RGB compressed with lzw to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A Span for a pixel row.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteLzwCompressedRgb<TPixel>(Image<TPixel> image, Span<byte> rowSpan, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();

            IMemoryOwner<byte> pixelData = this.memoryAllocator.Allocate<byte>(image.Width * image.Height * 3);
            Span<byte> pixels = pixelData.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);

                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction24Bit(rowSpan);
                }

                rowSpan.CopyTo(pixels.Slice(y * image.Width * 3));
            }

            using var lzwEncoder = new TiffLzwEncoder(this.memoryAllocator, pixelData, 8);
            lzwEncoder.Encode(memoryStream);

            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as RGB with packed bits compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A Span for a pixel row.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteRgbPackBitsCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bytes.
            int additionalBytes = ((image.Width * 3) / 127) + 1;
            using IManagedByteBuffer compressedRow = this.memoryAllocator.AllocateManagedByteBuffer((image.Width * 3) + additionalBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();
            int bytesWritten = 0;

            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                int size = PackBitsWriter.PackBits(rowSpan, compressedRowSpan);
                this.output.Write(compressedRow.Slice(0, size));
                bytesWritten += size;
                compressedRowSpan.Clear();
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as indices into a color map to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="quantizer">The quantizer to use.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used in combination with deflate or LZW compression.</param>
        /// <param name="colorMap">The color map.</param>
        /// <returns>The number of bytes written.</returns>
        public int WritePalettedRgb<TPixel>(Image<TPixel> image, IQuantizer quantizer, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor, out IExifValue colorMap)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int colorsPerChannel = 256;
            int colorPaletteSize = colorsPerChannel * 3;
            int colorPaletteBytes = colorPaletteSize * 2;
            using IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(image.Width);
            using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration);
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds());
            using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.AllocateManagedByteBuffer(colorPaletteBytes);
            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

            ReadOnlySpan<TPixel> quantizedColors = quantized.Palette.Span;
            int quantizedColorBytes = quantizedColors.Length * 3 * 2;

            // In the ColorMap, black is represented by 0,0,0 and white is represented by 65535, 65535, 65535.
            Span<Rgb48> quantizedColorRgb48 = MemoryMarshal.Cast<byte, Rgb48>(colorPalette.Slice(0, quantizedColorBytes));
            PixelOperations<TPixel>.Instance.ToRgb48(this.configuration, quantizedColors, quantizedColorRgb48);

            // It can happen that the quantized colors are less than the expected 256 per channel.
            var diffToMaxColors = colorsPerChannel - quantizedColors.Length;

            // In a TIFF ColorMap, all the Red values come first, followed by the Green values,
            // then the Blue values. Convert the quantized palette to this format.
            var palette = new ushort[colorPaletteSize];
            int paletteIdx = 0;
            for (int i = 0; i < quantizedColors.Length; i++)
            {
                palette[paletteIdx++] = quantizedColorRgb48[i].R;
            }

            paletteIdx += diffToMaxColors;

            for (int i = 0; i < quantizedColors.Length; i++)
            {
                palette[paletteIdx++] = quantizedColorRgb48[i].G;
            }

            paletteIdx += diffToMaxColors;

            for (int i = 0; i < quantizedColors.Length; i++)
            {
                palette[paletteIdx++] = quantizedColorRgb48[i].B;
            }

            colorMap = new ExifShortArray(ExifTagValue.ColorMap)
            {
                Value = palette
            };

            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteDeflateCompressedPalettedRgb(image, quantized, compressionLevel, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.Lzw)
            {
                return this.WriteLzwCompressedPalettedRgb(image, quantized, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WritePackBitsCompressedPalettedRgb(image, quantized);
            }

            // No compression.
            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<byte> pixelSpan = quantized.GetPixelRowSpan(y);
                this.output.Write(pixelSpan);
                bytesWritten += pixelSpan.Length;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as indices into a color map compressed with deflate compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="quantized">The quantized frame.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteDeflateCompressedPalettedRgb<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IManagedByteBuffer tmpBuffer = this.memoryAllocator.AllocateManagedByteBuffer(image.Width);
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.memoryAllocator, memoryStream, compressionLevel);

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<byte> pixelRow = quantized.GetPixelRowSpan(y);
                if (useHorizontalPredictor)
                {
                    // We need a writable Span here.
                    Span<byte> pixelRowCopy = tmpBuffer.GetSpan();
                    pixelRow.CopyTo(pixelRowCopy);
                    HorizontalPredictor.ApplyHorizontalPrediction8Bit(pixelRowCopy);
                    deflateStream.Write(pixelRowCopy);
                }
                else
                {
                    deflateStream.Write(pixelRow);
                }
            }

            deflateStream.Flush();
            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as indices into a color map compressed with lzw compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="quantized">The quantized frame.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteLzwCompressedPalettedRgb<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IMemoryOwner<byte> pixelData = this.memoryAllocator.Allocate<byte>(image.Width * image.Height);
            using IManagedByteBuffer tmpBuffer = this.memoryAllocator.AllocateManagedByteBuffer(image.Width);
            using var memoryStream = new MemoryStream();

            int bytesWritten = 0;
            Span<byte> pixels = pixelData.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<byte> indexedPixelRow = quantized.GetPixelRowSpan(y);

                if (useHorizontalPredictor)
                {
                    // We need a writable Span here.
                    Span<byte> pixelRowCopy = tmpBuffer.GetSpan();
                    indexedPixelRow.CopyTo(pixelRowCopy);
                    HorizontalPredictor.ApplyHorizontalPrediction8Bit(pixelRowCopy);
                    pixelRowCopy.CopyTo(pixels.Slice(y * image.Width));
                }
                else
                {
                    indexedPixelRow.CopyTo(pixels.Slice(y * image.Width));
                }
            }

            using var lzwEncoder = new TiffLzwEncoder(this.memoryAllocator, pixelData, 8);
            lzwEncoder.Encode(memoryStream);

            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as indices into a color map compressed with deflate compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="quantized">The quantized frame.</param>
        /// <returns>The number of bytes written.</returns>
        public int WritePackBitsCompressedPalettedRgb<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bytes.
            int additionalBytes = ((image.Width * 3) / 127) + 1;
            using IManagedByteBuffer compressedRow = this.memoryAllocator.AllocateManagedByteBuffer((image.Width * 3) + additionalBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<byte> pixelSpan = quantized.GetPixelRowSpan(y);

                int size = PackBitsWriter.PackBits(pixelSpan, compressedRowSpan);
                this.output.Write(compressedRowSpan.Slice(0, size));
                bytesWritten += size;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used. Should only be used with deflate or lzw compression.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteGray<TPixel>(Image<TPixel> image, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            using IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(image.Width);
            Span<byte> rowSpan = row.GetSpan();

            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteGrayDeflateCompressed(image, rowSpan, compressionLevel, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.Lzw)
            {
                return this.WriteGrayLzwCompressed(image, rowSpan, useHorizontalPredictor);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WriteGrayPackBitsCompressed(image, rowSpan);
            }

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                this.output.Write(rowSpan);
                bytesWritten += rowSpan.Length;
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray with deflate compression to the stream.
        /// </summary>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A span of a row of pixels.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteGrayDeflateCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.memoryAllocator, memoryStream, compressionLevel); // TODO: move zlib compression from png to a common place?

            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);

                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction8Bit(rowSpan);
                }

                deflateStream.Write(rowSpan);
            }

            deflateStream.Flush();

            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray with lzw compression to the stream.
        /// </summary>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A span of a row of pixels.</param>
        /// <param name="useHorizontalPredictor">Indicates if horizontal prediction should be used.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteGrayLzwCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesWritten = 0;
            using var memoryStream = new MemoryStream();

            IMemoryOwner<byte> pixelData = this.memoryAllocator.Allocate<byte>(image.Width * image.Height);
            Span<byte> pixels = pixelData.GetSpan();
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                if (useHorizontalPredictor)
                {
                    HorizontalPredictor.ApplyHorizontalPrediction8Bit(rowSpan);
                }

                rowSpan.CopyTo(pixels.Slice(y * image.Width));
            }

            using var lzwEncoder = new TiffLzwEncoder(this.memoryAllocator, pixelData, 8);
            lzwEncoder.Encode(memoryStream);

            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;
            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 8 bit gray to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="rowSpan">A span of a row of pixels.</param>
        /// <returns>The number of bytes written.</returns>
        private int WriteGrayPackBitsCompressed<TPixel>(Image<TPixel> image, Span<byte> rowSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bytes.
            int additionalBytes = (image.Width / 127) + 1;
            using IManagedByteBuffer compressedRow = this.memoryAllocator.AllocateManagedByteBuffer(image.Width + additionalBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.configuration, pixelRow, rowSpan, pixelRow.Length);
                int size = PackBitsWriter.PackBits(rowSpan, compressedRowSpan);
                this.output.Write(compressedRow.Slice(0, size));
                bytesWritten += size;
                compressedRowSpan.Clear();
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 1 bit black and white to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteBiColor<TPixel>(Image<TPixel> image, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int padding = image.Width % 8 == 0 ? 0 : 1;
            int bytesPerRow = (image.Width / 8) + padding;
            using IMemoryOwner<L8> pixelRowAsGray = this.memoryAllocator.Allocate<L8>(image.Width);
            using IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(bytesPerRow, AllocationOptions.Clean);
            Span<byte> outputRow = row.GetSpan();
            Span<L8> pixelRowAsGraySpan = pixelRowAsGray.GetSpan();

            // Convert image to black and white.
            // TODO: Should we allow to skip this by the user, if its known to be black and white already?
            using Image<TPixel> imageBlackWhite = image.Clone();
            imageBlackWhite.Mutate(img => img.BinaryDither(default(ErrorDither)));

            if (compression == TiffEncoderCompression.Deflate)
            {
                return this.WriteBiColorDeflate(imageBlackWhite, pixelRowAsGraySpan, outputRow, compressionLevel);
            }

            if (compression == TiffEncoderCompression.PackBits)
            {
                return this.WriteBiColorPackBits(imageBlackWhite, pixelRowAsGraySpan, outputRow);
            }

            if (compression == TiffEncoderCompression.CcittGroup3Fax)
            {
                var bitWriter = new T4BitWriter(this.memoryAllocator, this.configuration);
                return bitWriter.CompressImage(imageBlackWhite, pixelRowAsGraySpan, this.output);
            }

            if (compression == TiffEncoderCompression.ModifiedHuffman)
            {
                var bitWriter = new T4BitWriter(this.memoryAllocator, this.configuration, useModifiedHuffman: true);
                return bitWriter.CompressImage(imageBlackWhite, pixelRowAsGraySpan, this.output);
            }

            // Write image uncompressed.
            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<TPixel> pixelRow = imageBlackWhite.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.configuration, pixelRow, pixelRowAsGraySpan);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelRowAsGraySpan[x].PackedValue == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                this.output.Write(row);
                bytesWritten += row.Length();

                row.Clear();
            }

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 1 bit black and white with deflate compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="pixelRowAsGraySpan">A span for converting a pixel row to gray.</param>
        /// <param name="outputRow">A span which will be used to store the output pixels.</param>
        /// <param name="compressionLevel">The compression level for deflate compression.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteBiColorDeflate<TPixel>(Image<TPixel> image, Span<L8> pixelRowAsGraySpan, Span<byte> outputRow, DeflateCompressionLevel compressionLevel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.memoryAllocator, memoryStream, compressionLevel);

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.configuration, pixelRow, pixelRowAsGraySpan);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelRowAsGraySpan[x].PackedValue == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                deflateStream.Write(outputRow);

                outputRow.Clear();
            }

            deflateStream.Flush();
            byte[] buffer = memoryStream.ToArray();
            this.output.Write(buffer);
            bytesWritten += buffer.Length;

            return bytesWritten;
        }

        /// <summary>
        /// Writes the image data as 1 bit black and white with pack bits compression to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel data.</typeparam>
        /// <param name="image">The image to write to the stream.</param>
        /// <param name="pixelRowAsGraySpan">A span for converting a pixel row to gray.</param>
        /// <param name="outputRow">A span which will be used to store the output pixels.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteBiColorPackBits<TPixel>(Image<TPixel> image, Span<L8> pixelRowAsGraySpan, Span<byte> outputRow)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bits.
            int additionalBytes = (image.Width / 127) + 2;
            int compressedRowBytes = (image.Width / 8) + additionalBytes;
            using IManagedByteBuffer compressedRow = this.memoryAllocator.AllocateManagedByteBuffer(compressedRowBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<TPixel> pixelRow = image.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8(this.configuration, pixelRow, pixelRowAsGraySpan);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelRowAsGraySpan[x].PackedValue == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                var size = PackBitsWriter.PackBits(outputRow, compressedRowSpan);
                this.output.Write(compressedRowSpan.Slice(0, size));
                bytesWritten += size;

                outputRow.Clear();
            }

            return bytesWritten;
        }

        /// <summary>
        /// Disposes <see cref="TiffWriter"/> instance, ensuring any unwritten data is flushed.
        /// </summary>
        public void Dispose()
        {
            this.output.Flush();
        }
    }
}

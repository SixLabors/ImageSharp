// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal class TiffPaletteWriter : TiffBaseColorWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffPaletteWriter" /> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="entriesCollector">The entries collector.</param>
        public TiffPaletteWriter(TiffStreamWriter output, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
             : base(output, memoryAllocator, configuration, entriesCollector)
        {
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
        /// <returns>
        /// The number of bytes written.
        /// </returns>
        public override int Write<TPixel>(Image<TPixel> image, IQuantizer quantizer, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
        {
            int colorsPerChannel = 256;
            int colorPaletteSize = colorsPerChannel * 3;
            int colorPaletteBytes = colorPaletteSize * 2;
            using IManagedByteBuffer row = this.MemoryAllocator.AllocateManagedByteBuffer(image.Width);
            using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration);
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds());
            using IMemoryOwner<byte> colorPaletteBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(colorPaletteBytes);
            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

            ReadOnlySpan<TPixel> quantizedColors = quantized.Palette.Span;
            int quantizedColorBytes = quantizedColors.Length * 3 * 2;

            // In the ColorMap, black is represented by 0,0,0 and white is represented by 65535, 65535, 65535.
            Span<Rgb48> quantizedColorRgb48 = MemoryMarshal.Cast<byte, Rgb48>(colorPalette.Slice(0, quantizedColorBytes));
            PixelOperations<TPixel>.Instance.ToRgb48(this.Configuration, quantizedColors, quantizedColorRgb48);

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

            var colorMap = new ExifShortArray(ExifTagValue.ColorMap)
            {
                Value = palette
            };

            this.EntriesCollector.Add(colorMap);

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
                this.Output.Write(pixelSpan);
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
        private int WriteDeflateCompressedPalettedRgb<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IManagedByteBuffer tmpBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(image.Width);
            using var memoryStream = new MemoryStream();
            using var deflateStream = new ZlibDeflateStream(this.MemoryAllocator, memoryStream, compressionLevel);

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
            this.Output.Write(buffer);
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
        private int WriteLzwCompressedPalettedRgb<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IMemoryOwner<byte> pixelData = this.MemoryAllocator.Allocate<byte>(image.Width * image.Height);
            using IManagedByteBuffer tmpBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(image.Width);
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

            using var lzwEncoder = new TiffLzwEncoder(this.MemoryAllocator, pixelData);
            lzwEncoder.Encode(memoryStream);

            byte[] buffer = memoryStream.ToArray();
            this.Output.Write(buffer);
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
        private int WritePackBitsCompressedPalettedRgb<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Worst case is that the actual compressed data is larger then the input data. In this case we need 1 additional byte per 127 bytes.
            int additionalBytes = ((image.Width * 3) / 127) + 1;
            using IManagedByteBuffer compressedRow = this.MemoryAllocator.AllocateManagedByteBuffer((image.Width * 3) + additionalBytes, AllocationOptions.Clean);
            Span<byte> compressedRowSpan = compressedRow.GetSpan();

            int bytesWritten = 0;
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<byte> pixelSpan = quantized.GetPixelRowSpan(y);

                int size = PackBitsWriter.PackBits(pixelSpan, compressedRowSpan);
                this.Output.Write(compressedRowSpan.Slice(0, size));
                bytesWritten += size;
            }

            return bytesWritten;
        }
    }
}

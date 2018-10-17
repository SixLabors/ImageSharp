// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Performs the png encoding operation.
    /// </summary>
    internal sealed class PngEncoderCore : IDisposable
    {
        /// <summary>
        /// The dictionary of available color types.
        /// </summary>
        private static readonly Dictionary<PngColorType, byte[]> ColorTypes = new Dictionary<PngColorType, byte[]>()
        {
            [PngColorType.Grayscale] = new byte[] { 1, 2, 4, 8, 16 },
            [PngColorType.Rgb] = new byte[] { 8, 16 },
            [PngColorType.Palette] = new byte[] { 1, 2, 4, 8 },
            [PngColorType.GrayscaleWithAlpha] = new byte[] { 8, 16 },
            [PngColorType.RgbWithAlpha] = new byte[] { 8, 16 }
        };

        /// <summary>
        /// Used the manage memory allocations.
        /// </summary>
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
        /// The png filter method.
        /// </summary>
        private readonly PngFilterMethod pngFilterMethod;

        /// <summary>
        /// Gets or sets the CompressionLevel value
        /// </summary>
        private readonly int compressionLevel;

        /// <summary>
        /// Gets or sets the alpha threshold value
        /// </summary>
        private readonly byte threshold;

        /// <summary>
        /// The quantizer for reducing the color count.
        /// </summary>
        private IQuantizer quantizer;

        /// <summary>
        /// Gets or sets a value indicating whether to write the gamma chunk
        /// </summary>
        private bool writeGamma;

        /// <summary>
        /// The png bit depth
        /// </summary>
        private PngBitDepth? pngBitDepth;

        /// <summary>
        /// Gets or sets a value indicating whether to use 16 bit encoding for supported color types.
        /// </summary>
        private bool use16Bit;

        /// <summary>
        /// The png color type.
        /// </summary>
        private PngColorType? pngColorType;

        /// <summary>
        /// Gets or sets the Gamma value
        /// </summary>
        private float? gamma;

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
            this.pngColorType = options.ColorType;

            // Specification recommends default filter method None for paletted images and Paeth for others.
            this.pngFilterMethod = options.FilterMethod ?? (options.ColorType.Equals(PngColorType.Palette)
                ? PngFilterMethod.None
                : PngFilterMethod.Paeth);
            this.compressionLevel = options.CompressionLevel;
            this.gamma = options.Gamma;
            this.quantizer = options.Quantizer;
            this.threshold = options.Threshold;
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

            // Always take the encoder options over the metadata values.
            ImageMetaData metaData = image.MetaData;
            PngMetaData pngMetaData = metaData.GetFormatMetaData(PngFormat.Instance);
            this.gamma = this.gamma ?? pngMetaData.Gamma;
            this.writeGamma = this.gamma > 0;
            this.pngColorType = this.pngColorType ?? pngMetaData.ColorType;
            this.pngBitDepth = this.pngBitDepth ?? pngMetaData.BitDepth;
            this.use16Bit = this.pngBitDepth.Equals(PngBitDepth.Bit16);

            // Ensure we are not allowing impossible combinations.
            if (!ColorTypes.ContainsKey(this.pngColorType.Value))
            {
                throw new NotSupportedException("Color type is not supported or not valid.");
            }

            stream.Write(PngConstants.HeaderBytes, 0, PngConstants.HeaderBytes.Length);

            QuantizedFrame<TPixel> quantized = null;
            if (this.pngColorType == PngColorType.Palette)
            {
                byte bits = (byte)this.pngBitDepth;
                if (!ColorTypes[this.pngColorType.Value].Contains(bits))
                {
                    throw new NotSupportedException("Bit depth is not supported or not valid.");
                }

                // Use the metadata to determine what quantization depth to use if no quantizer has been set.
                if (this.quantizer == null)
                {
                    this.quantizer = new WuQuantizer(ImageMaths.GetColorCountForBitDepth(bits));
                }

                // Create quantized frame returning the palette and set the bit depth.
                quantized = this.quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(image.Frames.RootFrame);
                byte quantizedBits = (byte)ImageMaths.GetBitsNeededForColorDepth(quantized.Palette.Length).Clamp(1, 8);
                bits = Math.Max(bits, quantizedBits);

                // Png only supports in four pixel depths: 1, 2, 4, and 8 bits when using the PLTE chunk
                // We check again for the bit depth as the bit depth of the color palette from a given quantizer might not
                // be within the acceptable range.
                if (bits == 3)
                {
                    bits = 4;
                }
                else if (bits >= 5 && bits <= 7)
                {
                    bits = 8;
                }

                this.bitDepth = bits;
            }
            else
            {
                this.bitDepth = (byte)this.pngBitDepth;
                if (!ColorTypes[this.pngColorType.Value].Contains(this.bitDepth))
                {
                    throw new NotSupportedException("Bit depth is not supported or not valid.");
                }
            }

            this.bytesPerPixel = this.CalculateBytesPerPixel();

            var header = new PngHeader(
                width: image.Width,
                height: image.Height,
                bitDepth: this.bitDepth,
                colorType: this.pngColorType.Value,
                compressionMethod: 0, // None
                filterMethod: 0,
                interlaceMethod: 0); // TODO: Can't write interlaced yet.

            this.WriteHeaderChunk(stream, header);

            // Collect the indexed pixel data
            if (quantized != null)
            {
                this.WritePaletteChunk(stream, quantized);
            }

            this.WritePhysicalChunk(stream, metaData);
            this.WriteGammaChunk(stream);
            this.WriteExifChunk(stream, metaData);
            this.WriteDataChunks(image.Frames.RootFrame, quantized, stream);
            this.WriteEndChunk(stream);
            stream.Flush();

            quantized?.Dispose();
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

            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
            Span<byte> rawScanlineSpan = this.rawScanline.GetSpan();
            ref byte rawScanlineSpanRef = ref MemoryMarshal.GetReference(rawScanlineSpan);

            if (this.pngColorType.Equals(PngColorType.Grayscale))
            {
                // TODO: Research and add support for grayscale plus tRNS
                if (this.use16Bit)
                {
                    // 16 bit grayscale
                    Rgb48 rgb = default;
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 2)
                    {
                        Unsafe.Add(ref rowSpanRef, x).ToRgb48(ref rgb);
                        ushort luminance = (ushort)((RX * rgb.R) + (GX * rgb.G) + (BX * rgb.B));
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), luminance);
                    }
                }
                else
                {
                    if (this.bitDepth == 8)
                    {
                        // 8 bit grayscale
                        Rgb24 rgb = default;
                        for (int x = 0; x < rowSpan.Length; x++)
                        {
                            Unsafe.Add(ref rowSpanRef, x).ToRgb24(ref rgb);
                            Unsafe.Add(ref rawScanlineSpanRef, x) = (byte)((RX * rgb.R) + (GX * rgb.G) + (BX * rgb.B));
                        }
                    }
                    else
                    {
                        // 1, 2, and 4 bit grayscale
                        using (IManagedByteBuffer temp = this.memoryAllocator.AllocateManagedByteBuffer(rowSpan.Length, AllocationOptions.Clean))
                        {
                            int scaleFactor = 255 / (ImageMaths.GetColorCountForBitDepth(this.bitDepth) - 1);
                            Span<byte> tempSpan = temp.GetSpan();
                            ref byte tempSpanRef = ref MemoryMarshal.GetReference(tempSpan);

                            Rgb24 rgb = default;
                            for (int x = 0; x < rowSpan.Length; x++)
                            {
                                Unsafe.Add(ref rowSpanRef, x).ToRgb24(ref rgb);
                                float luminance = ((RX * rgb.R) + (GX * rgb.G) + (BX * rgb.B)) / scaleFactor;
                                Unsafe.Add(ref tempSpanRef, x) = (byte)luminance;
                                this.ScaleDownFrom8BitArray(tempSpan, rawScanlineSpan, this.bitDepth);
                            }
                        }
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
                        Unsafe.Add(ref rowSpanRef, x).ToRgba64(ref rgba);
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
                        Unsafe.Add(ref rowSpanRef, x).ToRgba32(ref rgba);
                        Unsafe.Add(ref rawScanlineSpanRef, o) = (byte)((RX * rgba.R) + (GX * rgba.G) + (BX * rgba.B));
                        Unsafe.Add(ref rawScanlineSpanRef, o + 1) = rgba.A;
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
                        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
                        for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 8)
                        {
                            Unsafe.Add(ref rowSpanRef, x).ToRgba64(ref rgba);
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
                        ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
                        for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 6)
                        {
                            Unsafe.Add(ref rowSpanRef, x).ToRgb48(ref rgb);
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
        /// <param name="quantized">The quantized pixels. Can be null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="IManagedByteBuffer"/></returns>
        private IManagedByteBuffer EncodePixelRow<TPixel>(ReadOnlySpan<TPixel> rowSpan, QuantizedFrame<TPixel> quantized, int row)
            where TPixel : struct, IPixel<TPixel>
        {
            switch (this.pngColorType)
            {
                case PngColorType.Palette:

                    if (this.bitDepth < 8)
                    {
                        this.ScaleDownFrom8BitArray(quantized.GetRowSpan(row), this.rawScanline.GetSpan(), this.bitDepth);
                    }
                    else
                    {
                        int stride = this.rawScanline.Length();
                        quantized.GetPixelSpan().Slice(row * stride, stride).CopyTo(this.rawScanline.GetSpan());
                    }

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
        /// <returns>Bytes per pixel</returns>
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
            header.WriteTo(this.chunkDataBuffer);

            this.WriteChunk(stream, PngChunkType.Header, this.chunkDataBuffer, 0, PngHeader.Size);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="quantized">The quantized frame.</param>
        private void WritePaletteChunk<TPixel>(Stream stream, QuantizedFrame<TPixel> quantized)
            where TPixel : struct, IPixel<TPixel>
        {
            // Grab the palette and write it to the stream.
            TPixel[] palette = quantized.Palette;
            int paletteLength = Math.Min(palette.Length, 256);
            int colorTableLength = paletteLength * 3;
            Rgba32 rgba = default;
            bool anyAlpha = false;

            using (IManagedByteBuffer colorTable = this.memoryAllocator.AllocateManagedByteBuffer(colorTableLength))
            using (IManagedByteBuffer alphaTable = this.memoryAllocator.AllocateManagedByteBuffer(paletteLength))
            {
                ref byte colorTableRef = ref MemoryMarshal.GetReference(colorTable.GetSpan());
                ref byte alphaTableRef = ref MemoryMarshal.GetReference(alphaTable.GetSpan());
                Span<byte> quantizedSpan = quantized.GetPixelSpan();

                for (int i = 0; i < paletteLength; i++)
                {
                    if (quantizedSpan.IndexOf((byte)i) > -1)
                    {
                        int offset = i * 3;
                        palette[i].ToRgba32(ref rgba);

                        byte alpha = rgba.A;

                        Unsafe.Add(ref colorTableRef, offset) = rgba.R;
                        Unsafe.Add(ref colorTableRef, offset + 1) = rgba.G;
                        Unsafe.Add(ref colorTableRef, offset + 2) = rgba.B;

                        if (alpha > this.threshold)
                        {
                            alpha = byte.MaxValue;
                        }

                        anyAlpha = anyAlpha || alpha < byte.MaxValue;
                        Unsafe.Add(ref alphaTableRef, i) = alpha;
                    }
                }

                this.WriteChunk(stream, PngChunkType.Palette, colorTable.Array, 0, colorTableLength);

                // Write the transparency data
                if (anyAlpha)
                {
                    this.WriteChunk(stream, PngChunkType.PaletteAlpha, alphaTable.Array, 0, paletteLength);
                }
            }
        }

        /// <summary>
        /// Writes the physical dimension information to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="meta">The image meta data.</param>
        private void WritePhysicalChunk(Stream stream, ImageMetaData meta)
        {
            PhysicalChunkData.FromMetadata(meta).WriteTo(this.chunkDataBuffer);

            this.WriteChunk(stream, PngChunkType.Physical, this.chunkDataBuffer, 0, PhysicalChunkData.Size);
        }

        /// <summary>
        /// Writes the eXIf chunk to the stream, if any EXIF Profile values are present in the meta data.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="meta">The image meta data.</param>
        private void WriteExifChunk(Stream stream, ImageMetaData meta)
        {
            if (meta.ExifProfile?.Values.Count > 0)
            {
                meta.SyncProfiles();
                this.WriteChunk(stream, PngChunkType.Exif, meta.ExifProfile.ToByteArray());
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
        /// <param name="quantized">The quantized pixel data. Can be null.</param>
        /// <param name="stream">The stream.</param>
        private void WriteDataChunks<TPixel>(ImageFrame<TPixel> pixels, QuantizedFrame<TPixel> quantized, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            this.bytesPerScanline = this.CalculateScanlineLength(this.width);
            int resultLength = this.bytesPerScanline + 1;

            this.previousScanline = this.memoryAllocator.AllocateManagedByteBuffer(this.bytesPerScanline, AllocationOptions.Clean);
            this.rawScanline = this.memoryAllocator.AllocateManagedByteBuffer(this.bytesPerScanline, AllocationOptions.Clean);
            this.result = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);

            switch (this.pngFilterMethod)
            {
                case PngFilterMethod.None:
                    break;

                case PngFilterMethod.Sub:
                    this.sub = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    break;

                case PngFilterMethod.Up:
                    this.up = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    break;

                case PngFilterMethod.Average:
                    this.average = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    break;

                case PngFilterMethod.Paeth:
                    this.paeth = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    break;

                case PngFilterMethod.Adaptive:
                    this.sub = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    this.up = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    this.average = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                    this.paeth = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
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
                        IManagedByteBuffer r = this.EncodePixelRow((ReadOnlySpan<TPixel>)pixels.GetPixelRowSpan(y), quantized, y);
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
        private void WriteEndChunk(Stream stream) => this.WriteChunk(stream, PngChunkType.End, null);

        /// <summary>
        /// Writes a chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="type">The type of chunk to write.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing data.</param>
        private void WriteChunk(Stream stream, PngChunkType type, byte[] data) => this.WriteChunk(stream, type, data, 0, data?.Length ?? 0);

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

        /// <summary>
        /// Packs the given 8 bit array into and array of <paramref name="bits"/> depths.
        /// </summary>
        /// <param name="source">The source span in 8 bits.</param>
        /// <param name="result">The resultant span in <paramref name="bits"/>.</param>
        /// <param name="bits">The bit depth.</param>
        private void ScaleDownFrom8BitArray(ReadOnlySpan<byte> source, Span<byte> result, int bits)
        {
            ref byte sourceRef = ref MemoryMarshal.GetReference(source);
            ref byte resultRef = ref MemoryMarshal.GetReference(result);

            byte mask = (byte)(0xFF >> (8 - bits));
            byte shift0 = (byte)(8 - bits);
            int shift = 8 - bits;
            int v = 0;
            int resultOffset = 0;

            for (int i = 0; i < source.Length; i++)
            {
                int value = Unsafe.Add(ref sourceRef, i) & mask;
                v |= value << shift;

                if (shift == 0)
                {
                    shift = shift0;
                    Unsafe.Add(ref resultRef, resultOffset) = (byte)v;
                    resultOffset++;
                    v = 0;
                }
                else
                {
                    shift -= bits;
                }
            }

            if (shift != shift0)
            {
                Unsafe.Add(ref resultRef, resultOffset) = (byte)v;
            }
        }

        /// <summary>
        /// Calculates the scanline length.
        /// </summary>
        /// <param name="width">The width of the row.</param>
        /// <returns>
        /// The <see cref="int"/> representing the length.
        /// </returns>
        private int CalculateScanlineLength(int width)
        {
            int mod = this.bitDepth == 16 ? 16 : 8;
            int scanlineLength = width * this.bitDepth * this.bytesPerPixel;

            int amount = scanlineLength % mod;
            if (amount != 0)
            {
                scanlineLength += mod - amount;
            }

            return scanlineLength / mod;
        }
    }
}
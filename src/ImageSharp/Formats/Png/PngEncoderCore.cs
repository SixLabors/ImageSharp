// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Performs the png encoding operation.
    /// </summary>
    internal sealed class PngEncoderCore : IImageEncoderInternals, IDisposable
    {
        /// <summary>
        /// The maximum block size, defaults at 64k for uncompressed blocks.
        /// </summary>
        private const int MaxBlockSize = 65535;

        /// <summary>
        /// Used the manage memory allocations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The configuration instance for the decoding operation.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Reusable buffer for writing general data.
        /// </summary>
        private readonly byte[] buffer = new byte[8];

        /// <summary>
        /// Reusable buffer for writing chunk data.
        /// </summary>
        private readonly byte[] chunkDataBuffer = new byte[16];

        /// <summary>
        /// The encoder options
        /// </summary>
        private readonly PngEncoderOptions options;

        /// <summary>
        /// The bit depth.
        /// </summary>
        private byte bitDepth;

        /// <summary>
        /// Gets or sets a value indicating whether to use 16 bit encoding for supported color types.
        /// </summary>
        private bool use16Bit;

        /// <summary>
        /// The number of bytes per pixel.
        /// </summary>
        private int bytesPerPixel;

        /// <summary>
        /// The image width.
        /// </summary>
        private int width;

        /// <summary>
        /// The image height.
        /// </summary>
        private int height;

        /// <summary>
        /// The raw data of previous scanline.
        /// </summary>
        private IManagedByteBuffer previousScanline;

        /// <summary>
        /// The raw data of current scanline.
        /// </summary>
        private IManagedByteBuffer currentScanline;

        /// <summary>
        /// The common buffer for the filters.
        /// </summary>
        private IManagedByteBuffer filterBuffer;

        /// <summary>
        /// The ext buffer for the sub filter, <see cref="PngFilterMethod.Adaptive"/>.
        /// </summary>
        private IManagedByteBuffer subFilter;

        /// <summary>
        /// The ext buffer for the average filter, <see cref="PngFilterMethod.Adaptive"/>.
        /// </summary>
        private IManagedByteBuffer averageFilter;

        /// <summary>
        /// The ext buffer for the Paeth filter, <see cref="PngFilterMethod.Adaptive"/>.
        /// </summary>
        private IManagedByteBuffer paethFilter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngEncoderCore" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator" /> to use for buffer allocations.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options for influencing the encoder</param>
        public PngEncoderCore(MemoryAllocator memoryAllocator, Configuration configuration, PngEncoderOptions options)
        {
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
            this.options = options;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to request cancellation.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
                where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.width = image.Width;
            this.height = image.Height;

            ImageMetadata metadata = image.Metadata;

            PngMetadata pngMetadata = metadata.GetFormatMetadata(PngFormat.Instance);
            PngEncoderOptionsHelpers.AdjustOptions<TPixel>(this.options, pngMetadata, out this.use16Bit, out this.bytesPerPixel);
            Image<TPixel> clonedImage = null;
            bool clearTransparency = this.options.TransparentColorMode == PngTransparentColorMode.Clear;
            if (clearTransparency)
            {
                clonedImage = image.Clone();
                ClearTransparentPixels(clonedImage);
            }

            IndexedImageFrame<TPixel> quantized = this.CreateQuantizedImage(image, clonedImage);

            stream.Write(PngConstants.HeaderBytes);

            this.WriteHeaderChunk(stream);
            this.WriteGammaChunk(stream);
            this.WritePaletteChunk(stream, quantized);
            this.WriteTransparencyChunk(stream, pngMetadata);
            this.WritePhysicalChunk(stream, metadata);
            this.WriteExifChunk(stream, metadata);
            this.WriteTextChunks(stream, pngMetadata);
            this.WriteDataChunks(clearTransparency ? clonedImage : image, quantized, stream);
            this.WriteEndChunk(stream);

            stream.Flush();

            quantized?.Dispose();
            clonedImage?.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.previousScanline?.Dispose();
            this.currentScanline?.Dispose();
            this.subFilter?.Dispose();
            this.averageFilter?.Dispose();
            this.paethFilter?.Dispose();
            this.filterBuffer?.Dispose();

            this.previousScanline = null;
            this.currentScanline = null;
            this.subFilter = null;
            this.averageFilter = null;
            this.paethFilter = null;
            this.filterBuffer = null;
        }

        /// <summary>
        /// Convert transparent pixels, to transparent black pixels, which can yield to better compression in some cases.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="image">The cloned image where the transparent pixels will be changed.</param>
        private static void ClearTransparentPixels<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba32 = default;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> span = image.GetPixelRowSpan(y);
                for (int x = 0; x < image.Width; x++)
                {
                    span[x].ToRgba32(ref rgba32);

                    if (rgba32.A == 0)
                    {
                        span[x].FromRgba32(Color.Transparent);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the quantized image and sets calculates and sets the bit depth.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="image">The image to quantize.</param>
        /// <param name="clonedImage">Cloned image with transparent pixels are changed to black.</param>
        /// <returns>The quantized image.</returns>
        private IndexedImageFrame<TPixel> CreateQuantizedImage<TPixel>(Image<TPixel> image, Image<TPixel> clonedImage)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IndexedImageFrame<TPixel> quantized;
            if (this.options.TransparentColorMode == PngTransparentColorMode.Clear)
            {
                quantized = PngEncoderOptionsHelpers.CreateQuantizedFrame(this.options, clonedImage);
                this.bitDepth = PngEncoderOptionsHelpers.CalculateBitDepth(this.options, quantized);
            }
            else
            {
                quantized = PngEncoderOptionsHelpers.CreateQuantizedFrame(this.options, image);
                this.bitDepth = PngEncoderOptionsHelpers.CalculateBitDepth(this.options, quantized);
            }

            return quantized;
        }

        /// <summary>Collects a row of grayscale pixels.</summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="rowSpan">The image row span.</param>
        private void CollectGrayscaleBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel rowSpanRef = ref MemoryMarshal.GetReference(rowSpan);
            Span<byte> rawScanlineSpan = this.currentScanline.GetSpan();
            ref byte rawScanlineSpanRef = ref MemoryMarshal.GetReference(rawScanlineSpan);

            if (this.options.ColorType == PngColorType.Grayscale)
            {
                if (this.use16Bit)
                {
                    // 16 bit grayscale
                    using (IMemoryOwner<L16> luminanceBuffer = this.memoryAllocator.Allocate<L16>(rowSpan.Length))
                    {
                        Span<L16> luminanceSpan = luminanceBuffer.GetSpan();
                        ref L16 luminanceRef = ref MemoryMarshal.GetReference(luminanceSpan);
                        PixelOperations<TPixel>.Instance.ToL16(this.configuration, rowSpan, luminanceSpan);

                        // Can't map directly to byte array as it's big-endian.
                        for (int x = 0, o = 0; x < luminanceSpan.Length; x++, o += 2)
                        {
                            L16 luminance = Unsafe.Add(ref luminanceRef, x);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), luminance.PackedValue);
                        }
                    }
                }
                else
                {
                    if (this.bitDepth == 8)
                    {
                        // 8 bit grayscale
                        PixelOperations<TPixel>.Instance.ToL8Bytes(
                            this.configuration,
                            rowSpan,
                            rawScanlineSpan,
                            rowSpan.Length);
                    }
                    else
                    {
                        // 1, 2, and 4 bit grayscale
                        using (IManagedByteBuffer temp = this.memoryAllocator.AllocateManagedByteBuffer(
                            rowSpan.Length,
                            AllocationOptions.Clean))
                        {
                            int scaleFactor = 255 / (ColorNumerics.GetColorCountForBitDepth(this.bitDepth) - 1);
                            Span<byte> tempSpan = temp.GetSpan();

                            // We need to first create an array of luminance bytes then scale them down to the correct bit depth.
                            PixelOperations<TPixel>.Instance.ToL8Bytes(
                                this.configuration,
                                rowSpan,
                                tempSpan,
                                rowSpan.Length);
                            PngEncoderHelpers.ScaleDownFrom8BitArray(tempSpan, rawScanlineSpan, this.bitDepth, scaleFactor);
                        }
                    }
                }
            }
            else
            {
                if (this.use16Bit)
                {
                    // 16 bit grayscale + alpha
                    // TODO: Should we consider in the future a GrayAlpha32 type.
                    using (IMemoryOwner<Rgba64> rgbaBuffer = this.memoryAllocator.Allocate<Rgba64>(rowSpan.Length))
                    {
                        Span<Rgba64> rgbaSpan = rgbaBuffer.GetSpan();
                        ref Rgba64 rgbaRef = ref MemoryMarshal.GetReference(rgbaSpan);
                        PixelOperations<TPixel>.Instance.ToRgba64(this.configuration, rowSpan, rgbaSpan);

                        // Can't map directly to byte array as it's big endian.
                        for (int x = 0, o = 0; x < rgbaSpan.Length; x++, o += 4)
                        {
                            Rgba64 rgba = Unsafe.Add(ref rgbaRef, x);
                            ushort luminance = ColorNumerics.Get16BitBT709Luminance(rgba.R, rgba.G, rgba.B);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), luminance);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgba.A);
                        }
                    }
                }
                else
                {
                    // 8 bit grayscale + alpha
                    // TODO: Should we consider in the future a GrayAlpha16 type.
                    Rgba32 rgba = default;
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 2)
                    {
                        Unsafe.Add(ref rowSpanRef, x).ToRgba32(ref rgba);
                        Unsafe.Add(ref rawScanlineSpanRef, o) =
                            ColorNumerics.Get8BitBT709Luminance(rgba.R, rgba.G, rgba.B);
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
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Span<byte> rawScanlineSpan = this.currentScanline.GetSpan();

            switch (this.bytesPerPixel)
            {
                case 4:
                {
                    // 8 bit Rgba
                    PixelOperations<TPixel>.Instance.ToRgba32Bytes(
                        this.configuration,
                        rowSpan,
                        rawScanlineSpan,
                        rowSpan.Length);
                    break;
                }

                case 3:
                {
                    // 8 bit Rgb
                    PixelOperations<TPixel>.Instance.ToRgb24Bytes(
                        this.configuration,
                        rowSpan,
                        rawScanlineSpan,
                        rowSpan.Length);
                    break;
                }

                case 8:
                {
                    // 16 bit Rgba
                    using (IMemoryOwner<Rgba64> rgbaBuffer = this.memoryAllocator.Allocate<Rgba64>(rowSpan.Length))
                    {
                        Span<Rgba64> rgbaSpan = rgbaBuffer.GetSpan();
                        ref Rgba64 rgbaRef = ref MemoryMarshal.GetReference(rgbaSpan);
                        PixelOperations<TPixel>.Instance.ToRgba64(this.configuration, rowSpan, rgbaSpan);

                        // Can't map directly to byte array as it's big endian.
                        for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 8)
                        {
                            Rgba64 rgba = Unsafe.Add(ref rgbaRef, x);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), rgba.R);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgba.G);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 4, 2), rgba.B);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 6, 2), rgba.A);
                        }
                    }

                    break;
                }

                default:
                {
                    // 16 bit Rgb
                    using (IMemoryOwner<Rgb48> rgbBuffer = this.memoryAllocator.Allocate<Rgb48>(rowSpan.Length))
                    {
                        Span<Rgb48> rgbSpan = rgbBuffer.GetSpan();
                        ref Rgb48 rgbRef = ref MemoryMarshal.GetReference(rgbSpan);
                        PixelOperations<TPixel>.Instance.ToRgb48(this.configuration, rowSpan, rgbSpan);

                        // Can't map directly to byte array as it's big endian.
                        for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 6)
                        {
                            Rgb48 rgb = Unsafe.Add(ref rgbRef, x);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), rgb.R);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgb.G);
                            BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 4, 2), rgb.B);
                        }
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
        private void CollectPixelBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan, IndexedImageFrame<TPixel> quantized, int row)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            switch (this.options.ColorType)
            {
                case PngColorType.Palette:

                    if (this.bitDepth < 8)
                    {
                        PngEncoderHelpers.ScaleDownFrom8BitArray(quantized.GetPixelRowSpan(row), this.currentScanline.GetSpan(), this.bitDepth);
                    }
                    else
                    {
                        quantized.GetPixelRowSpan(row).CopyTo(this.currentScanline.GetSpan());
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
        }

        /// <summary>
        /// Apply filter for the raw scanline.
        /// </summary>
        private IManagedByteBuffer FilterPixelBytes()
        {
            switch (this.options.FilterMethod)
            {
                case PngFilterMethod.None:
                    NoneFilter.Encode(this.currentScanline.GetSpan(), this.filterBuffer.GetSpan());
                    return this.filterBuffer;

                case PngFilterMethod.Sub:
                    SubFilter.Encode(this.currentScanline.GetSpan(), this.filterBuffer.GetSpan(), this.bytesPerPixel, out int _);
                    return this.filterBuffer;

                case PngFilterMethod.Up:
                    UpFilter.Encode(this.currentScanline.GetSpan(), this.previousScanline.GetSpan(), this.filterBuffer.GetSpan(), out int _);
                    return this.filterBuffer;

                case PngFilterMethod.Average:
                    AverageFilter.Encode(this.currentScanline.GetSpan(), this.previousScanline.GetSpan(), this.filterBuffer.GetSpan(), this.bytesPerPixel, out int _);
                    return this.filterBuffer;

                case PngFilterMethod.Paeth:
                    PaethFilter.Encode(this.currentScanline.GetSpan(), this.previousScanline.GetSpan(), this.filterBuffer.GetSpan(), this.bytesPerPixel, out int _);
                    return this.filterBuffer;

                default:
                    return this.GetOptimalFilteredScanline();
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
        private IManagedByteBuffer EncodePixelRow<TPixel>(ReadOnlySpan<TPixel> rowSpan, IndexedImageFrame<TPixel> quantized, int row)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.CollectPixelBytes(rowSpan, quantized, row);
            return this.FilterPixelBytes();
        }

        /// <summary>
        /// Encodes the indexed pixel data (with palette) for Adam7 interlaced mode.
        /// </summary>
        /// <param name="rowSpan">The row span.</param>
        private IManagedByteBuffer EncodeAdam7IndexedPixelRow(ReadOnlySpan<byte> rowSpan)
        {
            // CollectPixelBytes
            if (this.bitDepth < 8)
            {
                PngEncoderHelpers.ScaleDownFrom8BitArray(rowSpan, this.currentScanline.GetSpan(), this.bitDepth);
            }
            else
            {
                rowSpan.CopyTo(this.currentScanline.GetSpan());
            }

            return this.FilterPixelBytes();
        }

        /// <summary>
        /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
        /// to be most compressible, using lowest total variation as proxy for compressibility.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        private IManagedByteBuffer GetOptimalFilteredScanline()
        {
            // Palette images don't compress well with adaptive filtering.
            if (this.options.ColorType == PngColorType.Palette || this.bitDepth < 8)
            {
                NoneFilter.Encode(this.currentScanline.GetSpan(), this.filterBuffer.GetSpan());
                return this.filterBuffer;
            }

            this.AllocateExtBuffers();
            Span<byte> scanSpan = this.currentScanline.GetSpan();
            Span<byte> prevSpan = this.previousScanline.GetSpan();

            // This order, while different to the enumerated order is more likely to produce a smaller sum
            // early on which shaves a couple of milliseconds off the processing time.
            UpFilter.Encode(scanSpan, prevSpan, this.filterBuffer.GetSpan(), out int currentSum);

            // TODO: PERF.. We should be breaking out of the encoding for each line as soon as we hit the sum.
            // That way the above comment would actually be true. It used to be anyway...
            // If we could use SIMD for none branching filters we could really speed it up.
            int lowestSum = currentSum;
            IManagedByteBuffer actualResult = this.filterBuffer;

            PaethFilter.Encode(scanSpan, prevSpan, this.paethFilter.GetSpan(), this.bytesPerPixel, out currentSum);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                actualResult = this.paethFilter;
            }

            SubFilter.Encode(scanSpan, this.subFilter.GetSpan(), this.bytesPerPixel, out currentSum);

            if (currentSum < lowestSum)
            {
                lowestSum = currentSum;
                actualResult = this.subFilter;
            }

            AverageFilter.Encode(scanSpan, prevSpan, this.averageFilter.GetSpan(), this.bytesPerPixel, out currentSum);

            if (currentSum < lowestSum)
            {
                actualResult = this.averageFilter;
            }

            return actualResult;
        }

        /// <summary>
        /// Writes the header chunk to the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void WriteHeaderChunk(Stream stream)
        {
            var header = new PngHeader(
                width: this.width,
                height: this.height,
                bitDepth: this.bitDepth,
                colorType: this.options.ColorType.Value,
                compressionMethod: 0, // None
                filterMethod: 0,
                interlaceMethod: this.options.InterlaceMethod.Value);

            header.WriteTo(this.chunkDataBuffer);

            this.WriteChunk(stream, PngChunkType.Header, this.chunkDataBuffer, 0, PngHeader.Size);
        }

        /// <summary>
        /// Writes the palette chunk to the stream.
        /// Should be written before the first IDAT chunk.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="quantized">The quantized frame.</param>
        private void WritePaletteChunk<TPixel>(Stream stream, IndexedImageFrame<TPixel> quantized)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (quantized is null)
            {
                return;
            }

            // Grab the palette and write it to the stream.
            ReadOnlySpan<TPixel> palette = quantized.Palette.Span;
            int paletteLength = palette.Length;
            int colorTableLength = paletteLength * Unsafe.SizeOf<Rgb24>();
            bool hasAlpha = false;

            using IManagedByteBuffer colorTable = this.memoryAllocator.AllocateManagedByteBuffer(colorTableLength);
            using IManagedByteBuffer alphaTable = this.memoryAllocator.AllocateManagedByteBuffer(paletteLength);

            ref Rgb24 colorTableRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, Rgb24>(colorTable.GetSpan()));
            ref byte alphaTableRef = ref MemoryMarshal.GetReference(alphaTable.GetSpan());

            // Bulk convert our palette to RGBA to allow assignment to tables.
            using IMemoryOwner<Rgba32> rgbaOwner = quantized.Configuration.MemoryAllocator.Allocate<Rgba32>(paletteLength);
            Span<Rgba32> rgbaPaletteSpan = rgbaOwner.GetSpan();
            PixelOperations<TPixel>.Instance.ToRgba32(quantized.Configuration, quantized.Palette.Span, rgbaPaletteSpan);
            ref Rgba32 rgbaPaletteRef = ref MemoryMarshal.GetReference(rgbaPaletteSpan);

            // Loop, assign, and extract alpha values from the palette.
            for (int i = 0; i < paletteLength; i++)
            {
                Rgba32 rgba = Unsafe.Add(ref rgbaPaletteRef, i);
                byte alpha = rgba.A;

                Unsafe.Add(ref colorTableRef, i) = rgba.Rgb;
                if (alpha > this.options.Threshold)
                {
                    alpha = byte.MaxValue;
                }

                hasAlpha = hasAlpha || alpha < byte.MaxValue;
                Unsafe.Add(ref alphaTableRef, i) = alpha;
            }

            this.WriteChunk(stream, PngChunkType.Palette, colorTable.Array, 0, colorTableLength);

            // Write the transparency data
            if (hasAlpha)
            {
                this.WriteChunk(stream, PngChunkType.Transparency, alphaTable.Array, 0, paletteLength);
            }
        }

        /// <summary>
        /// Writes the physical dimension information to the stream.
        /// Should be written before IDAT chunk.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="meta">The image metadata.</param>
        private void WritePhysicalChunk(Stream stream, ImageMetadata meta)
        {
            if (((this.options.ChunkFilter ?? PngChunkFilter.None) & PngChunkFilter.ExcludePhysicalChunk) == PngChunkFilter.ExcludePhysicalChunk)
            {
                return;
            }

            PhysicalChunkData.FromMetadata(meta).WriteTo(this.chunkDataBuffer);

            this.WriteChunk(stream, PngChunkType.Physical, this.chunkDataBuffer, 0, PhysicalChunkData.Size);
        }

        /// <summary>
        /// Writes the eXIf chunk to the stream, if any EXIF Profile values are present in the metadata.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="meta">The image metadata.</param>
        private void WriteExifChunk(Stream stream, ImageMetadata meta)
        {
            if (((this.options.ChunkFilter ?? PngChunkFilter.None) & PngChunkFilter.ExcludeExifChunk) == PngChunkFilter.ExcludeExifChunk)
            {
                return;
            }

            if (meta.ExifProfile is null || meta.ExifProfile.Values.Count == 0)
            {
                return;
            }

            meta.SyncProfiles();
            this.WriteChunk(stream, PngChunkType.Exif, meta.ExifProfile.ToByteArray());
        }

        /// <summary>
        /// Writes a text chunk to the stream. Can be either a tTXt, iTXt or zTXt chunk,
        /// depending whether the text contains any latin characters or should be compressed.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="meta">The image metadata.</param>
        private void WriteTextChunks(Stream stream, PngMetadata meta)
        {
            if (((this.options.ChunkFilter ?? PngChunkFilter.None) & PngChunkFilter.ExcludeTextChunks) == PngChunkFilter.ExcludeTextChunks)
            {
                return;
            }

            const int MaxLatinCode = 255;
            for (int i = 0; i < meta.TextData.Count; i++)
            {
                PngTextData textData = meta.TextData[i];
                bool hasUnicodeCharacters = false;
                foreach (var c in textData.Value)
                {
                    if (c > MaxLatinCode)
                    {
                        hasUnicodeCharacters = true;
                        break;
                    }
                }

                if (hasUnicodeCharacters || (!string.IsNullOrWhiteSpace(textData.LanguageTag) ||
                                             !string.IsNullOrWhiteSpace(textData.TranslatedKeyword)))
                {
                    // Write iTXt chunk.
                    byte[] keywordBytes = PngConstants.Encoding.GetBytes(textData.Keyword);
                    byte[] textBytes = textData.Value.Length > this.options.TextCompressionThreshold
                        ? this.GetCompressedTextBytes(PngConstants.TranslatedEncoding.GetBytes(textData.Value))
                        : PngConstants.TranslatedEncoding.GetBytes(textData.Value);

                    byte[] translatedKeyword = PngConstants.TranslatedEncoding.GetBytes(textData.TranslatedKeyword);
                    byte[] languageTag = PngConstants.LanguageEncoding.GetBytes(textData.LanguageTag);

                    Span<byte> outputBytes = new byte[keywordBytes.Length + textBytes.Length +
                                                      translatedKeyword.Length + languageTag.Length + 5];
                    keywordBytes.CopyTo(outputBytes);
                    if (textData.Value.Length > this.options.TextCompressionThreshold)
                    {
                        // Indicate that the text is compressed.
                        outputBytes[keywordBytes.Length + 1] = 1;
                    }

                    int keywordStart = keywordBytes.Length + 3;
                    languageTag.CopyTo(outputBytes.Slice(keywordStart));
                    int translatedKeywordStart = keywordStart + languageTag.Length + 1;
                    translatedKeyword.CopyTo(outputBytes.Slice(translatedKeywordStart));
                    textBytes.CopyTo(outputBytes.Slice(translatedKeywordStart + translatedKeyword.Length + 1));
                    this.WriteChunk(stream, PngChunkType.InternationalText, outputBytes.ToArray());
                }
                else
                {
                    if (textData.Value.Length > this.options.TextCompressionThreshold)
                    {
                        // Write zTXt chunk.
                        byte[] compressedData =
                            this.GetCompressedTextBytes(PngConstants.Encoding.GetBytes(textData.Value));
                        Span<byte> outputBytes = new byte[textData.Keyword.Length + compressedData.Length + 2];
                        PngConstants.Encoding.GetBytes(textData.Keyword).CopyTo(outputBytes);
                        compressedData.CopyTo(outputBytes.Slice(textData.Keyword.Length + 2));
                        this.WriteChunk(stream, PngChunkType.CompressedText, outputBytes.ToArray());
                    }
                    else
                    {
                        // Write tEXt chunk.
                        Span<byte> outputBytes = new byte[textData.Keyword.Length + textData.Value.Length + 1];
                        PngConstants.Encoding.GetBytes(textData.Keyword).CopyTo(outputBytes);
                        PngConstants.Encoding.GetBytes(textData.Value)
                            .CopyTo(outputBytes.Slice(textData.Keyword.Length + 1));
                        this.WriteChunk(stream, PngChunkType.Text, outputBytes.ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// Compresses a given text using Zlib compression.
        /// </summary>
        /// <param name="textBytes">The text bytes to compress.</param>
        /// <returns>The compressed text byte array.</returns>
        private byte[] GetCompressedTextBytes(byte[] textBytes)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var deflateStream = new ZlibDeflateStream(this.memoryAllocator, memoryStream, this.options.CompressionLevel))
                {
                    deflateStream.Write(textBytes);
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Writes the gamma information to the stream.
        /// Should be written before PLTE and IDAT chunk.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void WriteGammaChunk(Stream stream)
        {
            if (((this.options.ChunkFilter ?? PngChunkFilter.None) & PngChunkFilter.ExcludeGammaChunk) == PngChunkFilter.ExcludeGammaChunk)
            {
                return;
            }

            if (this.options.Gamma > 0)
            {
                // 4-byte unsigned integer of gamma * 100,000.
                uint gammaValue = (uint)(this.options.Gamma * 100_000F);

                BinaryPrimitives.WriteUInt32BigEndian(this.chunkDataBuffer.AsSpan(0, 4), gammaValue);

                this.WriteChunk(stream, PngChunkType.Gamma, this.chunkDataBuffer, 0, 4);
            }
        }

        /// <summary>
        /// Writes the transparency chunk to the stream.
        /// Should be written after PLTE and before IDAT.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="pngMetadata">The image metadata.</param>
        private void WriteTransparencyChunk(Stream stream, PngMetadata pngMetadata)
        {
            if (!pngMetadata.HasTransparency)
            {
                return;
            }

            Span<byte> alpha = this.chunkDataBuffer.AsSpan();
            if (pngMetadata.ColorType == PngColorType.Rgb)
            {
                if (pngMetadata.TransparentRgb48.HasValue && this.use16Bit)
                {
                    Rgb48 rgb = pngMetadata.TransparentRgb48.Value;
                    BinaryPrimitives.WriteUInt16LittleEndian(alpha, rgb.R);
                    BinaryPrimitives.WriteUInt16LittleEndian(alpha.Slice(2, 2), rgb.G);
                    BinaryPrimitives.WriteUInt16LittleEndian(alpha.Slice(4, 2), rgb.B);

                    this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer, 0, 6);
                }
                else if (pngMetadata.TransparentRgb24.HasValue)
                {
                    alpha.Clear();
                    Rgb24 rgb = pngMetadata.TransparentRgb24.Value;
                    alpha[1] = rgb.R;
                    alpha[3] = rgb.G;
                    alpha[5] = rgb.B;
                    this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer, 0, 6);
                }
            }
            else if (pngMetadata.ColorType == PngColorType.Grayscale)
            {
                if (pngMetadata.TransparentL16.HasValue && this.use16Bit)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(alpha, pngMetadata.TransparentL16.Value.PackedValue);
                    this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer, 0, 2);
                }
                else if (pngMetadata.TransparentL8.HasValue)
                {
                    alpha.Clear();
                    alpha[1] = pngMetadata.TransparentL8.Value.PackedValue;
                    this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer, 0, 2);
                }
            }
        }

        /// <summary>
        /// Writes the pixel information to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The image.</param>
        /// <param name="quantized">The quantized pixel data. Can be null.</param>
        /// <param name="stream">The stream.</param>
        private void WriteDataChunks<TPixel>(Image<TPixel> pixels, IndexedImageFrame<TPixel> quantized, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            byte[] buffer;
            int bufferLength;

            using (var memoryStream = new MemoryStream())
            {
                using (var deflateStream = new ZlibDeflateStream(this.memoryAllocator, memoryStream, this.options.CompressionLevel))
                {
                    if (this.options.InterlaceMethod == PngInterlaceMode.Adam7)
                    {
                        if (quantized != null)
                        {
                            this.EncodeAdam7IndexedPixels(quantized, deflateStream);
                        }
                        else
                        {
                            this.EncodeAdam7Pixels(pixels, deflateStream);
                        }
                    }
                    else
                    {
                        this.EncodePixels(pixels, quantized, deflateStream);
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
        /// Allocates the buffers for each scanline.
        /// </summary>
        /// <param name="bytesPerScanline">The bytes per scanline.</param>
        /// <param name="resultLength">Length of the result.</param>
        private void AllocateBuffers(int bytesPerScanline, int resultLength)
        {
            // Clean up from any potential previous runs.
            this.subFilter?.Dispose();
            this.averageFilter?.Dispose();
            this.paethFilter?.Dispose();
            this.subFilter = null;
            this.averageFilter = null;
            this.paethFilter = null;

            this.previousScanline?.Dispose();
            this.currentScanline?.Dispose();
            this.filterBuffer?.Dispose();
            this.previousScanline = this.memoryAllocator.AllocateManagedByteBuffer(bytesPerScanline, AllocationOptions.Clean);
            this.currentScanline = this.memoryAllocator.AllocateManagedByteBuffer(bytesPerScanline, AllocationOptions.Clean);
            this.filterBuffer = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
        }

        /// <summary>
        /// Allocates the ext buffers for adaptive filter.
        /// </summary>
        private void AllocateExtBuffers()
        {
            if (this.subFilter == null)
            {
                int resultLength = this.filterBuffer.Length();

                this.subFilter = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                this.averageFilter = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
                this.paethFilter = this.memoryAllocator.AllocateManagedByteBuffer(resultLength, AllocationOptions.Clean);
            }
        }

        /// <summary>
        /// Encodes the pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="pixels">The pixels.</param>
        /// <param name="quantized">The quantized pixels span.</param>
        /// <param name="deflateStream">The deflate stream.</param>
        private void EncodePixels<TPixel>(Image<TPixel> pixels, IndexedImageFrame<TPixel> quantized, ZlibDeflateStream deflateStream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int bytesPerScanline = this.CalculateScanlineLength(this.width);
            int resultLength = bytesPerScanline + 1;
            this.AllocateBuffers(bytesPerScanline, resultLength);

            for (int y = 0; y < this.height; y++)
            {
                IManagedByteBuffer r = this.EncodePixelRow(pixels.GetPixelRowSpan(y), quantized, y);
                deflateStream.Write(r.Array, 0, resultLength);

                IManagedByteBuffer temp = this.currentScanline;
                this.currentScanline = this.previousScanline;
                this.previousScanline = temp;
            }
        }

        /// <summary>
        /// Interlaced encoding the pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="pixels">The pixels.</param>
        /// <param name="deflateStream">The deflate stream.</param>
        private void EncodeAdam7Pixels<TPixel>(Image<TPixel> pixels, ZlibDeflateStream deflateStream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int height = pixels.Height;
            for (int pass = 0; pass < 7; pass++)
            {
                int startRow = Adam7.FirstRow[pass];
                int startCol = Adam7.FirstColumn[pass];
                int blockWidth = Adam7.ComputeBlockWidth(width, pass);

                int bytesPerScanline = this.bytesPerPixel <= 1
                    ? ((blockWidth * this.bitDepth) + 7) / 8
                    : blockWidth * this.bytesPerPixel;

                int resultLength = bytesPerScanline + 1;

                this.AllocateBuffers(bytesPerScanline, resultLength);

                using (IMemoryOwner<TPixel> passData = this.memoryAllocator.Allocate<TPixel>(blockWidth))
                {
                    Span<TPixel> destSpan = passData.Memory.Span;
                    for (int row = startRow;
                        row < height;
                        row += Adam7.RowIncrement[pass])
                    {
                        // collect data
                        Span<TPixel> srcRow = pixels.GetPixelRowSpan(row);
                        for (int col = startCol, i = 0;
                            col < width;
                            col += Adam7.ColumnIncrement[pass])
                        {
                            destSpan[i++] = srcRow[col];
                        }

                        // encode data
                        // note: quantized parameter not used
                        // note: row parameter not used
                        IManagedByteBuffer r = this.EncodePixelRow((ReadOnlySpan<TPixel>)destSpan, null, -1);
                        deflateStream.Write(r.Array, 0, resultLength);

                        IManagedByteBuffer temp = this.currentScanline;
                        this.currentScanline = this.previousScanline;
                        this.previousScanline = temp;
                    }
                }
            }
        }

        /// <summary>
        /// Interlaced encoding the quantized (indexed, with palette) pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="quantized">The quantized.</param>
        /// <param name="deflateStream">The deflate stream.</param>
        private void EncodeAdam7IndexedPixels<TPixel>(IndexedImageFrame<TPixel> quantized, ZlibDeflateStream deflateStream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = quantized.Width;
            int height = quantized.Height;
            for (int pass = 0; pass < 7; pass++)
            {
                int startRow = Adam7.FirstRow[pass];
                int startCol = Adam7.FirstColumn[pass];
                int blockWidth = Adam7.ComputeBlockWidth(width, pass);

                int bytesPerScanline = this.bytesPerPixel <= 1
                    ? ((blockWidth * this.bitDepth) + 7) / 8
                    : blockWidth * this.bytesPerPixel;

                int resultLength = bytesPerScanline + 1;

                this.AllocateBuffers(bytesPerScanline, resultLength);

                using (IMemoryOwner<byte> passData = this.memoryAllocator.Allocate<byte>(blockWidth))
                {
                    Span<byte> destSpan = passData.Memory.Span;
                    for (int row = startRow;
                        row < height;
                        row += Adam7.RowIncrement[pass])
                    {
                        // collect data
                        ReadOnlySpan<byte> srcRow = quantized.GetPixelRowSpan(row);
                        for (int col = startCol, i = 0;
                            col < width;
                            col += Adam7.ColumnIncrement[pass])
                        {
                            destSpan[i++] = srcRow[col];
                        }

                        // encode data
                        IManagedByteBuffer r = this.EncodeAdam7IndexedPixelRow(destSpan);
                        deflateStream.Write(r.Array, 0, resultLength);

                        IManagedByteBuffer temp = this.currentScanline;
                        this.currentScanline = this.previousScanline;
                        this.previousScanline = temp;
                    }
                }
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

            uint crc = Crc32.Calculate(this.buffer.AsSpan(4, 4)); // Write the type buffer

            if (data != null && length > 0)
            {
                stream.Write(data, offset, length);

                crc = Crc32.Calculate(crc, data.AsSpan(offset, length));
            }

            BinaryPrimitives.WriteUInt32BigEndian(this.buffer, crc);

            stream.Write(this.buffer, 0, 4); // write the crc
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

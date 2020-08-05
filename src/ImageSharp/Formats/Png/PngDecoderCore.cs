// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Performs the png decoding operation.
    /// </summary>
    internal sealed class PngDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        private readonly bool ignoreMetadata;

        /// <summary>
        /// Used the manage memory allocations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private BufferedReadStream currentStream;

        /// <summary>
        /// The png header.
        /// </summary>
        private PngHeader header;

        /// <summary>
        /// The number of bytes per pixel.
        /// </summary>
        private int bytesPerPixel;

        /// <summary>
        /// The number of bytes per sample.
        /// </summary>
        private int bytesPerSample;

        /// <summary>
        /// The number of bytes per scanline.
        /// </summary>
        private int bytesPerScanline;

        /// <summary>
        /// The palette containing color information for indexed png's.
        /// </summary>
        private byte[] palette;

        /// <summary>
        /// The palette containing alpha channel color information for indexed png's.
        /// </summary>
        private byte[] paletteAlpha;

        /// <summary>
        /// A value indicating whether the end chunk has been reached.
        /// </summary>
        private bool isEndChunkReached;

        /// <summary>
        /// Previous scanline processed.
        /// </summary>
        private IManagedByteBuffer previousScanline;

        /// <summary>
        /// The current scanline that is being processed.
        /// </summary>
        private IManagedByteBuffer scanline;

        /// <summary>
        /// The index of the current scanline being processed.
        /// </summary>
        private int currentRow = Adam7.FirstRow[0];

        /// <summary>
        /// The current number of bytes read in the current scanline.
        /// </summary>
        private int currentRowBytesRead;

        /// <summary>
        /// Gets or sets the png color type.
        /// </summary>
        private PngColorType pngColorType;

        /// <summary>
        /// The next chunk of data to return.
        /// </summary>
        private PngChunk? nextChunk;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public PngDecoderCore(Configuration configuration, IPngDecoderOptions options)
        {
            this.Configuration = configuration ?? Configuration.Default;
            this.memoryAllocator = this.Configuration.MemoryAllocator;
            this.ignoreMetadata = options.IgnoreMetadata;
        }

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new Size(this.header.Width, this.header.Height);

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var metadata = new ImageMetadata();
            PngMetadata pngMetadata = metadata.GetPngMetadata();
            this.currentStream = stream;
            this.currentStream.Skip(8);
            Image<TPixel> image = null;
            try
            {
                while (!this.isEndChunkReached && this.TryReadChunk(out PngChunk chunk))
                {
                    try
                    {
                        switch (chunk.Type)
                        {
                            case PngChunkType.Header:
                                this.ReadHeaderChunk(pngMetadata, chunk.Data.Array);
                                break;
                            case PngChunkType.Physical:
                                this.ReadPhysicalChunk(metadata, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Gamma:
                                this.ReadGammaChunk(pngMetadata, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Data:
                                if (image is null)
                                {
                                    this.InitializeImage(metadata, out image);
                                }

                                this.ReadScanlines(chunk, image.Frames.RootFrame, pngMetadata);

                                break;
                            case PngChunkType.Palette:
                                var pal = new byte[chunk.Length];
                                Buffer.BlockCopy(chunk.Data.Array, 0, pal, 0, chunk.Length);
                                this.palette = pal;
                                break;
                            case PngChunkType.Transparency:
                                var alpha = new byte[chunk.Length];
                                Buffer.BlockCopy(chunk.Data.Array, 0, alpha, 0, chunk.Length);
                                this.paletteAlpha = alpha;
                                this.AssignTransparentMarkers(alpha, pngMetadata);
                                break;
                            case PngChunkType.Text:
                                this.ReadTextChunk(pngMetadata, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.CompressedText:
                                this.ReadCompressedTextChunk(pngMetadata, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.InternationalText:
                                this.ReadInternationalTextChunk(pngMetadata, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.Exif:
                                if (!this.ignoreMetadata)
                                {
                                    var exifData = new byte[chunk.Length];
                                    Buffer.BlockCopy(chunk.Data.Array, 0, exifData, 0, chunk.Length);
                                    metadata.ExifProfile = new ExifProfile(exifData);
                                }

                                break;
                            case PngChunkType.End:
                                this.isEndChunkReached = true;
                                break;
                            case PngChunkType.ProprietaryApple:
                                PngThrowHelper.ThrowInvalidChunkType("Proprietary Apple PNG detected! This PNG file is not conform to the specification and cannot be decoded.");
                                break;
                        }
                    }
                    finally
                    {
                        chunk.Data?.Dispose(); // Data is rented in ReadChunkData()
                    }
                }

                if (image is null)
                {
                    PngThrowHelper.ThrowNoData();
                }

                return image;
            }
            finally
            {
                this.scanline?.Dispose();
                this.previousScanline?.Dispose();
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            var metadata = new ImageMetadata();
            PngMetadata pngMetadata = metadata.GetPngMetadata();
            this.currentStream = stream;
            this.currentStream.Skip(8);
            try
            {
                while (!this.isEndChunkReached && this.TryReadChunk(out PngChunk chunk))
                {
                    try
                    {
                        switch (chunk.Type)
                        {
                            case PngChunkType.Header:
                                this.ReadHeaderChunk(pngMetadata, chunk.Data.Array);
                                break;
                            case PngChunkType.Physical:
                                this.ReadPhysicalChunk(metadata, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Gamma:
                                this.ReadGammaChunk(pngMetadata, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Data:
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            case PngChunkType.Text:
                                this.ReadTextChunk(pngMetadata, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.CompressedText:
                                this.ReadCompressedTextChunk(pngMetadata, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.InternationalText:
                                this.ReadInternationalTextChunk(pngMetadata, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.Exif:
                                if (!this.ignoreMetadata)
                                {
                                    var exifData = new byte[chunk.Length];
                                    Buffer.BlockCopy(chunk.Data.Array, 0, exifData, 0, chunk.Length);
                                    metadata.ExifProfile = new ExifProfile(exifData);
                                }

                                break;
                            case PngChunkType.End:
                                this.isEndChunkReached = true;
                                break;
                        }
                    }
                    finally
                    {
                        chunk.Data?.Dispose(); // Data is rented in ReadChunkData()
                    }
                }
            }
            finally
            {
                this.scanline?.Dispose();
                this.previousScanline?.Dispose();
            }

            if (this.header.Width == 0 && this.header.Height == 0)
            {
                PngThrowHelper.ThrowNoHeader();
            }

            return new ImageInfo(new PixelTypeInfo(this.CalculateBitsPerPixel()), this.header.Width, this.header.Height, metadata);
        }

        /// <summary>
        /// Reads the least significant bits from the byte pair with the others set to 0.
        /// </summary>
        /// <param name="buffer">The source buffer</param>
        /// <param name="offset">THe offset</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ReadByteLittleEndian(ReadOnlySpan<byte> buffer, int offset)
            => (byte)(((buffer[offset] & 0xFF) << 16) | (buffer[offset + 1] & 0xFF));

        /// <summary>
        /// Attempts to convert a byte array to a new array where each value in the original array is represented by the
        /// specified number of bits.
        /// </summary>
        /// <param name="source">The bytes to convert from. Cannot be empty.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <param name="bits">The number of bits per value.</param>
        /// <param name="buffer">The new array.</param>
        /// <returns>The resulting <see cref="ReadOnlySpan{Byte}"/> array.</returns>
        private bool TryScaleUpTo8BitArray(ReadOnlySpan<byte> source, int bytesPerScanline, int bits, out IManagedByteBuffer buffer)
        {
            if (bits >= 8)
            {
                buffer = null;
                return false;
            }

            buffer = this.memoryAllocator.AllocateManagedByteBuffer(bytesPerScanline * 8 / bits, AllocationOptions.Clean);
            ref byte sourceRef = ref MemoryMarshal.GetReference(source);
            ref byte resultRef = ref buffer.Array[0];
            int mask = 0xFF >> (8 - bits);
            int resultOffset = 0;

            for (int i = 0; i < bytesPerScanline; i++)
            {
                byte b = Unsafe.Add(ref sourceRef, i);
                for (int shift = 0; shift < 8; shift += bits)
                {
                    int colorIndex = (b >> (8 - bits - shift)) & mask;
                    Unsafe.Add(ref resultRef, resultOffset) = (byte)colorIndex;
                    resultOffset++;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads the data chunk containing physical dimension data.
        /// </summary>
        /// <param name="metadata">The metadata to read to.</param>
        /// <param name="data">The data containing physical data.</param>
        private void ReadPhysicalChunk(ImageMetadata metadata, ReadOnlySpan<byte> data)
        {
            var physicalChunk = PhysicalChunkData.Parse(data);

            metadata.ResolutionUnits = physicalChunk.UnitSpecifier == byte.MinValue
                ? PixelResolutionUnit.AspectRatio
                : PixelResolutionUnit.PixelsPerMeter;

            metadata.HorizontalResolution = physicalChunk.XAxisPixelsPerUnit;
            metadata.VerticalResolution = physicalChunk.YAxisPixelsPerUnit;
        }

        /// <summary>
        /// Reads the data chunk containing gamma data.
        /// </summary>
        /// <param name="pngMetadata">The metadata to read to.</param>
        /// <param name="data">The data containing physical data.</param>
        private void ReadGammaChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
        {
            // The value is encoded as a 4-byte unsigned integer, representing gamma times 100000.
            // For example, a gamma of 1/2.2 would be stored as 45455.
            pngMetadata.Gamma = BinaryPrimitives.ReadUInt32BigEndian(data) / 100_000F;
        }

        /// <summary>
        /// Initializes the image and various buffers needed for processing
        /// </summary>
        /// <typeparam name="TPixel">The type the pixels will be</typeparam>
        /// <param name="metadata">The metadata information for the image</param>
        /// <param name="image">The image that we will populate</param>
        private void InitializeImage<TPixel>(ImageMetadata metadata, out Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            image = Image.CreateUninitialized<TPixel>(
                this.Configuration,
                this.header.Width,
                this.header.Height,
                metadata);

            this.bytesPerPixel = this.CalculateBytesPerPixel();
            this.bytesPerScanline = this.CalculateScanlineLength(this.header.Width) + 1;
            this.bytesPerSample = 1;
            if (this.header.BitDepth >= 8)
            {
                this.bytesPerSample = this.header.BitDepth / 8;
            }

            this.previousScanline = this.memoryAllocator.AllocateManagedByteBuffer(this.bytesPerScanline, AllocationOptions.Clean);
            this.scanline = this.Configuration.MemoryAllocator.AllocateManagedByteBuffer(this.bytesPerScanline, AllocationOptions.Clean);
        }

        /// <summary>
        /// Calculates the correct number of bits per pixel for the given color type.
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        private int CalculateBitsPerPixel()
        {
            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                case PngColorType.Palette:
                    return this.header.BitDepth;
                case PngColorType.GrayscaleWithAlpha:
                    return this.header.BitDepth * 2;
                case PngColorType.Rgb:
                    return this.header.BitDepth * 3;
                case PngColorType.RgbWithAlpha:
                    return this.header.BitDepth * 4;
                default:
                    PngThrowHelper.ThrowNotSupportedColor();
                    return -1;
            }
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
                    return this.header.BitDepth == 16 ? 2 : 1;

                case PngColorType.GrayscaleWithAlpha:
                    return this.header.BitDepth == 16 ? 4 : 2;

                case PngColorType.Palette:
                    return 1;

                case PngColorType.Rgb:
                    return this.header.BitDepth == 16 ? 6 : 3;

                case PngColorType.RgbWithAlpha:
                default:
                    return this.header.BitDepth == 16 ? 8 : 4;
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
            int mod = this.header.BitDepth == 16 ? 16 : 8;
            int scanlineLength = width * this.header.BitDepth * this.bytesPerPixel;

            int amount = scanlineLength % mod;
            if (amount != 0)
            {
                scanlineLength += mod - amount;
            }

            return scanlineLength / mod;
        }

        /// <summary>
        /// Reads the scanlines within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="chunk">The png chunk containing the compressed scanline data.</param>
        /// <param name="image"> The pixel data.</param>
        /// <param name="pngMetadata">The png metadata</param>
        private void ReadScanlines<TPixel>(PngChunk chunk, ImageFrame<TPixel> image, PngMetadata pngMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var deframeStream = new ZlibInflateStream(this.currentStream, this.ReadNextDataChunk))
            {
                deframeStream.AllocateNewBytes(chunk.Length, true);
                DeflateStream dataStream = deframeStream.CompressedStream;

                if (this.header.InterlaceMethod == PngInterlaceMode.Adam7)
                {
                    this.DecodeInterlacedPixelData(dataStream, image, pngMetadata);
                }
                else
                {
                    this.DecodePixelData(dataStream, image, pngMetadata);
                }
            }
        }

        /// <summary>
        /// Decodes the raw pixel data row by row
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="compressedStream">The compressed pixel data stream.</param>
        /// <param name="image">The image to decode to.</param>
        /// <param name="pngMetadata">The png metadata</param>
        private void DecodePixelData<TPixel>(DeflateStream compressedStream, ImageFrame<TPixel> image, PngMetadata pngMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            while (this.currentRow < this.header.Height)
            {
                int bytesRead = compressedStream.Read(this.scanline.Array, this.currentRowBytesRead, this.bytesPerScanline - this.currentRowBytesRead);
                this.currentRowBytesRead += bytesRead;
                if (this.currentRowBytesRead < this.bytesPerScanline)
                {
                    return;
                }

                this.currentRowBytesRead = 0;
                Span<byte> scanlineSpan = this.scanline.GetSpan();

                switch ((FilterType)scanlineSpan[0])
                {
                    case FilterType.None:
                        break;

                    case FilterType.Sub:
                        SubFilter.Decode(scanlineSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Up:
                        UpFilter.Decode(scanlineSpan, this.previousScanline.GetSpan());
                        break;

                    case FilterType.Average:
                        AverageFilter.Decode(scanlineSpan, this.previousScanline.GetSpan(), this.bytesPerPixel);
                        break;

                    case FilterType.Paeth:
                        PaethFilter.Decode(scanlineSpan, this.previousScanline.GetSpan(), this.bytesPerPixel);
                        break;

                    default:
                        PngThrowHelper.ThrowUnknownFilter();
                        break;
                }

                this.ProcessDefilteredScanline(scanlineSpan, image, pngMetadata);

                this.SwapBuffers();
                this.currentRow++;
            }
        }

        /// <summary>
        /// Decodes the raw interlaced pixel data row by row
        /// <see href="https://github.com/juehv/DentalImageViewer/blob/8a1a4424b15d6cc453b5de3f273daf3ff5e3a90d/DentalImageViewer/lib/jiu-0.14.3/net/sourceforge/jiu/codecs/PNGCodec.java"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="compressedStream">The compressed pixel data stream.</param>
        /// <param name="image">The current image.</param>
        /// <param name="pngMetadata">The png metadata.</param>
        private void DecodeInterlacedPixelData<TPixel>(DeflateStream compressedStream, ImageFrame<TPixel> image, PngMetadata pngMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int pass = 0;
            int width = this.header.Width;
            while (true)
            {
                int numColumns = Adam7.ComputeColumns(width, pass);

                if (numColumns == 0)
                {
                    pass++;

                    // This pass contains no data; skip to next pass
                    continue;
                }

                int bytesPerInterlaceScanline = this.CalculateScanlineLength(numColumns) + 1;

                while (this.currentRow < this.header.Height)
                {
                    int bytesRead = compressedStream.Read(this.scanline.Array, this.currentRowBytesRead, bytesPerInterlaceScanline - this.currentRowBytesRead);
                    this.currentRowBytesRead += bytesRead;
                    if (this.currentRowBytesRead < bytesPerInterlaceScanline)
                    {
                        return;
                    }

                    this.currentRowBytesRead = 0;

                    Span<byte> scanSpan = this.scanline.Slice(0, bytesPerInterlaceScanline);
                    Span<byte> prevSpan = this.previousScanline.Slice(0, bytesPerInterlaceScanline);

                    switch ((FilterType)scanSpan[0])
                    {
                        case FilterType.None:
                            break;

                        case FilterType.Sub:
                            SubFilter.Decode(scanSpan, this.bytesPerPixel);
                            break;

                        case FilterType.Up:
                            UpFilter.Decode(scanSpan, prevSpan);
                            break;

                        case FilterType.Average:
                            AverageFilter.Decode(scanSpan, prevSpan, this.bytesPerPixel);
                            break;

                        case FilterType.Paeth:
                            PaethFilter.Decode(scanSpan, prevSpan, this.bytesPerPixel);
                            break;

                        default:
                            PngThrowHelper.ThrowUnknownFilter();
                            break;
                    }

                    Span<TPixel> rowSpan = image.GetPixelRowSpan(this.currentRow);
                    this.ProcessInterlacedDefilteredScanline(this.scanline.GetSpan(), rowSpan, pngMetadata, Adam7.FirstColumn[pass], Adam7.ColumnIncrement[pass]);

                    this.SwapBuffers();

                    this.currentRow += Adam7.RowIncrement[pass];
                }

                pass++;
                this.previousScanline.Clear();

                if (pass < 7)
                {
                    this.currentRow = Adam7.FirstRow[pass];
                }
                else
                {
                    pass = 0;
                    break;
                }
            }
        }

        /// <summary>
        /// Processes the de-filtered scanline filling the image pixel data
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="defilteredScanline">The de-filtered scanline</param>
        /// <param name="pixels">The image</param>
        /// <param name="pngMetadata">The png metadata.</param>
        private void ProcessDefilteredScanline<TPixel>(ReadOnlySpan<byte> defilteredScanline, ImageFrame<TPixel> pixels, PngMetadata pngMetadata)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Span<TPixel> rowSpan = pixels.GetPixelRowSpan(this.currentRow);

            // Trim the first marker byte from the buffer
            ReadOnlySpan<byte> trimmed = defilteredScanline.Slice(1, defilteredScanline.Length - 1);

            // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
            ReadOnlySpan<byte> scanlineSpan = this.TryScaleUpTo8BitArray(trimmed, this.bytesPerScanline - 1, this.header.BitDepth, out IManagedByteBuffer buffer)
            ? buffer.GetSpan()
            : trimmed;

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                    PngScanlineProcessor.ProcessGrayscaleScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentL16.GetValueOrDefault(),
                        pngMetadata.TransparentL8.GetValueOrDefault());

                    break;

                case PngColorType.GrayscaleWithAlpha:
                    PngScanlineProcessor.ProcessGrayscaleWithAlphaScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;

                case PngColorType.Palette:
                    PngScanlineProcessor.ProcessPaletteScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.palette,
                        this.paletteAlpha);

                    break;

                case PngColorType.Rgb:
                    PngScanlineProcessor.ProcessRgbScanline(
                        this.Configuration,
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentRgb48.GetValueOrDefault(),
                        pngMetadata.TransparentRgb24.GetValueOrDefault());

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessRgbaScanline(
                        this.Configuration,
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;
            }

            buffer?.Dispose();
        }

        /// <summary>
        /// Processes the interlaced de-filtered scanline filling the image pixel data
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="defilteredScanline">The de-filtered scanline</param>
        /// <param name="rowSpan">The current image row.</param>
        /// <param name="pngMetadata">The png metadata.</param>
        /// <param name="pixelOffset">The column start index. Always 0 for none interlaced images.</param>
        /// <param name="increment">The column increment. Always 1 for none interlaced images.</param>
        private void ProcessInterlacedDefilteredScanline<TPixel>(ReadOnlySpan<byte> defilteredScanline, Span<TPixel> rowSpan, PngMetadata pngMetadata, int pixelOffset = 0, int increment = 1)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Trim the first marker byte from the buffer
            ReadOnlySpan<byte> trimmed = defilteredScanline.Slice(1, defilteredScanline.Length - 1);

            // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
            ReadOnlySpan<byte> scanlineSpan = this.TryScaleUpTo8BitArray(trimmed, this.bytesPerScanline, this.header.BitDepth, out IManagedByteBuffer buffer)
            ? buffer.GetSpan()
            : trimmed;

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                    PngScanlineProcessor.ProcessInterlacedGrayscaleScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pixelOffset,
                        increment,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentL16.GetValueOrDefault(),
                        pngMetadata.TransparentL8.GetValueOrDefault());

                    break;

                case PngColorType.GrayscaleWithAlpha:
                    PngScanlineProcessor.ProcessInterlacedGrayscaleWithAlphaScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pixelOffset,
                        increment,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;

                case PngColorType.Palette:
                    PngScanlineProcessor.ProcessInterlacedPaletteScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pixelOffset,
                        increment,
                        this.palette,
                        this.paletteAlpha);

                    break;

                case PngColorType.Rgb:
                    PngScanlineProcessor.ProcessInterlacedRgbScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pixelOffset,
                        increment,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        pngMetadata.HasTransparency,
                        pngMetadata.TransparentRgb48.GetValueOrDefault(),
                        pngMetadata.TransparentRgb24.GetValueOrDefault());

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessInterlacedRgbaScanline(
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        pixelOffset,
                        increment,
                        this.bytesPerPixel,
                        this.bytesPerSample);

                    break;
            }

            buffer?.Dispose();
        }

        /// <summary>
        /// Decodes and assigns marker colors that identify transparent pixels in non indexed images.
        /// </summary>
        /// <param name="alpha">The alpha tRNS array.</param>
        /// <param name="pngMetadata">The png metadata.</param>
        private void AssignTransparentMarkers(ReadOnlySpan<byte> alpha, PngMetadata pngMetadata)
        {
            if (this.pngColorType == PngColorType.Rgb)
            {
                if (alpha.Length >= 6)
                {
                    if (this.header.BitDepth == 16)
                    {
                        ushort rc = BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(0, 2));
                        ushort gc = BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(2, 2));
                        ushort bc = BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(4, 2));

                        pngMetadata.TransparentRgb48 = new Rgb48(rc, gc, bc);
                        pngMetadata.HasTransparency = true;
                        return;
                    }

                    byte r = ReadByteLittleEndian(alpha, 0);
                    byte g = ReadByteLittleEndian(alpha, 2);
                    byte b = ReadByteLittleEndian(alpha, 4);
                    pngMetadata.TransparentRgb24 = new Rgb24(r, g, b);
                    pngMetadata.HasTransparency = true;
                }
            }
            else if (this.pngColorType == PngColorType.Grayscale)
            {
                if (alpha.Length >= 2)
                {
                    if (this.header.BitDepth == 16)
                    {
                        pngMetadata.TransparentL16 = new L16(BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(0, 2)));
                    }
                    else
                    {
                        pngMetadata.TransparentL8 = new L8(ReadByteLittleEndian(alpha, 0));
                    }

                    pngMetadata.HasTransparency = true;
                }
            }
        }

        /// <summary>
        /// Reads a header chunk from the data.
        /// </summary>
        /// <param name="pngMetadata">The png metadata.</param>
        /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
        private void ReadHeaderChunk(PngMetadata pngMetadata, ReadOnlySpan<byte> data)
        {
            this.header = PngHeader.Parse(data);

            this.header.Validate();

            pngMetadata.BitDepth = (PngBitDepth)this.header.BitDepth;
            pngMetadata.ColorType = this.header.ColorType;
            pngMetadata.InterlaceMethod = this.header.InterlaceMethod;

            this.pngColorType = this.header.ColorType;
        }

        /// <summary>
        /// Reads a text chunk containing image properties from the data.
        /// </summary>
        /// <param name="metadata">The metadata to decode to.</param>
        /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
        private void ReadTextChunk(PngMetadata metadata, ReadOnlySpan<byte> data)
        {
            if (this.ignoreMetadata)
            {
                return;
            }

            int zeroIndex = data.IndexOf((byte)0);

            // Keywords are restricted to 1 to 79 bytes in length.
            if (zeroIndex < PngConstants.MinTextKeywordLength || zeroIndex > PngConstants.MaxTextKeywordLength)
            {
                return;
            }

            ReadOnlySpan<byte> keywordBytes = data.Slice(0, zeroIndex);
            if (!this.TryReadTextKeyword(keywordBytes, out string name))
            {
                return;
            }

            string value = PngConstants.Encoding.GetString(data.Slice(zeroIndex + 1));

            metadata.TextData.Add(new PngTextData(name, value, string.Empty, string.Empty));
        }

        /// <summary>
        /// Reads the compressed text chunk. Contains a uncompressed keyword and a compressed text string.
        /// </summary>
        /// <param name="metadata">The metadata to decode to.</param>
        /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
        private void ReadCompressedTextChunk(PngMetadata metadata, ReadOnlySpan<byte> data)
        {
            if (this.ignoreMetadata)
            {
                return;
            }

            int zeroIndex = data.IndexOf((byte)0);
            if (zeroIndex < PngConstants.MinTextKeywordLength || zeroIndex > PngConstants.MaxTextKeywordLength)
            {
                return;
            }

            byte compressionMethod = data[zeroIndex + 1];
            if (compressionMethod != 0)
            {
                // Only compression method 0 is supported (zlib datastream with deflate compression).
                return;
            }

            ReadOnlySpan<byte> keywordBytes = data.Slice(0, zeroIndex);
            if (!this.TryReadTextKeyword(keywordBytes, out string name))
            {
                return;
            }

            ReadOnlySpan<byte> compressedData = data.Slice(zeroIndex + 2);

            if (this.TryUncompressTextData(compressedData, PngConstants.Encoding, out string uncompressed))
            {
                metadata.TextData.Add(new PngTextData(name, uncompressed, string.Empty, string.Empty));
            }
        }

        /// <summary>
        /// Reads a iTXt chunk, which contains international text data. It contains:
        /// - A uncompressed keyword.
        /// - Compression flag, indicating if a compression is used.
        /// - Compression method.
        /// - Language tag (optional).
        /// - A translated keyword (optional).
        /// - Text data, which is either compressed or uncompressed.
        /// </summary>
        /// <param name="metadata">The metadata to decode to.</param>
        /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
        private void ReadInternationalTextChunk(PngMetadata metadata, ReadOnlySpan<byte> data)
        {
            if (this.ignoreMetadata)
            {
                return;
            }

            int zeroIndexKeyword = data.IndexOf((byte)0);
            if (zeroIndexKeyword < PngConstants.MinTextKeywordLength || zeroIndexKeyword > PngConstants.MaxTextKeywordLength)
            {
                return;
            }

            byte compressionFlag = data[zeroIndexKeyword + 1];
            if (!(compressionFlag == 0 || compressionFlag == 1))
            {
                return;
            }

            byte compressionMethod = data[zeroIndexKeyword + 2];
            if (compressionMethod != 0)
            {
                // Only compression method 0 is supported (zlib datastream with deflate compression).
                return;
            }

            int langStartIdx = zeroIndexKeyword + 3;
            int languageLength = data.Slice(langStartIdx).IndexOf((byte)0);
            if (languageLength < 0)
            {
                return;
            }

            string language = PngConstants.LanguageEncoding.GetString(data.Slice(langStartIdx, languageLength));

            int translatedKeywordStartIdx = langStartIdx + languageLength + 1;
            int translatedKeywordLength = data.Slice(translatedKeywordStartIdx).IndexOf((byte)0);
            string translatedKeyword = PngConstants.TranslatedEncoding.GetString(data.Slice(translatedKeywordStartIdx, translatedKeywordLength));

            ReadOnlySpan<byte> keywordBytes = data.Slice(0, zeroIndexKeyword);
            if (!this.TryReadTextKeyword(keywordBytes, out string keyword))
            {
                return;
            }

            int dataStartIdx = translatedKeywordStartIdx + translatedKeywordLength + 1;
            if (compressionFlag == 1)
            {
                ReadOnlySpan<byte> compressedData = data.Slice(dataStartIdx);

                if (this.TryUncompressTextData(compressedData, PngConstants.TranslatedEncoding, out string uncompressed))
                {
                    metadata.TextData.Add(new PngTextData(keyword, uncompressed, language, translatedKeyword));
                }
            }
            else
            {
                string value = PngConstants.TranslatedEncoding.GetString(data.Slice(dataStartIdx));
                metadata.TextData.Add(new PngTextData(keyword, value, language, translatedKeyword));
            }
        }

        /// <summary>
        /// Decompresses a byte array with zlib compressed text data.
        /// </summary>
        /// <param name="compressedData">Compressed text data bytes.</param>
        /// <param name="encoding">The string encoding to use.</param>
        /// <param name="value">The uncompressed value.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        private bool TryUncompressTextData(ReadOnlySpan<byte> compressedData, Encoding encoding, out string value)
        {
            using (var memoryStream = new MemoryStream(compressedData.ToArray()))
            using (var bufferedStream = new BufferedReadStream(this.Configuration, memoryStream))
            using (var inflateStream = new ZlibInflateStream(bufferedStream))
            {
                if (!inflateStream.AllocateNewBytes(compressedData.Length, false))
                {
                    value = null;
                    return false;
                }

                var uncompressedBytes = new List<byte>();

                // Note: this uses a buffer which is only 4 bytes long to read the stream, maybe allocating a larger buffer makes sense here.
                int bytesRead = inflateStream.CompressedStream.Read(this.buffer, 0, this.buffer.Length);
                while (bytesRead != 0)
                {
                    uncompressedBytes.AddRange(this.buffer.AsSpan().Slice(0, bytesRead).ToArray());
                    bytesRead = inflateStream.CompressedStream.Read(this.buffer, 0, this.buffer.Length);
                }

                value = encoding.GetString(uncompressedBytes.ToArray());
                return true;
            }
        }

        /// <summary>
        /// Reads the next data chunk.
        /// </summary>
        /// <returns>Count of bytes in the next data chunk, or 0 if there are no more data chunks left.</returns>
        private int ReadNextDataChunk()
        {
            if (this.nextChunk != null)
            {
                return 0;
            }

            this.currentStream.Read(this.buffer, 0, 4);

            if (this.TryReadChunk(out PngChunk chunk))
            {
                if (chunk.Type == PngChunkType.Data)
                {
                    return chunk.Length;
                }

                this.nextChunk = chunk;
            }

            return 0;
        }

        /// <summary>
        /// Reads a chunk from the stream.
        /// </summary>
        /// <param name="chunk">The image format chunk.</param>
        /// <returns>
        /// The <see cref="PngChunk"/>.
        /// </returns>
        private bool TryReadChunk(out PngChunk chunk)
        {
            if (this.nextChunk != null)
            {
                chunk = this.nextChunk.Value;

                this.nextChunk = null;

                return true;
            }

            if (!this.TryReadChunkLength(out int length))
            {
                chunk = default;

                // IEND
                return false;
            }

            while (length < 0 || length > (this.currentStream.Length - this.currentStream.Position))
            {
                // Not a valid chunk so try again until we reach a known chunk.
                if (!this.TryReadChunkLength(out length))
                {
                    chunk = default;

                    return false;
                }
            }

            PngChunkType type = this.ReadChunkType();

            // NOTE: Reading the chunk data is the responsible of the caller
            if (type == PngChunkType.Data)
            {
                chunk = new PngChunk(length, type);

                return true;
            }

            chunk = new PngChunk(
                length: length,
                type: type,
                data: this.ReadChunkData(length));

            this.ValidateChunk(chunk);

            return true;
        }

        /// <summary>
        /// Validates the png chunk.
        /// </summary>
        /// <param name="chunk">The <see cref="PngChunk"/>.</param>
        private void ValidateChunk(in PngChunk chunk)
        {
            uint inputCrc = this.ReadChunkCrc();

            if (chunk.IsCritical)
            {
                Span<byte> chunkType = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(chunkType, (uint)chunk.Type);

                uint validCrc = Crc32.Calculate(chunkType);
                validCrc = Crc32.Calculate(validCrc, chunk.Data.GetSpan());

                if (validCrc != inputCrc)
                {
                    string chunkTypeName = Encoding.ASCII.GetString(chunkType);
                    PngThrowHelper.ThrowInvalidChunkCrc(chunkTypeName);
                }
            }
        }

        /// <summary>
        /// Reads the cycle redundancy chunk from the data.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        private uint ReadChunkCrc()
        {
            uint crc = 0;
            if (this.currentStream.Read(this.buffer, 0, 4) == 4)
            {
                crc = BinaryPrimitives.ReadUInt32BigEndian(this.buffer);
            }

            return crc;
        }

        /// <summary>
        /// Skips the chunk data and the cycle redundancy chunk read from the data.
        /// </summary>
        /// <param name="chunk">The image format chunk.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void SkipChunkDataAndCrc(in PngChunk chunk)
        {
            this.currentStream.Skip(chunk.Length);
            this.currentStream.Skip(4);
        }

        /// <summary>
        /// Reads the chunk data from the stream.
        /// </summary>
        /// <param name="length">The length of the chunk data to read.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private IManagedByteBuffer ReadChunkData(int length)
        {
            // We rent the buffer here to return it afterwards in Decode()
            IManagedByteBuffer buffer = this.Configuration.MemoryAllocator.AllocateManagedByteBuffer(length, AllocationOptions.Clean);

            this.currentStream.Read(buffer.Array, 0, length);

            return buffer;
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        [MethodImpl(InliningOptions.ShortMethod)]
        private PngChunkType ReadChunkType()
        {
            if (this.currentStream.Read(this.buffer, 0, 4) == 4)
            {
                return (PngChunkType)BinaryPrimitives.ReadUInt32BigEndian(this.buffer);
            }
            else
            {
                PngThrowHelper.ThrowInvalidChunkType();

                // The IDE cannot detect the throw here.
                return default;
            }
        }

        /// <summary>
        /// Attempts to read the length of the next chunk.
        /// </summary>
        /// <returns>
        /// Whether the length was read.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private bool TryReadChunkLength(out int result)
        {
            if (this.currentStream.Read(this.buffer, 0, 4) == 4)
            {
                result = BinaryPrimitives.ReadInt32BigEndian(this.buffer);

                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Tries to reads a text chunk keyword, which have some restrictions to be valid:
        /// Keywords shall contain only printable Latin-1 characters and should not have leading or trailing whitespace.
        /// See: https://www.w3.org/TR/PNG/#11zTXt
        /// </summary>
        /// <param name="keywordBytes">The keyword bytes.</param>
        /// <param name="name">The name.</param>
        /// <returns>True, if the keyword could be read and is valid.</returns>
        private bool TryReadTextKeyword(ReadOnlySpan<byte> keywordBytes, out string name)
        {
            name = string.Empty;

            // Keywords shall contain only printable Latin-1.
            foreach (byte c in keywordBytes)
            {
                if (!((c >= 32 && c <= 126) || (c >= 161 && c <= 255)))
                {
                    return false;
                }
            }

            // Keywords should not be empty or have leading or trailing whitespace.
            name = PngConstants.Encoding.GetString(keywordBytes);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith(" ") || name.EndsWith(" "))
            {
                return false;
            }

            return true;
        }

        private void SwapBuffers()
        {
            IManagedByteBuffer temp = this.previousScanline;
            this.previousScanline = this.scanline;
            this.scanline = temp;
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Performs the png decoding operation.
    /// </summary>
    internal sealed class PngDecoderCore
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// Reusable crc for validating chunks.
        /// </summary>
        private readonly Crc32 crc = new Crc32();

        /// <summary>
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Gets the encoding to use
        /// </summary>
        private readonly Encoding textEncoding;

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
        private Stream currentStream;

        /// <summary>
        /// The png header.
        /// </summary>
        private PngHeader header;

        /// <summary>
        /// The number of bytes per pixel.
        /// </summary>
        private int bytesPerPixel;

        /// <summary>
        /// The number of bytes per sample
        /// </summary>
        private int bytesPerSample;

        /// <summary>
        /// The number of bytes per scanline
        /// </summary>
        private int bytesPerScanline;

        /// <summary>
        /// The palette containing color information for indexed png's
        /// </summary>
        private byte[] palette;

        /// <summary>
        /// The palette containing alpha channel color information for indexed png's
        /// </summary>
        private byte[] paletteAlpha;

        /// <summary>
        /// A value indicating whether the end chunk has been reached.
        /// </summary>
        private bool isEndChunkReached;

        /// <summary>
        /// Previous scanline processed
        /// </summary>
        private IManagedByteBuffer previousScanline;

        /// <summary>
        /// The current scanline that is being processed
        /// </summary>
        private IManagedByteBuffer scanline;

        /// <summary>
        /// The index of the current scanline being processed
        /// </summary>
        private int currentRow = Adam7.FirstRow[0];

        /// <summary>
        /// The current pass for an interlaced PNG
        /// </summary>
        private int pass;

        /// <summary>
        /// The current number of bytes read in the current scanline
        /// </summary>
        private int currentRowBytesRead;

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        private PngColorType pngColorType;

        /// <summary>
        /// Represents any color in an 8 bit Rgb24 encoded png that should be transparent
        /// </summary>
        private Rgb24 rgb24Trans;

        /// <summary>
        /// Represents any color in a 16 bit Rgb24 encoded png that should be transparent
        /// </summary>
        private Rgb48 rgb48Trans;

        /// <summary>
        /// Represents any color in an 8 bit grayscale encoded png that should be transparent
        /// </summary>
        private byte luminanceTrans;

        /// <summary>
        /// Represents any color in a 16 bit grayscale encoded png that should be transparent
        /// </summary>
        private ushort luminance16Trans;

        /// <summary>
        /// Whether the image has transparency chunk and markers were decoded
        /// </summary>
        private bool hasTrans;

        /// <summary>
        /// The next chunk of data to return
        /// </summary>
        private PngChunk? nextChunk;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public PngDecoderCore(Configuration configuration, IPngDecoderOptions options)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.memoryAllocator = this.configuration.MemoryAllocator;
            this.textEncoding = options.TextEncoding ?? PngConstants.DefaultEncoding;
            this.ignoreMetadata = options.IgnoreMetadata;
        }

        /// <summary>
        /// Decodes the stream to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream containing image data. </param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the stream does not contain and end chunk.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the image is larger than the maximum allowable size.
        /// </exception>
        /// <returns>The decoded image</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var metaData = new ImageMetaData();
            PngMetaData pngMetaData = metaData.GetFormatMetaData(PngFormat.Instance);
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
                                this.ReadHeaderChunk(pngMetaData, chunk.Data.Array);
                                break;
                            case PngChunkType.Physical:
                                this.ReadPhysicalChunk(metaData, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Gamma:
                                this.ReadGammaChunk(pngMetaData, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Data:
                                if (image is null)
                                {
                                    this.InitializeImage(metaData, out image);
                                }

                                using (var deframeStream = new ZlibInflateStream(this.currentStream, this.ReadNextDataChunk))
                                {
                                    deframeStream.AllocateNewBytes(chunk.Length);
                                    this.ReadScanlines(deframeStream.CompressedStream, image.Frames.RootFrame);
                                }

                                break;
                            case PngChunkType.Palette:
                                byte[] pal = new byte[chunk.Length];
                                Buffer.BlockCopy(chunk.Data.Array, 0, pal, 0, chunk.Length);
                                this.palette = pal;
                                break;
                            case PngChunkType.PaletteAlpha:
                                byte[] alpha = new byte[chunk.Length];
                                Buffer.BlockCopy(chunk.Data.Array, 0, alpha, 0, chunk.Length);
                                this.paletteAlpha = alpha;
                                this.AssignTransparentMarkers(alpha);
                                break;
                            case PngChunkType.Text:
                                this.ReadTextChunk(metaData, chunk.Data.Array.AsSpan(0, chunk.Length));
                                break;
                            case PngChunkType.Exif:
                                if (!this.ignoreMetadata)
                                {
                                    byte[] exifData = new byte[chunk.Length];
                                    Buffer.BlockCopy(chunk.Data.Array, 0, exifData, 0, chunk.Length);
                                    metaData.ExifProfile = new ExifProfile(exifData);
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

                if (image is null)
                {
                    throw new ImageFormatException("PNG Image does not contain a data chunk");
                }

                return image;
            }
            finally
            {
                this.scanline?.Dispose();
                this.previousScanline?.Dispose();
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            var metaData = new ImageMetaData();
            PngMetaData pngMetaData = metaData.GetFormatMetaData(PngFormat.Instance);
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
                                this.ReadHeaderChunk(pngMetaData, chunk.Data.Array);
                                break;
                            case PngChunkType.Physical:
                                this.ReadPhysicalChunk(metaData, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Gamma:
                                this.ReadGammaChunk(pngMetaData, chunk.Data.GetSpan());
                                break;
                            case PngChunkType.Data:
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            case PngChunkType.Text:
                                this.ReadTextChunk(metaData, chunk.Data.Array.AsSpan(0, chunk.Length));
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
                throw new ImageFormatException("PNG Image does not contain a header chunk");
            }

            return new ImageInfo(new PixelTypeInfo(this.CalculateBitsPerPixel()), this.header.Width, this.header.Height, metaData);
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
        private void ReadPhysicalChunk(ImageMetaData metadata, ReadOnlySpan<byte> data)
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
        private void ReadGammaChunk(PngMetaData pngMetadata, ReadOnlySpan<byte> data)
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
        private void InitializeImage<TPixel>(ImageMetaData metadata, out Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            image = new Image<TPixel>(this.configuration, this.header.Width, this.header.Height, metadata);
            this.bytesPerPixel = this.CalculateBytesPerPixel();
            this.bytesPerScanline = this.CalculateScanlineLength(this.header.Width) + 1;
            this.bytesPerSample = 1;
            if (this.header.BitDepth >= 8)
            {
                this.bytesPerSample = this.header.BitDepth / 8;
            }

            this.previousScanline = this.memoryAllocator.AllocateManagedByteBuffer(this.bytesPerScanline, AllocationOptions.Clean);
            this.scanline = this.configuration.MemoryAllocator.AllocateManagedByteBuffer(this.bytesPerScanline, AllocationOptions.Clean);
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
                    throw new NotSupportedException("Unsupported PNG color type");
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
        /// <param name="dataStream">The <see cref="MemoryStream"/> containing data.</param>
        /// <param name="image"> The pixel data.</param>
        private void ReadScanlines<TPixel>(Stream dataStream, ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.header.InterlaceMethod == PngInterlaceMode.Adam7)
            {
                this.DecodeInterlacedPixelData(dataStream, image);
            }
            else
            {
                this.DecodePixelData(dataStream, image);
            }
        }

        /// <summary>
        /// Decodes the raw pixel data row by row
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="compressedStream">The compressed pixel data stream.</param>
        /// <param name="image">The image to decode to.</param>
        private void DecodePixelData<TPixel>(Stream compressedStream, ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
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
                        throw new ImageFormatException("Unknown filter type.");
                }

                this.ProcessDefilteredScanline(scanlineSpan, image);

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
        private void DecodeInterlacedPixelData<TPixel>(Stream compressedStream, ImageFrame<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            while (true)
            {
                int numColumns = Adam7.ComputeColumns(this.header.Width, this.pass);

                if (numColumns == 0)
                {
                    this.pass++;

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
                            throw new ImageFormatException("Unknown filter type.");
                    }

                    Span<TPixel> rowSpan = image.GetPixelRowSpan(this.currentRow);
                    this.ProcessInterlacedDefilteredScanline(this.scanline.GetSpan(), rowSpan, Adam7.FirstColumn[this.pass], Adam7.ColumnIncrement[this.pass]);

                    this.SwapBuffers();

                    this.currentRow += Adam7.RowIncrement[this.pass];
                }

                this.pass++;
                this.previousScanline.Clear();

                if (this.pass < 7)
                {
                    this.currentRow = Adam7.FirstRow[this.pass];
                }
                else
                {
                    this.pass = 0;
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
        private void ProcessDefilteredScanline<TPixel>(ReadOnlySpan<byte> defilteredScanline, ImageFrame<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
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
                        this.hasTrans,
                        this.luminance16Trans,
                        this.luminanceTrans);

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
                        this.header,
                        scanlineSpan,
                        rowSpan,
                        this.bytesPerPixel,
                        this.bytesPerSample,
                        this.hasTrans,
                        this.rgb48Trans,
                        this.rgb24Trans);

                    break;

                case PngColorType.RgbWithAlpha:
                    PngScanlineProcessor.ProcessRgbaScanline(
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
        /// <param name="pixelOffset">The column start index. Always 0 for none interlaced images.</param>
        /// <param name="increment">The column increment. Always 1 for none interlaced images.</param>
        private void ProcessInterlacedDefilteredScanline<TPixel>(ReadOnlySpan<byte> defilteredScanline, Span<TPixel> rowSpan, int pixelOffset = 0, int increment = 1)
            where TPixel : struct, IPixel<TPixel>
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
                        this.hasTrans,
                        this.luminance16Trans,
                        this.luminanceTrans);

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
                        this.hasTrans,
                        this.rgb48Trans,
                        this.rgb24Trans);

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
        /// Decodes and assigns marker colors that identify transparent pixels in non indexed images
        /// </summary>
        /// <param name="alpha">The alpha tRNS array</param>
        private void AssignTransparentMarkers(ReadOnlySpan<byte> alpha)
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

                        this.rgb48Trans = new Rgb48(rc, gc, bc);
                        this.hasTrans = true;
                        return;
                    }

                    byte r = ReadByteLittleEndian(alpha, 0);
                    byte g = ReadByteLittleEndian(alpha, 2);
                    byte b = ReadByteLittleEndian(alpha, 4);
                    this.rgb24Trans = new Rgb24(r, g, b);
                    this.hasTrans = true;
                }
            }
            else if (this.pngColorType == PngColorType.Grayscale)
            {
                if (alpha.Length >= 2)
                {
                    if (this.header.BitDepth == 16)
                    {
                        this.luminance16Trans = BinaryPrimitives.ReadUInt16LittleEndian(alpha.Slice(0, 2));
                    }
                    else
                    {
                        this.luminanceTrans = ReadByteLittleEndian(alpha, 0);
                    }

                    this.hasTrans = true;
                }
            }
        }

        /// <summary>
        /// Reads a header chunk from the data.
        /// </summary>
        /// <param name="pngMetaData">The png metadata.</param>
        /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
        private void ReadHeaderChunk(PngMetaData pngMetaData, ReadOnlySpan<byte> data)
        {
            this.header = PngHeader.Parse(data);

            this.header.Validate();

            pngMetaData.BitDepth = (PngBitDepth)this.header.BitDepth;
            pngMetaData.ColorType = this.header.ColorType;

            this.pngColorType = this.header.ColorType;
        }

        /// <summary>
        /// Reads a text chunk containing image properties from the data.
        /// </summary>
        /// <param name="metadata">The metadata to decode to.</param>
        /// <param name="data">The <see cref="T:Span"/> containing the data.</param>
        private void ReadTextChunk(ImageMetaData metadata, ReadOnlySpan<byte> data)
        {
            if (this.ignoreMetadata)
            {
                return;
            }

            int zeroIndex = data.IndexOf((byte)0);

            string name = this.textEncoding.GetString(data.Slice(0, zeroIndex));
            string value = this.textEncoding.GetString(data.Slice(zeroIndex + 1));

            metadata.Properties.Add(new ImageProperty(name, value));
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
                // Not a valid chunk so we skip back all but one of the four bytes we have just read.
                // That lets us read one byte at a time until we reach a known chunk.
                this.currentStream.Position -= 3;

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
                data: this.ReadChunkData(length),
                crc: this.ReadChunkCrc());

            if (chunk.IsCritical)
            {
                this.ValidateChunk(chunk);
            }

            return true;
        }

        /// <summary>
        /// Validates the png chunk.
        /// </summary>
        /// <param name="chunk">The <see cref="PngChunk"/>.</param>
        private void ValidateChunk(in PngChunk chunk)
        {
            Span<byte> chunkType = stackalloc byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(chunkType, (uint)chunk.Type);

            this.crc.Reset();
            this.crc.Update(chunkType);
            this.crc.Update(chunk.Data.GetSpan());

            if (this.crc.Value != chunk.Crc)
            {
                string chunkTypeName = Encoding.UTF8.GetString(chunkType);

                throw new ImageFormatException($"CRC Error. PNG {chunkTypeName} chunk is corrupt!");
            }
        }

        /// <summary>
        /// Reads the cycle redundancy chunk from the data.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid or corrupt.
        /// </exception>
        private uint ReadChunkCrc()
        {
            return this.currentStream.Read(this.buffer, 0, 4) == 4
                ? BinaryPrimitives.ReadUInt32BigEndian(this.buffer)
                : throw new ImageFormatException("Image stream is not valid!");
        }

        /// <summary>
        /// Skips the chunk data and the cycle redundancy chunk read from the data.
        /// </summary>
        /// <param name="chunk">The image format chunk.</param>
        private void SkipChunkDataAndCrc(in PngChunk chunk)
        {
            this.currentStream.Skip(chunk.Length);
            this.currentStream.Skip(4);
        }

        /// <summary>
        /// Reads the chunk data from the stream.
        /// </summary>
        /// <param name="length">The length of the chunk data to read.</param>
        private IManagedByteBuffer ReadChunkData(int length)
        {
            // We rent the buffer here to return it afterwards in Decode()
            IManagedByteBuffer buffer = this.configuration.MemoryAllocator.AllocateManagedByteBuffer(length, AllocationOptions.Clean);

            this.currentStream.Read(buffer.Array, 0, length);

            return buffer;
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private PngChunkType ReadChunkType()
        {
            return this.currentStream.Read(this.buffer, 0, 4) == 4
                ? (PngChunkType)BinaryPrimitives.ReadUInt32BigEndian(this.buffer)
                : throw new ImageFormatException("Invalid PNG data.");
        }

        /// <summary>
        /// Attempts to read the length of the next chunk.
        /// </summary>
        /// <returns>
        /// Whether the the length was read.
        /// </returns>
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

        private void SwapBuffers()
        {
            IManagedByteBuffer temp = this.previousScanline;
            this.previousScanline = this.scanline;
            this.scanline = temp;
        }
    }
}
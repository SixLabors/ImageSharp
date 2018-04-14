// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Performs the png decoding operation.
    /// </summary>
    internal sealed class PngDecoderCore
    {
        /// <summary>
        /// The dictionary of available color types.
        /// </summary>
        private static readonly Dictionary<PngColorType, byte[]> ColorTypes = new Dictionary<PngColorType, byte[]>()
        {
            [PngColorType.Grayscale] = new byte[] { 1, 2, 4, 8 },
            [PngColorType.Rgb] = new byte[] { 8, 16 },
            [PngColorType.Palette] = new byte[] { 1, 2, 4, 8 },
            [PngColorType.GrayscaleWithAlpha] = new byte[] { 8 },
            [PngColorType.RgbWithAlpha] = new byte[] { 8, 16 }
        };

        /// <summary>
        /// The amount to increment when processing each column per scanline for each interlaced pass
        /// </summary>
        private static readonly int[] Adam7ColumnIncrement = { 8, 8, 4, 4, 2, 2, 1 };

        /// <summary>
        /// The index to start at when processing each column per scanline for each interlaced pass
        /// </summary>
        private static readonly int[] Adam7FirstColumn = { 0, 4, 0, 2, 0, 1, 0 };

        /// <summary>
        /// The index to start at when processing each row per scanline for each interlaced pass
        /// </summary>
        private static readonly int[] Adam7FirstRow = { 0, 0, 4, 0, 2, 0, 1 };

        /// <summary>
        /// The amount to increment when processing each row per scanline for each interlaced pass
        /// </summary>
        private static readonly int[] Adam7RowIncrement = { 8, 8, 8, 4, 4, 2, 2 };

        /// <summary>
        /// Reusable buffer for reading chunk types.
        /// </summary>
        private readonly byte[] chunkTypeBuffer = new byte[4];

        /// <summary>
        /// Reusable buffer for reading chunk lengths.
        /// </summary>
        private readonly byte[] chunkLengthBuffer = new byte[4];

        /// <summary>
        /// Reusable buffer for reading crc values.
        /// </summary>
        private readonly byte[] crcBuffer = new byte[4];

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
        private int currentRow = Adam7FirstRow[0];

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
        /// Represents any color in an Rgb24 encoded png that should be transparent
        /// </summary>
        private Rgb24 rgb24Trans;

        /// <summary>
        /// Represents any color in a grayscale encoded png that should be transparent
        /// </summary>
        private byte intensityTrans;

        /// <summary>
        /// Whether the image has transparency chunk and markers were decoded
        /// </summary>
        private bool hasTrans;

        /// <summary>
        /// Initializes a new instance of the <see cref="PngDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public PngDecoderCore(Configuration configuration, IPngDecoderOptions options)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.textEncoding = options.TextEncoding ?? PngConstants.DefaultEncoding;
            this.ignoreMetadata = options.IgnoreMetadata;
        }

        private MemoryManager MemoryManager => this.configuration.MemoryManager;

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
            var metadata = new ImageMetaData();
            this.currentStream = stream;
            this.currentStream.Skip(8);
            Image<TPixel> image = null;
            try
            {
                using (var deframeStream = new ZlibInflateStream(this.currentStream))
                {
                    while (!this.isEndChunkReached && this.TryReadChunk(out PngChunk chunk))
                    {
                        try
                        {
                            switch (chunk.Type)
                            {
                                case PngChunkType.Header:
                                    this.ReadHeaderChunk(chunk.Data.Array);
                                    this.ValidateHeader();
                                    break;
                                case PngChunkType.Physical:
                                    this.ReadPhysicalChunk(metadata, chunk.Data.Array);
                                    break;
                                case PngChunkType.Data:
                                    if (image == null)
                                    {
                                        this.InitializeImage(metadata, out image);
                                    }

                                    deframeStream.AllocateNewBytes(chunk.Length);
                                    this.ReadScanlines(deframeStream.CompressedStream, image.Frames.RootFrame);
                                    this.currentStream.Read(this.crcBuffer, 0, 4);
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
                                    this.ReadTextChunk(metadata, chunk.Data.Array, chunk.Length);
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

                if (image == null)
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
            var metadata = new ImageMetaData();
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
                                this.ReadHeaderChunk(chunk.Data.Array);
                                this.ValidateHeader();
                                break;
                            case PngChunkType.Physical:
                                this.ReadPhysicalChunk(metadata, chunk.Data.Array);
                                break;
                            case PngChunkType.Data:
                                this.SkipChunkDataAndCrc(chunk);
                                break;
                            case PngChunkType.Text:
                                this.ReadTextChunk(metadata, chunk.Data.Array, chunk.Length);
                                break;
                            case PngChunkType.End:
                                this.isEndChunkReached = true;
                                break;
                        }
                    }
                    finally
                    {
                        // Data is rented in ReadChunkData()
                        if (chunk.Data != null)
                        {
                            ArrayPool<byte>.Shared.Return(chunk.Data.Array);
                        }
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

            return new ImageInfo(new PixelTypeInfo(this.CalculateBitsPerPixel()), this.header.Width, this.header.Height, metadata);
        }

        /// <summary>
        /// Converts a byte array to a new array where each value in the original array is represented by the specified number of bits.
        /// </summary>
        /// <param name="source">The bytes to convert from. Cannot be empty.</param>
        /// <param name="bytesPerScanline">The number of bytes per scanline</param>
        /// <param name="bits">The number of bits per value.</param>
        /// <returns>The resulting <see cref="ReadOnlySpan{Byte}"/> array.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="bits"/> is less than or equals than zero.</exception>
        private static ReadOnlySpan<byte> ToArrayByBitsLength(ReadOnlySpan<byte> source, int bytesPerScanline, int bits)
        {
            Guard.MustBeGreaterThan(source.Length, 0, nameof(source));
            Guard.MustBeGreaterThan(bits, 0, nameof(bits));

            if (bits >= 8)
            {
                return source;
            }

            byte[] result = new byte[bytesPerScanline * 8 / bits];
            int mask = 0xFF >> (8 - bits);
            int resultOffset = 0;

            for (int i = 0; i < bytesPerScanline - 1; i++)
            {
                byte b = source[i];
                for (int shift = 0; shift < 8; shift += bits)
                {
                    int colorIndex = (b >> (8 - bits - shift)) & mask;

                    result[resultOffset] = (byte)colorIndex;

                    resultOffset++;
                }
            }

            return result;
        }

        /// <summary>
        /// Reads an integer value from 2 consecutive bytes in LSB order
        /// </summary>
        /// <param name="buffer">The source buffer</param>
        /// <param name="offset">THe offset</param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReadIntFrom2Bytes(byte[] buffer, int offset)
        {
            return ((buffer[offset] & 0xFF) << 16) | (buffer[offset + 1] & 0xFF);
        }

        /// <summary>
        /// Reads the data chunk containing physical dimension data.
        /// </summary>
        /// <param name="metadata">The metadata to read to.</param>
        /// <param name="data">The data containing physical data.</param>
        private void ReadPhysicalChunk(ImageMetaData metadata, ReadOnlySpan<byte> data)
        {
            // 39.3700787 = inches in a meter.
            metadata.HorizontalResolution = BinaryPrimitives.ReadInt32BigEndian(data.Slice(0, 4)) / 39.3700787d;
            metadata.VerticalResolution = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)) / 39.3700787d;
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

            this.previousScanline = this.MemoryManager.AllocateCleanManagedByteBuffer(this.bytesPerScanline);
            this.scanline = this.configuration.MemoryManager.AllocateCleanManagedByteBuffer(this.bytesPerScanline);
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
                    return 1;

                case PngColorType.GrayscaleWithAlpha:
                    return 2;

                case PngColorType.Palette:
                    return 1;

                case PngColorType.Rgb:
                    if (this.header.BitDepth == 16)
                    {
                        return 6;
                    }

                    return 3;

                case PngColorType.RgbWithAlpha:
                default:
                    if (this.header.BitDepth == 16)
                    {
                        return 8;
                    }

                    return 4;
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
                Span<byte> scanlineSpan = this.scanline.Span;
                var filterType = (FilterType)scanlineSpan[0];

                switch (filterType)
                {
                    case FilterType.None:
                        break;

                    case FilterType.Sub:

                        SubFilter.Decode(scanlineSpan, this.bytesPerPixel);
                        break;

                    case FilterType.Up:

                        UpFilter.Decode(scanlineSpan, this.previousScanline.Span);
                        break;

                    case FilterType.Average:

                        AverageFilter.Decode(scanlineSpan, this.previousScanline.Span, this.bytesPerPixel);
                        break;

                    case FilterType.Paeth:

                        PaethFilter.Decode(scanlineSpan, this.previousScanline.Span, this.bytesPerPixel);
                        break;

                    default:
                        throw new ImageFormatException("Unknown filter type.");
                }

                this.ProcessDefilteredScanline(this.scanline.Array, image);

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
                int numColumns = this.ComputeColumnsAdam7(this.pass);

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
                    var filterType = (FilterType)scanSpan[0];

                    switch (filterType)
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
                    this.ProcessInterlacedDefilteredScanline(this.scanline.Span, rowSpan, Adam7FirstColumn[this.pass], Adam7ColumnIncrement[this.pass]);

                    this.SwapBuffers();

                    this.currentRow += Adam7RowIncrement[this.pass];
                }

                this.pass++;
                this.previousScanline.Clear();

                if (this.pass < 7)
                {
                    this.currentRow = Adam7FirstRow[this.pass];
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
            var color = default(TPixel);
            Span<TPixel> rowSpan = pixels.GetPixelRowSpan(this.currentRow);

            // Trim the first marker byte from the buffer
            ReadOnlySpan<byte> scanlineBuffer = defilteredScanline.Slice(1, defilteredScanline.Length - 1);

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                    int factor = 255 / ((int)Math.Pow(2, this.header.BitDepth) - 1);
                    ReadOnlySpan<byte> newScanline1 = ToArrayByBitsLength(scanlineBuffer, this.bytesPerScanline, this.header.BitDepth);

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        byte intensity = (byte)(newScanline1[x] * factor);
                        if (this.hasTrans && intensity == this.intensityTrans)
                        {
                            color.PackFromRgba32(new Rgba32(intensity, intensity, intensity, 0));
                        }
                        else
                        {
                            color.PackFromRgba32(new Rgba32(intensity, intensity, intensity));
                        }

                        rowSpan[x] = color;
                    }

                    break;

                case PngColorType.GrayscaleWithAlpha:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = x * this.bytesPerPixel;

                        byte intensity = scanlineBuffer[offset];
                        byte alpha = scanlineBuffer[offset + this.bytesPerSample];

                        color.PackFromRgba32(new Rgba32(intensity, intensity, intensity, alpha));
                        rowSpan[x] = color;
                    }

                    break;

                case PngColorType.Palette:

                    this.ProcessScanlineFromPalette(scanlineBuffer, rowSpan);

                    break;

                case PngColorType.Rgb:

                    if (!this.hasTrans)
                    {
                        if (this.header.BitDepth == 16)
                        {
                            int length = this.header.Width * 3;
                            using (IBuffer<byte> compressed = this.configuration.MemoryManager.Allocate<byte>(length))
                            {
                                // TODO: Should we use pack from vector here instead?
                                this.From16BitTo8Bit(scanlineBuffer, compressed.Span, length);
                                PixelOperations<TPixel>.Instance.PackFromRgb24Bytes(compressed.Span, rowSpan, this.header.Width);
                            }
                        }
                        else
                        {
                            PixelOperations<TPixel>.Instance.PackFromRgb24Bytes(scanlineBuffer, rowSpan, this.header.Width);
                        }
                    }
                    else
                    {
                        if (this.header.BitDepth == 16)
                        {
                            int length = this.header.Width * 3;
                            using (IBuffer<byte> compressed = this.configuration.MemoryManager.Allocate<byte>(length))
                            {
                                // TODO: Should we use pack from vector here instead?
                                this.From16BitTo8Bit(scanlineBuffer, compressed.Span, length);

                                Span<Rgb24> rgb24Span = MemoryMarshal.Cast<byte, Rgb24>(compressed.Span);
                                for (int x = 0; x < this.header.Width; x++)
                                {
                                    ref Rgb24 rgb24 = ref rgb24Span[x];
                                    var rgba32 = default(Rgba32);
                                    rgba32.Rgb = rgb24;
                                    rgba32.A = (byte)(rgb24.Equals(this.rgb24Trans) ? 0 : 255);

                                    color.PackFromRgba32(rgba32);
                                    rowSpan[x] = color;
                                }
                            }
                        }
                        else
                        {
                            ReadOnlySpan<Rgb24> rgb24Span = MemoryMarshal.Cast<byte, Rgb24>(scanlineBuffer);
                            for (int x = 0; x < this.header.Width; x++)
                            {
                                ref readonly Rgb24 rgb24 = ref rgb24Span[x];
                                var rgba32 = default(Rgba32);
                                rgba32.Rgb = rgb24;
                                rgba32.A = (byte)(rgb24.Equals(this.rgb24Trans) ? 0 : 255);

                                color.PackFromRgba32(rgba32);
                                rowSpan[x] = color;
                            }
                        }
                    }

                    break;

                case PngColorType.RgbWithAlpha:

                    if (this.header.BitDepth == 16)
                    {
                        int length = this.header.Width * 4;
                        using (IBuffer<byte> compressed = this.configuration.MemoryManager.Allocate<byte>(length))
                        {
                            // TODO: Should we use pack from vector here instead?
                            this.From16BitTo8Bit(scanlineBuffer, compressed.Span, length);
                            PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(compressed.Span, rowSpan, this.header.Width);
                        }
                    }
                    else
                    {
                        PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(scanlineBuffer, rowSpan, this.header.Width);
                    }

                    break;
            }
        }

        /// <summary>
        /// Compresses the given span from 16bpp to 8bpp
        /// </summary>
        /// <param name="source">The source buffer</param>
        /// <param name="target">The target buffer</param>
        /// <param name="length">The target length</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void From16BitTo8Bit(ReadOnlySpan<byte> source, Span<byte> target, int length)
        {
            for (int i = 0, j = 0; i < length; i++, j += 2)
            {
                target[i] = (byte)((source[j + 1] << 8) + source[j]);
            }
        }

        /// <summary>
        /// Decodes and assigns marker colors that identify transparent pixels in non indexed images
        /// </summary>
        /// <param name="alpha">The aplha tRNS array</param>
        private void AssignTransparentMarkers(byte[] alpha)
        {
            if (this.pngColorType == PngColorType.Rgb)
            {
                if (alpha.Length >= 6)
                {
                    byte r = (byte)ReadIntFrom2Bytes(alpha, 0);
                    byte g = (byte)ReadIntFrom2Bytes(alpha, 2);
                    byte b = (byte)ReadIntFrom2Bytes(alpha, 4);
                    this.rgb24Trans = new Rgb24(r, g, b);
                    this.hasTrans = true;
                }
            }
            else if (this.pngColorType == PngColorType.Grayscale)
            {
                if (alpha.Length >= 2)
                {
                    this.intensityTrans = (byte)ReadIntFrom2Bytes(alpha, 0);
                    this.hasTrans = true;
                }
            }
        }

        /// <summary>
        /// Processes a scanline that uses a palette
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel we are expanding to</typeparam>
        /// <param name="defilteredScanline">The scanline</param>
        /// <param name="row">Thecurrent  output image row</param>
        private void ProcessScanlineFromPalette<TPixel>(ReadOnlySpan<byte> defilteredScanline, Span<TPixel> row)
            where TPixel : struct, IPixel<TPixel>
        {
            ReadOnlySpan<byte> newScanline = ToArrayByBitsLength(defilteredScanline, this.bytesPerScanline, this.header.BitDepth);
            byte[] pal = this.palette;
            var color = default(TPixel);

            var rgba = default(Rgba32);

            if (this.paletteAlpha != null && this.paletteAlpha.Length > 0)
            {
                // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                // channel and we should try to read it.
                for (int x = 0; x < this.header.Width; x++)
                {
                    int index = newScanline[x];
                    int pixelOffset = index * 3;

                    rgba.A = this.paletteAlpha.Length > index ? this.paletteAlpha[index] : (byte)255;
                    rgba.Rgb = pal.GetRgb24(pixelOffset);

                    color.PackFromRgba32(rgba);
                    row[x] = color;
                }
            }
            else
            {
                rgba.A = 255;

                for (int x = 0; x < this.header.Width; x++)
                {
                    int index = newScanline[x];
                    int pixelOffset = index * 3;

                    rgba.Rgb = pal.GetRgb24(pixelOffset);

                    color.PackFromRgba32(rgba);
                    row[x] = color;
                }
            }
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
            var color = default(TPixel);

            // Trim the first marker byte from the buffer
            ReadOnlySpan<byte> scanlineBuffer = defilteredScanline.Slice(1, defilteredScanline.Length - 1);

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:
                    int factor = 255 / ((int)Math.Pow(2, this.header.BitDepth) - 1);
                    ReadOnlySpan<byte> newScanline1 = ToArrayByBitsLength(scanlineBuffer, this.bytesPerScanline, this.header.BitDepth);

                    for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o++)
                    {
                        byte intensity = (byte)(newScanline1[o] * factor);
                        if (this.hasTrans && intensity == this.intensityTrans)
                        {
                            color.PackFromRgba32(new Rgba32(intensity, intensity, intensity, 0));
                        }
                        else
                        {
                            color.PackFromRgba32(new Rgba32(intensity, intensity, intensity));
                        }

                        rowSpan[x] = color;
                    }

                    break;

                case PngColorType.GrayscaleWithAlpha:

                    for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                    {
                        byte intensity = scanlineBuffer[o];
                        byte alpha = scanlineBuffer[o + this.bytesPerSample];
                        color.PackFromRgba32(new Rgba32(intensity, intensity, intensity, alpha));
                        rowSpan[x] = color;
                    }

                    break;

                case PngColorType.Palette:

                    ReadOnlySpan<byte> newScanline = ToArrayByBitsLength(scanlineBuffer, this.bytesPerScanline, this.header.BitDepth);
                    var rgba = default(Rgba32);

                    if (this.paletteAlpha != null && this.paletteAlpha.Length > 0)
                    {
                        // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                        // channel and we should try to read it.
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o++)
                        {
                            int index = newScanline[o];
                            int offset = index * 3;

                            rgba.A = this.paletteAlpha.Length > index ? this.paletteAlpha[index] : (byte)255;
                            rgba.Rgb = this.palette.GetRgb24(offset);

                            color.PackFromRgba32(rgba);
                            rowSpan[x] = color;
                        }
                    }
                    else
                    {
                        rgba.A = 255;

                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o++)
                        {
                            int index = newScanline[o];
                            int offset = index * 3;

                            rgba.Rgb = this.palette.GetRgb24(offset);

                            color.PackFromRgba32(rgba);
                            rowSpan[x] = color;
                        }
                    }

                    break;

                case PngColorType.Rgb:

                    rgba.A = 255;

                    if (this.header.BitDepth == 16)
                    {
                        int length = this.header.Width * 3;
                        using (IBuffer<byte> compressed = this.configuration.MemoryManager.Allocate<byte>(length))
                        {
                            Span<byte> compressedSpan = compressed.Span;

                            // TODO: Should we use pack from vector here instead?
                            this.From16BitTo8Bit(scanlineBuffer, compressedSpan, length);

                            if (this.hasTrans)
                            {
                                for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 3)
                                {
                                    rgba.R = compressedSpan[o];
                                    rgba.G = compressedSpan[o + 1];
                                    rgba.B = compressedSpan[o + 2];
                                    rgba.A = (byte)(this.rgb24Trans.Equals(rgba.Rgb) ? 0 : 255);

                                    color.PackFromRgba32(rgba);
                                    rowSpan[x] = color;
                                }
                            }
                            else
                            {
                                for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 3)
                                {
                                    rgba.R = compressedSpan[o];
                                    rgba.G = compressedSpan[o + 1];
                                    rgba.B = compressedSpan[o + 2];

                                    color.PackFromRgba32(rgba);
                                    rowSpan[x] = color;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (this.hasTrans)
                        {
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                            {
                                rgba.R = scanlineBuffer[o];
                                rgba.G = scanlineBuffer[o + this.bytesPerSample];
                                rgba.B = scanlineBuffer[o + (2 * this.bytesPerSample)];
                                rgba.A = (byte)(this.rgb24Trans.Equals(rgba.Rgb) ? 0 : 255);

                                color.PackFromRgba32(rgba);
                                rowSpan[x] = color;
                            }
                        }
                        else
                        {
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                            {
                                rgba.R = scanlineBuffer[o];
                                rgba.G = scanlineBuffer[o + this.bytesPerSample];
                                rgba.B = scanlineBuffer[o + (2 * this.bytesPerSample)];

                                color.PackFromRgba32(rgba);
                                rowSpan[x] = color;
                            }
                        }
                    }

                    break;

                case PngColorType.RgbWithAlpha:

                    if (this.header.BitDepth == 16)
                    {
                        int length = this.header.Width * 4;
                        using (IBuffer<byte> compressed = this.configuration.MemoryManager.Allocate<byte>(length))
                        {
                            Span<byte> compressedSpan = compressed.Span;

                            // TODO: Should we use pack from vector here instead?
                            this.From16BitTo8Bit(scanlineBuffer, compressedSpan, length);
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 4)
                            {
                                rgba.R = compressedSpan[o];
                                rgba.G = compressedSpan[o + 1];
                                rgba.B = compressedSpan[o + 2];
                                rgba.A = compressedSpan[o + 3];

                                color.PackFromRgba32(rgba);
                                rowSpan[x] = color;
                            }
                        }
                    }
                    else
                    {
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                        {
                            rgba.R = scanlineBuffer[o];
                            rgba.G = scanlineBuffer[o + this.bytesPerSample];
                            rgba.B = scanlineBuffer[o + (2 * this.bytesPerSample)];
                            rgba.A = scanlineBuffer[o + (3 * this.bytesPerSample)];

                            color.PackFromRgba32(rgba);
                            rowSpan[x] = color;
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Reads a text chunk containing image properties from the data.
        /// </summary>
        /// <param name="metadata">The metadata to decode to.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing  data.</param>
        /// <param name="length">The maximum length to read.</param>
        private void ReadTextChunk(ImageMetaData metadata, byte[] data, int length)
        {
            if (this.ignoreMetadata)
            {
                return;
            }

            int zeroIndex = 0;

            for (int i = 0; i < length; i++)
            {
                if (data[i] == 0)
                {
                    zeroIndex = i;
                    break;
                }
            }

            string name = this.textEncoding.GetString(data, 0, zeroIndex);
            string value = this.textEncoding.GetString(data, zeroIndex + 1, length - zeroIndex - 1);

            metadata.Properties.Add(new ImageProperty(name, value));
        }

        /// <summary>
        /// Reads a header chunk from the data.
        /// </summary>
        /// <param name="data">The <see cref="T:ReadOnlySpan{byte}"/> containing data.</param>
        private void ReadHeaderChunk(ReadOnlySpan<byte> data)
        {
            this.header = new PngHeader(
                width: BinaryPrimitives.ReadInt32BigEndian(data.Slice(0, 4)),
                height: BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)),
                bitDepth: data[8],
                colorType: (PngColorType)data[9],
                compressionMethod: data[10],
                filterMethod: data[11],
                interlaceMethod: (PngInterlaceMode)data[12]);
        }

        /// <summary>
        /// Validates the png header.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown if the image does pass validation.
        /// </exception>
        private void ValidateHeader()
        {
            if (!ColorTypes.ContainsKey(this.header.ColorType))
            {
                throw new NotSupportedException("Color type is not supported or not valid.");
            }

            if (!ColorTypes[this.header.ColorType].Contains(this.header.BitDepth))
            {
                throw new NotSupportedException("Bit depth is not supported or not valid.");
            }

            if (this.header.FilterMethod != 0)
            {
                throw new NotSupportedException("The png specification only defines 0 as filter method.");
            }

            if (this.header.InterlaceMethod != PngInterlaceMode.None && this.header.InterlaceMethod != PngInterlaceMode.Adam7)
            {
                throw new NotSupportedException("The png specification only defines 'None' and 'Adam7' as interlaced methods.");
            }

            this.pngColorType = this.header.ColorType;
        }

        /// <summary>
        /// Reads a chunk from the stream.
        /// </summary>
        /// <returns>
        /// The <see cref="PngChunk"/>.
        /// </returns>
        private bool TryReadChunk(out PngChunk chunk)
        {
            int length = this.ReadChunkLength();

            if (length == -1)
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

                length = this.ReadChunkLength();

                if (length == -1)
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

        private void ValidateChunk(in PngChunk chunk)
        {
            this.crc.Reset();
            this.crc.Update(this.chunkTypeBuffer);
            this.crc.Update(chunk.Data.Span);

            if (this.crc.Value != chunk.Crc)
            {
                string chunkTypeName = Encoding.UTF8.GetString(this.chunkTypeBuffer, 0, 4);

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
            int numBytes = this.currentStream.Read(this.crcBuffer, 0, 4);

            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            return BinaryPrimitives.ReadUInt32BigEndian(this.crcBuffer);
        }

        /// <summary>
        /// Skips the chunk data and the cycle redundancy chunk read from the data.
        /// </summary>
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
            IManagedByteBuffer buffer = this.configuration.MemoryManager.AllocateCleanManagedByteBuffer(length);

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
            int numBytes = this.currentStream.Read(this.chunkTypeBuffer, 0, 4);

            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            return (PngChunkType)BinaryPrimitives.ReadUInt32BigEndian(this.chunkTypeBuffer.AsSpan());
        }

        /// <summary>
        /// Calculates the length of the given chunk.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private int ReadChunkLength()
        {
            int numBytes = this.currentStream.Read(this.chunkLengthBuffer, 0, 4);

            if (numBytes < 4)
            {
                return -1;
            }

            return BinaryPrimitives.ReadInt32BigEndian(this.chunkLengthBuffer);
        }

        /// <summary>
        /// Returns the correct number of columns for each interlaced pass.
        /// </summary>
        /// <param name="passIndex">Th current pass index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeColumnsAdam7(int passIndex)
        {
            int width = this.header.Width;
            switch (passIndex)
            {
                case 0: return (width + 7) / 8;
                case 1: return (width + 3) / 8;
                case 2: return (width + 3) / 4;
                case 3: return (width + 1) / 4;
                case 4: return (width + 1) / 2;
                case 5: return width / 2;
                case 6: return width;
                default: throw new ArgumentException($"Not a valid pass index: {passIndex}");
            }
        }

        private void SwapBuffers()
        {
            IManagedByteBuffer temp = this.previousScanline;
            this.previousScanline = this.scanline;
            this.scanline = temp;
        }
    }
}

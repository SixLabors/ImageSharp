// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
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

        private MemoryAllocator MemoryAllocator => this.configuration.MemoryAllocator;

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
                                case PngChunkType.Exif:
                                    if (!this.ignoreMetadata)
                                    {
                                        byte[] exifData = new byte[chunk.Length];
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
        {
            return (byte)(((buffer[offset] & 0xFF) << 16) | (buffer[offset + 1] & 0xFF));
        }

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

            buffer = this.MemoryAllocator.AllocateCleanManagedByteBuffer(bytesPerScanline * 8 / bits);
            byte[] result = buffer.Array;
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

            return true;
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

            this.previousScanline = this.MemoryAllocator.AllocateCleanManagedByteBuffer(this.bytesPerScanline);
            this.scanline = this.configuration.MemoryAllocator.AllocateCleanManagedByteBuffer(this.bytesPerScanline);
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
                    this.ProcessInterlacedDefilteredScanline(this.scanline.GetSpan(), rowSpan, Adam7FirstColumn[this.pass], Adam7ColumnIncrement[this.pass]);

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
            TPixel pixel = default;
            Span<TPixel> rowSpan = pixels.GetPixelRowSpan(this.currentRow);

            // Trim the first marker byte from the buffer
            ReadOnlySpan<byte> trimmed = defilteredScanline.Slice(1, defilteredScanline.Length - 1);

            // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
            ReadOnlySpan<byte> scanlineSpan = this.TryScaleUpTo8BitArray(trimmed, this.bytesPerScanline, this.header.BitDepth, out IManagedByteBuffer buffer)
            ? buffer.GetSpan()
            : trimmed;

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:

                    int factor = 255 / ((int)Math.Pow(2, this.header.BitDepth) - 1);

                    if (!this.hasTrans)
                    {
                        if (this.header.BitDepth == 16)
                        {
                            Rgb48 rgb48 = default;
                            for (int x = 0, o = 0; x < this.header.Width; x++, o += 2)
                            {
                                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgb48.R = luminance;
                                rgb48.G = luminance;
                                rgb48.B = luminance;
                                pixel.PackFromRgb48(rgb48);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            // TODO: We should really be using Rgb24 here but IPixel does not have a PackFromRgb24 method.
                            var rgba32 = new Rgba32(0, 0, 0, byte.MaxValue);
                            for (int x = 0; x < this.header.Width; x++)
                            {
                                byte luminance = (byte)(scanlineSpan[x] * factor);
                                rgba32.R = luminance;
                                rgba32.G = luminance;
                                rgba32.B = luminance;
                                pixel.PackFromRgba32(rgba32);
                                rowSpan[x] = pixel;
                            }
                        }
                    }
                    else
                    {
                        if (this.header.BitDepth == 16)
                        {
                            Rgba64 rgba64 = default;
                            for (int x = 0, o = 0; x < this.header.Width; x++, o += 2)
                            {
                                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgba64.R = luminance;
                                rgba64.G = luminance;
                                rgba64.B = luminance;
                                rgba64.A = luminance.Equals(this.luminance16Trans) ? ushort.MinValue : ushort.MaxValue;

                                pixel.PackFromRgba64(rgba64);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            Rgba32 rgba32 = default;
                            for (int x = 0; x < this.header.Width; x++)
                            {
                                byte luminance = (byte)(scanlineSpan[x] * factor);
                                rgba32.R = luminance;
                                rgba32.G = luminance;
                                rgba32.B = luminance;
                                rgba32.A = luminance.Equals(this.luminanceTrans) ? byte.MinValue : byte.MaxValue;

                                pixel.PackFromRgba32(rgba32);
                                rowSpan[x] = pixel;
                            }
                        }
                    }

                    break;

                case PngColorType.GrayscaleWithAlpha:

                    if (this.header.BitDepth == 16)
                    {
                        Rgba64 rgba64 = default;
                        for (int x = 0, o = 0; x < this.header.Width; x++, o += 4)
                        {
                            ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                            ushort alpha = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                            rgba64.R = luminance;
                            rgba64.G = luminance;
                            rgba64.B = luminance;
                            rgba64.A = alpha;

                            pixel.PackFromRgba64(rgba64);
                            rowSpan[x] = pixel;
                        }
                    }
                    else
                    {
                        Rgba32 rgba32 = default;
                        for (int x = 0; x < this.header.Width; x++)
                        {
                            int offset = x * this.bytesPerPixel;
                            byte luminance = scanlineSpan[offset];
                            byte alpha = scanlineSpan[offset + this.bytesPerSample];

                            rgba32.R = luminance;
                            rgba32.G = luminance;
                            rgba32.B = luminance;
                            rgba32.A = alpha;

                            pixel.PackFromRgba32(rgba32);
                            rowSpan[x] = pixel;
                        }
                    }

                    break;

                case PngColorType.Palette:

                    this.ProcessScanlineFromPalette(scanlineSpan, rowSpan);

                    break;

                case PngColorType.Rgb:

                    if (!this.hasTrans)
                    {
                        if (this.header.BitDepth == 16)
                        {
                            Rgb48 rgb48 = default;
                            for (int x = 0, o = 0; x < this.header.Width; x++, o += 6)
                            {
                                rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                                rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 4, 2));
                                pixel.PackFromRgb48(rgb48);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            PixelOperations<TPixel>.Instance.PackFromRgb24Bytes(scanlineSpan, rowSpan, this.header.Width);
                        }
                    }
                    else
                    {
                        if (this.header.BitDepth == 16)
                        {
                            Rgb48 rgb48 = default;
                            Rgba64 rgba64 = default;
                            for (int x = 0, o = 0; x < this.header.Width; x++, o += 6)
                            {
                                rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                                rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 4, 2));

                                rgba64.Rgb = rgb48;
                                rgba64.A = rgb48.Equals(this.rgb48Trans) ? ushort.MinValue : ushort.MaxValue;

                                pixel.PackFromRgba64(rgba64);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            ReadOnlySpan<Rgb24> rgb24Span = MemoryMarshal.Cast<byte, Rgb24>(scanlineSpan);
                            for (int x = 0; x < this.header.Width; x++)
                            {
                                ref readonly Rgb24 rgb24 = ref rgb24Span[x];
                                Rgba32 rgba32 = default;
                                rgba32.Rgb = rgb24;
                                rgba32.A = rgb24.Equals(this.rgb24Trans) ? byte.MinValue : byte.MaxValue;

                                pixel.PackFromRgba32(rgba32);
                                rowSpan[x] = pixel;
                            }
                        }
                    }

                    break;

                case PngColorType.RgbWithAlpha:

                    if (this.header.BitDepth == 16)
                    {
                        Rgba64 rgba64 = default;
                        for (int x = 0, o = 0; x < this.header.Width; x++, o += 8)
                        {
                            rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                            rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                            rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 4, 2));
                            rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 6, 2));
                            pixel.PackFromRgba64(rgba64);
                            rowSpan[x] = pixel;
                        }
                    }
                    else
                    {
                        PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(scanlineSpan, rowSpan, this.header.Width);
                    }

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
            TPixel pixel = default;

            // Trim the first marker byte from the buffer
            ReadOnlySpan<byte> trimmed = defilteredScanline.Slice(1, defilteredScanline.Length - 1);

            // Convert 1, 2, and 4 bit pixel data into the 8 bit equivalent.
            ReadOnlySpan<byte> scanlineSpan = this.TryScaleUpTo8BitArray(trimmed, this.bytesPerScanline, this.header.BitDepth, out IManagedByteBuffer buffer)
            ? buffer.GetSpan()
            : trimmed;

            switch (this.pngColorType)
            {
                case PngColorType.Grayscale:

                    int factor = 255 / ((int)Math.Pow(2, this.header.BitDepth) - 1);

                    if (!this.hasTrans)
                    {
                        if (this.header.BitDepth == 16)
                        {
                            Rgb48 rgb48 = default;
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 2)
                            {
                                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgb48.R = luminance;
                                rgb48.G = luminance;
                                rgb48.B = luminance;

                                pixel.PackFromRgb48(rgb48);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            // TODO: We should really be using Rgb24 here but IPixel does not have a PackFromRgb24 method.
                            var rgba32 = new Rgba32(0, 0, 0, byte.MaxValue);
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o++)
                            {
                                byte luminance = (byte)(scanlineSpan[o] * factor);
                                rgba32.R = luminance;
                                rgba32.G = luminance;
                                rgba32.B = luminance;

                                pixel.PackFromRgba32(rgba32);
                                rowSpan[x] = pixel;
                            }
                        }
                    }
                    else
                    {
                        if (this.header.BitDepth == 16)
                        {
                            Rgba64 rgba64 = default;
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 2)
                            {
                                ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgba64.R = luminance;
                                rgba64.G = luminance;
                                rgba64.B = luminance;
                                rgba64.A = luminance.Equals(this.luminance16Trans) ? ushort.MinValue : ushort.MaxValue;

                                pixel.PackFromRgba64(rgba64);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            Rgba32 rgba32 = default;
                            for (int x = pixelOffset; x < this.header.Width; x += increment)
                            {
                                byte luminance = (byte)(scanlineSpan[x] * factor);
                                rgba32.R = luminance;
                                rgba32.G = luminance;
                                rgba32.B = luminance;
                                rgba32.A = luminance.Equals(this.luminanceTrans) ? byte.MinValue : byte.MaxValue;

                                pixel.PackFromRgba32(rgba32);
                                rowSpan[x] = pixel;
                            }
                        }
                    }

                    break;

                case PngColorType.GrayscaleWithAlpha:

                    if (this.header.BitDepth == 16)
                    {
                        Rgba64 rgba64 = default;
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 4)
                        {
                            ushort luminance = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                            ushort alpha = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                            rgba64.R = luminance;
                            rgba64.G = luminance;
                            rgba64.B = luminance;
                            rgba64.A = alpha;

                            pixel.PackFromRgba64(rgba64);
                            rowSpan[x] = pixel;
                        }
                    }
                    else
                    {
                        Rgba32 rgba32 = default;
                        for (int x = pixelOffset; x < this.header.Width; x += increment)
                        {
                            int offset = x * this.bytesPerPixel;
                            byte luminance = scanlineSpan[offset];
                            byte alpha = scanlineSpan[offset + this.bytesPerSample];
                            rgba32.R = luminance;
                            rgba32.G = luminance;
                            rgba32.B = luminance;
                            rgba32.A = alpha;

                            pixel.PackFromRgba32(rgba32);
                            rowSpan[x] = pixel;
                        }
                    }

                    break;

                case PngColorType.Palette:

                    Span<Rgb24> palettePixels = MemoryMarshal.Cast<byte, Rgb24>(this.palette);

                    if (this.paletteAlpha?.Length > 0)
                    {
                        // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                        // channel and we should try to read it.
                        Rgba32 rgba = default;
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o++)
                        {
                            int index = scanlineSpan[o];
                            rgba.A = this.paletteAlpha.Length > index ? this.paletteAlpha[index] : byte.MaxValue;
                            rgba.Rgb = palettePixels[index];

                            pixel.PackFromRgba32(rgba);
                            rowSpan[x] = pixel;
                        }
                    }
                    else
                    {
                        var rgba = new Rgba32(0, 0, 0, byte.MaxValue);
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o++)
                        {
                            int index = scanlineSpan[o];
                            rgba.Rgb = palettePixels[index];

                            pixel.PackFromRgba32(rgba);
                            rowSpan[x] = pixel;
                        }
                    }

                    break;

                case PngColorType.Rgb:

                    if (this.header.BitDepth == 16)
                    {
                        if (this.hasTrans)
                        {
                            Rgb48 rgb48 = default;
                            Rgba64 rgba64 = default;
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 6)
                            {
                                rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                                rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 4, 2));

                                rgba64.Rgb = rgb48;
                                rgba64.A = rgb48.Equals(this.rgb48Trans) ? ushort.MinValue : ushort.MaxValue;

                                pixel.PackFromRgba64(rgba64);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            Rgb48 rgb48 = default;
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 6)
                            {
                                rgb48.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                                rgb48.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                                rgb48.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 4, 2));
                                pixel.PackFromRgb48(rgb48);
                                rowSpan[x] = pixel;
                            }
                        }
                    }
                    else
                    {
                        if (this.hasTrans)
                        {
                            Rgba32 rgba = default;
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                            {
                                rgba.R = scanlineSpan[o];
                                rgba.G = scanlineSpan[o + this.bytesPerSample];
                                rgba.B = scanlineSpan[o + (2 * this.bytesPerSample)];
                                rgba.A = this.rgb24Trans.Equals(rgba.Rgb) ? byte.MinValue : byte.MaxValue;

                                pixel.PackFromRgba32(rgba);
                                rowSpan[x] = pixel;
                            }
                        }
                        else
                        {
                            var rgba = new Rgba32(0, 0, 0, byte.MaxValue);
                            for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                            {
                                rgba.R = scanlineSpan[o];
                                rgba.G = scanlineSpan[o + this.bytesPerSample];
                                rgba.B = scanlineSpan[o + (2 * this.bytesPerSample)];

                                pixel.PackFromRgba32(rgba);
                                rowSpan[x] = pixel;
                            }
                        }
                    }

                    break;

                case PngColorType.RgbWithAlpha:

                    if (this.header.BitDepth == 16)
                    {
                        Rgba64 rgba64 = default;
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += 8)
                        {
                            rgba64.R = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o, 2));
                            rgba64.G = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 2, 2));
                            rgba64.B = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 4, 2));
                            rgba64.A = BinaryPrimitives.ReadUInt16BigEndian(scanlineSpan.Slice(o + 6, 2));
                            pixel.PackFromRgba64(rgba64);
                            rowSpan[x] = pixel;
                        }
                    }
                    else
                    {
                        Rgba32 rgba = default;
                        for (int x = pixelOffset, o = 0; x < this.header.Width; x += increment, o += this.bytesPerPixel)
                        {
                            rgba.R = scanlineSpan[o];
                            rgba.G = scanlineSpan[o + this.bytesPerSample];
                            rgba.B = scanlineSpan[o + (2 * this.bytesPerSample)];
                            rgba.A = scanlineSpan[o + (3 * this.bytesPerSample)];

                            pixel.PackFromRgba32(rgba);
                            rowSpan[x] = pixel;
                        }
                    }

                    break;
            }

            buffer?.Dispose();
        }

        /// <summary>
        /// Decodes and assigns marker colors that identify transparent pixels in non indexed images
        /// </summary>
        /// <param name="alpha">The aplha tRNS array</param>
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
        /// Processes a scanline that uses a palette
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel we are expanding to</typeparam>
        /// <param name="scanline">The defiltered scanline</param>
        /// <param name="row">Thecurrent  output image row</param>
        private void ProcessScanlineFromPalette<TPixel>(ReadOnlySpan<byte> scanline, Span<TPixel> row)
            where TPixel : struct, IPixel<TPixel>
        {
            ReadOnlySpan<Rgb24> palettePixels = MemoryMarshal.Cast<byte, Rgb24>(this.palette);
            var color = default(TPixel);

            if (this.paletteAlpha?.Length > 0)
            {
                Rgba32 rgba = default;

                // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                // channel and we should try to read it.
                for (int x = 0; x < this.header.Width; x++)
                {
                    int index = scanline[x];
                    rgba.A = this.paletteAlpha.Length > index ? this.paletteAlpha[index] : byte.MaxValue;
                    rgba.Rgb = palettePixels[index];

                    color.PackFromRgba32(rgba);
                    row[x] = color;
                }
            }
            else
            {
                // TODO: We should have PackFromRgb24.
                var rgba = new Rgba32(0, 0, 0, byte.MaxValue);
                for (int x = 0; x < this.header.Width; x++)
                {
                    int index = scanline[x];

                    rgba.Rgb = palettePixels[index];

                    color.PackFromRgba32(rgba);
                    row[x] = color;
                }
            }
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
        /// Reads a chunk from the stream.
        /// </summary>
        /// <param name="chunk">The image format chunk.</param>
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

        /// <summary>
        /// Validates the png chunk.
        /// </summary>
        /// <param name="chunk">The <see cref="PngChunk"/>.</param>
        private void ValidateChunk(in PngChunk chunk)
        {
            this.crc.Reset();
            this.crc.Update(this.chunkTypeBuffer);
            this.crc.Update(chunk.Data.GetSpan());

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
            IManagedByteBuffer buffer = this.configuration.MemoryAllocator.AllocateCleanManagedByteBuffer(length);

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
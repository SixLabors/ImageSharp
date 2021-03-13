// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Performs the bitmap decoding operation.
    /// </summary>
    /// <remarks>
    /// A useful decoding source example can be found at <see href="https://dxr.mozilla.org/mozilla-central/source/image/decoders/nsBMPDecoder.cpp"/>
    /// </remarks>
    internal sealed class BmpDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// The default mask for the red part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int DefaultRgb16RMask = 0x7C00;

        /// <summary>
        /// The default mask for the green part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int DefaultRgb16GMask = 0x3E0;

        /// <summary>
        /// The default mask for the blue part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int DefaultRgb16BMask = 0x1F;

        /// <summary>
        /// RLE flag value that indicates following byte has special meaning.
        /// </summary>
        private const int RleCommand = 0x00;

        /// <summary>
        /// RLE flag value marking end of a scan line.
        /// </summary>
        private const int RleEndOfLine = 0x00;

        /// <summary>
        /// RLE flag value marking end of bitmap data.
        /// </summary>
        private const int RleEndOfBitmap = 0x01;

        /// <summary>
        /// RLE flag value marking the start of [x,y] offset instruction.
        /// </summary>
        private const int RleDelta = 0x02;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private BufferedReadStream stream;

        /// <summary>
        /// The metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// The bitmap specific metadata.
        /// </summary>
        private BmpMetadata bmpMetadata;

        /// <summary>
        /// The file header containing general information.
        /// </summary>
        private BmpFileHeader fileHeader;

        /// <summary>
        /// Indicates which bitmap file marker was read.
        /// </summary>
        private BmpFileMarkerType fileMarkerType;

        /// <summary>
        /// The info header containing detailed information about the bitmap.
        /// </summary>
        private BmpInfoHeader infoHeader;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The bitmap decoder options.
        /// </summary>
        private readonly IBmpDecoderOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public BmpDecoderCore(Configuration configuration, IBmpDecoderOptions options)
        {
            this.Configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <inheritdoc />
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new Size(this.infoHeader.Width, this.infoHeader.Height);

        /// <inheritdoc />
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            try
            {
                int bytesPerColorMapEntry = this.ReadImageHeaders(stream, out bool inverted, out byte[] palette);

                var image = new Image<TPixel>(this.Configuration, this.infoHeader.Width, this.infoHeader.Height, this.metadata);

                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

                switch (this.infoHeader.Compression)
                {
                    case BmpCompression.RGB:
                        if (this.infoHeader.BitsPerPixel == 32)
                        {
                            if (this.bmpMetadata.InfoHeaderType == BmpInfoHeaderType.WinVersion3)
                            {
                                this.ReadRgb32Slow(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                            }
                            else
                            {
                                this.ReadRgb32Fast(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                            }
                        }
                        else if (this.infoHeader.BitsPerPixel == 24)
                        {
                            this.ReadRgb24(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                        }
                        else if (this.infoHeader.BitsPerPixel == 16)
                        {
                            this.ReadRgb16(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);
                        }
                        else if (this.infoHeader.BitsPerPixel <= 8)
                        {
                            this.ReadRgbPalette(
                                pixels,
                                palette,
                                this.infoHeader.Width,
                                this.infoHeader.Height,
                                this.infoHeader.BitsPerPixel,
                                bytesPerColorMapEntry,
                                inverted);
                        }

                        break;

                    case BmpCompression.RLE24:
                        this.ReadRle24(pixels, this.infoHeader.Width, this.infoHeader.Height, inverted);

                        break;

                    case BmpCompression.RLE8:
                    case BmpCompression.RLE4:
                        this.ReadRle(this.infoHeader.Compression, pixels, palette, this.infoHeader.Width, this.infoHeader.Height, inverted);

                        break;

                    case BmpCompression.BitFields:
                    case BmpCompression.BI_ALPHABITFIELDS:
                        this.ReadBitFields(pixels, inverted);

                        break;

                    default:
                        BmpThrowHelper.ThrowNotSupportedException("Does not support this kind of bitmap files.");

                        break;
                }

                return image;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("Bitmap does not have a valid format.", e);
            }
        }

        /// <inheritdoc />
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.ReadImageHeaders(stream, out _, out _);
            return new ImageInfo(new PixelTypeInfo(this.infoHeader.BitsPerPixel), this.infoHeader.Width, this.infoHeader.Height, this.metadata);
        }

        /// <summary>
        /// Returns the y- value based on the given height.
        /// </summary>
        /// <param name="y">The y- value representing the current row.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        /// <returns>The <see cref="int"/> representing the inverted value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Invert(int y, int height, bool inverted) => (!inverted) ? height - y - 1 : y;

        /// <summary>
        /// Calculates the amount of bytes to pad a row.
        /// </summary>
        /// <param name="width">The image width.</param>
        /// <param name="componentCount">The pixel component count.</param>
        /// <returns>
        /// The padding.
        /// </returns>
        private static int CalculatePadding(int width, int componentCount)
        {
            int padding = (width * componentCount) % 4;

            if (padding != 0)
            {
                padding = 4 - padding;
            }

            return padding;
        }

        /// <summary>
        /// Decodes a bitmap containing the BITFIELDS Compression type. For each color channel, there will be a bitmask
        /// which will be used to determine which bits belong to that channel.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadBitFields<TPixel>(Buffer2D<TPixel> pixels, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (this.infoHeader.BitsPerPixel == 16)
            {
                this.ReadRgb16(
                    pixels,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    inverted,
                    this.infoHeader.RedMask,
                    this.infoHeader.GreenMask,
                    this.infoHeader.BlueMask);
            }
            else
            {
                this.ReadRgb32BitFields(
                    pixels,
                    this.infoHeader.Width,
                    this.infoHeader.Height,
                    inverted,
                    this.infoHeader.RedMask,
                    this.infoHeader.GreenMask,
                    this.infoHeader.BlueMask,
                    this.infoHeader.AlphaMask);
            }
        }

        /// <summary>
        /// Looks up color values and builds the image from de-compressed RLE8 or RLE4 data.
        /// Compressed RLE8 stream is uncompressed by <see cref="UncompressRle8(int, Span{byte}, Span{bool}, Span{bool})"/>
        /// Compressed RLE4 stream is uncompressed by <see cref="UncompressRle4(int, Span{byte}, Span{bool}, Span{bool})"/>
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="compression">The compression type. Either RLE4 or RLE8.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="colors">The <see cref="T:byte[]"/> containing the colors.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRle<TPixel>(BmpCompression compression, Buffer2D<TPixel> pixels, byte[] colors, int width, int height, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height, AllocationOptions.Clean))
            using (IMemoryOwner<bool> undefinedPixels = this.memoryAllocator.Allocate<bool>(width * height, AllocationOptions.Clean))
            using (IMemoryOwner<bool> rowsWithUndefinedPixels = this.memoryAllocator.Allocate<bool>(height, AllocationOptions.Clean))
            {
                Span<bool> rowsWithUndefinedPixelsSpan = rowsWithUndefinedPixels.Memory.Span;
                Span<bool> undefinedPixelsSpan = undefinedPixels.Memory.Span;
                Span<byte> bufferSpan = buffer.Memory.Span;
                if (compression is BmpCompression.RLE8)
                {
                    this.UncompressRle8(width, bufferSpan, undefinedPixelsSpan, rowsWithUndefinedPixelsSpan);
                }
                else
                {
                    this.UncompressRle4(width, bufferSpan, undefinedPixelsSpan, rowsWithUndefinedPixelsSpan);
                }

                for (int y = 0; y < height; y++)
                {
                    int newY = Invert(y, height, inverted);
                    int rowStartIdx = y * width;
                    Span<byte> bufferRow = bufferSpan.Slice(rowStartIdx, width);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    bool rowHasUndefinedPixels = rowsWithUndefinedPixelsSpan[y];
                    if (rowHasUndefinedPixels)
                    {
                        // Slow path with undefined pixels.
                        for (int x = 0; x < width; x++)
                        {
                            byte colorIdx = bufferRow[x];
                            if (undefinedPixelsSpan[rowStartIdx + x])
                            {
                                switch (this.options.RleSkippedPixelHandling)
                                {
                                    case RleSkippedPixelHandling.FirstColorOfPalette:
                                        color.FromBgr24(Unsafe.As<byte, Bgr24>(ref colors[colorIdx * 4]));
                                        break;
                                    case RleSkippedPixelHandling.Transparent:
                                        color.FromVector4(Vector4.Zero);
                                        break;

                                    // Default handling for skipped pixels is black (which is what System.Drawing is also doing).
                                    default:
                                        color.FromVector4(new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                                        break;
                                }
                            }
                            else
                            {
                                color.FromBgr24(Unsafe.As<byte, Bgr24>(ref colors[colorIdx * 4]));
                            }

                            pixelRow[x] = color;
                        }
                    }
                    else
                    {
                        // Fast path without any undefined pixels.
                        for (int x = 0; x < width; x++)
                        {
                            color.FromBgr24(Unsafe.As<byte, Bgr24>(ref colors[bufferRow[x] * 4]));
                            pixelRow[x] = color;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Looks up color values and builds the image from de-compressed RLE24.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRle24<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            using (IMemoryOwner<byte> buffer = this.memoryAllocator.Allocate<byte>(width * height * 3, AllocationOptions.Clean))
            using (IMemoryOwner<bool> undefinedPixels = this.memoryAllocator.Allocate<bool>(width * height, AllocationOptions.Clean))
            using (IMemoryOwner<bool> rowsWithUndefinedPixels = this.memoryAllocator.Allocate<bool>(height, AllocationOptions.Clean))
            {
                Span<bool> rowsWithUndefinedPixelsSpan = rowsWithUndefinedPixels.Memory.Span;
                Span<bool> undefinedPixelsSpan = undefinedPixels.Memory.Span;
                Span<byte> bufferSpan = buffer.GetSpan();

                this.UncompressRle24(width, bufferSpan, undefinedPixelsSpan, rowsWithUndefinedPixelsSpan);
                for (int y = 0; y < height; y++)
                {
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);
                    bool rowHasUndefinedPixels = rowsWithUndefinedPixelsSpan[y];
                    if (rowHasUndefinedPixels)
                    {
                        // Slow path with undefined pixels.
                        var yMulWidth = y * width;
                        int rowStartIdx = yMulWidth * 3;
                        for (int x = 0; x < width; x++)
                        {
                            int idx = rowStartIdx + (x * 3);
                            if (undefinedPixelsSpan[yMulWidth + x])
                            {
                                switch (this.options.RleSkippedPixelHandling)
                                {
                                    case RleSkippedPixelHandling.FirstColorOfPalette:
                                        color.FromBgr24(Unsafe.As<byte, Bgr24>(ref bufferSpan[idx]));
                                        break;
                                    case RleSkippedPixelHandling.Transparent:
                                        color.FromVector4(Vector4.Zero);
                                        break;

                                    // Default handling for skipped pixels is black (which is what System.Drawing is also doing).
                                    default:
                                        color.FromVector4(new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                                        break;
                                }
                            }
                            else
                            {
                                color.FromBgr24(Unsafe.As<byte, Bgr24>(ref bufferSpan[idx]));
                            }

                            pixelRow[x] = color;
                        }
                    }
                    else
                    {
                        // Fast path without any undefined pixels.
                        int rowStartIdx = y * width * 3;
                        for (int x = 0; x < width; x++)
                        {
                            int idx = rowStartIdx + (x * 3);
                            color.FromBgr24(Unsafe.As<byte, Bgr24>(ref bufferSpan[idx]));
                            pixelRow[x] = color;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Produce uncompressed bitmap data from a RLE4 stream.
        /// </summary>
        /// <remarks>
        /// RLE4 is a 2-byte run-length encoding.
        /// <br/>If first byte is 0, the second byte may have special meaning.
        /// <br/>Otherwise, the first byte is the length of the run and second byte contains two color indexes.
        /// </remarks>
        /// <param name="w">The width of the bitmap.</param>
        /// <param name="buffer">Buffer for uncompressed data.</param>
        /// <param name="undefinedPixels">Keeps track over skipped and therefore undefined pixels.</param>
        /// <param name="rowsWithUndefinedPixels">Keeps track of rows, which have undefined pixels.</param>
        private void UncompressRle4(int w, Span<byte> buffer, Span<bool> undefinedPixels, Span<bool> rowsWithUndefinedPixels)
        {
            Span<byte> cmd = stackalloc byte[2];
            int count = 0;

            while (count < buffer.Length)
            {
                if (this.stream.Read(cmd, 0, cmd.Length) != 2)
                {
                    BmpThrowHelper.ThrowInvalidImageContentException("Failed to read 2 bytes from the stream while uncompressing RLE4 bitmap.");
                }

                if (cmd[0] == RleCommand)
                {
                    switch (cmd[1])
                    {
                        case RleEndOfBitmap:
                            int skipEoB = buffer.Length - count;
                            RleSkipEndOfBitmap(count, w, skipEoB, undefinedPixels, rowsWithUndefinedPixels);

                            return;

                        case RleEndOfLine:
                            count += RleSkipEndOfLine(count, w, undefinedPixels, rowsWithUndefinedPixels);

                            break;

                        case RleDelta:
                            int dx = this.stream.ReadByte();
                            int dy = this.stream.ReadByte();
                            count += RleSkipDelta(count, w, dx, dy, undefinedPixels, rowsWithUndefinedPixels);

                            break;

                        default:
                            // If the second byte > 2, we are in 'absolute mode'.
                            // The second byte contains the number of color indexes that follow.
                            int max = cmd[1];
                            int bytesToRead = (max + 1) / 2;

                            var run = new byte[bytesToRead];

                            this.stream.Read(run, 0, run.Length);

                            int idx = 0;
                            for (int i = 0; i < max; i++)
                            {
                                byte twoPixels = run[idx];
                                if (i % 2 == 0)
                                {
                                    byte leftPixel = (byte)((twoPixels >> 4) & 0xF);
                                    buffer[count++] = leftPixel;
                                }
                                else
                                {
                                    byte rightPixel = (byte)(twoPixels & 0xF);
                                    buffer[count++] = rightPixel;
                                    idx++;
                                }
                            }

                            // Absolute mode data is aligned to two-byte word-boundary.
                            int padding = bytesToRead & 1;

                            this.stream.Skip(padding);

                            break;
                    }
                }
                else
                {
                    int max = cmd[0];

                    // The second byte contains two color indexes, one in its high-order 4 bits and one in its low-order 4 bits.
                    byte twoPixels = cmd[1];
                    byte rightPixel = (byte)(twoPixels & 0xF);
                    byte leftPixel = (byte)((twoPixels >> 4) & 0xF);

                    for (int idx = 0; idx < max; idx++)
                    {
                        if (idx % 2 == 0)
                        {
                            buffer[count] = leftPixel;
                        }
                        else
                        {
                            buffer[count] = rightPixel;
                        }

                        count++;
                    }
                }
            }
        }

        /// <summary>
        /// Produce uncompressed bitmap data from a RLE8 stream.
        /// </summary>
        /// <remarks>
        /// RLE8 is a 2-byte run-length encoding.
        /// <br/>If first byte is 0, the second byte may have special meaning.
        /// <br/>Otherwise, the first byte is the length of the run and second byte is the color for the run.
        /// </remarks>
        /// <param name="w">The width of the bitmap.</param>
        /// <param name="buffer">Buffer for uncompressed data.</param>
        /// <param name="undefinedPixels">Keeps track of skipped and therefore undefined pixels.</param>
        /// <param name="rowsWithUndefinedPixels">Keeps track of rows, which have undefined pixels.</param>
        private void UncompressRle8(int w, Span<byte> buffer, Span<bool> undefinedPixels, Span<bool> rowsWithUndefinedPixels)
        {
            Span<byte> cmd = stackalloc byte[2];
            int count = 0;

            while (count < buffer.Length)
            {
                if (this.stream.Read(cmd, 0, cmd.Length) != 2)
                {
                    BmpThrowHelper.ThrowInvalidImageContentException("Failed to read 2 bytes from stream while uncompressing RLE8 bitmap.");
                }

                if (cmd[0] == RleCommand)
                {
                    switch (cmd[1])
                    {
                        case RleEndOfBitmap:
                            int skipEoB = buffer.Length - count;
                            RleSkipEndOfBitmap(count, w, skipEoB, undefinedPixels, rowsWithUndefinedPixels);

                            return;

                        case RleEndOfLine:
                            count += RleSkipEndOfLine(count, w, undefinedPixels, rowsWithUndefinedPixels);

                            break;

                        case RleDelta:
                            int dx = this.stream.ReadByte();
                            int dy = this.stream.ReadByte();
                            count += RleSkipDelta(count, w, dx, dy, undefinedPixels, rowsWithUndefinedPixels);

                            break;

                        default:
                            // If the second byte > 2, we are in 'absolute mode'.
                            // Take this number of bytes from the stream as uncompressed data.
                            int length = cmd[1];

                            var run = new byte[length];

                            this.stream.Read(run, 0, run.Length);

                            run.AsSpan().CopyTo(buffer.Slice(count));

                            count += run.Length;

                            // Absolute mode data is aligned to two-byte word-boundary.
                            int padding = length & 1;

                            this.stream.Skip(padding);

                            break;
                    }
                }
                else
                {
                    int max = count + cmd[0]; // as we start at the current count in the following loop, max is count + cmd[0]
                    byte colorIdx = cmd[1]; // store the value to avoid the repeated indexer access inside the loop.

                    for (; count < max; count++)
                    {
                        buffer[count] = colorIdx;
                    }
                }
            }
        }

        /// <summary>
        /// Produce uncompressed bitmap data from a RLE24 stream.
        /// </summary>
        /// <remarks>
        /// <br/>If first byte is 0, the second byte may have special meaning.
        /// <br/>Otherwise, the first byte is the length of the run and following three bytes are the color for the run.
        /// </remarks>
        /// <param name="w">The width of the bitmap.</param>
        /// <param name="buffer">Buffer for uncompressed data.</param>
        /// <param name="undefinedPixels">Keeps track of skipped and therefore undefined pixels.</param>
        /// <param name="rowsWithUndefinedPixels">Keeps track of rows, which have undefined pixels.</param>
        private void UncompressRle24(int w, Span<byte> buffer, Span<bool> undefinedPixels, Span<bool> rowsWithUndefinedPixels)
        {
            Span<byte> cmd = stackalloc byte[2];
            int uncompressedPixels = 0;

            while (uncompressedPixels < buffer.Length)
            {
                if (this.stream.Read(cmd, 0, cmd.Length) != 2)
                {
                    BmpThrowHelper.ThrowInvalidImageContentException("Failed to read 2 bytes from stream while uncompressing RLE24 bitmap.");
                }

                if (cmd[0] == RleCommand)
                {
                    switch (cmd[1])
                    {
                        case RleEndOfBitmap:
                            int skipEoB = (buffer.Length - (uncompressedPixels * 3)) / 3;
                            RleSkipEndOfBitmap(uncompressedPixels, w, skipEoB, undefinedPixels, rowsWithUndefinedPixels);

                            return;

                        case RleEndOfLine:
                            uncompressedPixels += RleSkipEndOfLine(uncompressedPixels, w, undefinedPixels, rowsWithUndefinedPixels);

                            break;

                        case RleDelta:
                            int dx = this.stream.ReadByte();
                            int dy = this.stream.ReadByte();
                            uncompressedPixels += RleSkipDelta(uncompressedPixels, w, dx, dy, undefinedPixels, rowsWithUndefinedPixels);

                            break;

                        default:
                            // If the second byte > 2, we are in 'absolute mode'.
                            // Take this number of bytes from the stream as uncompressed data.
                            int length = cmd[1];

                            var run = new byte[length * 3];

                            this.stream.Read(run, 0, run.Length);

                            run.AsSpan().CopyTo(buffer.Slice(start: uncompressedPixels * 3));

                            uncompressedPixels += length;

                            // Absolute mode data is aligned to two-byte word-boundary.
                            int padding = run.Length & 1;

                            this.stream.Skip(padding);

                            break;
                    }
                }
                else
                {
                    int max = uncompressedPixels + cmd[0];
                    byte blueIdx = cmd[1];
                    byte greenIdx = (byte)this.stream.ReadByte();
                    byte redIdx = (byte)this.stream.ReadByte();

                    int bufferIdx = uncompressedPixels * 3;
                    for (; uncompressedPixels < max; uncompressedPixels++)
                    {
                        buffer[bufferIdx++] = blueIdx;
                        buffer[bufferIdx++] = greenIdx;
                        buffer[bufferIdx++] = redIdx;
                    }
                }
            }
        }

        /// <summary>
        /// Keeps track of skipped / undefined pixels, when the EndOfBitmap command occurs.
        /// </summary>
        /// <param name="count">The already processed pixel count.</param>
        /// <param name="w">The width of the image.</param>
        /// <param name="skipPixelCount">The skipped pixel count.</param>
        /// <param name="undefinedPixels">The undefined pixels.</param>
        /// <param name="rowsWithUndefinedPixels">Rows with undefined pixels.</param>
        private static void RleSkipEndOfBitmap(
            int count,
            int w,
            int skipPixelCount,
            Span<bool> undefinedPixels,
            Span<bool> rowsWithUndefinedPixels)
        {
            for (int i = count; i < count + skipPixelCount; i++)
            {
                undefinedPixels[i] = true;
            }

            int skippedRowIdx = count / w;
            int skippedRows = (skipPixelCount / w) - 1;
            int lastSkippedRow = Math.Min(skippedRowIdx + skippedRows, rowsWithUndefinedPixels.Length - 1);
            for (int i = skippedRowIdx; i <= lastSkippedRow; i++)
            {
                rowsWithUndefinedPixels[i] = true;
            }
        }

        /// <summary>
        /// Keeps track of undefined / skipped pixels, when the EndOfLine command occurs.
        /// </summary>
        /// <param name="count">The already uncompressed pixel count.</param>
        /// <param name="w">The width of image.</param>
        /// <param name="undefinedPixels">The undefined pixels.</param>
        /// <param name="rowsWithUndefinedPixels">The rows with undefined pixels.</param>
        /// <returns>The number of skipped pixels.</returns>
        private static int RleSkipEndOfLine(int count, int w, Span<bool> undefinedPixels, Span<bool> rowsWithUndefinedPixels)
        {
            rowsWithUndefinedPixels[count / w] = true;
            int remainingPixelsInRow = count % w;
            if (remainingPixelsInRow > 0)
            {
                int skipEoL = w - remainingPixelsInRow;
                for (int i = count; i < count + skipEoL; i++)
                {
                    undefinedPixels[i] = true;
                }

                return skipEoL;
            }

            return 0;
        }

        /// <summary>
        /// Keeps track of undefined / skipped pixels, when the delta command occurs.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <param name="w">The width of the image.</param>
        /// <param name="dx">Delta skip in x direction.</param>
        /// <param name="dy">Delta skip in y direction.</param>
        /// <param name="undefinedPixels">The undefined pixels.</param>
        /// <param name="rowsWithUndefinedPixels">The rows with undefined pixels.</param>
        /// <returns>The number of skipped pixels.</returns>
        private static int RleSkipDelta(
            int count,
            int w,
            int dx,
            int dy,
            Span<bool> undefinedPixels,
            Span<bool> rowsWithUndefinedPixels)
        {
            int skipDelta = (w * dy) + dx;
            for (int i = count; i < count + skipDelta; i++)
            {
                undefinedPixels[i] = true;
            }

            int skippedRowIdx = count / w;
            int lastSkippedRow = Math.Min(skippedRowIdx + dy, rowsWithUndefinedPixels.Length - 1);
            for (int i = skippedRowIdx; i <= lastSkippedRow; i++)
            {
                rowsWithUndefinedPixels[i] = true;
            }

            return skipDelta;
        }

        /// <summary>
        /// Reads the color palette from the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="colors">The <see cref="T:byte[]"/> containing the colors.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="bitsPerPixel">The number of bits per pixel.</param>
        /// <param name="bytesPerColorMapEntry">Usually 4 bytes, but in case of Windows 2.x bitmaps or OS/2 1.x bitmaps
        /// the bytes per color palette entry's can be 3 bytes instead of 4.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgbPalette<TPixel>(Buffer2D<TPixel> pixels, byte[] colors, int width, int height, int bitsPerPixel, int bytesPerColorMapEntry, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Pixels per byte (bits per pixel).
            int ppb = 8 / bitsPerPixel;

            int arrayWidth = (width + ppb - 1) / ppb;

            // Bit mask
            int mask = 0xFF >> (8 - bitsPerPixel);

            // Rows are aligned on 4 byte boundaries.
            int padding = arrayWidth % 4;
            if (padding != 0)
            {
                padding = 4 - padding;
            }

            using (IManagedByteBuffer row = this.memoryAllocator.AllocateManagedByteBuffer(arrayWidth + padding, AllocationOptions.Clean))
            {
                TPixel color = default;
                Span<byte> rowSpan = row.GetSpan();

                for (int y = 0; y < height; y++)
                {
                    int newY = Invert(y, height, inverted);
                    this.stream.Read(row.Array, 0, row.Length());
                    int offset = 0;
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    for (int x = 0; x < arrayWidth; x++)
                    {
                        int colOffset = x * ppb;
                        for (int shift = 0, newX = colOffset; shift < ppb && newX < width; shift++, newX++)
                        {
                            int colorIndex = ((rowSpan[offset] >> (8 - bitsPerPixel - (shift * bitsPerPixel))) & mask) * bytesPerColorMapEntry;

                            color.FromBgr24(Unsafe.As<byte, Bgr24>(ref colors[colorIndex]));
                            pixelRow[newX] = color;
                        }

                        offset++;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the 16 bit color palette from the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        /// <param name="redMask">The bitmask for the red channel.</param>
        /// <param name="greenMask">The bitmask for the green channel.</param>
        /// <param name="blueMask">The bitmask for the blue channel.</param>
        private void ReadRgb16<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted, int redMask = DefaultRgb16RMask, int greenMask = DefaultRgb16GMask, int blueMask = DefaultRgb16BMask)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 2);
            int stride = (width * 2) + padding;
            TPixel color = default;

            int rightShiftRedMask = CalculateRightShift((uint)redMask);
            int rightShiftGreenMask = CalculateRightShift((uint)greenMask);
            int rightShiftBlueMask = CalculateRightShift((uint)blueMask);

            // Each color channel contains either 5 or 6 Bits values.
            int redMaskBits = CountBits((uint)redMask);
            int greenMaskBits = CountBits((uint)greenMask);
            int blueMaskBits = CountBits((uint)blueMask);

            using (IManagedByteBuffer buffer = this.memoryAllocator.AllocateManagedByteBuffer(stride))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(buffer.Array, 0, stride);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    int offset = 0;
                    for (int x = 0; x < width; x++)
                    {
                        short temp = BitConverter.ToInt16(buffer.Array, offset);

                        // Rescale values, so the values range from 0 to 255.
                        int r = (redMaskBits == 5) ? GetBytesFrom5BitValue((temp & redMask) >> rightShiftRedMask) : GetBytesFrom6BitValue((temp & redMask) >> rightShiftRedMask);
                        int g = (greenMaskBits == 5) ? GetBytesFrom5BitValue((temp & greenMask) >> rightShiftGreenMask) : GetBytesFrom6BitValue((temp & greenMask) >> rightShiftGreenMask);
                        int b = (blueMaskBits == 5) ? GetBytesFrom5BitValue((temp & blueMask) >> rightShiftBlueMask) : GetBytesFrom6BitValue((temp & blueMask) >> rightShiftBlueMask);
                        var rgb = new Rgb24((byte)r, (byte)g, (byte)b);

                        color.FromRgb24(rgb);
                        pixelRow[x] = color;
                        offset += 2;
                    }
                }
            }
        }

        /// <summary>
        /// Performs final shifting from a 5bit value to an 8bit one.
        /// </summary>
        /// <param name="value">The masked and shifted value.</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetBytesFrom5BitValue(int value) => (byte)((value << 3) | (value >> 2));

        /// <summary>
        /// Performs final shifting from a 6bit value to an 8bit one.
        /// </summary>
        /// <param name="value">The masked and shifted value.</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetBytesFrom6BitValue(int value) => (byte)((value << 2) | (value >> 4));

        /// <summary>
        /// Reads the 24 bit color palette from the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb24<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 3);

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, padding))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.FromBgr24Bytes(
                        this.Configuration,
                        row.GetSpan(),
                        pixelSpan,
                        width);
                }
            }
        }

        /// <summary>
        /// Reads the 32 bit color palette from the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb32Fast<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 4);

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, padding))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(row);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
                    PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                        this.Configuration,
                        row.GetSpan(),
                        pixelSpan,
                        width);
                }
            }
        }

        /// <summary>
        /// Reads the 32 bit color palette from the stream, checking the alpha component of each pixel.
        /// This is a special case only used for 32bpp WinBMPv3 files, which could be in either BGR0 or BGRA format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        private void ReadRgb32Slow<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int padding = CalculatePadding(width, 4);

            using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 4, padding))
            using (IMemoryOwner<Bgra32> bgraRow = this.memoryAllocator.Allocate<Bgra32>(width))
            {
                Span<Bgra32> bgraRowSpan = bgraRow.GetSpan();
                long currentPosition = this.stream.Position;
                bool hasAlpha = false;

                // Loop though the rows checking each pixel. We start by assuming it's
                // an BGR0 image. If we hit a non-zero alpha value, then we know it's
                // actually a BGRA image, and change tactics accordingly.
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(row);

                    PixelOperations<Bgra32>.Instance.FromBgra32Bytes(
                        this.Configuration,
                        row.GetSpan(),
                        bgraRowSpan,
                        width);

                    // Check each pixel in the row to see if it has an alpha value.
                    for (int x = 0; x < width; x++)
                    {
                        Bgra32 bgra = bgraRowSpan[x];
                        if (bgra.A > 0)
                        {
                            hasAlpha = true;
                            break;
                        }
                    }

                    if (hasAlpha)
                    {
                        break;
                    }
                }

                // Reset our stream for a second pass.
                this.stream.Position = currentPosition;

                // Process the pixels in bulk taking the raw alpha component value.
                if (hasAlpha)
                {
                    for (int y = 0; y < height; y++)
                    {
                        this.stream.Read(row);

                        int newY = Invert(y, height, inverted);
                        Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);

                        PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                            this.Configuration,
                            row.GetSpan(),
                            pixelSpan,
                            width);
                    }

                    return;
                }

                // Slow path. We need to set each alpha component value to fully opaque.
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(row);
                    PixelOperations<Bgra32>.Instance.FromBgra32Bytes(
                        this.Configuration,
                        row.GetSpan(),
                        bgraRowSpan,
                        width);

                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);

                    for (int x = 0; x < width; x++)
                    {
                        Bgra32 bgra = bgraRowSpan[x];
                        bgra.A = byte.MaxValue;
                        ref TPixel pixel = ref pixelSpan[x];
                        pixel.FromBgra32(bgra);
                    }
                }
            }
        }

        /// <summary>
        /// Decode an 32 Bit Bitmap containing a bitmask for each color channel.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The output pixel buffer containing the decoded image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="inverted">Whether the bitmap is inverted.</param>
        /// <param name="redMask">The bitmask for the red channel.</param>
        /// <param name="greenMask">The bitmask for the green channel.</param>
        /// <param name="blueMask">The bitmask for the blue channel.</param>
        /// <param name="alphaMask">The bitmask for the alpha channel.</param>
        private void ReadRgb32BitFields<TPixel>(Buffer2D<TPixel> pixels, int width, int height, bool inverted, int redMask, int greenMask, int blueMask, int alphaMask)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel color = default;
            int padding = CalculatePadding(width, 4);
            int stride = (width * 4) + padding;

            int rightShiftRedMask = CalculateRightShift((uint)redMask);
            int rightShiftGreenMask = CalculateRightShift((uint)greenMask);
            int rightShiftBlueMask = CalculateRightShift((uint)blueMask);
            int rightShiftAlphaMask = CalculateRightShift((uint)alphaMask);

            int bitsRedMask = CountBits((uint)redMask);
            int bitsGreenMask = CountBits((uint)greenMask);
            int bitsBlueMask = CountBits((uint)blueMask);
            int bitsAlphaMask = CountBits((uint)alphaMask);
            float invMaxValueRed = 1.0f / (0xFFFFFFFF >> (32 - bitsRedMask));
            float invMaxValueGreen = 1.0f / (0xFFFFFFFF >> (32 - bitsGreenMask));
            float invMaxValueBlue = 1.0f / (0xFFFFFFFF >> (32 - bitsBlueMask));
            uint maxValueAlpha = 0xFFFFFFFF >> (32 - bitsAlphaMask);
            float invMaxValueAlpha = 1.0f / maxValueAlpha;

            bool unusualBitMask = bitsRedMask > 8 || bitsGreenMask > 8 || bitsBlueMask > 8 || invMaxValueAlpha > 8;

            using (IManagedByteBuffer buffer = this.memoryAllocator.AllocateManagedByteBuffer(stride))
            {
                for (int y = 0; y < height; y++)
                {
                    this.stream.Read(buffer.Array, 0, stride);
                    int newY = Invert(y, height, inverted);
                    Span<TPixel> pixelRow = pixels.GetRowSpan(newY);

                    int offset = 0;
                    for (int x = 0; x < width; x++)
                    {
                        uint temp = BitConverter.ToUInt32(buffer.Array, offset);

                        if (unusualBitMask)
                        {
                            uint r = (uint)(temp & redMask) >> rightShiftRedMask;
                            uint g = (uint)(temp & greenMask) >> rightShiftGreenMask;
                            uint b = (uint)(temp & blueMask) >> rightShiftBlueMask;
                            float alpha = alphaMask != 0 ? invMaxValueAlpha * ((uint)(temp & alphaMask) >> rightShiftAlphaMask) : 1.0f;
                            var vector4 = new Vector4(
                                r * invMaxValueRed,
                                g * invMaxValueGreen,
                                b * invMaxValueBlue,
                                alpha);
                            color.FromVector4(vector4);
                        }
                        else
                        {
                            byte r = (byte)((temp & redMask) >> rightShiftRedMask);
                            byte g = (byte)((temp & greenMask) >> rightShiftGreenMask);
                            byte b = (byte)((temp & blueMask) >> rightShiftBlueMask);
                            byte a = alphaMask != 0 ? (byte)((temp & alphaMask) >> rightShiftAlphaMask) : (byte)255;
                            color.FromRgba32(new Rgba32(r, g, b, a));
                        }

                        pixelRow[x] = color;
                        offset += 4;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the necessary right shifts for a given color bitmask (the 0 bits to the right).
        /// </summary>
        /// <param name="n">The color bit mask.</param>
        /// <returns>Number of bits to shift right.</returns>
        private static int CalculateRightShift(uint n)
        {
            int count = 0;
            while (n > 0)
            {
                if ((1 & n) == 0)
                {
                    count++;
                }
                else
                {
                    break;
                }

                n >>= 1;
            }

            return count;
        }

        /// <summary>
        /// Counts none zero bits.
        /// </summary>
        /// <param name="n">A color mask.</param>
        /// <returns>The none zero bits.</returns>
        private static int CountBits(uint n)
        {
            int count = 0;
            while (n != 0)
            {
                count++;
                n &= n - 1;
            }

            return count;
        }

        /// <summary>
        /// Reads the <see cref="BmpInfoHeader"/> from the stream.
        /// </summary>
        private void ReadInfoHeader()
        {
            Span<byte> buffer = stackalloc byte[BmpInfoHeader.MaxHeaderSize];

            // Read the header size.
            this.stream.Read(buffer, 0, BmpInfoHeader.HeaderSizeSize);

            int headerSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            if (headerSize < BmpInfoHeader.CoreSize || headerSize > BmpInfoHeader.MaxHeaderSize)
            {
                BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. HeaderSize is '{headerSize}'.");
            }

            // Read the rest of the header.
            this.stream.Read(buffer, BmpInfoHeader.HeaderSizeSize, headerSize - BmpInfoHeader.HeaderSizeSize);

            BmpInfoHeaderType infoHeaderType = BmpInfoHeaderType.WinVersion2;
            if (headerSize == BmpInfoHeader.CoreSize)
            {
                // 12 bytes
                infoHeaderType = BmpInfoHeaderType.WinVersion2;
                this.infoHeader = BmpInfoHeader.ParseCore(buffer);
            }
            else if (headerSize == BmpInfoHeader.Os22ShortSize)
            {
                // 16 bytes
                infoHeaderType = BmpInfoHeaderType.Os2Version2Short;
                this.infoHeader = BmpInfoHeader.ParseOs22Short(buffer);
            }
            else if (headerSize == BmpInfoHeader.SizeV3)
            {
                // == 40 bytes
                infoHeaderType = BmpInfoHeaderType.WinVersion3;
                this.infoHeader = BmpInfoHeader.ParseV3(buffer);

                // If the info header is BMP version 3 and the compression type is BITFIELDS,
                // color masks for each color channel follow the info header.
                if (this.infoHeader.Compression == BmpCompression.BitFields)
                {
                    var bitfieldsBuffer = new byte[12];
                    this.stream.Read(bitfieldsBuffer, 0, 12);
                    Span<byte> data = bitfieldsBuffer.AsSpan();
                    this.infoHeader.RedMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                    this.infoHeader.GreenMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
                    this.infoHeader.BlueMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
                }
                else if (this.infoHeader.Compression == BmpCompression.BI_ALPHABITFIELDS)
                {
                    var bitfieldsBuffer = new byte[16];
                    this.stream.Read(bitfieldsBuffer, 0, 16);
                    Span<byte> data = bitfieldsBuffer.AsSpan();
                    this.infoHeader.RedMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                    this.infoHeader.GreenMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
                    this.infoHeader.BlueMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
                    this.infoHeader.AlphaMask = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
                }
            }
            else if (headerSize == BmpInfoHeader.AdobeV3Size)
            {
                // == 52 bytes
                infoHeaderType = BmpInfoHeaderType.AdobeVersion3;
                this.infoHeader = BmpInfoHeader.ParseAdobeV3(buffer, withAlpha: false);
            }
            else if (headerSize == BmpInfoHeader.AdobeV3WithAlphaSize)
            {
                // == 56 bytes
                infoHeaderType = BmpInfoHeaderType.AdobeVersion3WithAlpha;
                this.infoHeader = BmpInfoHeader.ParseAdobeV3(buffer, withAlpha: true);
            }
            else if (headerSize == BmpInfoHeader.Os2v2Size)
            {
                // == 64 bytes
                infoHeaderType = BmpInfoHeaderType.Os2Version2;
                this.infoHeader = BmpInfoHeader.ParseOs2Version2(buffer);
            }
            else if (headerSize >= BmpInfoHeader.SizeV4)
            {
                // >= 108 bytes
                infoHeaderType = headerSize == BmpInfoHeader.SizeV4 ? BmpInfoHeaderType.WinVersion4 : BmpInfoHeaderType.WinVersion5;
                this.infoHeader = BmpInfoHeader.ParseV4(buffer);
            }
            else
            {
                BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. HeaderSize '{headerSize}'.");
            }

            // Resolution is stored in PPM.
            var meta = new ImageMetadata
            {
                ResolutionUnits = PixelResolutionUnit.PixelsPerMeter
            };
            if (this.infoHeader.XPelsPerMeter > 0 && this.infoHeader.YPelsPerMeter > 0)
            {
                meta.HorizontalResolution = this.infoHeader.XPelsPerMeter;
                meta.VerticalResolution = this.infoHeader.YPelsPerMeter;
            }
            else
            {
                // Convert default metadata values to PPM.
                meta.HorizontalResolution = Math.Round(UnitConverter.InchToMeter(ImageMetadata.DefaultHorizontalResolution));
                meta.VerticalResolution = Math.Round(UnitConverter.InchToMeter(ImageMetadata.DefaultVerticalResolution));
            }

            this.metadata = meta;

            short bitsPerPixel = this.infoHeader.BitsPerPixel;
            this.bmpMetadata = this.metadata.GetBmpMetadata();
            this.bmpMetadata.InfoHeaderType = infoHeaderType;

            // We can only encode at these bit rates so far (1 bit and 4 bit are still missing).
            if (bitsPerPixel.Equals((short)BmpBitsPerPixel.Pixel8)
                || bitsPerPixel.Equals((short)BmpBitsPerPixel.Pixel16)
                || bitsPerPixel.Equals((short)BmpBitsPerPixel.Pixel24)
                || bitsPerPixel.Equals((short)BmpBitsPerPixel.Pixel32))
            {
                this.bmpMetadata.BitsPerPixel = (BmpBitsPerPixel)bitsPerPixel;
            }
        }

        /// <summary>
        /// Reads the <see cref="BmpFileHeader"/> from the stream.
        /// </summary>
        private void ReadFileHeader()
        {
            Span<byte> buffer = stackalloc byte[BmpFileHeader.Size];
            this.stream.Read(buffer, 0, BmpFileHeader.Size);

            short fileTypeMarker = BinaryPrimitives.ReadInt16LittleEndian(buffer);
            switch (fileTypeMarker)
            {
                case BmpConstants.TypeMarkers.Bitmap:
                    this.fileMarkerType = BmpFileMarkerType.Bitmap;
                    this.fileHeader = BmpFileHeader.Parse(buffer);
                    break;
                case BmpConstants.TypeMarkers.BitmapArray:
                    this.fileMarkerType = BmpFileMarkerType.BitmapArray;

                    // Because we only decode the first bitmap in the array, the array header will be ignored.
                    // The bitmap file header of the first image follows the array header.
                    this.stream.Read(buffer, 0, BmpFileHeader.Size);
                    this.fileHeader = BmpFileHeader.Parse(buffer);
                    if (this.fileHeader.Type != BmpConstants.TypeMarkers.Bitmap)
                    {
                        BmpThrowHelper.ThrowNotSupportedException($"Unsupported bitmap file inside a BitmapArray file. File header bitmap type marker '{this.fileHeader.Type}'.");
                    }

                    break;

                default:
                    BmpThrowHelper.ThrowNotSupportedException($"ImageSharp does not support this BMP file. File header bitmap type marker '{fileTypeMarker}'.");
                    break;
            }
        }

        /// <summary>
        /// Reads the <see cref="BmpFileHeader"/> and <see cref="BmpInfoHeader"/> from the stream and sets the corresponding fields.
        /// </summary>
        /// <returns>Bytes per color palette entry. Usually 4 bytes, but in case of Windows 2.x bitmaps or OS/2 1.x bitmaps
        /// the bytes per color palette entry's can be 3 bytes instead of 4.</returns>
        private int ReadImageHeaders(BufferedReadStream stream, out bool inverted, out byte[] palette)
        {
            this.stream = stream;

            this.ReadFileHeader();
            this.ReadInfoHeader();

            // see http://www.drdobbs.com/architecture-and-design/the-bmp-file-format-part-1/184409517
            // If the height is negative, then this is a Windows bitmap whose origin
            // is the upper-left corner and not the lower-left. The inverted flag
            // indicates a lower-left origin.Our code will be outputting an
            // upper-left origin pixel array.
            inverted = false;
            if (this.infoHeader.Height < 0)
            {
                inverted = true;
                this.infoHeader.Height = -this.infoHeader.Height;
            }

            int bytesPerColorMapEntry = 4;
            int colorMapSizeBytes = -1;
            if (this.infoHeader.ClrUsed == 0)
            {
                if (this.infoHeader.BitsPerPixel == 1
                    || this.infoHeader.BitsPerPixel == 4
                    || this.infoHeader.BitsPerPixel == 8)
                {
                    switch (this.fileMarkerType)
                    {
                        case BmpFileMarkerType.Bitmap:
                            colorMapSizeBytes = this.fileHeader.Offset - BmpFileHeader.Size - this.infoHeader.HeaderSize;
                            int colorCountForBitDepth = ColorNumerics.GetColorCountForBitDepth(this.infoHeader.BitsPerPixel);
                            bytesPerColorMapEntry = colorMapSizeBytes / colorCountForBitDepth;

                            // Edge case for less-than-full-sized palette: bytesPerColorMapEntry should be at least 3.
                            bytesPerColorMapEntry = Math.Max(bytesPerColorMapEntry, 3);

                            break;
                        case BmpFileMarkerType.BitmapArray:
                        case BmpFileMarkerType.ColorIcon:
                        case BmpFileMarkerType.ColorPointer:
                        case BmpFileMarkerType.Icon:
                        case BmpFileMarkerType.Pointer:
                            // OS/2 bitmaps always have 3 colors per color palette entry.
                            bytesPerColorMapEntry = 3;
                            colorMapSizeBytes = ColorNumerics.GetColorCountForBitDepth(this.infoHeader.BitsPerPixel) * bytesPerColorMapEntry;
                            break;
                    }
                }
            }
            else
            {
                colorMapSizeBytes = this.infoHeader.ClrUsed * bytesPerColorMapEntry;
            }

            palette = null;

            if (colorMapSizeBytes > 0)
            {
                // Usually the color palette is 1024 byte (256 colors * 4), but the documentation does not mention a size limit.
                // Make sure, that we will not read pass the bitmap offset (starting position of image data).
                if ((this.stream.Position + colorMapSizeBytes) > this.fileHeader.Offset)
                {
                    BmpThrowHelper.ThrowInvalidImageContentException(
                        $"Reading the color map would read beyond the bitmap offset. Either the color map size of '{colorMapSizeBytes}' is invalid or the bitmap offset.");
                }

                palette = new byte[colorMapSizeBytes];

                this.stream.Read(palette, 0, colorMapSizeBytes);
            }

            this.infoHeader.VerifyDimensions();

            int skipAmount = this.fileHeader.Offset - (int)this.stream.Position;
            if ((skipAmount + (int)this.stream.Position) > this.stream.Length)
            {
                BmpThrowHelper.ThrowInvalidImageContentException("Invalid fileheader offset found. Offset is greater than the stream length.");
            }

            if (skipAmount > 0)
            {
                this.stream.Skip(skipAmount);
            }

            return bytesPerColorMapEntry;
        }
    }
}

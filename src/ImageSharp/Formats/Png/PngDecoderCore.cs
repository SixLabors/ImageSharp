// <copyright file="PngDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using static ComparableExtensions;

    /// <summary>
    /// Performs the png decoding operation.
    /// </summary>
    internal class PngDecoderCore
    {
        /// <summary>
        /// The dictionary of available color types.
        /// </summary>
        private static readonly Dictionary<int, byte[]> ColorTypes = new Dictionary<int, byte[]>();

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
        /// Reusable buffer for reading char arrays.
        /// </summary>
        private readonly char[] chars = new char[4];

        /// <summary>
        /// Reusable crc for validating chunks.
        /// </summary>
        private readonly Crc32 crc = new Crc32();

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
        /// Initializes static members of the <see cref="PngDecoderCore"/> class.
        /// </summary>
        static PngDecoderCore()
        {
            ColorTypes.Add((int)PngColorType.Grayscale, new byte[] { 1, 2, 4, 8 });

            ColorTypes.Add((int)PngColorType.Rgb, new byte[] { 8 });

            ColorTypes.Add((int)PngColorType.Palette, new byte[] { 1, 2, 4, 8 });

            ColorTypes.Add((int)PngColorType.GrayscaleWithAlpha, new byte[] { 8 });

            ColorTypes.Add((int)PngColorType.RgbWithAlpha, new byte[] { 8 });
        }

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        public PngColorType PngColorType { get; set; }

        /// <summary>
        /// Decodes the stream to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to decode to.</param>
        /// <param name="stream">The stream containing image data. </param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the stream does not contain and end chunk.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if the image is larger than the maximum allowable size.
        /// </exception>
        public void Decode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            Image<TColor, TPacked> currentImage = image;
            this.currentStream = stream;
            this.currentStream.Skip(8);

            bool isEndChunkReached = false;

            using (MemoryStream dataStream = new MemoryStream())
            {
                PngChunk currentChunk;
                while ((currentChunk = this.ReadChunk()) != null)
                {
                    if (isEndChunkReached)
                    {
                        throw new ImageFormatException("Image does not end with end chunk.");
                    }

                    try
                    {
                        switch (currentChunk.Type)
                        {
                            case PngChunkTypes.Header:
                                this.ReadHeaderChunk(currentChunk.Data);
                                this.ValidateHeader();
                                break;
                            case PngChunkTypes.Physical:
                                this.ReadPhysicalChunk(currentImage, currentChunk.Data);
                                break;
                            case PngChunkTypes.Data:
                                dataStream.Write(currentChunk.Data, 0, currentChunk.Length);
                                break;
                            case PngChunkTypes.Palette:
                                byte[] pal = new byte[currentChunk.Length];
                                Buffer.BlockCopy(currentChunk.Data, 0, pal, 0, currentChunk.Length);
                                this.palette = pal;
                                image.Quality = pal.Length / 3;
                                break;
                            case PngChunkTypes.PaletteAlpha:
                                byte[] alpha = new byte[currentChunk.Length];
                                Buffer.BlockCopy(currentChunk.Data, 0, alpha, 0, currentChunk.Length);
                                this.paletteAlpha = alpha;
                                break;
                            case PngChunkTypes.Text:
                                this.ReadTextChunk(currentImage, currentChunk.Data, currentChunk.Length);
                                break;
                            case PngChunkTypes.End:
                                isEndChunkReached = true;
                                break;
                        }
                    }
                    finally
                    {
                        // Data is rented in ReadChunkData()
                        ArrayPool<byte>.Shared.Return(currentChunk.Data);
                    }
                }

                if (this.header.Width > image.MaxWidth || this.header.Height > image.MaxHeight)
                {
                    throw new ArgumentOutOfRangeException($"The input png '{this.header.Width}x{this.header.Height}' is bigger than the max allowed size '{image.MaxWidth}x{image.MaxHeight}'");
                }

                image.InitPixels(this.header.Width, this.header.Height);

                using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
                {
                    this.ReadScanlines(dataStream, pixels);
                }
            }
        }

        /// <summary>
        /// Reads the data chunk containing physical dimension data.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to read to.</param>
        /// <param name="data">The data containing physical data.</param>
        private void ReadPhysicalChunk<TColor, TPacked>(Image<TColor, TPacked> image, byte[] data)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            data.ReverseBytes(0, 4);
            data.ReverseBytes(4, 4);

            // 39.3700787 = inches in a meter.
            image.HorizontalResolution = BitConverter.ToInt32(data, 0) / 39.3700787d;
            image.VerticalResolution = BitConverter.ToInt32(data, 4) / 39.3700787d;
        }

        /// <summary>
        /// Calculates the correct number of bytes per pixel for the given color type.
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        private int CalculateBytesPerPixel()
        {
            switch (this.PngColorType)
            {
                case PngColorType.Grayscale:
                    return 1;

                case PngColorType.GrayscaleWithAlpha:
                    return 2;

                case PngColorType.Palette:
                    return 1;

                case PngColorType.Rgb:
                    return 3;

                // PngColorType.RgbWithAlpha:
                default:
                    return 4;
            }
        }

        /// <summary>
        /// Calculates the scanline length.
        /// </summary>
        /// <returns>The <see cref="int"/> representing the length.</returns>
        private int CalculateScanlineLength()
        {
            int scanlineLength = this.header.Width * this.header.BitDepth * this.bytesPerPixel;

            int amount = scanlineLength % 8;
            if (amount != 0)
            {
                scanlineLength += 8 - amount;
            }

            return scanlineLength / 8;
        }

        /// <summary>
        /// Reads the scanlines within the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="dataStream">The <see cref="MemoryStream"/> containing data.</param>
        /// <param name="pixels"> The pixel data.</param>
        private void ReadScanlines<TColor, TPacked>(MemoryStream dataStream, PixelAccessor<TColor, TPacked> pixels)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            this.bytesPerPixel = this.CalculateBytesPerPixel();
            this.bytesPerScanline = this.CalculateScanlineLength() + 1;
            this.bytesPerSample = 1;
            if (this.header.BitDepth >= 8)
            {
                this.bytesPerSample = this.header.BitDepth / 8;
            }

            dataStream.Position = 0;
            using (ZlibInflateStream compressedStream = new ZlibInflateStream(dataStream))
            {
                this.DecodePixelData(compressedStream, pixels);
            }
        }

        /// <summary>
        /// Decodes the raw pixel data row by row
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="compressedStream">The compressed pixel data stream.</param>
        /// <param name="pixels">The image pixel accessor.</param>
        private void DecodePixelData<TColor, TPacked>(Stream compressedStream, PixelAccessor<TColor, TPacked> pixels)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            byte[] previousScanline = ArrayPool<byte>.Shared.Rent(this.bytesPerScanline);
            byte[] scanline = ArrayPool<byte>.Shared.Rent(this.bytesPerScanline);

            try
            {
                for (int y = 0; y < this.header.Height; y++)
                {
                    compressedStream.Read(scanline, 0, this.bytesPerScanline);

                    FilterType filterType = (FilterType)scanline[0];

                    switch (filterType)
                    {
                        case FilterType.None:

                            NoneFilter.Decode(scanline);

                            break;

                        case FilterType.Sub:

                            SubFilter.Decode(scanline, this.bytesPerPixel);

                            break;

                        case FilterType.Up:

                            UpFilter.Decode(scanline, previousScanline);

                            break;

                        case FilterType.Average:

                            AverageFilter.Decode(scanline, previousScanline, this.bytesPerPixel);

                            break;

                        case FilterType.Paeth:

                            PaethFilter.Decode(scanline, previousScanline, this.bytesPerPixel);

                            break;

                        default:
                            throw new ImageFormatException("Unknown filter type.");
                    }

                    this.ProcessDefilteredScanline(scanline, y, pixels);

                    Swap(ref scanline, ref previousScanline);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(previousScanline);
                ArrayPool<byte>.Shared.Return(scanline);
            }
        }

        /// <summary>
        /// Processes the de-filtered scanline filling the image pixel data
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="defilteredScanline">The de-filtered scanline</param>
        /// <param name="row">The current image row.</param>
        /// <param name="pixels">The image pixels</param>
        private void ProcessDefilteredScanline<TColor, TPacked>(byte[] defilteredScanline, int row, PixelAccessor<TColor, TPacked> pixels)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            TColor color = default(TColor);
            switch (this.PngColorType)
            {
                case PngColorType.Grayscale:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * this.bytesPerPixel);

                        byte intensity = defilteredScanline[offset];
                        color.PackFromBytes(intensity, intensity, intensity, 255);
                        pixels[x, row] = color;
                    }

                    break;

                case PngColorType.GrayscaleWithAlpha:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * this.bytesPerPixel);

                        byte intensity = defilteredScanline[offset];
                        byte alpha = defilteredScanline[offset + this.bytesPerSample];

                        color.PackFromBytes(intensity, intensity, intensity, alpha);
                        pixels[x, row] = color;
                    }

                    break;

                case PngColorType.Palette:

                    byte[] newScanline = defilteredScanline.ToArrayByBitsLength(this.header.BitDepth);

                    if (this.paletteAlpha != null && this.paletteAlpha.Length > 0)
                    {
                        // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                        // channel and we should try to read it.
                        for (int x = 0; x < this.header.Width; x++)
                        {
                            int index = newScanline[x];
                            int pixelOffset = index * 3;

                            byte a = this.paletteAlpha.Length > index ? this.paletteAlpha[index] : (byte)255;

                            if (a > 0)
                            {
                                byte r = this.palette[pixelOffset];
                                byte g = this.palette[pixelOffset + 1];
                                byte b = this.palette[pixelOffset + 2];
                                color.PackFromBytes(r, g, b, a);
                            }
                            else
                            {
                                color.PackFromBytes(0, 0, 0, 0);
                            }

                            pixels[x, row] = color;
                        }
                    }
                    else
                    {
                        for (int x = 0; x < this.header.Width; x++)
                        {
                            int index = newScanline[x];
                            int pixelOffset = index * 3;

                            byte r = this.palette[pixelOffset];
                            byte g = this.palette[pixelOffset + 1];
                            byte b = this.palette[pixelOffset + 2];

                            color.PackFromBytes(r, g, b, 255);
                            pixels[x, row] = color;
                        }
                    }

                    break;

                case PngColorType.Rgb:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * this.bytesPerPixel);

                        byte r = defilteredScanline[offset];
                        byte g = defilteredScanline[offset + this.bytesPerSample];
                        byte b = defilteredScanline[offset + (2 * this.bytesPerSample)];

                        color.PackFromBytes(r, g, b, 255);
                        pixels[x, row] = color;
                    }

                    break;

                case PngColorType.RgbWithAlpha:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * this.bytesPerPixel);

                        byte r = defilteredScanline[offset];
                        byte g = defilteredScanline[offset + this.bytesPerSample];
                        byte b = defilteredScanline[offset + (2 * this.bytesPerSample)];
                        byte a = defilteredScanline[offset + (3 * this.bytesPerSample)];

                        color.PackFromBytes(r, g, b, a);
                        pixels[x, row] = color;
                    }

                    break;
            }
        }

        /// <summary>
        /// Reads a text chunk containing image properties from the data.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The image to decode to.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing  data.</param>
        /// <param name="length">The maximum length to read.</param>
        private void ReadTextChunk<TColor, TPacked>(Image<TColor, TPacked> image, byte[] data, int length)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            int zeroIndex = 0;

            for (int i = 0; i < length; i++)
            {
                if (data[i] == 0)
                {
                    zeroIndex = i;
                    break;
                }
            }

            string name = Encoding.Unicode.GetString(data, 0, zeroIndex);
            string value = Encoding.Unicode.GetString(data, zeroIndex + 1, length - zeroIndex - 1);

            image.Properties.Add(new ImageProperty(name, value));
        }

        /// <summary>
        /// Reads a header chunk from the data.
        /// </summary>
        /// <param name="data">The <see cref="T:byte[]"/> containing  data.</param>
        private void ReadHeaderChunk(byte[] data)
        {
            this.header = new PngHeader();

            data.ReverseBytes(0, 4);
            data.ReverseBytes(4, 4);

            this.header.Width = BitConverter.ToInt32(data, 0);
            this.header.Height = BitConverter.ToInt32(data, 4);

            this.header.BitDepth = data[8];
            this.header.ColorType = data[9];
            this.header.CompressionMethod = data[10];
            this.header.FilterMethod = data[11];
            this.header.InterlaceMethod = data[12];
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

            if (this.header.InterlaceMethod != 0)
            {
                // TODO: Support interlacing
                throw new NotSupportedException("Interlacing is not supported.");
            }

            this.PngColorType = (PngColorType)this.header.ColorType;
        }

        /// <summary>
        /// Reads a chunk from the stream.
        /// </summary>
        /// <returns>
        /// The <see cref="PngChunk"/>.
        /// </returns>
        private PngChunk ReadChunk()
        {
            PngChunk chunk = new PngChunk();
            this.ReadChunkLength(chunk);
            if (chunk.Length < 0)
            {
                return null;
            }

            this.ReadChunkType(chunk);
            this.ReadChunkData(chunk);
            this.ReadChunkCrc(chunk);

            return chunk;
        }

        /// <summary>
        /// Reads the cycle redundancy chunk from the data.
        /// </summary>
        /// <param name="chunk">The chunk.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid or corrupt.
        /// </exception>
        private void ReadChunkCrc(PngChunk chunk)
        {
            int numBytes = this.currentStream.Read(this.crcBuffer, 0, 4);
            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            this.crcBuffer.ReverseBytes();

            chunk.Crc = BitConverter.ToUInt32(this.crcBuffer, 0);

            this.crc.Reset();
            this.crc.Update(this.chunkTypeBuffer);
            this.crc.Update(chunk.Data, 0, chunk.Length);

            if (this.crc.Value != chunk.Crc)
            {
                throw new ImageFormatException("CRC Error. PNG Image chunk is corrupt!");
            }
        }

        /// <summary>
        /// Reads the chunk data from the stream.
        /// </summary>
        /// <param name="chunk">The chunk.</param>
        private void ReadChunkData(PngChunk chunk)
        {
            // We rent the buffer here to return it afterwards in Decode()
            chunk.Data = ArrayPool<byte>.Shared.Rent(chunk.Length);
            this.currentStream.Read(chunk.Data, 0, chunk.Length);
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <param name="chunk">The chunk.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private void ReadChunkType(PngChunk chunk)
        {
            int numBytes = this.currentStream.Read(this.chunkTypeBuffer, 0, 4);
            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            this.chars[0] = (char)this.chunkTypeBuffer[0];
            this.chars[1] = (char)this.chunkTypeBuffer[1];
            this.chars[2] = (char)this.chunkTypeBuffer[2];
            this.chars[3] = (char)this.chunkTypeBuffer[3];

            chunk.Type = new string(this.chars);
        }

        /// <summary>
        /// Calculates the length of the given chunk.
        /// </summary>
        /// <param name="chunk">The chunk.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private void ReadChunkLength(PngChunk chunk)
        {
            int numBytes = this.currentStream.Read(this.chunkLengthBuffer, 0, 4);
            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            if (numBytes <= 0)
            {
                chunk.Length = -1;
                return;
            }

            this.chunkLengthBuffer.ReverseBytes();

            chunk.Length = BitConverter.ToInt32(this.chunkLengthBuffer, 0);
        }
    }
}
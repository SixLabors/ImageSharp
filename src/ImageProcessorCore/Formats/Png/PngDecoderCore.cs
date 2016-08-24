// <copyright file="PngDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Numerics;

namespace ImageProcessorCore.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Performs the png decoding operation.
    /// </summary>
    internal class PngDecoderCore
    {
        ///// <summary>
        ///// The dictionary of available color types.
        ///// </summary>
        private static readonly Dictionary<int, byte[]> ColorTypes = new Dictionary<int, byte[]>();

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
        /// The palette containing color information for indexed pngs
        /// </summary>
        private byte[] palette;

        /// <summary>
        /// The palette containing alpha channel color information for indexed pngs
        /// </summary>
        private byte[] paletteAlpha;

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        public PngColorType PngColorType { get; set; }

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
        /// Decodes the stream to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam> 
        /// <param name="image">The image to decode to.</param>
        /// <param name="stream">The stream containing image data. </param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the stream does not contain and end chunk.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the image is larger than the maximum allowable size.
        /// </exception>
        public void Decode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Image<TColor, TPacked> currentImage = image;
            this.currentStream = stream;
            this.currentStream.Seek(8, SeekOrigin.Current);

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

                    if (currentChunk.Type == PngChunkTypes.Header)
                    {
                        this.ReadHeaderChunk(currentChunk.Data);
                        this.ValidateHeader();
                    }
                    else if (currentChunk.Type == PngChunkTypes.Physical)
                    {
                        this.ReadPhysicalChunk(currentImage, currentChunk.Data);
                    }
                    else if (currentChunk.Type == PngChunkTypes.Data)
                    {
                        dataStream.Write(currentChunk.Data, 0, currentChunk.Data.Length);
                    }
                    else if (currentChunk.Type == PngChunkTypes.Palette)
                    {
                        this.palette = currentChunk.Data;
                        image.Quality = this.palette.Length / 3;
                    }
                    else if (currentChunk.Type == PngChunkTypes.PaletteAlpha)
                    {
                        this.paletteAlpha = currentChunk.Data;
                    }
                    else if (currentChunk.Type == PngChunkTypes.Text)
                    {
                        this.ReadTextChunk(currentImage, currentChunk.Data);
                    }
                    else if (currentChunk.Type == PngChunkTypes.End)
                    {
                        isEndChunkReached = true;
                    }
                }

                if (this.header.Width > image.MaxWidth || this.header.Height > image.MaxHeight)
                {
                    throw new ArgumentOutOfRangeException(
                        $"The input png '{this.header.Width}x{this.header.Height}' is bigger than the "
                        + $"max allowed size '{image.MaxWidth}x{image.MaxHeight}'");
                }

                TColor[] pixels = new TColor[this.header.Width * this.header.Height];


                this.ReadScanlines<TColor, TPacked>(dataStream, pixels);


                image.SetPixels(this.header.Width, this.header.Height, pixels);
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
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Array.Reverse(data, 0, 4);
            Array.Reverse(data, 4, 4);

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

                // PngColorType.RgbWithAlpha
                // TODO: Maybe figure out a way to detect if there are any transparent
                // pixels and encode RGB if none.
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
        /// <param name="dataStream">The <see cref="MemoryStream"/> containing data.</param>
        /// <param name="pixels">
        /// The <see cref="T:floaTColor[]"/> containing pixel data.</param>
        private void ReadScanlines<TColor, TPacked>(MemoryStream dataStream, TColor[] pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            this.bytesPerPixel = this.CalculateBytesPerPixel();
            this.bytesPerScanline = this.CalculateScanlineLength() + 1;
            this.bytesPerSample = 1;
            if (this.header.BitDepth >= 8)
            {
                this.bytesPerSample = (this.header.BitDepth) / 8;
            }

            dataStream.Position = 0;
            using (ZlibInflateStream compressedStream = new ZlibInflateStream(dataStream))
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    compressedStream.CopyTo(decompressedStream);
                    decompressedStream.Flush();

                    byte[] decompressedBytes = decompressedStream.ToArray();
                    DecodePixelData<TColor, TPacked>(decompressedBytes, pixels);
                }
            }
        }

        /// <summary>
        /// Decodes the raw pixel data row by row
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="pixelData">The pixel data.</param>
        /// <param name="pixels">The image pixels.</param>
        private void DecodePixelData<TColor, TPacked>(byte[] pixelData, TColor[] pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            byte[] previousScanline = new byte[this.bytesPerScanline];

            for (int y = 0; y < this.header.Height; y++)
            {
                byte[] scanline = new byte[this.bytesPerScanline];
                Array.Copy(pixelData, y * this.bytesPerScanline, scanline, 0, this.bytesPerScanline);
                FilterType filterType = (FilterType)scanline[0];
                byte[] defilteredScanline;

                switch (filterType)
                {
                    case FilterType.None:

                        defilteredScanline = NoneFilter.Decode(scanline);

                        break;

                    case FilterType.Sub:

                        defilteredScanline = SubFilter.Decode(scanline, bytesPerPixel);

                        break;

                    case FilterType.Up:

                        defilteredScanline = UpFilter.Decode(scanline, previousScanline);

                        break;

                    case FilterType.Average:

                        defilteredScanline = AverageFilter.Decode(scanline, previousScanline, bytesPerPixel);

                        break;

                    case FilterType.Paeth:

                        defilteredScanline = PaethFilter.Decode(scanline, previousScanline, bytesPerPixel);

                        break;

                    default:
                        throw new ImageFormatException("Unknown filter type.");
                }

                previousScanline = defilteredScanline;
                ProcessDefilteredScanline<TColor, TPacked>(defilteredScanline, y, pixels);
            }
        }

        /// <summary>
        /// Processes the defiltered scanline filling the image pixel data
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="defilteredScanline"></param>
        /// <param name="row">The current image row.</param>
        /// <param name="pixels">The image pixels</param>
        private void ProcessDefilteredScanline<TColor, TPacked>(byte[] defilteredScanline, int row, TColor[] pixels)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            switch (this.PngColorType)
            {
                case PngColorType.Grayscale:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * bytesPerPixel);

                        byte intensity = defilteredScanline[offset];

                        TColor color = default(TColor);
                        color.PackFromVector4(new Vector4(intensity, intensity, intensity, 255) / 255F);
                        pixels[(row * this.header.Width) + x] = color;
                    }

                    break;

                case PngColorType.GrayscaleWithAlpha:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * bytesPerPixel);

                        byte intensity = defilteredScanline[offset];
                        byte alpha = defilteredScanline[offset + bytesPerSample];

                        TColor color = default(TColor);
                        color.PackFromVector4(new Vector4(intensity, intensity, intensity, alpha) / 255F);
                        pixels[(row * this.header.Width) + x] = color;
                    }

                    break;

                case PngColorType.Palette:

                    byte[] newScanline = defilteredScanline.ToArrayByBitsLength(header.BitDepth);

                    if (this.paletteAlpha != null && this.paletteAlpha.Length > 0)
                    {
                        // If the alpha palette is not null and has one or more entries, this means, that the image contains an alpha
                        // channel and we should try to read it.
                        for (int i = 0; i < header.Width; i++)
                        {
                            int index = newScanline[i];
                            int offset = (row * header.Width) + i;
                            int pixelOffset = index * 3;

                            byte a = this.paletteAlpha.Length > index ? this.paletteAlpha[index] : (byte)255;
                            TColor color = default(TColor);
                            if (a > 0)
                            {
                                byte r = this.palette[pixelOffset];
                                byte g = this.palette[pixelOffset + 1];
                                byte b = this.palette[pixelOffset + 2];
                                color.PackFromVector4(new Vector4(r, g, b, a) / 255F);
                            }

                            pixels[offset] = color;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < header.Width; i++)
                        {
                            int index = newScanline[i];
                            int offset = (row * header.Width) + i;
                            int pixelOffset = index * 3;

                            byte r = this.palette[pixelOffset];
                            byte g = this.palette[pixelOffset + 1];
                            byte b = this.palette[pixelOffset + 2];

                            TColor color = default(TColor);
                            color.PackFromVector4(new Vector4(r, g, b, 255) / 255F);
                            pixels[offset] = color;
                        }
                    }

                    break;

                case PngColorType.Rgb:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * bytesPerPixel);

                        byte r = defilteredScanline[offset];
                        byte g = defilteredScanline[offset + bytesPerSample];
                        byte b = defilteredScanline[offset + 2 * bytesPerSample];

                        TColor color = default(TColor);
                        color.PackFromVector4(new Vector4(r, g, b, 255) / 255F);
                        pixels[(row * this.header.Width) + x] = color;
                    }

                    break;

                case PngColorType.RgbWithAlpha:

                    for (int x = 0; x < this.header.Width; x++)
                    {
                        int offset = 1 + (x * bytesPerPixel);

                        byte r = defilteredScanline[offset];
                        byte g = defilteredScanline[offset + bytesPerSample];
                        byte b = defilteredScanline[offset + 2 * bytesPerSample];
                        byte a = defilteredScanline[offset + 3 * bytesPerSample];

                        TColor color = default(TColor);
                        color.PackFromVector4(new Vector4(r, g, b, a) / 255F);
                        pixels[(row * this.header.Width) + x] = color;
                    }

                    break;

                default:
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
        private void ReadTextChunk<TColor, TPacked>(Image<TColor, TPacked> image, byte[] data)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            int zeroIndex = 0;

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0)
                {
                    zeroIndex = i;
                    break;
                }
            }

            string name = Encoding.Unicode.GetString(data, 0, zeroIndex);
            string value = Encoding.Unicode.GetString(data, zeroIndex + 1, data.Length - zeroIndex - 1);

            image.Properties.Add(new ImageProperty(name, value));
        }

        /// <summary>
        /// Reads a header chunk from the data.
        /// </summary>
        /// <param name="data">The <see cref="T:byte[]"/> containing  data.</param>
        private void ReadHeaderChunk(byte[] data)
        {
            this.header = new PngHeader();

            Array.Reverse(data, 0, 4);
            Array.Reverse(data, 4, 4);

            this.header.Width = BitConverter.ToInt32(data, 0);
            this.header.Height = BitConverter.ToInt32(data, 4);

            this.header.BitDepth = data[8];
            this.header.ColorType = data[9];
            this.header.FilterMethod = data[11];
            this.header.InterlaceMethod = data[12];
            this.header.CompressionMethod = data[10];
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

            if (this.ReadChunkLength(chunk) == 0)
            {
                return null;
            }

            if (chunk.Length <= 0)
            {
                return null;
            }

            byte[] typeBuffer = this.ReadChunkType(chunk);

            this.ReadChunkData(chunk);
            this.ReadChunkCrc(chunk, typeBuffer);

            return chunk;
        }

        /// <summary>
        /// Reads the cycle redundancy chunk from the data.
        /// </summary>
        /// <param name="chunk">The chunk.</param>
        /// <param name="typeBuffer">The type buffer.</param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid or corrupt.
        /// </exception>
        private void ReadChunkCrc(PngChunk chunk, byte[] typeBuffer)
        {
            byte[] crcBuffer = new byte[4];

            int numBytes = this.currentStream.Read(crcBuffer, 0, 4);
            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            Array.Reverse(crcBuffer);

            chunk.Crc = BitConverter.ToUInt32(crcBuffer, 0);

            Crc32 crc = new Crc32();
            crc.Update(typeBuffer);
            crc.Update(chunk.Data);

            if (crc.Value != chunk.Crc)
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
            chunk.Data = new byte[chunk.Length];
            this.currentStream.Read(chunk.Data, 0, chunk.Length);
        }

        /// <summary>
        /// Identifies the chunk type from the chunk.
        /// </summary>
        /// <param name="chunk">The chunk.</param>
        /// <returns>
        /// The <see cref="T:byte[]"/> containing identifying information.
        /// </returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private byte[] ReadChunkType(PngChunk chunk)
        {
            byte[] typeBuffer = new byte[4];

            int numBytes = this.currentStream.Read(typeBuffer, 0, 4);
            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            char[] chars = new char[4];
            chars[0] = (char)typeBuffer[0];
            chars[1] = (char)typeBuffer[1];
            chars[2] = (char)typeBuffer[2];
            chars[3] = (char)typeBuffer[3];

            chunk.Type = new string(chars);

            return typeBuffer;
        }

        /// <summary>
        /// Calculates the length of the given chunk.
        /// </summary>
        /// <param name="chunk">he chunk.</param>
        /// <returns>
        /// The <see cref="int"/> representing the chunk length.
        /// </returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the input stream is not valid.
        /// </exception>
        private int ReadChunkLength(PngChunk chunk)
        {
            byte[] lengthBuffer = new byte[4];

            int numBytes = this.currentStream.Read(lengthBuffer, 0, 4);
            if (numBytes >= 1 && numBytes <= 3)
            {
                throw new ImageFormatException("Image stream is not valid!");
            }

            Array.Reverse(lengthBuffer);

            chunk.Length = BitConverter.ToInt32(lengthBuffer, 0);

            return numBytes;
        }
    }
}

// <copyright file="PngDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

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
        /// <summary>
        /// The dictionary of available color types.
        /// </summary>
        private static readonly Dictionary<int, PngColorTypeInformation> ColorTypes
            = new Dictionary<int, PngColorTypeInformation>();

        /// <summary>
        /// The image to decode.
        /// </summary>
        //private IImage currentImage;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The png header.
        /// </summary>
        private PngHeader header;

        /// <summary>
        /// Initializes static members of the <see cref="PngDecoderCore"/> class.
        /// </summary>
        static PngDecoderCore()
        {
            ColorTypes.Add(
                0,
                new PngColorTypeInformation(1, new[] { 1, 2, 4, 8 }, (p, a) => new GrayscaleReader(false)));

            ColorTypes.Add(
                2,
                new PngColorTypeInformation(3, new[] { 8 }, (p, a) => new TrueColorReader(false)));

            ColorTypes.Add(
                3,
                new PngColorTypeInformation(1, new[] { 1, 2, 4, 8 }, (p, a) => new PaletteIndexReader(p, a)));

            ColorTypes.Add(
                4,
                new PngColorTypeInformation(2, new[] { 8 }, (p, a) => new GrayscaleReader(true)));

            ColorTypes.Add(6,
                new PngColorTypeInformation(4, new[] { 8 }, (p, a) => new TrueColorReader(true)));
        }

        /// <summary>
        /// Decodes the stream to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam> 
        /// <param name="image">The image to decode to.</param>
        /// <param name="stream">The stream containing image data. </param>
        /// <exception cref="ImageFormatException">
        /// Thrown if the stream does not contain and end chunk.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the image is larger than the maximum allowable size.
        /// </exception>
        public void Decode<T, TP>(Image<T, TP> image, Stream stream)
            where T : IPackedVector<TP>
            where TP : struct
        {
            Image<T, TP> currentImage = image;
            this.currentStream = stream;
            this.currentStream.Seek(8, SeekOrigin.Current);

            bool isEndChunkReached = false;

            byte[] palette = null;
            byte[] paletteAlpha = null;

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
                        palette = currentChunk.Data;
                    }
                    else if (currentChunk.Type == PngChunkTypes.PaletteAlpha)
                    {
                        paletteAlpha = currentChunk.Data;
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

                T[] pixels = new T[this.header.Width * this.header.Height];

                PngColorTypeInformation colorTypeInformation = ColorTypes[this.header.ColorType];

                if (colorTypeInformation != null)
                {
                    IColorReader colorReader = colorTypeInformation.CreateColorReader(palette, paletteAlpha);

                    this.ReadScanlines<T, TP>(dataStream, pixels, colorReader, colorTypeInformation);
                }

                image.SetPixels(this.header.Width, this.header.Height, pixels);
            }
        }

        /// <summary>
        /// Computes a simple linear function of the three neighboring pixels (left, above, upper left), then chooses
        /// as predictor the neighboring pixel closest to the computed value.
        /// </summary>
        /// <param name="left">The left neighbour pixel.</param>
        /// <param name="above">The above neighbour pixel.</param>
        /// <param name="upperLeft">The upper left neighbour pixel.</param>
        /// <returns>
        /// The <see cref="byte"/>.
        /// </returns>
        private static byte PaethPredicator(byte left, byte above, byte upperLeft)
        {
            byte predicator;

            int p = left + above - upperLeft;
            int pa = Math.Abs(p - left);
            int pb = Math.Abs(p - above);
            int pc = Math.Abs(p - upperLeft);

            if (pa <= pb && pa <= pc)
            {
                predicator = left;
            }
            else if (pb <= pc)
            {
                predicator = above;
            }
            else
            {
                predicator = upperLeft;
            }

            return predicator;
        }

        /// <summary>
        /// Reads the data chunk containing physical dimension data.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="image">The image to read to.</param>
        /// <param name="data">The data containing physical data.</param>
        private void ReadPhysicalChunk<T, TP>(Image<T, TP> image, byte[] data)
            where T : IPackedVector<TP>
            where TP : struct
        {
            Array.Reverse(data, 0, 4);
            Array.Reverse(data, 4, 4);

            // 39.3700787 = inches in a meter.
            image.HorizontalResolution = BitConverter.ToInt32(data, 0) / 39.3700787d;
            image.VerticalResolution = BitConverter.ToInt32(data, 4) / 39.3700787d;
        }

        /// <summary>
        /// Calculates the scanline length.
        /// </summary>
        /// <param name="colorTypeInformation">The color type information.</param>
        /// <returns>The <see cref="int"/> representing the length.</returns>
        private int CalculateScanlineLength(PngColorTypeInformation colorTypeInformation)
        {
            int scanlineLength = this.header.Width * this.header.BitDepth * colorTypeInformation.ChannelsPerColor;

            int amount = scanlineLength % 8;
            if (amount != 0)
            {
                scanlineLength += 8 - amount;
            }

            return scanlineLength / 8;
        }

        /// <summary>
        /// Calculates a scanline step.
        /// </summary>
        /// <param name="colorTypeInformation">The color type information.</param>
        /// <returns>The <see cref="int"/> representing the length of each step.</returns>
        private int CalculateScanlineStep(PngColorTypeInformation colorTypeInformation)
        {
            int scanlineStep = 1;

            if (this.header.BitDepth >= 8)
            {
                scanlineStep = (colorTypeInformation.ChannelsPerColor * this.header.BitDepth) / 8;
            }

            return scanlineStep;
        }

        /// <summary>
        /// Reads the scanlines within the image.
        /// </summary>
        /// <param name="dataStream">The <see cref="MemoryStream"/> containing data.</param>
        /// <param name="pixels">
        /// The <see cref="T:float[]"/> containing pixel data.</param>
        /// <param name="colorReader">The color reader.</param>
        /// <param name="colorTypeInformation">The color type information.</param>
        private void ReadScanlines<T, TP>(MemoryStream dataStream, T[] pixels, IColorReader colorReader, PngColorTypeInformation colorTypeInformation)
            where T : IPackedVector<TP>
            where TP : struct
        {
            dataStream.Position = 0;

            int scanlineLength = this.CalculateScanlineLength(colorTypeInformation);
            int scanlineStep = this.CalculateScanlineStep(colorTypeInformation);

            byte[] lastScanline = new byte[scanlineLength];
            byte[] currentScanline = new byte[scanlineLength];
            int filter = 0, column = -1;

            using (ZlibInflateStream compressedStream = new ZlibInflateStream(dataStream))
            {
                int readByte;
                while ((readByte = compressedStream.ReadByte()) >= 0)
                {
                    if (column == -1)
                    {
                        filter = readByte;

                        column++;
                    }
                    else
                    {
                        currentScanline[column] = (byte)readByte;

                        byte a;
                        byte b;
                        byte c;

                        if (column >= scanlineStep)
                        {
                            a = currentScanline[column - scanlineStep];
                            c = lastScanline[column - scanlineStep];
                        }
                        else
                        {
                            a = 0;
                            c = 0;
                        }

                        b = lastScanline[column];

                        if (filter == 1)
                        {
                            currentScanline[column] = (byte)(currentScanline[column] + a);
                        }
                        else if (filter == 2)
                        {
                            currentScanline[column] = (byte)(currentScanline[column] + b);
                        }
                        else if (filter == 3)
                        {
                            currentScanline[column] = (byte)(currentScanline[column] + (byte)((a + b) / 2));
                        }
                        else if (filter == 4)
                        {
                            currentScanline[column] = (byte)(currentScanline[column] + PaethPredicator(a, b, c));
                        }

                        column++;

                        if (column == scanlineLength)
                        {
                            colorReader.ReadScanline<T, TP>(currentScanline, pixels, this.header);
                            column = -1;

                            this.Swap(ref currentScanline, ref lastScanline);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads a text chunk containing image properties from the data.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="image">The image to decode to.</param>
        /// <param name="data">The <see cref="T:byte[]"/> containing  data.</param>
        private void ReadTextChunk<T, TP>(Image<T, TP> image, byte[] data)
            where T : IPackedVector<TP>
            where TP : struct
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
        /// <exception cref="ImageFormatException">
        /// Thrown if the image does pass validation.
        /// </exception>
        private void ValidateHeader()
        {
            if (!ColorTypes.ContainsKey(this.header.ColorType))
            {
                throw new ImageFormatException("Color type is not supported or not valid.");
            }

            if (!ColorTypes[this.header.ColorType].SupportedBitDepths.Contains(this.header.BitDepth))
            {
                throw new ImageFormatException("Bit depth is not supported or not valid.");
            }

            if (this.header.FilterMethod != 0)
            {
                throw new ImageFormatException("The png specification only defines 0 as filter method.");
            }

            if (this.header.InterlaceMethod != 0)
            {
                throw new ImageFormatException("Interlacing is not supported.");
            }
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

        /// <summary>
        /// Swaps two references.
        /// </summary>
        /// <typeparam name="TRef">The type of the references to swap.</typeparam>
        /// <param name="lhs">The first reference.</param>
        /// <param name="rhs">The second reference.</param>
        private void Swap<TRef>(ref TRef lhs, ref TRef rhs)
                    where TRef : class
        {
            TRef tmp = lhs;

            lhs = rhs;
            rhs = tmp;
        }
    }
}

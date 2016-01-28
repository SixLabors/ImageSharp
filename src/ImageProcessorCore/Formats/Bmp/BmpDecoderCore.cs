// <copyright file="BmpDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Performs the bmp decoding operation.
    /// </summary>
    internal class BmpDecoderCore
    {
        /// <summary>
        /// The mask for the red part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int Rgb16RMask = 0x00007C00;

        /// <summary>
        /// The mask for the green part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int Rgb16GMask = 0x000003E0;

        /// <summary>
        /// The mask for the blue part of the color for 16 bit rgb bitmaps.
        /// </summary>
        private const int Rgb16BMask = 0x0000001F;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The file header containing general information.
        /// TODO: Why is this not used? We advance the stream but do not use the values parsed.
        /// </summary>
        private BmpFileHeader fileHeader;

        /// <summary>
        /// The info header containing detailed information about the bitmap.
        /// </summary>
        private BmpInfoHeader infoHeader;

        /// <summary>
        /// Decodes the image from the specified this._stream and sets
        /// the data to image.
        /// </summary>
        /// <param name="image">The image, where the data should be set to.
        /// Cannot be null (Nothing in Visual Basic).</param>
        /// <param name="stream">The this._stream, where the image should be
        /// decoded from. Cannot be null (Nothing in Visual Basic).</param>
        /// <exception cref="ArgumentNullException">
        ///    <para><paramref name="image"/> is null.</para>
        ///    <para>- or -</para>
        ///    <para><paramref name="stream"/> is null.</para>
        /// </exception>
        public void Decode(Image image, Stream stream)
        {
            this.currentStream = stream;

            try
            {
                this.ReadFileHeader();
                this.ReadInfoHeader();

                int colorMapSize = -1;

                if (this.infoHeader.ClrUsed == 0)
                {
                    if (this.infoHeader.BitsPerPixel == 1 ||
                        this.infoHeader.BitsPerPixel == 4 ||
                        this.infoHeader.BitsPerPixel == 8)
                    {
                        colorMapSize = (int)Math.Pow(2, this.infoHeader.BitsPerPixel) * 4;
                    }
                }
                else
                {
                    colorMapSize = this.infoHeader.ClrUsed * 4;
                }

                byte[] palette = null;

                if (colorMapSize > 0)
                {
                    // 255 * 4
                    if (colorMapSize > 1020)
                    {
                        throw new ImageFormatException($"Invalid bmp colormap size '{colorMapSize}'");
                    }

                    palette = new byte[colorMapSize];

                    this.currentStream.Read(palette, 0, colorMapSize);
                }

                if (this.infoHeader.Width > ImageBase.MaxWidth || this.infoHeader.Height > ImageBase.MaxHeight)
                {
                    throw new ArgumentOutOfRangeException(
                        $"The input bitmap '{this.infoHeader.Width}x{this.infoHeader.Height}' is "
                        + $"bigger then the max allowed size '{ImageBase.MaxWidth}x{ImageBase.MaxHeight}'");
                }

                float[] imageData = new float[this.infoHeader.Width * this.infoHeader.Height * 4];

                switch (this.infoHeader.Compression)
                {
                    case BmpCompression.RGB:
                        if (this.infoHeader.HeaderSize != 40)
                        {
                            throw new ImageFormatException(
                                $"Header Size value '{this.infoHeader.HeaderSize}' is not valid.");
                        }

                        if (this.infoHeader.BitsPerPixel == 32)
                        {
                            this.ReadRgb32(imageData, this.infoHeader.Width, this.infoHeader.Height);
                        }
                        else if (this.infoHeader.BitsPerPixel == 24)
                        {
                            this.ReadRgb24(imageData, this.infoHeader.Width, this.infoHeader.Height);
                        }
                        else if (this.infoHeader.BitsPerPixel == 16)
                        {
                            this.ReadRgb16(imageData, this.infoHeader.Width, this.infoHeader.Height);
                        }
                        else if (this.infoHeader.BitsPerPixel <= 8)
                        {
                            this.ReadRgbPalette(
                                imageData,
                                palette,
                                this.infoHeader.Width,
                                this.infoHeader.Height,
                                this.infoHeader.BitsPerPixel);
                        }

                        break;
                    default:
                        throw new NotSupportedException("Does not support this kind of bitmap files.");
                }

                image.SetPixels(this.infoHeader.Width, this.infoHeader.Height, imageData);
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("Bitmap does not have a valid format.", e);
            }
        }

        /// <summary>
        /// Returns the y- value based on the given height.
        /// </summary>
        /// <param name="y">The y- value representing the current row.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <returns>The <see cref="int"/> representing the inverted value.</returns>
        private static int Invert(int y, int height)
        {
            int row;

            if (height > 0)
            {
                row = height - y - 1;
            }
            else
            {
                row = y;
            }

            return row;
        }

        /// <summary>
        /// Reads the color palette from the stream.
        /// </summary>
        /// <param name="imageData">The <see cref="T:float[]"/> image data to assign the palette to.</param>
        /// <param name="colors">The <see cref="T:byte[]"/> containing the colors.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        /// <param name="bits">The number of bits per pixel.</param>
        private void ReadRgbPalette(float[] imageData, byte[] colors, int width, int height, int bits)
        {
            // Pixels per byte (bits per pixel)
            int ppb = 8 / bits;

            int arrayWidth = (width + ppb - 1) / ppb;

            // Bit mask
            int mask = 0xFF >> (8 - bits);

            byte[] data = new byte[arrayWidth * height];

            this.currentStream.Read(data, 0, data.Length);

            // Rows are aligned on 4 byte boundaries
            int alignment = arrayWidth % 4;
            if (alignment != 0)
            {
                alignment = 4 - alignment;
            }

            Parallel.For(
                0,
                height,
                y =>
                    {
                        int rowOffset = y * (arrayWidth + alignment);

                        for (int x = 0; x < arrayWidth; x++)
                        {
                            int offset = rowOffset + x;

                            // Revert the y value, because bitmaps are saved from down to top
                            int row = Invert(y, height);

                            int colOffset = x * ppb;

                            for (int shift = 0; shift < ppb && (colOffset + shift) < width; shift++)
                            {
                                int colorIndex = ((data[offset] >> (8 - bits - (shift * bits))) & mask) * 4;
                                int arrayOffset = ((row * width) + (colOffset + shift)) * 4;

                                // We divide by 255 as we will store the colors in our floating point format.
                                // Stored in r-> g-> b-> a order.
                                imageData[arrayOffset] = colors[colorIndex + 2] / 255f; // r
                                imageData[arrayOffset + 1] = colors[colorIndex + 1] / 255f; // g
                                imageData[arrayOffset + 2] = colors[colorIndex] / 255f; // b
                                imageData[arrayOffset + 3] = 1; // a
                            }
                        }
                    });
        }

        /// <summary>
        /// Reads the 16 bit color palette from the stream
        /// </summary>
        /// <param name="imageData">The <see cref="T:float[]"/> image data to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        private void ReadRgb16(float[] imageData, int width, int height)
        {
            // We divide here as we will store the colors in our floating point format.
            const float ScaleR = 0.25F; // (256 / 32) / 32
            const float ScaleG = 0.0625F; // (256 / 64) / 64

            int alignment;
            byte[] data = this.GetImageArray(width, height, 2, out alignment);

            Parallel.For(
                0,
                height,
                y =>
                    {
                        int rowOffset = y * ((width * 2) + alignment);

                        // Revert the y value, because bitmaps are saved from down to top
                        int row = Invert(y, height);

                        for (int x = 0; x < width; x++)
                        {
                            int offset = rowOffset + (x * 2);

                            short temp = BitConverter.ToInt16(data, offset);

                            float r = ((temp & Rgb16RMask) >> 11) * ScaleR;
                            float g = ((temp & Rgb16GMask) >> 5) * ScaleG;
                            float b = (temp & Rgb16BMask) * ScaleR;

                            int arrayOffset = ((row * width) + x) * 4;

                            // Stored in r-> g-> b-> a order.
                            imageData[arrayOffset] = r;
                            imageData[arrayOffset + 1] = g;
                            imageData[arrayOffset + 2] = b;
                            imageData[arrayOffset + 3] = 1;
                        }
                    });
        }

        /// <summary>
        /// Reads the 24 bit color palette from the stream
        /// </summary>
        /// <param name="imageData">The <see cref="T:float[]"/> image data to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        private void ReadRgb24(float[] imageData, int width, int height)
        {
            int alignment;
            byte[] data = this.GetImageArray(width, height, 3, out alignment);

            Parallel.For(
                0,
                height,
                y =>
                    {
                        int rowOffset = y * ((width * 3) + alignment);

                        // Revert the y value, because bitmaps are saved from down to top
                        int row = Invert(y, height);

                        for (int x = 0; x < width; x++)
                        {
                            int offset = rowOffset + (x * 3);
                            int arrayOffset = ((row * width) + x) * 4;

                            // We divide by 255 as we will store the colors in our floating point format.
                            // Stored in r-> g-> b-> a order.
                            imageData[arrayOffset] = data[offset + 2] / 255f;
                            imageData[arrayOffset + 1] = data[offset + 1] / 255f;
                            imageData[arrayOffset + 2] = data[offset] / 255f;
                            imageData[arrayOffset + 3] = 1;
                        }
                    });
        }

        /// <summary>
        /// Reads the 32 bit color palette from the stream
        /// </summary>
        /// <param name="imageData">The <see cref="T:float[]"/> image data to assign the palette to.</param>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height of the bitmap.</param>
        private void ReadRgb32(float[] imageData, int width, int height)
        {
            int alignment;
            byte[] data = this.GetImageArray(width, height, 4, out alignment);

            Parallel.For(
                0,
                height,
                y =>
                    {
                        int rowOffset = y * ((width * 4) + alignment);

                        // Revert the y value, because bitmaps are saved from down to top
                        int row = Invert(y, height);

                        for (int x = 0; x < width; x++)
                        {
                            int offset = rowOffset + (x * 4);
                            int arrayOffset = ((row * width) + x) * 4;

                            // We divide by 255 as we will store the colors in our floating point format.
                            // Stored in r-> g-> b-> a order.
                            imageData[arrayOffset] = data[offset + 2] / 255f;
                            imageData[arrayOffset + 1] = data[offset + 1] / 255f;
                            imageData[arrayOffset + 2] = data[offset] / 255f;
                            imageData[arrayOffset + 3] = 1; // TODO: Can we use our real alpha here?
                        }
                    });
        }

        /// <summary>
        /// Returns a <see cref="T:byte[]"/> containing the pixels for the current bitmap.
        /// </summary>
        /// <param name="width">The width of the bitmap.</param>
        /// <param name="height">The height.</param>
        /// <param name="bytes">The number of bytes per pixel.</param>
        /// <param name="alignment">The alignment of the pixels.</param>
        /// <returns>
        /// The <see cref="T:byte[]"/> containing the pixels.
        /// </returns>
        private byte[] GetImageArray(int width, int height, int bytes, out int alignment)
        {
            int dataWidth = width;

            alignment = (width * bytes) % 4;

            if (alignment != 0)
            {
                alignment = 4 - alignment;
            }

            int size = ((dataWidth * bytes) + alignment) * height;

            byte[] data = new byte[size];

            this.currentStream.Read(data, 0, size);

            return data;
        }

        /// <summary>
        /// Reads the <see cref="BmpInfoHeader"/> from the stream.
        /// </summary>
        private void ReadInfoHeader()
        {
            byte[] data = new byte[BmpInfoHeader.Size];

            this.currentStream.Read(data, 0, BmpInfoHeader.Size);

            this.infoHeader = new BmpInfoHeader
            {
                HeaderSize = BitConverter.ToInt32(data, 0),
                Width = BitConverter.ToInt32(data, 4),
                Height = BitConverter.ToInt32(data, 8),
                Planes = BitConverter.ToInt16(data, 12),
                BitsPerPixel = BitConverter.ToInt16(data, 14),
                ImageSize = BitConverter.ToInt32(data, 20),
                XPelsPerMeter = BitConverter.ToInt32(data, 24),
                YPelsPerMeter = BitConverter.ToInt32(data, 28),
                ClrUsed = BitConverter.ToInt32(data, 32),
                ClrImportant = BitConverter.ToInt32(data, 36),
                Compression = (BmpCompression)BitConverter.ToInt32(data, 16)
            };
        }

        /// <summary>
        /// Reads the <see cref="BmpFileHeader"/> from the stream.
        /// </summary>
        private void ReadFileHeader()
        {
            byte[] data = new byte[BmpFileHeader.Size];

            this.currentStream.Read(data, 0, BmpFileHeader.Size);

            this.fileHeader = new BmpFileHeader
            {
                Type = BitConverter.ToInt16(data, 0),
                FileSize = BitConverter.ToInt32(data, 2),
                Reserved = BitConverter.ToInt32(data, 6),
                Offset = BitConverter.ToInt32(data, 10)
            };
        }
    }
}

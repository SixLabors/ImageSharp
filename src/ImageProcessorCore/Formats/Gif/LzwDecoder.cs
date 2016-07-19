// <copyright file="LzwDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Decompresses and decodes data using the dynamic LZW algorithms.
    /// </summary>
    internal sealed class LzwDecoder
    {
        /// <summary>
        /// The max decoder pixel stack size.
        /// </summary>
        private const int MaxStackSize = 4096;

        /// <summary>
        /// The null code.
        /// </summary>
        private const int NullCode = -1;

        /// <summary>
        /// The stream to decode.
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwDecoder"/> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public LzwDecoder(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            this.stream = stream;
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices from the stream.
        /// <remarks>
        /// </remarks>
        /// </summary>
        /// <param name="width">The width of the pixel index array.</param>
        /// <param name="height">The height of the pixel index array.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <returns>The decoded and uncompressed array.</returns>
        public byte[] DecodePixels(int width, int height, int dataSize)
        {
            Guard.MustBeLessThan(dataSize, int.MaxValue, nameof(dataSize));

            // The resulting index table.
            byte[] pixels = new byte[width * height];

            // Calculate the clear code. The value of the clear code is 2 ^ dataSize
            int clearCode = 1 << dataSize;

            int codeSize = dataSize + 1;

            // Calculate the end code
            int endCode = clearCode + 1;

            // Calculate the available code.
            int availableCode = clearCode + 2;

            // Jillzhangs Code see: http://giflib.codeplex.com/
            // Adapted from John Cristy's ImageMagick.
            int code;
            int oldCode = NullCode;
            int codeMask = (1 << codeSize) - 1;
            int bits = 0;

            int[] prefix = new int[MaxStackSize];
            int[] suffix = new int[MaxStackSize];
            int[] pixelStatck = new int[MaxStackSize + 1];

            int top = 0;
            int count = 0;
            int bi = 0;
            int xyz = 0;

            int data = 0;
            int first = 0;

            for (code = 0; code < clearCode; code++)
            {
                prefix[code] = 0;
                suffix[code] = (byte)code;
            }

            byte[] buffer = null;
            while (xyz < pixels.Length)
            {
                if (top == 0)
                {
                    if (bits < codeSize)
                    {
                        // Load bytes until there are enough bits for a code.
                        if (count == 0)
                        {
                            // Read a new data block.
                            buffer = this.ReadBlock();
                            count = buffer.Length;
                            if (count == 0)
                            {
                                break;
                            }

                            bi = 0;
                        }

                        if (buffer != null)
                        {
                            data += buffer[bi] << bits;
                        }

                        bits += 8;
                        bi++;
                        count--;
                        continue;
                    }

                    // Get the next code
                    code = data & codeMask;
                    data >>= codeSize;
                    bits -= codeSize;

                    // Interpret the code
                    if (code > availableCode || code == endCode)
                    {
                        break;
                    }

                    if (code == clearCode)
                    {
                        // Reset the decoder
                        codeSize = dataSize + 1;
                        codeMask = (1 << codeSize) - 1;
                        availableCode = clearCode + 2;
                        oldCode = NullCode;
                        continue;
                    }

                    if (oldCode == NullCode)
                    {
                        pixelStatck[top++] = suffix[code]; 
                        oldCode = code;
                        first = code;
                        continue;
                    }

                    int inCode = code;
                    if (code == availableCode)
                    {
                        pixelStatck[top++] = (byte)first;

                        code = oldCode;
                    }

                    while (code > clearCode)
                    {
                        pixelStatck[top++] = suffix[code];
                        code = prefix[code];
                    }

                    first = suffix[code];

                    pixelStatck[top++] = suffix[code];

                    // Fix for Gifs that have "deferred clear code" as per here :
                    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                    if (availableCode < MaxStackSize)
                    {
                        prefix[availableCode] = oldCode;
                        suffix[availableCode] = first;
                        availableCode++;
                        if (availableCode == codeMask + 1 && availableCode < MaxStackSize)
                        {
                            codeSize++;
                            codeMask = (1 << codeSize) - 1;
                        }
                    }

                    oldCode = inCode;
                }

                // Pop a pixel off the pixel stack.
                top--;

                // Clear missing pixels
                pixels[xyz++] = (byte)pixelStatck[top];
            }

            return pixels;
        }

        /// <summary>
        /// Reads the next data block from the stream. A data block begins with a byte,
        /// which defines the size of the block, followed by the block itself.
        /// </summary>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private byte[] ReadBlock()
        {
            int blockSize = this.stream.ReadByte();
            return this.ReadBytes(blockSize);
        }

        /// <summary>
        /// Reads the specified number of bytes from the data stream.
        /// </summary>
        /// <param name="length">
        /// The number of bytes to read.
        /// </param>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private byte[] ReadBytes(int length)
        {
            byte[] buffer = new byte[length];
            this.stream.Read(buffer, 0, length);
            return buffer;
        }
    }
}

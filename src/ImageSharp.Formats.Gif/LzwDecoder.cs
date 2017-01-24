// <copyright file="LzwDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Buffers;
    using System.IO;

    /// <summary>
    /// Decompresses and decodes data using the dynamic LZW algorithms.
    /// </summary>
    internal sealed class LzwDecoder : IDisposable
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
        /// The prefix buffer.
        /// </summary>
        private readonly int[] prefix;

        /// <summary>
        /// The suffix buffer.
        /// </summary>
        private readonly int[] suffix;

        /// <summary>
        /// The pixel stack buffer.
        /// </summary>
        private readonly int[] pixelStack;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwDecoder"/> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public LzwDecoder(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            this.stream = stream;

            this.prefix = ArrayPool<int>.Shared.Rent(MaxStackSize);
            this.suffix = ArrayPool<int>.Shared.Rent(MaxStackSize);
            this.pixelStack = ArrayPool<int>.Shared.Rent(MaxStackSize + 1);

            Array.Clear(this.prefix, 0, MaxStackSize);
            Array.Clear(this.suffix, 0, MaxStackSize);
            Array.Clear(this.pixelStack, 0, MaxStackSize + 1);
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices from the stream.
        /// </summary>
        /// <param name="width">The width of the pixel index array.</param>
        /// <param name="height">The height of the pixel index array.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <param name="pixels">The pixel array to decode to.</param>
        public void DecodePixels(int width, int height, int dataSize, byte[] pixels)
        {
            Guard.MustBeLessThan(dataSize, int.MaxValue, nameof(dataSize));

            // The resulting index table length.
            int length = width * height;

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

            int top = 0;
            int count = 0;
            int bi = 0;
            int xyz = 0;

            int data = 0;
            int first = 0;

            for (code = 0; code < clearCode; code++)
            {
                this.prefix[code] = 0;
                this.suffix[code] = (byte)code;
            }

            byte[] buffer = new byte[255];
            while (xyz < length)
            {
                if (top == 0)
                {
                    if (bits < codeSize)
                    {
                        // Load bytes until there are enough bits for a code.
                        if (count == 0)
                        {
                            // Read a new data block.
                            count = this.ReadBlock(buffer);
                            if (count == 0)
                            {
                                break;
                            }

                            bi = 0;
                        }

                        data += buffer[bi] << bits;

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
                        this.pixelStack[top++] = this.suffix[code];
                        oldCode = code;
                        first = code;
                        continue;
                    }

                    int inCode = code;
                    if (code == availableCode)
                    {
                        this.pixelStack[top++] = (byte)first;

                        code = oldCode;
                    }

                    while (code > clearCode)
                    {
                        this.pixelStack[top++] = this.suffix[code];
                        code = this.prefix[code];
                    }

                    first = this.suffix[code];

                    this.pixelStack[top++] = this.suffix[code];

                    // Fix for Gifs that have "deferred clear code" as per here :
                    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                    if (availableCode < MaxStackSize)
                    {
                        this.prefix[availableCode] = oldCode;
                        this.suffix[availableCode] = first;
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
                pixels[xyz++] = (byte)this.pixelStack[top];
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }

        /// <summary>
        /// Reads the next data block from the stream. A data block begins with a byte,
        /// which defines the size of the block, followed by the block itself.
        /// </summary>
        /// <param name="buffer">The buffer to store the block in.</param>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private int ReadBlock(byte[] buffer)
        {
            int bufferSize = this.stream.ReadByte();
            if (bufferSize < 1)
            {
                return 0;
            }

            int count = this.stream.Read(buffer, 0, bufferSize);
            return count != bufferSize ? 0 : bufferSize;
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        private void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                ArrayPool<int>.Shared.Return(this.prefix);
                ArrayPool<int>.Shared.Return(this.suffix);
                ArrayPool<int>.Shared.Return(this.pixelStack);
            }

            this.isDisposed = true;
        }
    }
}
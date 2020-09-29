// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Decompresses and decodes data using the dynamic LZW algorithms.
    /// </summary>
    /// <remarks>
    /// This code is based on the <see cref="LzwDecoder"/> used for GIF decoding. There is potential
    /// for a shared implementation. Differences between the GIF and TIFF implementations of the LZW
    /// encoding are: (i) The GIF implementation includes an initial 'data size' byte, whilst this is
    /// always 8 for TIFF. (ii) The GIF implementation writes a number of sub-blocks with an initial
    /// byte indicating the length of the sub-block. In TIFF the data is written as a single block
    /// with no length indicator (this can be determined from the 'StripByteCounts' entry).
    /// </remarks>
    internal sealed class TiffLzwDecoder
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
        /// The memory allocator.
        /// </summary>
        private readonly MemoryAllocator allocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffLzwDecoder" /> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="allocator">The memory allocator.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="stream" /> is null.</exception>
        public TiffLzwDecoder(Stream stream, MemoryAllocator allocator)
        {
            Guard.NotNull(stream, nameof(stream));

            this.stream = stream;
            this.allocator = allocator;
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices from the stream.
        /// </summary>
        /// <param name="length">The length of the compressed data.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <param name="pixels">The pixel array to decode to.</param>
        public void DecodePixels(int length, int dataSize, Span<byte> pixels)
        {
            Guard.MustBeLessThan(dataSize, int.MaxValue, nameof(dataSize));

            // Initialize buffers
            using IMemoryOwner<int> prefixMemory = this.allocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
            using IMemoryOwner<int> suffixMemory = this.allocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
            using IMemoryOwner<int> pixelStackMemory = this.allocator.Allocate<int>(MaxStackSize + 1, AllocationOptions.Clean);

            Span<int> prefix = prefixMemory.GetSpan();
            Span<int> suffix = suffixMemory.GetSpan();
            Span<int> pixelStack = pixelStackMemory.GetSpan();

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

            int inputByte = 0;
            int bits = 0;

            int top = 0;
            int xyz = 0;

            int first = 0;

            for (code = 0; code < clearCode; code++)
            {
                prefix[code] = 0;
                suffix[code] = (byte)code;
            }

            // Decoding process
            while (xyz < length)
            {
                if (top == 0)
                {
                    // Get the next code
                    int data = inputByte & ((1 << bits) - 1);

                    while (bits < codeSize)
                    {
                        inputByte = this.stream.ReadByte();
                        data = (data << 8) | inputByte;
                        bits += 8;
                    }

                    data >>= bits - codeSize;
                    bits -= codeSize;
                    code = data & codeMask;

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
                        pixelStack[top++] = suffix[code];
                        oldCode = code;
                        first = code;
                        continue;
                    }

                    int inCode = code;
                    if (code == availableCode)
                    {
                        pixelStack[top++] = (byte)first;

                        code = oldCode;
                    }

                    while (code > clearCode)
                    {
                        pixelStack[top++] = suffix[code];
                        code = prefix[code];
                    }

                    first = suffix[code];

                    pixelStack[top++] = suffix[code];

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
                pixels[xyz++] = (byte)pixelStack[top];
            }
        }
    }
}

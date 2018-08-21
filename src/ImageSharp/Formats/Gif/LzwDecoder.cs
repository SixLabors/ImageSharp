// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Gif
{
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
        private readonly IMemoryOwner<int> prefix;

        /// <summary>
        /// The suffix buffer.
        /// </summary>
        private readonly IMemoryOwner<int> suffix;

        /// <summary>
        /// The pixel stack buffer.
        /// </summary>
        private readonly IMemoryOwner<int> pixelStack;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwDecoder"/> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="stream">The stream to read from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public LzwDecoder(MemoryAllocator memoryAllocator, Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));

            this.prefix = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
            this.suffix = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
            this.pixelStack = memoryAllocator.Allocate<int>(MaxStackSize + 1, AllocationOptions.Clean);
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices from the stream.
        /// </summary>
        /// <param name="width">The width of the pixel index array.</param>
        /// <param name="height">The height of the pixel index array.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <param name="pixels">The pixel array to decode to.</param>
        public void DecodePixels(int width, int height, int dataSize, Span<byte> pixels)
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

            ref int prefixRef = ref MemoryMarshal.GetReference(this.prefix.GetSpan());
            ref int suffixRef = ref MemoryMarshal.GetReference(this.suffix.GetSpan());
            ref int pixelStackRef = ref MemoryMarshal.GetReference(this.pixelStack.GetSpan());
            ref byte pixelsRef = ref MemoryMarshal.GetReference(pixels);

            for (code = 0; code < clearCode; code++)
            {
                Unsafe.Add(ref suffixRef, code) = (byte)code;
            }

#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[255];
#else
            byte[] buffer = new byte[255];
#endif

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
                        Unsafe.Add(ref pixelStackRef, top++) = Unsafe.Add(ref suffixRef, code);
                        oldCode = code;
                        first = code;
                        continue;
                    }

                    int inCode = code;
                    if (code == availableCode)
                    {
                        Unsafe.Add(ref pixelStackRef, top++) = (byte)first;

                        code = oldCode;
                    }

                    while (code > clearCode)
                    {
                        Unsafe.Add(ref pixelStackRef, top++) = Unsafe.Add(ref suffixRef, code);
                        code = Unsafe.Add(ref prefixRef, code);
                    }

                    int suffixCode = Unsafe.Add(ref suffixRef, code);
                    first = suffixCode;
                    Unsafe.Add(ref pixelStackRef, top++) = suffixCode;

                    // Fix for Gifs that have "deferred clear code" as per here :
                    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                    if (availableCode < MaxStackSize)
                    {
                        Unsafe.Add(ref prefixRef, availableCode) = oldCode;
                        Unsafe.Add(ref suffixRef, availableCode) = first;
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
                Unsafe.Add(ref pixelsRef, xyz++) = (byte)Unsafe.Add(ref pixelStackRef, top);
            }
        }

        /// <summary>
        /// Reads the next data block from the stream. A data block begins with a byte,
        /// which defines the size of the block, followed by the block itself.
        /// </summary>
        /// <param name="buffer">The buffer to store the block in.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP2_1
        private int ReadBlock(Span<byte> buffer)
#else
        private int ReadBlock(byte[] buffer)
#endif
        {
            int bufferSize = this.stream.ReadByte();

            if (bufferSize < 1)
            {
                return 0;
            }

            int count = this.stream.Read(buffer, 0, bufferSize);

            return count != bufferSize ? 0 : bufferSize;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.prefix.Dispose();
            this.suffix.Dispose();
            this.pixelStack.Dispose();
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

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
        /// The maximum bits for a lzw code.
        /// </summary>
        private const int MaximumLzwBits = 12;

        /// <summary>
        /// The null code.
        /// </summary>
        private const int NullCode = -1;

        /// <summary>
        /// The stream to decode.
        /// </summary>
        private readonly BufferedReadStream stream;

        /// <summary>
        /// The prefix buffer.
        /// </summary>
        private readonly IMemoryOwner<int> prefix;

        /// <summary>
        /// The suffix buffer.
        /// </summary>
        private readonly IMemoryOwner<int> suffix;

        /// <summary>
        /// The scratch buffer for reading data blocks.
        /// </summary>
        private readonly IMemoryOwner<byte> scratchBuffer;

        /// <summary>
        /// The pixel stack buffer.
        /// </summary>
        private readonly IMemoryOwner<int> pixelStack;
        private readonly int minCodeSize;
        private readonly int clearCode;
        private readonly int endCode;
        private int code;
        private int codeSize;
        private int codeMask;
        private int availableCode;
        private int oldCode = NullCode;
        private int bits;
        private int top;
        private int count;
        private int bufferIndex;
        private int data;
        private int first;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwDecoder"/> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="minCodeSize">The minimum code size.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public LzwDecoder(MemoryAllocator memoryAllocator, BufferedReadStream stream, int minCodeSize)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));

            this.prefix = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
            this.suffix = memoryAllocator.Allocate<int>(MaxStackSize, AllocationOptions.Clean);
            this.pixelStack = memoryAllocator.Allocate<int>(MaxStackSize + 1, AllocationOptions.Clean);
            this.scratchBuffer = memoryAllocator.Allocate<byte>(byte.MaxValue, AllocationOptions.None);
            this.minCodeSize = minCodeSize;

            // Calculate the clear code. The value of the clear code is 2 ^ minCodeSize
            this.clearCode = 1 << minCodeSize;
            this.codeSize = minCodeSize + 1;
            this.codeMask = (1 << this.codeSize) - 1;
            this.endCode = this.clearCode + 1;
            this.availableCode = this.clearCode + 2;

            ref int suffixRef = ref MemoryMarshal.GetReference(this.suffix.GetSpan());
            for (this.code = 0; this.code < this.clearCode; this.code++)
            {
                Unsafe.Add(ref suffixRef, this.code) = (byte)this.code;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the minimum code size is valid.
        /// </summary>
        /// <param name="minCodeSize">The minimum code size.</param>
        /// <returns>
        /// <see langword="true"/> if the minimum code size is valid; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool IsValidMinCodeSize(int minCodeSize)
        {
            // It is possible to specify a larger LZW minimum code size than the palette length in bits
            // which may leave a gap in the codes where no colors are assigned.
            // http://www.matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp#lzw_compression
            int clearCode = 1 << minCodeSize;
            if (minCodeSize < 2 || minCodeSize > MaximumLzwBits || clearCode > MaxStackSize)
            {
                // Don't attempt to decode the frame indices.
                // Theoretically we could determine a min code size from the length of the provided
                // color palette but we won't bother since the image is most likely corrupted.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices for a single row from the stream, assigning the pixel values to the buffer.
        /// </summary>
        /// <param name="indices">The pixel indices array to decode to.</param>
        public void DecodePixelRow(Span<byte> indices)
        {
            indices.Clear();

            ref byte pixelsRowRef = ref MemoryMarshal.GetReference(indices);
            ref int prefixRef = ref MemoryMarshal.GetReference(this.prefix.GetSpan());
            ref int suffixRef = ref MemoryMarshal.GetReference(this.suffix.GetSpan());
            ref int pixelStackRef = ref MemoryMarshal.GetReference(this.pixelStack.GetSpan());
            Span<byte> buffer = this.scratchBuffer.GetSpan();

            int x = 0;
            int xyz = 0;
            while (xyz < indices.Length)
            {
                if (this.top == 0)
                {
                    if (this.bits < this.codeSize)
                    {
                        // Load bytes until there are enough bits for a code.
                        if (this.count == 0)
                        {
                            // Read a new data block.
                            this.count = this.ReadBlock(buffer);
                            if (this.count == 0)
                            {
                                break;
                            }

                            this.bufferIndex = 0;
                        }

                        this.data += buffer[this.bufferIndex] << this.bits;

                        this.bits += 8;
                        this.bufferIndex++;
                        this.count--;
                        continue;
                    }

                    // Get the next code
                    this.code = this.data & this.codeMask;
                    this.data >>= this.codeSize;
                    this.bits -= this.codeSize;

                    // Interpret the code
                    if (this.code > this.availableCode || this.code == this.endCode)
                    {
                        break;
                    }

                    if (this.code == this.clearCode)
                    {
                        // Reset the decoder
                        this.codeSize = this.minCodeSize + 1;
                        this.codeMask = (1 << this.codeSize) - 1;
                        this.availableCode = this.clearCode + 2;
                        this.oldCode = NullCode;
                        continue;
                    }

                    if (this.oldCode == NullCode)
                    {
                        Unsafe.Add(ref pixelStackRef, this.top++) = Unsafe.Add(ref suffixRef, this.code);
                        this.oldCode = this.code;
                        this.first = this.code;
                        continue;
                    }

                    int inCode = this.code;
                    if (this.code == this.availableCode)
                    {
                        Unsafe.Add(ref pixelStackRef, this.top++) = (byte)this.first;

                        this.code = this.oldCode;
                    }

                    while (this.code > this.clearCode)
                    {
                        Unsafe.Add(ref pixelStackRef, this.top++) = Unsafe.Add(ref suffixRef, this.code);
                        this.code = Unsafe.Add(ref prefixRef, this.code);
                    }

                    int suffixCode = Unsafe.Add(ref suffixRef, this.code);
                    this.first = suffixCode;
                    Unsafe.Add(ref pixelStackRef, this.top++) = suffixCode;

                    // Fix for Gifs that have "deferred clear code" as per here :
                    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                    if (this.availableCode < MaxStackSize)
                    {
                        Unsafe.Add(ref prefixRef, this.availableCode) = this.oldCode;
                        Unsafe.Add(ref suffixRef, this.availableCode) = this.first;
                        this.availableCode++;
                        if (this.availableCode == this.codeMask + 1 && this.availableCode < MaxStackSize)
                        {
                            this.codeSize++;
                            this.codeMask = (1 << this.codeSize) - 1;
                        }
                    }

                    this.oldCode = inCode;
                }

                // Pop a pixel off the pixel stack.
                this.top--;

                // Clear missing pixels
                xyz++;
                Unsafe.Add(ref pixelsRowRef, x++) = (byte)Unsafe.Add(ref pixelStackRef, this.top);
            }
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices from the stream allowing skipping of the data.
        /// </summary>
        /// <param name="length">The resulting index table length.</param>
        public void SkipIndices(int length)
        {
            ref int prefixRef = ref MemoryMarshal.GetReference(this.prefix.GetSpan());
            ref int suffixRef = ref MemoryMarshal.GetReference(this.suffix.GetSpan());
            ref int pixelStackRef = ref MemoryMarshal.GetReference(this.pixelStack.GetSpan());
            Span<byte> buffer = this.scratchBuffer.GetSpan();

            int xyz = 0;
            while (xyz < length)
            {
                if (this.top == 0)
                {
                    if (this.bits < this.codeSize)
                    {
                        // Load bytes until there are enough bits for a code.
                        if (this.count == 0)
                        {
                            // Read a new data block.
                            this.count = this.ReadBlock(buffer);
                            if (this.count == 0)
                            {
                                break;
                            }

                            this.bufferIndex = 0;
                        }

                        this.data += buffer[this.bufferIndex] << this.bits;

                        this.bits += 8;
                        this.bufferIndex++;
                        this.count--;
                        continue;
                    }

                    // Get the next code
                    this.code = this.data & this.codeMask;
                    this.data >>= this.codeSize;
                    this.bits -= this.codeSize;

                    // Interpret the code
                    if (this.code > this.availableCode || this.code == this.endCode)
                    {
                        break;
                    }

                    if (this.code == this.clearCode)
                    {
                        // Reset the decoder
                        this.codeSize = this.minCodeSize + 1;
                        this.codeMask = (1 << this.codeSize) - 1;
                        this.availableCode = this.clearCode + 2;
                        this.oldCode = NullCode;
                        continue;
                    }

                    if (this.oldCode == NullCode)
                    {
                        Unsafe.Add(ref pixelStackRef, this.top++) = Unsafe.Add(ref suffixRef, this.code);
                        this.oldCode = this.code;
                        this.first = this.code;
                        continue;
                    }

                    int inCode = this.code;
                    if (this.code == this.availableCode)
                    {
                        Unsafe.Add(ref pixelStackRef, this.top++) = (byte)this.first;

                        this.code = this.oldCode;
                    }

                    while (this.code > this.clearCode)
                    {
                        Unsafe.Add(ref pixelStackRef, this.top++) = Unsafe.Add(ref suffixRef, this.code);
                        this.code = Unsafe.Add(ref prefixRef, this.code);
                    }

                    int suffixCode = Unsafe.Add(ref suffixRef, this.code);
                    this.first = suffixCode;
                    Unsafe.Add(ref pixelStackRef, this.top++) = suffixCode;

                    // Fix for Gifs that have "deferred clear code" as per here :
                    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                    if (this.availableCode < MaxStackSize)
                    {
                        Unsafe.Add(ref prefixRef, this.availableCode) = this.oldCode;
                        Unsafe.Add(ref suffixRef, this.availableCode) = this.first;
                        this.availableCode++;
                        if (this.availableCode == this.codeMask + 1 && this.availableCode < MaxStackSize)
                        {
                            this.codeSize++;
                            this.codeMask = (1 << this.codeSize) - 1;
                        }
                    }

                    this.oldCode = inCode;
                }

                // Pop a pixel off the pixel stack.
                this.top--;

                // Clear missing pixels
                xyz++;
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
        private int ReadBlock(Span<byte> buffer)
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
            this.scratchBuffer.Dispose();
        }
    }
}

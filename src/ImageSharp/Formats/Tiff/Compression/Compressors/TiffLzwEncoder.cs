// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors
{
    /*
        This implementation is a port of a java tiff encoder by Harald Kuhr: https://github.com/haraldk/TwelveMonkeys

        Original licence:

        BSD 3-Clause License

        * Copyright (c) 2015, Harald Kuhr
        * All rights reserved.
        *
        * Redistribution and use in source and binary forms, with or without
        * modification, are permitted provided that the following conditions are met:
        *
        * * Redistributions of source code must retain the above copyright notice, this
        * list of conditions and the following disclaimer.
        *
        * * Redistributions in binary form must reproduce the above copyright notice,
        *   this list of conditions and the following disclaimer in the documentation
        *   and/or other materials provided with the distribution.
        *
        ** Neither the name of the copyright holder nor the names of its
        * contributors may be used to endorse or promote products derived from
        *   this software without specific prior written permission.
        *
        * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
        * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
        * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
        * DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
        * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
        * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
        * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
        * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
        * OR TORT(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
        * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
    */

    /// <summary>
    /// Encodes and compresses the image data using dynamic Lempel-Ziv compression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This code is based on the <see cref="LzwEncoder"/> used for GIF encoding. There is potential
    /// for a shared implementation. Differences between the GIF and TIFF implementations of the LZW
    /// encoding are: (i) The GIF implementation includes an initial 'data size' byte, whilst this is
    /// always 8 for TIFF. (ii) The GIF implementation writes a number of sub-blocks with an initial
    /// byte indicating the length of the sub-block. In TIFF the data is written as a single block
    /// with no length indicator (this can be determined from the 'StripByteCounts' entry).
    /// </para>
    /// </remarks>
    internal sealed class TiffLzwEncoder : IDisposable
    {
        // Clear: Re-initialize tables.
        private static readonly int ClearCode = 256;

        // End of Information.
        private static readonly int EoiCode = 257;

        private static readonly int MinBits = 9;
        private static readonly int MaxBits = 12;

        private static readonly int TableSize = 1 << MaxBits;

        // A child is made up of a parent (or prefix) code plus a suffix byte
        // and siblings are strings with a common parent(or prefix) and different suffix bytes.
        private readonly IMemoryOwner<int> children;

        private readonly IMemoryOwner<int> siblings;

        private readonly IMemoryOwner<int> suffixes;

        // Initial setup
        private int parent;
        private int bitsPerCode;
        private int nextValidCode;
        private int maxCode;

        // Buffer for partial codes
        private int bits;
        private int bitPos;
        private int bufferPosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffLzwEncoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        public TiffLzwEncoder(MemoryAllocator memoryAllocator)
        {
            this.children = memoryAllocator.Allocate<int>(TableSize);
            this.siblings = memoryAllocator.Allocate<int>(TableSize);
            this.suffixes = memoryAllocator.Allocate<int>(TableSize);
        }

        /// <summary>
        /// Encodes and compresses the indexed pixels to the stream.
        /// </summary>
        /// <param name="data">The data to compress.</param>
        /// <param name="stream">The stream to write to.</param>
        public void Encode(Span<byte> data, Stream stream)
        {
            this.Reset();

            Span<int> childrenSpan = this.children.GetSpan();
            Span<int> suffixesSpan = this.suffixes.GetSpan();
            Span<int> siblingsSpan = this.siblings.GetSpan();
            int length = data.Length;

            if (length == 0)
            {
                return;
            }

            if (this.parent == -1)
            {
                // Init stream.
                this.WriteCode(stream, ClearCode);
                this.parent = this.ReadNextByte(data);
            }

            while (this.bufferPosition < data.Length)
            {
                int value = this.ReadNextByte(data);
                int child = childrenSpan[this.parent];

                if (child > 0)
                {
                    if (suffixesSpan[child] == value)
                    {
                        this.parent = child;
                    }
                    else
                    {
                        int sibling = child;

                        while (true)
                        {
                            if (siblingsSpan[sibling] > 0)
                            {
                                sibling = siblingsSpan[sibling];

                                if (suffixesSpan[sibling] == value)
                                {
                                    this.parent = sibling;
                                    break;
                                }
                            }
                            else
                            {
                                siblingsSpan[sibling] = (short)this.nextValidCode;
                                suffixesSpan[this.nextValidCode] = (short)value;
                                this.WriteCode(stream, this.parent);
                                this.parent = value;
                                this.nextValidCode++;

                                this.IncreaseCodeSizeOrResetIfNeeded(stream);

                                break;
                            }
                        }
                    }
                }
                else
                {
                    childrenSpan[this.parent] = (short)this.nextValidCode;
                    suffixesSpan[this.nextValidCode] = (short)value;
                    this.WriteCode(stream, this.parent);
                    this.parent = value;
                    this.nextValidCode++;

                    this.IncreaseCodeSizeOrResetIfNeeded(stream);
                }
            }

            // Write EOI when we are done.
            this.WriteCode(stream, this.parent);
            this.WriteCode(stream, EoiCode);

            // Flush partial codes by writing 0 pad.
            if (this.bitPos > 0)
            {
                this.WriteCode(stream, 0);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.children.Dispose();
            this.siblings.Dispose();
            this.suffixes.Dispose();
        }

        private void Reset()
        {
            this.children.Clear();
            this.siblings.Clear();
            this.suffixes.Clear();

            this.parent = -1;
            this.bitsPerCode = MinBits;
            this.nextValidCode = EoiCode + 1;
            this.maxCode = (1 << this.bitsPerCode) - 1;

            this.bits = 0;
            this.bitPos = 0;
            this.bufferPosition = 0;
        }

        private byte ReadNextByte(Span<byte> data) => data[this.bufferPosition++];

        private void IncreaseCodeSizeOrResetIfNeeded(Stream stream)
        {
            if (this.nextValidCode > this.maxCode)
            {
                if (this.bitsPerCode == MaxBits)
                {
                    // Reset stream by writing Clear code.
                    this.WriteCode(stream, ClearCode);

                    // Reset tables.
                    this.ResetTables();
                }
                else
                {
                    // Increase code size.
                    this.bitsPerCode++;
                    this.maxCode = MaxValue(this.bitsPerCode);
                }
            }
        }

        private void WriteCode(Stream stream, int code)
        {
            this.bits = (this.bits << this.bitsPerCode) | (code & this.maxCode);
            this.bitPos += this.bitsPerCode;

            while (this.bitPos >= 8)
            {
                int b = (this.bits >> (this.bitPos - 8)) & 0xff;
                stream.WriteByte((byte)b);
                this.bitPos -= 8;
            }

            this.bits &= BitmaskFor(this.bitPos);
        }

        private void ResetTables()
        {
            this.children.GetSpan().Clear();
            this.siblings.GetSpan().Clear();
            this.bitsPerCode = MinBits;
            this.maxCode = MaxValue(this.bitsPerCode);
            this.nextValidCode = EoiCode + 1;
        }

        private static int MaxValue(int codeLen) => (1 << codeLen) - 1;

        private static int BitmaskFor(int bits) => MaxValue(bits);
    }
}

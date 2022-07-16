// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.BitReader
{
    /// <summary>
    /// A bit reader for VP8 streams.
    /// </summary>
    internal class Vp8BitReader : BitReaderBase
    {
        private const int BitsCount = 56;

        /// <summary>
        /// Current value.
        /// </summary>
        private ulong value;

        /// <summary>
        /// Current range minus 1. In [127, 254] interval.
        /// </summary>
        private uint range;

        /// <summary>
        /// Number of valid bits left.
        /// </summary>
        private int bits;

        /// <summary>
        /// Max packed-read position of the buffer.
        /// </summary>
        private uint bufferMax;

        private uint bufferEnd;

        /// <summary>
        /// True if input is exhausted.
        /// </summary>
        private bool eof;

        /// <summary>
        /// Byte position in buffer.
        /// </summary>
        private long pos;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8BitReader"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to read from.</param>
        /// <param name="imageDataSize">The raw image data size in bytes.</param>
        /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
        /// <param name="partitionLength">The partition length.</param>
        /// <param name="startPos">Start index in the data array. Defaults to 0.</param>
        public Vp8BitReader(Stream inputStream, uint imageDataSize, MemoryAllocator memoryAllocator, uint partitionLength, int startPos = 0)
        {
            Guard.MustBeLessThan(imageDataSize, int.MaxValue, nameof(imageDataSize));

            this.ImageDataSize = imageDataSize;
            this.PartitionLength = partitionLength;
            this.ReadImageDataFromStream(inputStream, (int)imageDataSize, memoryAllocator);
            this.InitBitreader(partitionLength, startPos);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8BitReader"/> class.
        /// </summary>
        /// <param name="imageData">The raw encoded image data.</param>
        /// <param name="partitionLength">The partition length.</param>
        /// <param name="startPos">Start index in the data array. Defaults to 0.</param>
        public Vp8BitReader(IMemoryOwner<byte> imageData, uint partitionLength, int startPos = 0)
        {
            this.Data = imageData;
            this.ImageDataSize = (uint)imageData.Memory.Length;
            this.PartitionLength = partitionLength;
            this.InitBitreader(partitionLength, startPos);
        }

        public int Pos => (int)this.pos;

        public uint ImageDataSize { get; }

        public uint PartitionLength { get; }

        public uint Remaining { get; set; }

        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetBit(int prob)
        {
            uint range = this.range;
            if (this.bits < 0)
            {
                this.LoadNewBytes();
            }

            int pos = this.bits;
            uint split = (uint)((range * prob) >> 8);
            ulong value = this.value >> pos;
            bool bit = value > split;
            if (bit)
            {
                range -= split;
                this.value -= (ulong)(split + 1) << pos;
            }
            else
            {
                range = split + 1;
            }

            int shift = 7 ^ Numerics.Log2(range);
            range <<= shift;
            this.bits -= shift;

            this.range = range - 1;

            return bit ? 1 : 0;
        }

        // Simplified version of VP8GetBit() for prob=0x80 (note shift is always 1 here)
        public int GetSigned(int v)
        {
            if (this.bits < 0)
            {
                this.LoadNewBytes();
            }

            int pos = this.bits;
            uint split = this.range >> 1;
            ulong value = this.value >> pos;
            ulong mask = (split - value) >> 31;  // -1 or 0
            this.bits -= 1;
            this.range = (this.range + (uint)mask) | 1;
            this.value -= ((split + 1) & mask) << pos;

            return (v ^ (int)mask) - (int)mask;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public bool ReadBool() => this.ReadValue(1) is 1;

        [MethodImpl(InliningOptions.ShortMethod)]
        public uint ReadValue(int nBits)
        {
            DebugGuard.MustBeGreaterThan(nBits, 0, nameof(nBits));
            DebugGuard.MustBeLessThanOrEqualTo(nBits, 32, nameof(nBits));

            uint v = 0;
            while (nBits-- > 0)
            {
                v |= (uint)this.GetBit(0x80) << nBits;
            }

            return v;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public int ReadSignedValue(int nBits)
        {
            DebugGuard.MustBeGreaterThan(nBits, 0, nameof(nBits));
            DebugGuard.MustBeLessThanOrEqualTo(nBits, 32, nameof(nBits));

            int value = (int)this.ReadValue(nBits);
            return this.ReadValue(1) != 0 ? -value : value;
        }

        private void InitBitreader(uint size, int pos = 0)
        {
            long posPlusSize = pos + size;
            this.range = 255 - 1;
            this.value = 0;
            this.bits = -8; // to load the very first 8 bits.
            this.eof = false;
            this.pos = pos;
            this.bufferEnd = (uint)posPlusSize;
            this.bufferMax = (uint)(size > 8 ? posPlusSize - 8 + 1 : pos);

            this.LoadNewBytes();
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private void LoadNewBytes()
        {
            if (this.pos < this.bufferMax)
            {
                ulong inBits = BinaryPrimitives.ReadUInt64LittleEndian(this.Data.Memory.Span.Slice((int)this.pos, 8));
                this.pos += BitsCount >> 3;
                ulong bits = this.ByteSwap64(inBits);
                bits >>= 64 - BitsCount;
                this.value = bits | (this.value << BitsCount);
                this.bits += BitsCount;
            }
            else
            {
                this.LoadFinalBytes();
            }
        }

        private void LoadFinalBytes()
        {
            // Only read 8bits at a time.
            if (this.pos < this.bufferEnd)
            {
                this.bits += 8;
                this.value = this.Data.Memory.Span[(int)this.pos++] | (this.value << 8);
            }
            else if (!this.eof)
            {
                this.value <<= 8;
                this.bits += 8;
                this.eof = true;
            }
            else
            {
                this.bits = 0;  // This is to avoid undefined behaviour with shifts.
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private ulong ByteSwap64(ulong x)
        {
            x = ((x & 0xffffffff00000000ul) >> 32) | ((x & 0x00000000fffffffful) << 32);
            x = ((x & 0xffff0000ffff0000ul) >> 16) | ((x & 0x0000ffff0000fffful) << 16);
            x = ((x & 0xff00ff00ff00ff00ul) >> 8) | ((x & 0x00ff00ff00ff00fful) << 8);
            return x;
        }
    }
}

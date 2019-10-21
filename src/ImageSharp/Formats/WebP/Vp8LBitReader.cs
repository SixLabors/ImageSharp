// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// A bit reader for VP8 streams.
    /// </summary>
    public class Vp8LBitReader
    {
        private readonly Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitReader"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to read from.</param>
        public Vp8LBitReader(Stream inputStream)
        {
            this.stream = new MemoryStream();
            long inputStreamPos = inputStream.Position;
            inputStream.CopyTo(this.stream);
            inputStream.Position = inputStreamPos;
            this.Offset = 0;
            this.Bit = 0;
        }

        private long Offset { get; set; }

        private int Bit { get; set; }

        /// <summary>
        /// Gets a value indicating whether the offset is inside the inputStream length.
        /// </summary>
        private bool ValidPosition
        {
            get
            {
                return this.Offset < this.stream.Length;
            }
        }

        /// <summary>
        /// Reads a unsigned short value from the inputStream. The bits of each byte are read in least-significant-bit-first order.
        /// </summary>
        /// <param name="count">The number of bits to read (should not exceed 16).</param>
        /// <returns>A ushort value.</returns>
        public uint Read(int count)
        {
            uint readValue = 0;
            for (int bitPos = 0; bitPos < count; bitPos++)
            {
                bool bitRead = this.ReadBit();
                if (bitRead)
                {
                    readValue = (uint)(readValue | (1 << bitPos));
                }
            }

            return readValue;
        }

        /// <summary>
        /// Reads one bit.
        /// </summary>
        /// <returns>True, if the bit is one, otherwise false.</returns>
        public bool ReadBit()
        {
            if (!this.ValidPosition)
            {
                WebPThrowHelper.ThrowImageFormatException("The image inputStream does not contain enough data");
            }

            this.stream.Seek(this.Offset, SeekOrigin.Begin);
            byte value = (byte)((this.stream.ReadByte() >> this.Bit) & 1);
            this.AdvanceBit();
            this.stream.Seek(this.Offset, SeekOrigin.Begin);

            return value == 1;
        }

        /// <summary>
        /// Advances the inputStream by one Bit.
        /// </summary>
        public void AdvanceBit()
        {
            this.Bit = (this.Bit + 1) % 8;
            if (this.Bit == 0)
            {
                this.Offset++;
            }
        }
    }
}

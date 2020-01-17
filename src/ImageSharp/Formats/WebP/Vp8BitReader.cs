// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// A bit reader for VP8 streams.
    /// </summary>
    internal class Vp8BitReader : BitReaderBase
    {
        /// <summary>
        /// Current value.
        /// </summary>
        private long value;

        /// <summary>
        /// Current range minus 1. In [127, 254] interval.
        /// </summary>
        private int range;

        /// <summary>
        /// Number of valid bits left.
        /// </summary>
        private int bits;

        /// <summary>
        /// The next byte to be read.
        /// </summary>
        private byte buf;

        /// <summary>
        /// End of read buffer.
        /// </summary>
        private byte bufEnd;

        /// <summary>
        /// Max packed-read position on buffer.
        /// </summary>
        private byte bufMax;

        /// <summary>
        /// True if input is exhausted.
        /// </summary>
        private bool eof;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8BitReader"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to read from.</param>
        /// <param name="imageDataSize">The raw image data size in bytes.</param>
        /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
        public Vp8BitReader(Stream inputStream, uint imageDataSize, MemoryAllocator memoryAllocator)
        {
            this.ReadImageDataFromStream(inputStream, (int)imageDataSize, memoryAllocator);
        }

        /// <inheritdoc/>
        public override bool ReadBit()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override uint ReadValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            throw new NotImplementedException();
        }

        public int ReadSignedValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            throw new NotImplementedException();
        }
    }
}

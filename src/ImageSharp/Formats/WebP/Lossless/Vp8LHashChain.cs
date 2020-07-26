// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LHashChain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHashChain"/> class.
        /// </summary>
        /// <param name="size">The size off the chain.</param>
        public Vp8LHashChain(int size)
        {
            this.OffsetLength = new uint[size];
            this.OffsetLength.AsSpan().Fill(0xcdcdcdcd);
            this.Size = size;
        }

        /// <summary>
        /// Gets the offset length.
        /// The 20 most significant bits contain the offset at which the best match is found.
        /// These 20 bits are the limit defined by GetWindowSizeForHashChain (through WindowSize = 1 &lt;&lt; 20).
        /// The lower 12 bits contain the length of the match.
        /// </summary>
        public uint[] OffsetLength { get; }

        /// <summary>
        /// Gets the size of the hash chain.
        /// This is the maximum size of the hash_chain that can be constructed.
        /// Typically this is the pixel count (width x height) for a given image.
        /// </summary>
        public int Size { get; }

        public int FindLength(int basePosition)
        {
            return (int)(this.OffsetLength[basePosition] & ((1U << BackwardReferenceEncoder.MaxLengthBits) - 1));
        }

        public int FindOffset(int basePosition)
        {
            return (int)(this.OffsetLength[basePosition] >> BackwardReferenceEncoder.MaxLengthBits);
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LHashChain
    {
        /// <summary>
        /// The 20 most significant bits contain the offset at which the best match is found.
        /// These 20 bits are the limit defined by GetWindowSizeForHashChain (through WindowSize = 1 << 20).
        /// The lower 12 bits contain the length of the match. The 12 bit limit is
        /// defined in MaxFindCopyLength with MAX_LENGTH=4096.
        /// </summary>
        public uint[] OffsetLength { get; }

        /// <summary>
        /// This is the maximum size of the hash_chain that can be constructed.
        /// Typically this is the pixel count (width x height) for a given image.
        /// </summary>
        public int Size { get; }

        public Vp8LHashChain(int size)
        {
            this.OffsetLength = new uint[size];
            this.OffsetLength.AsSpan().Fill(0xcdcdcdcd);
            this.Size = size;
        }
    }
}

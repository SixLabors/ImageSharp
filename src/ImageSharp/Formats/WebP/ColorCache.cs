// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// A small hash-addressed array to store recently used colors, to be able to recall them with shorter codes.
    /// </summary>
    internal class ColorCache
    {
        private const uint HashMul = 0x1e35a7bdu;

        /// <summary>
        /// Gets the color entries.
        /// </summary>
        public uint[] Colors { get; private set; }

        /// <summary>
        /// Gets the hash shift: 32 - hashBits.
        /// </summary>
        public int HashShift { get; private set; }

        /// <summary>
        /// Gets the hash bits.
        /// </summary>
        public int HashBits { get; private set; }

        /// <summary>
        /// Initializes a new color cache.
        /// </summary>
        /// <param name="hashBits">The hashBits determine the size of cache. It will be 1 left shifted by hashBits.</param>
        public void Init(int hashBits)
        {
            int hashSize = 1 << hashBits;
            this.Colors = new uint[hashSize];
            this.HashBits = hashBits;
            this.HashShift = 32 - hashBits;
        }

        /// <summary>
        /// Inserts a new color into the cache.
        /// </summary>
        /// <param name="argb">The color to insert.</param>
        public void Insert(uint argb)
        {
            int key = this.HashPix(argb, this.HashShift);
            this.Colors[key] = argb;
        }

        public uint Lookup(int key)
        {
            return this.Colors[key];
        }

        private int HashPix(uint argb, int shift)
        {
            return (int)((argb * HashMul) >> shift);
        }
    }
}

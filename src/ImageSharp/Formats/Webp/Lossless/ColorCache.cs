// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
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
        /// <param name="bgra">The color to insert.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Insert(uint bgra)
        {
            int key = HashPix(bgra, this.HashShift);
            this.Colors[key] = bgra;
        }

        /// <summary>
        /// Gets a color for a given key.
        /// </summary>
        /// <param name="key">The key to lookup.</param>
        /// <returns>The color for the key.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public uint Lookup(int key) => this.Colors[key];

        /// <summary>
        /// Returns the index of the given color.
        /// </summary>
        /// <param name="bgra">The color to check.</param>
        /// <returns>The index of the color in the cache or -1 if its not present.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int Contains(uint bgra)
        {
            int key = HashPix(bgra, this.HashShift);
            return (this.Colors[key] == bgra) ? key : -1;
        }

        /// <summary>
        /// Gets the index of a color.
        /// </summary>
        /// <param name="bgra">The color.</param>
        /// <returns>The index for the color.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int GetIndex(uint bgra) => HashPix(bgra, this.HashShift);

        /// <summary>
        /// Adds a new color to the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="bgra">The color to add.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Set(uint key, uint bgra) => this.Colors[key] = bgra;

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int HashPix(uint argb, int shift) => (int)((argb * HashMul) >> shift);
    }
}

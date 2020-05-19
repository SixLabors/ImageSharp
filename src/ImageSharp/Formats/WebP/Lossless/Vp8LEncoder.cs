// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    /// <summary>
    /// Encoder for lossless webp images.
    /// </summary>
    internal class Vp8LEncoder : IDisposable
    {
        public Vp8LEncoder(MemoryAllocator memoryAllocator)
        {
            this.Palette = memoryAllocator.Allocate<uint>(WebPConstants.MaxPaletteSize);
        }

        /// <summary>
        /// Gets or sets the huffman image bits.
        /// </summary>
        public int HistoBits { get; set; }

        /// <summary>
        /// Gets or sets the bits used for the transformation.
        /// </summary>
        public int TransformBits { get; set; }

        /// <summary>
        /// Gets or sets the cache bits.
        /// </summary>
        public bool CacheBits { get; }

        /// <summary>
        /// Gets a value indicating whether to use the cross color transform.
        /// </summary>
        public bool UseCrossColorTransform { get; }

        /// <summary>
        /// Gets a value indicating whether to use the substract green transform.
        /// </summary>
        public bool UseSubtractGreenTransform { get; }

        /// <summary>
        /// Gets a value indicating whether to use the predictor transform.
        /// </summary>
        public bool UsePredictorTransform { get; }

        /// <summary>
        /// Gets or sets a value indicating whether to use color indexing transform.
        /// </summary>
        public bool UsePalette { get; set; }

        /// <summary>
        /// Gets or sets the palette size.
        /// </summary>
        public int PaletteSize { get; set; }

        /// <summary>
        /// Gets the palette.
        /// </summary>
        public IMemoryOwner<uint> Palette { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Palette.Dispose();
        }
    }
}

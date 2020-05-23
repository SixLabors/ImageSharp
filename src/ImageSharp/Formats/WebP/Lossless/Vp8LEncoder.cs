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
        /// <summary>
        /// Maximum number of reference blocks the image will be segmented into.
        /// </summary>
        private const int MaxRefsBlockPerImage = 16;

        /// <summary>
        /// Minimum block size for backward references.
        /// </summary>
        private const int MinBlockSize = 256;

        public Vp8LEncoder(MemoryAllocator memoryAllocator, int width, int height)
        {
            var pixelCount = width * height;

            this.Palette = memoryAllocator.Allocate<uint>(WebPConstants.MaxPaletteSize);
            this.Refs = new Vp8LBackwardRefs[3];
            this.HashChain = new Vp8LHashChain(pixelCount);

            // We round the block size up, so we're guaranteed to have at most MAX_REFS_BLOCK_PER_IMAGE blocks used:
            int refsBlockSize = ((pixelCount - 1) / MaxRefsBlockPerImage) + 1;
            for (int i = 0; i < this.Refs.Length; ++i)
            {
                this.Refs[i] = new Vp8LBackwardRefs();
                this.Refs[i].BlockSize = (refsBlockSize < MinBlockSize) ? MinBlockSize : refsBlockSize;
            }
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
        /// Gets or sets a value indicating whether to use a color cache.
        /// </summary>
        public bool UseColorCache { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the cross color transform.
        /// </summary>
        public bool UseCrossColorTransform { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the substract green transform.
        /// </summary>
        public bool UseSubtractGreenTransform { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the predictor transform.
        /// </summary>
        public bool UsePredictorTransform { get; set; }

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

        public Vp8LBackwardRefs[] Refs { get; }

        public Vp8LHashChain HashChain { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Palette.Dispose();
        }
    }
}

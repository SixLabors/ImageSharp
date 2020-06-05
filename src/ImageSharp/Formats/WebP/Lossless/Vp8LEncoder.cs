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

        private MemoryAllocator memoryAllocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LEncoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The width of the input image.</param>
        /// <param name="height">The height of the input image.</param>
        public Vp8LEncoder(MemoryAllocator memoryAllocator, int width, int height)
        {
            var pixelCount = width * height;

            this.Bgra = memoryAllocator.Allocate<uint>(pixelCount);
            this.Palette = memoryAllocator.Allocate<uint>(WebPConstants.MaxPaletteSize);
            this.Refs = new Vp8LBackwardRefs[3];
            this.HashChain = new Vp8LHashChain(pixelCount);
            this.memoryAllocator = memoryAllocator;

            // We round the block size up, so we're guaranteed to have at most MaxRefsBlockPerImage blocks used:
            int refsBlockSize = ((pixelCount - 1) / MaxRefsBlockPerImage) + 1;
            for (int i = 0; i < this.Refs.Length; ++i)
            {
                this.Refs[i] = new Vp8LBackwardRefs();
                this.Refs[i].BlockSize = (refsBlockSize < MinBlockSize) ? MinBlockSize : refsBlockSize;
            }
        }

        /// <summary>
        /// Gets transformed image data.
        /// </summary>
        public IMemoryOwner<uint> Bgra { get; }

        /// <summary>
        /// Gets the scratch memory for bgra rows used for prediction.
        /// </summary>
        public IMemoryOwner<uint> BgraScratch { get; set; }

        /// <summary>
        /// Gets or sets the packed image width.
        /// </summary>
        public int CurrentWidth { get; set; }

        /// <summary>
        /// Gets or sets the huffman image bits.
        /// </summary>
        public int HistoBits { get; set; }

        /// <summary>
        /// Gets or sets the bits used for the transformation.
        /// </summary>
        public int TransformBits { get; set; }

        /// <summary>
        /// Gets or sets the transform data.
        /// </summary>
        public IMemoryOwner<uint> TransformData { get; set; }

        /// <summary>
        /// Gets or sets the cache bits. If equal to 0, don't use color cache.
        /// </summary>
        public int CacheBits { get; set; }

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

        /// <summary>
        /// Gets the backward references.
        /// </summary>
        public Vp8LBackwardRefs[] Refs { get; }

        /// <summary>
        /// Gets the hash chain.
        /// </summary>
        public Vp8LHashChain HashChain { get; }

        public void AllocateTransformBuffer(int width, int height)
        {
            int imageSize = width * height;

            // VP8LResidualImage needs room for 2 scanlines of uint32 pixels with an extra
            // pixel in each, plus 2 regular scanlines of bytes.
            int argbScratchSize = this.UsePredictorTransform ? ((width + 1) * 2) + (((width * 2) + 4 - 1) / 4) : 0;
            int transformDataSize = (this.UsePredictorTransform || this.UseCrossColorTransform) ? LosslessUtils.SubSampleSize(width, this.TransformBits) * LosslessUtils.SubSampleSize(height, this.TransformBits) : 0;

            this.BgraScratch = this.memoryAllocator.Allocate<uint>(argbScratchSize);
            this.TransformData = this.memoryAllocator.Allocate<uint>(transformDataSize);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Bgra.Dispose();
            this.BgraScratch.Dispose();
            this.Palette.Dispose();
            this.TransformData.Dispose();
        }
    }
}

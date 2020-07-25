// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.WebP.BitWriter;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// Encoder for lossy webp images.
    /// </summary>
    internal class Vp8Encoder
    {
        /// <summary>
        /// The <see cref="MemoryAllocator"/> to use for buffer allocations.
        /// </summary>
        private MemoryAllocator memoryAllocator;

        /// <summary>
        /// A bit writer for writing lossy webp streams.
        /// </summary>
        private Vp8BitWriter bitWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Encoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="width">The width of the input image.</param>
        /// <param name="height">The height of the input image.</param>
        public Vp8Encoder(MemoryAllocator memoryAllocator, int width, int height)
        {
            this.memoryAllocator = memoryAllocator;

            // TODO: initialize bitwriter
        }

        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }
    }
}

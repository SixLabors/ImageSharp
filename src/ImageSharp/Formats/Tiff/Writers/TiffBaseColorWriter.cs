// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    /// <summary>
    /// Utility class for writing TIFF data to a <see cref="Stream"/>.
    /// </summary>
    internal abstract class TiffBaseColorWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffBaseColorWriter" /> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="entriesCollector">The entries collector.</param>
        protected TiffBaseColorWriter(TiffStreamWriter output, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
        {
            this.Output = output;
            this.MemoryAllocator = memoryAllocator;
            this.Configuration = configuration;
            this.EntriesCollector = entriesCollector;
        }

        protected TiffStreamWriter Output { get; }

        protected MemoryAllocator MemoryAllocator { get; }

        protected Configuration Configuration { get; }

        protected TiffEncoderEntriesCollector EntriesCollector { get; }

        public abstract int Write<TPixel>(Image<TPixel> image, IQuantizer quantizer, TiffEncoderCompression compression, DeflateCompressionLevel compressionLevel, bool useHorizontalPredictor)
            where TPixel : unmanaged, IPixel<TPixel>;
    }
}

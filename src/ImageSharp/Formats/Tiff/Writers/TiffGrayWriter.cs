// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    internal sealed class TiffGrayWriter<TPixel> : TiffCompositeColorWriter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        public TiffGrayWriter(ImageFrame<TPixel> image, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
            : base(image, memoryAllocator, configuration, entriesCollector)
        {
        }

        /// <inheritdoc />
        public override int BitsPerPixel => 8;

        /// <inheritdoc />
        protected override void EncodePixels(Span<TPixel> pixels, Span<byte> buffer) => PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixels, buffer, pixels.Length);
    }
}

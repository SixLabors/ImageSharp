// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Image encoder for writing image data to a stream in gif format.
    /// </summary>
    public sealed class GifEncoder : IImageEncoder, IGifEncoderOptions
    {
        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// Defaults to the <see cref="OctreeQuantizer"/>
        /// </summary>
        public IQuantizer Quantizer { get; set; } = KnownQuantizers.Octree;

        /// <summary>
        /// Gets or sets the color table mode: Global or local.
        /// </summary>
        public GifColorTableMode? ColorTableMode { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new GifEncoderCore(image.GetConfiguration(), this);
            encoder.Encode(image, stream);
        }
    }
}

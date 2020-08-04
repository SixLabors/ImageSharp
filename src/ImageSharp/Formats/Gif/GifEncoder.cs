// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Gets or sets the <see cref="IPixelSamplingStrategy"/> used for quantization
        /// when building a global color table in case of <see cref="GifColorTableMode.Global"/>.
        /// </summary>
        public IPixelSamplingStrategy GlobalPixelSamplingStrategy { get; set; } = new DefaultPixelSamplingStrategy();

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new GifEncoderCore(image.GetConfiguration(), this);
            encoder.Encode(image, stream);
        }

        /// <inheritdoc/>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new GifEncoderCore(image.GetConfiguration(), this);
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }
    }
}

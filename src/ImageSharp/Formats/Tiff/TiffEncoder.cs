// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Encoder for writing the data image to a stream in TIFF format.
    /// </summary>
    public class TiffEncoder : IImageEncoder, ITiffEncoderOptions
    {
        /// <summary>
        /// Gets or sets the number of bits per pixel. 8 bit implies a grayscale image.
        /// </summary>
        public TiffBitsPerPixel? BitsPerPixel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating which compression to use.
        /// </summary>
        public TiffEncoderCompression Compression { get; set; } = TiffEncoderCompression.None;

        /// <summary>
        /// Gets or sets a value indicating whether to use a color palette.
        /// </summary>
        public bool UseColorPalette { get; set; }

        /// <summary>
        /// Gets or sets the quantizer for color images with a palette.
        /// Defaults to OctreeQuantizer.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encode = new TiffEncoderCore(this, image.GetMemoryAllocator());
            encode.Encode(image, stream);
        }

        /// <inheritdoc/>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new TiffEncoderCore(this, image.GetMemoryAllocator());
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }
    }
}

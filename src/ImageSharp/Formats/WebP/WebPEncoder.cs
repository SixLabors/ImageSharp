// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    ///  Image encoder for writing an image to a stream in the WebP format.
    /// </summary>
    public sealed class WebPEncoder : IImageEncoder, IWebPEncoderOptions
    {
        /// <inheritdoc/>
        public bool Lossless { get; set; }

        /// <inheritdoc/>
        public float Quality { get; set; }

        /// <inheritdoc/>
        public bool AlphaCompression { get; set; }

        /// <inheritdoc/>
        public int EntropyPasses { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebPEncoderCore(this, image.GetMemoryAllocator());
            encoder.Encode(image, stream);
        }
    }
}

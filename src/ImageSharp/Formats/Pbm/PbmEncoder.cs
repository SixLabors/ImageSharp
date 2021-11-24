// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Image encoder for writing an image to a stream as PGM, PBM, PPM or PAM bitmap.
    /// </summary>
    public sealed class PbmEncoder : IImageEncoder, IPbmEncoderOptions
    {
        /// <summary>
        /// Gets or sets the Encoding of the pixels.
        /// </summary>
        public PbmEncoding? Encoding { get; set; }

        /// <summary>
        /// Gets or sets the Color type of the resulting image.
        /// </summary>
        public PbmColorType? ColorType { get; set; }

        /// <summary>
        /// Gets or sets the maximum pixel value, per component.
        /// </summary>
        public int? MaxPixelValue { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new PbmEncoderCore(image.GetConfiguration(), this);
            encoder.Encode(image, stream);
        }

        /// <inheritdoc/>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new PbmEncoderCore(image.GetConfiguration(), this);
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }
    }
}

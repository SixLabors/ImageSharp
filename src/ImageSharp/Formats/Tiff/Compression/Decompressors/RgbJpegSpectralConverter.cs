// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Spectral converter for YCbCr TIFF's which use the JPEG compression.
    /// The jpeg data should be always treated as RGB color space.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    internal sealed class RgbJpegSpectralConverter<TPixel> : SpectralConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbJpegSpectralConverter{TPixel}"/> class.
        /// This Spectral converter will always convert the pixel data to RGB color.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public RgbJpegSpectralConverter(Configuration configuration, CancellationToken cancellationToken)
            : base(configuration, cancellationToken)
        {
        }

        /// <inheritdoc/>
        public override JpegColorConverter GetColorConverter(JpegFrame frame, IRawJpegData jpegData) => JpegColorConverter.GetConverter(JpegColorSpace.RGB, frame.Precision);
    }
}

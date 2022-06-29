// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Image decoder for generating an image out of a TIFF stream.
    /// </summary>
    public class TiffDecoder : ImageDecoder<TiffDecoderOptions>
    {
        /// <inheritdoc/>
        public override Image<TPixel> DecodeSpecialized<TPixel>(TiffDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            TiffDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TiffDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc/>
        public override Image DecodeSpecialized(TiffDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(TiffDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new TiffDecoderCore(options).Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}

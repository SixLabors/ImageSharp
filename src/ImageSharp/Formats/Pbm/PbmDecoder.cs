// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Image decoder for reading PGM, PBM or PPM bitmaps from a stream. These images are from
    /// the family of PNM images.
    /// <list type="bullet">
    /// <item>
    /// <term>PBM</term>
    /// <description>Black and white images.</description>
    /// </item>
    /// <item>
    /// <term>PGM</term>
    /// <description>Grayscale images.</description>
    /// </item>
    /// <item>
    /// <term>PPM</term>
    /// <description>Color images, with RGB pixels.</description>
    /// </item>
    /// </list>
    /// The specification of these images is found at <seealso href="http://netpbm.sourceforge.net/doc/pnm.html"/>.
    /// </summary>
    public sealed class PbmDecoder : ImageDecoder<PbmDecoderOptions>
    {
        /// <inheritdoc />
        public override Image<TPixel> DecodeSpecialized<TPixel>(PbmDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            PbmDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<PbmDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc />
        public override Image DecodeSpecialized(PbmDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgb24>(options, stream, cancellationToken);

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(PbmDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new PbmDecoderCore(options).Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}

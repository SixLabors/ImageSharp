// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    public sealed class PbmDecoder : IImageDecoder, IImageInfoDetector
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new PbmDecoderCore(configuration);
            return decoder.Decode<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            => this.Decode<Rgb24>(configuration, stream, cancellationToken);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new PbmDecoderCore(configuration);
            return decoder.Identify(configuration, stream, cancellationToken);
        }
    }
}

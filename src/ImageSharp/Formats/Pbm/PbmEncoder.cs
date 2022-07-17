// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Image encoder for writing an image to a stream as PGM, PBM or PPM bitmap. These images are from
    /// the family of PNM images.
    /// <para>
    /// The PNM formats are a fairly simple image format. They share a plain text header, consisting of:
    /// signature, width, height and max_pixel_value only. The pixels follow thereafter and can be in
    /// plain text decimals separated by spaces, or binary encoded.
    /// <list type="bullet">
    /// <item>
    /// <term>PBM</term>
    /// <description>Black and white images, with 1 representing black and 0 representing white.</description>
    /// </item>
    /// <item>
    /// <term>PGM</term>
    /// <description>Grayscale images, scaling from 0 to max_pixel_value, 0 representing black and max_pixel_value representing white.</description>
    /// </item>
    /// <item>
    /// <term>PPM</term>
    /// <description>Color images, with RGB pixels (in that order), with 0 representing black and 2 representing full color.</description>
    /// </item>
    /// </list>
    /// </para>
    /// The specification of these images is found at <seealso href="http://netpbm.sourceforge.net/doc/pnm.html"/>.
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
        /// Gets or sets the data type of the pixels components.
        /// </summary>
        public PbmComponentType? ComponentType { get; set; }

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

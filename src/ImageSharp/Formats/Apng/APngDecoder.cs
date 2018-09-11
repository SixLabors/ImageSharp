using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Apng
{
    /// <summary>
    /// Implements the APNG decoder.
    /// </summary>
    public class ApngDecoder : IImageDecoder, IPngDecoderOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <summary>
        /// Gets or sets the encoding that should be used when reading text chunks.
        /// </summary>
        public Encoding TextEncoding { get; set; } = PngConstants.DefaultEncoding;

        /// <summary>
        /// Decodes the APNG from the specified stream to the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The decoded APNG image.</returns>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var decoder = new PngDecoderCore(configuration, this);

            // Look for the first AcTL chuck...

            throw new NotImplementedException();
        }
    }

    // ref: https://wiki.mozilla.org/APNG_Specification

}

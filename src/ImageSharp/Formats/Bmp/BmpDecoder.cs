// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image decoder for generating an image out of a Windows bitmap stream.
    /// </summary>
    /// <remarks>
    /// Does not support the following formats at the moment:
    /// <list type="bullet">
    ///    <item>JPG</item>
    ///    <item>PNG</item>
    ///    <item>Some OS/2 specific subtypes like: Bitmap Array, Color Icon, Color Pointer, Icon, Pointer.</item>
    /// </list>
    /// Formats will be supported in a later releases. We advise always
    /// to use only 24 Bit Windows bitmaps.
    /// </remarks>
    public sealed class BmpDecoder : IImageDecoder, IBmpDecoderOptions, IImageInfoDetector
    {
        /// <summary>
        /// Gets or sets a value indicating how to deal with skipped pixels, which can occur during decoding run length encoded bitmaps.
        /// </summary>
        public RleSkippedPixelHandling RleSkippedPixelHandling { get; set; } = RleSkippedPixelHandling.Black;

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new BmpDecoderCore(configuration, this);
            return decoder.Decode<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            => this.Decode<Rgba32>(configuration, stream, cancellationToken);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new BmpDecoderCore(configuration, this).Identify(configuration, stream, cancellationToken);
        }
    }
}

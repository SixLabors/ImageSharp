// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new PbmDecoderCore(configuration);
            return decoder.Decode<TPixel>(configuration, stream);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream)
            => this.Decode<Rgb24>(configuration, stream);

        /// <inheritdoc/>
        public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new PbmDecoderCore(configuration);
            return decoder.DecodeAsync<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken)
            .ConfigureAwait(false);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new PbmDecoderCore(configuration);
            return decoder.Identify(configuration, stream);
        }

        /// <inheritdoc/>
        public async Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            // The introduction of a local variable that refers to an object the implements
            // IDisposable means you must use async/await, where the compiler generates the
            // state machine and a continuation.
            var decoder = new PbmDecoderCore(configuration);
            return await decoder.IdentifyAsync(configuration, stream, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

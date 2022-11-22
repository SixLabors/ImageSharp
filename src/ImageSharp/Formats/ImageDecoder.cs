// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Acts as a base class for image decoders.
/// Types that inherit this decoder are required to implement cancellable synchronous decoding operations only.
/// </summary>
public abstract class ImageDecoder : IImageDecoder
{
    /// <inheritdoc/>
    public Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => WithSeekableStream(
              options,
              stream,
              s => this.Decode<TPixel>(options, s, default));

    /// <inheritdoc/>
    public Image Decode(DecoderOptions options, Stream stream)
        => WithSeekableStream(
              options,
              stream,
              s => this.Decode(options, s, default));

    /// <inheritdoc/>
    public Task<Image<TPixel>> DecodeAsync<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => this.Decode<TPixel>(options, s, ct),
            cancellationToken);

    /// <inheritdoc/>
    public Task<Image> DecodeAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        => WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => this.Decode(options, s, ct),
            cancellationToken);

    /// <inheritdoc/>
    public IImageInfo Identify(DecoderOptions options, Stream stream)
          => WithSeekableStream(
              options,
              stream,
              s => this.Identify(options, s, default));

    /// <inheritdoc/>
    public Task<IImageInfo> IdentifyAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
         => WithSeekableStreamAsync(
             options,
             stream,
             (s, ct) => this.Identify(options, s, ct),
             cancellationToken);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}" /> of a specific pixel type.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}" />.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected abstract Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image" />.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image" />.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected abstract Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IImageInfo"/> object.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected abstract IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Performs a scaling operation against the decoded image. If the target size is not set, or the image size
    /// already matches the target size, the image is untouched.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="image">The decoded image.</param>
    protected static void ScaleToTargetSize(DecoderOptions options, Image image)
    {
        if (ShouldResize(options, image))
        {
            ResizeOptions resizeOptions = new()
            {
                Size = options.TargetSize.Value,
                Sampler = options.Sampler,
                Mode = ResizeMode.Max
            };

            image.Mutate(x => x.Resize(resizeOptions));
        }
    }

    /// <summary>
    /// Determines whether the decoded image should be resized.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="image">The decoded image.</param>
    /// <returns><see langword="true"/> if the image should be resized, otherwise; <see langword="false"/>.</returns>
    private static bool ShouldResize(DecoderOptions options, Image image)
    {
        if (options.TargetSize is null)
        {
            return false;
        }

        Size targetSize = options.TargetSize.Value;
        Size currentSize = image.Size();
        return currentSize.Width != targetSize.Width && currentSize.Height != targetSize.Height;
    }

    internal static T WithSeekableStream<T>(
        DecoderOptions options,
        Stream stream,
        Func<Stream, T> action)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        if (!stream.CanRead)
        {
            throw new NotSupportedException("Cannot read from the stream.");
        }

        T Action(Stream s, long position)
        {
            T result = action(s);

            // Issue #2259. Our buffered reads may have left the stream in an incorrect non-zero position.
            // Reset the position of the seekable stream if we did not read to the end to allow additional reads.
            if (stream.CanSeek && stream.Position != s.Position && s.Position != s.Length)
            {
                stream.Position = position + s.Position;
            }

            return result;
        }

        if (stream.CanSeek)
        {
            return Action(stream, stream.Position);
        }

        Configuration configuration = options.Configuration;
        using ChunkedMemoryStream memoryStream = new(configuration.MemoryAllocator);
        stream.CopyTo(memoryStream, configuration.StreamProcessingBufferSize);
        memoryStream.Position = 0;

        return Action(memoryStream, 0);
    }

    internal static async Task<T> WithSeekableStreamAsync<T>(
        DecoderOptions options,
        Stream stream,
        Func<Stream, CancellationToken, T> action,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        if (!stream.CanRead)
        {
            throw new NotSupportedException("Cannot read from the stream.");
        }

        T Action(Stream s, long position, CancellationToken ct)
        {
            T result = action(s, ct);

            // Issue #2259. Our buffered reads may have left the stream in an incorrect non-zero position.
            // Reset the position of the seekable stream if we did not read to the end to allow additional reads.
            if (stream.CanSeek && stream.Position != s.Position && s.Position != s.Length)
            {
                stream.Position = position + s.Position;
            }

            if (ct.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            return result;
        }

        // NOTE: We are explicitly not executing the action against the stream here as we do in WithSeekableStream() because that
        // would incur synchronous IO reads which must be avoided in this asynchronous method. Instead, we will *always* run the
        // code below to copy the stream to an in-memory buffer before invoking the action.
        if (stream is MemoryStream ms)
        {
            return Action(ms, ms.Position, cancellationToken);
        }

        if (stream is ChunkedMemoryStream cms)
        {
            return Action(cms, cms.Position, cancellationToken);
        }

        if (stream is BufferedReadStream brs && brs.BaseStream is MemoryStream)
        {
            return Action(brs, brs.Position, cancellationToken);
        }

        if (stream is BufferedReadStream brs2 && brs2.BaseStream is ChunkedMemoryStream)
        {
            return Action(brs2, brs2.Position, cancellationToken);
        }

        Configuration configuration = options.Configuration;
        using ChunkedMemoryStream memoryStream = new(configuration.MemoryAllocator);
        await stream.CopyToAsync(memoryStream, configuration.StreamProcessingBufferSize, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;
        return Action(memoryStream, 0, cancellationToken);
    }
}

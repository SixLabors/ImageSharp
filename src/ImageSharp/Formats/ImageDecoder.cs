// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

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
        => WithSeekableMemoryStreamAsync(
            options,
            stream,
            (s, ct) => this.Decode<TPixel>(options, s, ct),
            cancellationToken);

    /// <inheritdoc/>
    public Task<Image> DecodeAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        => WithSeekableMemoryStreamAsync(
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
         => WithSeekableMemoryStreamAsync(
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

        T PeformActionAndResetPosition(Stream s, long position)
        {
            T result = action(s);

            // Issue #2259. Our buffered reads may have left the stream in an incorrect non-zero position.
            // Reset the position of the seekable stream if we did not read to the end to allow additional reads.
            // The stream is always seekable in this scenario.
            if (stream.Position != s.Position && s.Position != s.Length)
            {
                stream.Position = position + s.Position;
            }

            return result;
        }

        if (stream.CanSeek)
        {
            return PeformActionAndResetPosition(stream, stream.Position);
        }

        Configuration configuration = options.Configuration;
        using ChunkedMemoryStream memoryStream = new(configuration.MemoryAllocator);
        stream.CopyTo(memoryStream, configuration.StreamProcessingBufferSize);
        memoryStream.Position = 0;

        return action(memoryStream);
    }

    internal static Task<T> WithSeekableMemoryStreamAsync<T>(
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

        Task<T> PeformActionAndResetPosition(Stream s, long position, CancellationToken ct)
        {
            try
            {
                T result = action(s, ct);

                // Issue #2259. Our buffered reads may have left the stream in an incorrect non-zero position.
                // Reset the position of the seekable stream if we did not read to the end to allow additional reads.
                // We check here that the input stream is seekable because it is not guaranteed to be so since
                // we always copy input streams of unknown type.
                if (stream.CanSeek && stream.Position != s.Position && s.Position != s.Length)
                {
                    stream.Position = position + s.Position;
                }

                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                return Task.FromCanceled<T>(cancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        // NOTE: We are explicitly not executing the action against the stream here as we do in WithSeekableStream() because that
        // would incur synchronous IO reads which must be avoided in this asynchronous method. Instead, we will *always* run the
        // code below to copy the stream to an in-memory buffer before invoking the action.
        if (stream is MemoryStream ms)
        {
            return PeformActionAndResetPosition(ms, ms.Position, cancellationToken);
        }

        if (stream is ChunkedMemoryStream cms)
        {
            return PeformActionAndResetPosition(cms, cms.Position, cancellationToken);
        }

        return CopyToMemoryStreamAndActionAsync(options, stream, PeformActionAndResetPosition, cancellationToken);
    }

    private static async Task<T> CopyToMemoryStreamAndActionAsync<T>(
        DecoderOptions options,
        Stream stream,
        Func<Stream, long, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        long position = stream.CanSeek ? stream.Position : 0;
        Configuration configuration = options.Configuration;
        using ChunkedMemoryStream memoryStream = new(configuration.MemoryAllocator);
        await stream.CopyToAsync(memoryStream, configuration.StreamProcessingBufferSize, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;
        return await action(memoryStream, position, cancellationToken).ConfigureAwait(false);
    }
}

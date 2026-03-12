// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Acts as a base class for image encoders.
/// Types that inherit this encoder are required to implement cancellable synchronous encoding operations only.
/// </summary>
public abstract class ImageEncoder : IImageEncoder
{
    /// <inheritdoc/>
    public bool SkipMetadata { get; init; }

    /// <inheritdoc/>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
         => this.EncodeWithSeekableStream(image, stream, default);

    /// <inheritdoc/>
    public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.EncodeWithSeekableStreamAsync(image, stream, cancellationToken);

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}" />.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}" /> to encode from.</param>
    /// <param name="stream">The <see cref="Stream" /> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected abstract void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    private void EncodeWithSeekableStream<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        image.SynchronizeMetadata();

        Configuration configuration = image.Configuration;
        if (stream.CanSeek)
        {
            this.Encode(image, stream, cancellationToken);
        }
        else
        {
            using ChunkedMemoryStream ms = new(configuration.MemoryAllocator);
            this.Encode(image, ms, cancellationToken);
            ms.Position = 0;
            ms.CopyTo(stream, configuration.StreamProcessingBufferSize);
        }
    }

    private async Task EncodeWithSeekableStreamAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        image.SynchronizeMetadata();

        Configuration configuration = image.Configuration;
        if (stream.CanSeek)
        {
            await DoEncodeAsync(stream).ConfigureAwait(false);
        }
        else
        {
            await using ChunkedMemoryStream ms = new(configuration.MemoryAllocator);
            await DoEncodeAsync(ms);
            ms.Position = 0;
            await ms.CopyToAsync(stream, configuration.StreamProcessingBufferSize, cancellationToken)
                    .ConfigureAwait(false);
        }

        Task DoEncodeAsync(Stream innerStream)
        {
            try
            {
                // TODO: Are synchronous IO writes OK? We avoid reads.
                this.Encode(image, innerStream, cancellationToken);
                return Task.CompletedTask;
            }
            catch (OperationCanceledException)
            {
                return Task.FromCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }
    }
}

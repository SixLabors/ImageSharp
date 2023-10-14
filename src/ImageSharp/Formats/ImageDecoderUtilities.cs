// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Utility methods for <see cref="IImageDecoderInternals"/>.
/// </summary>
internal static class ImageDecoderUtilities
{
    internal static ImageInfo Identify(
        this IImageDecoderInternals decoder,
        Configuration configuration,
        Stream stream,
        CancellationToken cancellationToken)
    {
        using BufferedReadStream bufferedReadStream = new(configuration, stream, cancellationToken);

        try
        {
            return decoder.Identify(bufferedReadStream, cancellationToken);
        }
        catch (InvalidMemoryOperationException ex)
        {
            throw new InvalidImageContentException(decoder.Dimensions, ex);
        }
        catch (Exception)
        {
            throw;
        }
    }

    internal static Image<TPixel> Decode<TPixel>(
        this IImageDecoderInternals decoder,
        Configuration configuration,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        => decoder.Decode<TPixel>(configuration, stream, DefaultLargeImageExceptionFactory, cancellationToken);

    internal static Image<TPixel> Decode<TPixel>(
        this IImageDecoderInternals decoder,
        Configuration configuration,
        Stream stream,
        Func<InvalidMemoryOperationException, Size, InvalidImageContentException> largeImageExceptionFactory,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Test may pass a BufferedReadStream in order to monitor EOF hits, if so, use the existing instance.
        BufferedReadStream bufferedReadStream = stream as BufferedReadStream ?? new BufferedReadStream(configuration, stream, cancellationToken);

        try
        {
            return decoder.Decode<TPixel>(bufferedReadStream, cancellationToken);
        }
        catch (InvalidMemoryOperationException ex)
        {
            throw largeImageExceptionFactory(ex, decoder.Dimensions);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            if (bufferedReadStream != stream)
            {
                bufferedReadStream.Dispose();
            }
        }
    }

    private static InvalidImageContentException DefaultLargeImageExceptionFactory(
        InvalidMemoryOperationException memoryOperationException,
        Size dimensions) =>
        new(dimensions, memoryOperationException);
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

internal static class ImageEncoderUtilities
{
    public static async Task EncodeAsync<TPixel>(
        this IImageEncoderInternals encoder,
        Image<TPixel> image,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = image.GetConfiguration();
        if (stream.CanSeek)
        {
            await DoEncodeAsync(stream).ConfigureAwait(false);
        }
        else
        {
            using MemoryStream ms = new();
            await DoEncodeAsync(ms);
            ms.Position = 0;
            await ms.CopyToAsync(stream, configuration.StreamProcessingBufferSize, cancellationToken)
                    .ConfigureAwait(false);
        }

        Task DoEncodeAsync(Stream innerStream)
        {
            try
            {
                encoder.Encode(image, innerStream, cancellationToken);
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

    public static void Encode<TPixel>(
        this IImageEncoderInternals encoder,
        Image<TPixel> image,
        Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => encoder.Encode(image, stream, default);
}

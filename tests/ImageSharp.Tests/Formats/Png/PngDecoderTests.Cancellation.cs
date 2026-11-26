// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

public partial class PngDecoderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decode_DisposesChunkDataBuffer_WhenChunkReadIsCancelled(bool identifyOnly)
    {
        byte[] pngBytes;
        using (Image<Rgba32> source = new(4, 4))
        using (MemoryStream encoded = new())
        {
            source.SaveAsPng(encoded);
            pngBytes = encoded.ToArray();
        }

        using CancellationTokenSource cts = new();
        using PausedMemoryStream stream = new(pngBytes);

        stream.OnWaiting(s =>
        {
            // The signature uses bytes 0-7, and the chunk length and type use bytes 8-15. The decoder reads the length and type in one read. The test cancels
            // during the read of the chunk data, after BufferedReadStream checked its token. That read completes. The CRC read then checks the cancellation
            // after the decoder allocated the data.
            if (s.Position == 16)
            {
                cts.Cancel();
                stream.Release();
            }
            else
            {
                stream.Next();
            }
        });

        Configuration configuration = Configuration.CreateDefaultInstance();

        // A small buffer makes each read of the decoder reach the stream, so the cancellation occurs inside one read.
        configuration.StreamProcessingBufferSize = 4;
        DecoderOptions options = new(configuration);

        // Calling the decoder directly avoids cancellation during format detection.
        if (identifyOnly)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await PngDecoder.Instance.IdentifyAsync(options, stream, cts.Token);
            });
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using Image<Rgba32> image =
                    await PngDecoder.Instance.DecodeAsync<Rgba32>(options, stream, cts.Token);
            });
        }

        Assert.True(cts.IsCancellationRequested);
    }

    /// <summary>
    /// Cancels a decode at each stream operation in turn, for each integrity mode. The CRC check of image data runs only
    /// when the mode does not ignore image data errors, so each mode reads a different sequence of stream operations.
    /// Each run must throw <see cref="OperationCanceledException"/> or complete, and must return each buffer that it
    /// allocates.
    /// </summary>
    /// <param name="integrity">The integrity mode of the decode.</param>
    /// <param name="identifyOnly">Whether to identify the image instead of decoding it.</param>
    /// <returns>The task.</returns>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict, true)]
    [InlineData(SegmentIntegrityHandling.Strict, false)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary, true)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary, false)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData, true)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData, false)]
    public async Task Decode_ReturnsEachBuffer_WhenCancelledAtEachStreamOperation(SegmentIntegrityHandling integrity, bool identifyOnly)
    {
        byte[] pngBytes = CreateMultiPixelPngBytes();

        // A run with no cancellation gives the count of stream operations.
        int operationCount = 0;
        using (PausedMemoryStream stream = new(pngBytes))
        {
            stream.OnWaiting(() =>
            {
                operationCount++;
                stream.Next();
            });

            await DecodeOrIdentifyAsync(stream, CreateOptions(new TestMemoryAllocator(), integrity), identifyOnly, CancellationToken.None);
        }

        int cancelledCount = 0;
        for (int cancelAt = 0; cancelAt < operationCount; cancelAt++)
        {
            TestMemoryAllocator allocator = new();
            allocator.EnableNonThreadSafeLogging();

            using CancellationTokenSource cts = new();
            using PausedMemoryStream stream = new(pngBytes);
            int operation = 0;

            // The operation completes after the cancellation. Thus the next token check of the decoder observes it.
            stream.OnWaiting(() =>
            {
                if (operation++ == cancelAt)
                {
                    cts.Cancel();
                    stream.Release();
                }
                else
                {
                    stream.Next();
                }
            });

            try
            {
                await DecodeOrIdentifyAsync(stream, CreateOptions(allocator, integrity), identifyOnly, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelledCount++;
            }

            Assert.Equal(0, allocator.LiveBufferBytes);
            Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        }

        // A cancellation before the last token check stops the decode.
        Assert.True(cancelledCount > 0);
    }

    /// <summary>
    /// Creates decoder options that read 4 bytes for each stream buffer fill, so that each chunk field is a separate
    /// stream operation.
    /// </summary>
    /// <param name="allocator">The allocator of the decode.</param>
    /// <param name="integrity">The integrity mode of the decode.</param>
    /// <returns>The options.</returns>
    private static DecoderOptions CreateOptions(TestMemoryAllocator allocator, SegmentIntegrityHandling integrity)
    {
        Configuration configuration = Configuration.CreateDefaultInstance();
        configuration.MemoryAllocator = allocator;
        configuration.StreamProcessingBufferSize = 4;
        return new DecoderOptions(configuration) { SegmentIntegrityHandling = integrity };
    }

    /// <summary>
    /// Decodes or identifies the image, and disposes a decoded image.
    /// </summary>
    /// <remarks>
    /// The call goes to the PNG decoder directly, so format detection does not read the stream.
    /// </remarks>
    /// <param name="stream">The image stream.</param>
    /// <param name="options">The decoder options.</param>
    /// <param name="identifyOnly">Whether to identify the image instead of decoding it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    private static async Task DecodeOrIdentifyAsync(
        Stream stream,
        DecoderOptions options,
        bool identifyOnly,
        CancellationToken cancellationToken)
    {
        if (identifyOnly)
        {
            await PngDecoder.Instance.IdentifyAsync(options, stream, cancellationToken);
            return;
        }

        using Image<Rgba32> image = await PngDecoder.Instance.DecodeAsync<Rgba32>(options, stream, cancellationToken);
    }

    private static byte[] CreateMultiPixelPngBytes()
    {
        using Image<Rgba32> source = new(16, 16);
        using MemoryStream encoded = new();
        source.SaveAsPng(encoded);
        return encoded.ToArray();
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
            // The signature occupies bytes 0-7 and the chunk length occupies bytes 8-11.
            // Cancel during the type read, after BufferedReadStream has checked its token.
            // That read completes; the next read checks cancellation after allocating chunk data.
            if (s.Position == 12)
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

        // Read the chunk length and type separately so cancellation occurs during the type read.
        configuration.StreamProcessingBufferSize = 4;
        DecoderOptions options = new() { Configuration = configuration };

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
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decode_DisposesChunkDataBuffer_WhenCancelledDuringChunkCrcRead(bool identifyOnly)
    {
        byte[] pngBytes = CreateMultiPixelPngBytes();
        int idatCrcOffset = FindFirstChunkCrcOffset(pngBytes, "IDAT");

        using CancellationTokenSource cts = new();
        using PausedMemoryStream stream = new(pngBytes);

        // BufferedReadStream reads the IDAT data directly from this stream and then seeks this stream to the end of
        // the data. That seek is the first operation at the CRC offset. Cancel during it, so that the CRC read in
        // ValidateChunk is the first token check after the chunk data is allocated.
        stream.OnWaiting(s =>
        {
            if (s.Position == idatCrcOffset)
            {
                cts.Cancel();
                stream.Release();
            }
            else
            {
                stream.Next();
            }
        });

        await AssertCancelledDecodeDisposesAllAllocationsAsync(stream, cts.Token, identifyOnly);
        Assert.True(cts.IsCancellationRequested);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decode_DisposesChunkDataBuffer_WhenCancelledDuringIdatPositionRestore(bool identifyOnly)
    {
        byte[] pngBytes = CreateMultiPixelPngBytes();
        int idatCrcOffset = FindFirstChunkCrcOffset(pngBytes, "IDAT");

        using CancellationTokenSource cts = new();
        using PausedMemoryStream stream = new(pngBytes);
        int operationsAtCrcOffset = 0;

        // The first operation at the CRC offset is the seek after the direct IDAT data read. The second is the read
        // that fills the BufferedReadStream buffer with the CRC. Cancel during that read. The read completes, so
        // restoring the IDAT position in TryReadChunk is the first token check after it.
        stream.OnWaiting(s =>
        {
            if (s.Position == idatCrcOffset && ++operationsAtCrcOffset == 2)
            {
                cts.Cancel();
                stream.Release();
            }
            else
            {
                stream.Next();
            }
        });

        await AssertCancelledDecodeDisposesAllAllocationsAsync(stream, cts.Token, identifyOnly);
        Assert.True(cts.IsCancellationRequested);
    }

    private static async Task AssertCancelledDecodeDisposesAllAllocationsAsync(
        Stream stream,
        CancellationToken cancellationToken,
        bool identifyOnly)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.CreateDefaultInstance();
        configuration.MemoryAllocator = allocator;
        configuration.StreamProcessingBufferSize = 4;
        DecoderOptions options = new() { Configuration = configuration };

        if (identifyOnly)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await PngDecoder.Instance.IdentifyAsync(options, stream, cancellationToken));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using Image<Rgba32> image =
                    await PngDecoder.Instance.DecodeAsync<Rgba32>(options, stream, cancellationToken);
            });
        }

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
    }

    private static byte[] CreateMultiPixelPngBytes()
    {
        using Image<Rgba32> source = new(16, 16);
        using MemoryStream encoded = new();
        source.SaveAsPng(encoded);
        return encoded.ToArray();
    }

    private static int FindFirstChunkCrcOffset(byte[] pngBytes, string chunkType)
    {
        int offset = 8;
        while (offset + 12 <= pngBytes.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(offset));
            string type = Encoding.ASCII.GetString(pngBytes, offset + 4, 4);
            int crcOffset = offset + 8 + length;
            if (type == chunkType)
            {
                return crcOffset;
            }

            offset = crcOffset + 4;
        }

        throw new InvalidOperationException($"Chunk '{chunkType}' not found.");
    }
}

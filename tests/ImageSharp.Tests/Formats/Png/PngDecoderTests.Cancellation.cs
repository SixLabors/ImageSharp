// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

public partial class PngDecoderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Decode_DisposesChunkDataBuffer_WhenChunkReadIsCancelled(bool identifyOnly)
    {
        // Every PNG starts with an 8-byte signature followed by the first chunk's 4-byte length
        // and 4-byte type fields, so byte 16 is always the start of that chunk's data (IHDR here).
        // Cancelling exactly there reproduces the leak from GH issue #3202: ReadChunkData rents
        // its buffer from the MemoryAllocator and only then reads into it, so a cancellation
        // observed by that read leaves the rented buffer undisposed without the try/finally.
        //
        // PngDecoder.Instance is used directly (rather than Image.Load/Identify) to bypass the
        // generic format-detection probe, which reads the stream header independently and would
        // otherwise observe the cancellation itself before PNG-specific parsing starts.
        const int firstChunkDataOffset = 16;
        byte[] pngBytes;
        using (Image<Rgba32> source = new(4, 4))
        using (MemoryStream encoded = new())
        {
            source.SaveAsPng(encoded);
            pngBytes = encoded.ToArray();
        }

        using CancelAtPositionStream stream = new(pngBytes, firstChunkDataOffset);

        // BufferedReadStream prefetches up to this size into its own internal buffer, so a large
        // size would let it read straight through the cancellation point in one call before
        // PngDecoderCore.ReadChunkData ever allocates its buffer. Matching the buffer size to the
        // cancellation point makes the length/type fields resolve from one prefetch and the
        // chunk-data read (which exceeds the remaining buffer) go directly to the stream instead.
        Configuration configuration = Configuration.CreateDefaultInstance();
        configuration.StreamProcessingBufferSize = firstChunkDataOffset - 8;
        DecoderOptions options = new() { Configuration = configuration };

        if (identifyOnly)
        {
            Assert.ThrowsAny<OperationCanceledException>(() => PngDecoder.Instance.Identify(options, stream));
        }
        else
        {
            Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                using Image<Rgba32> image = PngDecoder.Instance.Decode<Rgba32>(options, stream);
            });
        }
    }

    /// <summary>
    /// A stream over an in-memory PNG that throws <see cref="OperationCanceledException"/> once a
    /// read is requested at or past <paramref name="cancelAtPosition"/>, regardless of the
    /// requested length. Using the stream position (rather than the requested length) to decide
    /// when to throw keeps the trigger point exact even though <see cref="BufferedReadStream"/>
    /// prefetches more than the caller asks for.
    /// </summary>
    /// <remarks>
    /// Only the <c>byte[]</c> overload of <see cref="Read(byte[], int, int)"/> is overridden.
    /// <see cref="MemoryStream"/> does not implement <see cref="Stream.Read(Span{byte})"/> in
    /// terms of this overload (or vice versa), so overriding both and having each delegate to the
    /// other would recurse indefinitely.
    /// </remarks>
    private sealed class CancelAtPositionStream(byte[] data, int cancelAtPosition) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.Position >= cancelAtPosition)
            {
                throw new OperationCanceledException();
            }

            return base.Read(buffer, offset, count);
        }
    }
}

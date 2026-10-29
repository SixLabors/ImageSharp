// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Webp.BitWriter;
using SixLabors.ImageSharp.Formats.Webp.Lossy;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class Vp8BitWriterTests
{
    [Fact]
    public void BitWriterResize_WithinCapacity_DoesNotReallocate()
    {
        using Vp8Encoder encoder = CreateEncoder();
        Vp8BitWriter writer = new(4096, encoder);
        byte[] buffer = writer.Buffer;

        writer.BitWriterResize(1);
        writer.BitWriterResize(4096);

        Assert.Same(buffer, writer.Buffer);
        Assert.Equal(4096, writer.Buffer.Length);
    }

    [Fact]
    public void BitWriterResize_GrowsBufferGeometrically()
    {
        const int targetBytes = 256 * 1024;
        using Vp8Encoder encoder = CreateEncoder();
        Vp8BitWriter writer = new(1024, encoder);
        Random random = new(42);
        byte[] buffer = writer.Buffer;
        int reallocations = 0;

        // Emit pseudo-random modes until the partition holds at least targetBytes, counting how often
        // the backing buffer is replaced.
        while (writer.NumBytes < targetBytes)
        {
            writer.PutUvMode(random.Next(4));
            if (!ReferenceEquals(buffer, writer.Buffer))
            {
                reallocations++;
                buffer = writer.Buffer;
            }
        }

        writer.Finish();

        Assert.True(writer.Buffer.Length >= writer.NumBytes);

        // Growing by 1.5x (rounded up to the next KiB) from 1 KiB needs 12 reallocations to hold 256 KiB.
        // Growing in 1 KiB steps (one reallocation per KiB written) would need 256.
        Assert.InRange(reallocations, 1, 16);
    }

    private static Vp8Encoder CreateEncoder()
        => new(
            Configuration.Default.MemoryAllocator,
            Configuration.Default,
            16,
            16,
            75,
            false,
            WebpEncodingMethod.Default,
            1,
            60,
            50,
            false);
}

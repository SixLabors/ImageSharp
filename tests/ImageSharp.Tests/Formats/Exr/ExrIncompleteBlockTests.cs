// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

/// <summary>
/// Tests RLE and B44 blocks that end before the block's pixel data is complete.
/// </summary>
[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrIncompleteBlockTests
{
    // The 256x1 test image stores one FLOAT red sample per pixel, so one block holds 1024 bytes.
    private const int FloatBlockLength = 256 * sizeof(float);

    /// <summary>
    /// Incomplete RLE blocks are rejected unless image-data recovery is enabled.
    /// </summary>
    /// <param name="length">The number of decoded bytes the block supplies.</param>
    /// <param name="integrity">The image-data integrity policy.</param>
    [Theory]
    [InlineData(0, SegmentIntegrityHandling.Strict)]
    [InlineData(520, SegmentIntegrityHandling.Strict)]
    [InlineData(0, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(520, SegmentIntegrityHandling.IgnoreAncillary)]
    public void Decode_IncompleteRunLengthBlock_Throws(int length, SegmentIntegrityHandling integrity)
    {
        byte[] data = BuildRunLengthExr(length);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(options, data));
    }

    /// <summary>
    /// A recovered RLE block keeps its complete samples and never exposes pooled bytes.
    /// </summary>
    /// <param name="length">The number of decoded bytes the block supplies.</param>
    /// <param name="completeSamples">The number of samples that have both of their byte halves.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(520, 4)]
    public void Decode_IncompleteRunLengthBlock_IgnoreImageData_KeepsCompleteSamples(int length, int completeSamples)
    {
        byte[] data = BuildRunLengthExr(length);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator(0x3F);
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        using Image<RgbaVector> image = Image.Load<RgbaVector>(options, data);
        AssertRedPrefix(image, completeSamples);
    }

    /// <summary>
    /// Incomplete B44 blocks are rejected unless image-data recovery is enabled.
    /// </summary>
    /// <param name="tiles">The number of complete 4x4 tiles the block supplies.</param>
    /// <param name="integrity">The image-data integrity policy.</param>
    [Theory]
    [InlineData(0, SegmentIntegrityHandling.Strict)]
    [InlineData(1, SegmentIntegrityHandling.Strict)]
    [InlineData(0, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(1, SegmentIntegrityHandling.IgnoreAncillary)]
    public void Decode_IncompleteB44Block_Throws(int tiles, SegmentIntegrityHandling integrity)
    {
        byte[] data = BuildB44Exr(tiles);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(options, data));
    }

    /// <summary>
    /// A recovered B44 block keeps its complete tiles and never exposes pooled bytes.
    /// </summary>
    /// <param name="tiles">The number of complete 4x4 tiles the block supplies.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Decode_IncompleteB44Block_IgnoreImageData_KeepsCompleteTiles(int tiles)
    {
        byte[] data = BuildB44Exr(tiles);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator(0x3F);
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        using Image<RgbaVector> image = Image.Load<RgbaVector>(options, data);

        // Each tile covers four pixels of the single row.
        AssertRedPrefix(image, tiles * 4);
    }

    /// <summary>
    /// Asserts that the first pixels have red 1 and that all other pixels are black.
    /// </summary>
    /// <param name="image">The decoded 256x1 image.</param>
    /// <param name="count">The number of pixels with red 1.</param>
    private static void AssertRedPrefix(Image<RgbaVector> image, int count)
    {
        Assert.Equal(new Size(256, 1), image.Size);

        // The file has only a red channel. The decoder sets green and blue to 0 and alpha to 1.
        // Color conversion can move red 1 by a few millionths. Stale pooled bytes decode far outside the tolerance.
        ApproximateFloatComparer comparer = new(1e-4F);
        for (int x = 0; x < image.Width; x++)
        {
            Vector4 expected = new(x < count ? 1 : 0, 0, 0, 1);
            Assert.Equal(expected, image[x, 0].ToVector4(), comparer);
        }
    }

    /// <summary>
    /// Builds a FLOAT RLE image whose block stops after <paramref name="length"/> decoded bytes.
    /// </summary>
    /// <param name="length">The number of decoded bytes the block supplies.</param>
    /// <returns>The encoded image.</returns>
    private static byte[] BuildRunLengthExr(int length)
    {
        // EXR RLE decodes to a predictor stream. Reconstructed, the stream holds byte 0 of every
        // sample pair in the first half and byte 1 in the second half. For float 1 (00 00 80 3F),
        // the first half repeats 00 80 and the second half repeats 00 3F.
        byte[] reconstructed = new byte[FloatBlockLength];
        int half = reconstructed.Length / 2;
        for (int i = 0; i < half; i += 2)
        {
            reconstructed[i + 1] = 0x80;
            reconstructed[half + i + 1] = 0x3F;
        }

        // A prefix that ends 8 bytes into the second half has both halves of the first 16 bytes.
        // Those 16 bytes are the first 4 FLOAT samples.
        byte[] predicted = new byte[length];
        for (int i = 0; i < length; i++)
        {
            // The decoder computes t[i] = t[i - 1] + p[i] - 128, so p[i] = t[i] - t[i - 1] + 128.
            predicted[i] = i == 0 ? reconstructed[0] : (byte)(reconstructed[i] - reconstructed[i - 1] + 128);
        }

        // Store the predictor bytes as literal runs. A negative header byte -n is followed by n literal bytes.
        using MemoryStream encoded = new();
        for (int i = 0; i < predicted.Length; i += 127)
        {
            int count = Math.Min(127, predicted.Length - i);
            encoded.WriteByte((byte)(sbyte)-count);
            encoded.Write(predicted, i, count);
        }

        return ExrZipDecoderTests.BuildExr(encoded.ToArray(), ExrPixelType.Float, (byte)ExrCompression.RunLengthEncoded, 0);
    }

    /// <summary>
    /// Builds a HALF B44 image whose block stops after <paramref name="tiles"/> complete tiles.
    /// </summary>
    /// <param name="tiles">The number of complete 4x4 tiles the block supplies.</param>
    /// <returns>The encoded image.</returns>
    private static byte[] BuildB44Exr(int tiles)
    {
        // A 3-byte B44 tile is a flat tile. A third byte of 52 or more selects that form.
        // The first two bytes hold an ordered HALF value. 0xBC00 maps to HALF 0x3C00, which is 1.
        // The 256x1 row needs 64 tiles, so any smaller count leaves the block incomplete.
        byte[] encoded = new byte[tiles * 3];
        for (int i = 0; i < encoded.Length; i += 3)
        {
            encoded[i] = 0xBC;
            encoded[i + 1] = 0x00;
            encoded[i + 2] = 0xFF;
        }

        return ExrZipDecoderTests.BuildExr(encoded, ExrPixelType.Half, (byte)ExrCompression.B44, 0);
    }
}

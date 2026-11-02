// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
public class JxlBitReaderTests
{
    /// <summary>
    /// Verifies bit order, zero-width fields, complete 32-bit reads and a reservoir refill across container fragments.
    /// </summary>
    /// <param name="file">The stored raw or fragmented codestream.</param>
    /// <param name="windowSize">The borrowed window size. Eight bytes forces physical refills; 64 retains later fields.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Splines, 8)]
    [InlineData(TestImages.Jxl.Splines, 64)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0, 8)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0, 64)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, 8)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, 64)]
    public void ReadBitsRetainsFieldOrderAcrossRefills(string file, int windowSize)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader codestream = new(buffered);
        InlineArray64<byte> readerBuffer = default;
        JxlBitReader reader = new(codestream, readerBuffer[..windowSize]);

        // These literal values describe the first twelve stored bytes of libjxl's splines codestream.
        // They are independent of the container reader and include the transition to a second 64-bit reservoir.
        Assert.Equal(0U, reader.ReadBits(0));
        Assert.Equal(0, codestream.Position);
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        Assert.Equal(0x90013FFAU, reader.ReadBits(32));
        Assert.Equal(0xD8U, reader.ReadBits(8));
        Assert.Equal(0x63U, reader.ReadBits(8));
        Assert.Equal(0x00E00010U, reader.ReadBits(32));
        Assert.Equal(96, reader.Position);

        // An ended section must retain later prefetched window bytes without consuming them early.
        reader.SeekBytePosition(0);
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        reader.BeginSection(1);
        Assert.Equal(0xFAU, reader.ReadBits(8));
        reader.EndSection();
        Assert.Equal(0x90013FU, reader.ReadBits(24));

        // Bit and byte skips must drain both caches before advancing the physical source. The next three
        // bytes at offset nine are 00 E0 00 in the independently recorded twelve-byte prefix above.
        reader.SeekBytePosition(0);
        reader.ReadBits(1);
        reader.SkipBits(71);
        Assert.Equal(0x00E000U, reader.ReadBits(24));
        reader.SeekBytePosition(0);
        reader.ReadBits(8);
        Assert.True(reader.TrySkipBytes(8));
        Assert.Equal(0x00E000U, reader.ReadBits(24));

        // An aligned byte copy follows the same cache order as bit reads.
        reader.SeekBytePosition(0);
        reader.ReadBits(8);
        InlineArray8<byte> byteFields = default;
        reader.ReadBytes(byteFields);
        ReadOnlySpan<byte> expectedFields = [0x0A, 0xFA, 0x3F, 0x01, 0x90, 0xD8, 0x63, 0x10];

        Assert.True(((ReadOnlySpan<byte>)byteFields).SequenceEqual(expectedFields));
        Assert.Equal(72, reader.Position);

        // Start a one-byte section and prefetch its bits, then move back to the signature.
        // Reading sixteen bits proves that seeking discards both the cached bits and the previous eight-bit section bound.
        reader.SeekBytePosition(0);
        reader.BeginSection(1);
        Assert.Equal(1U, reader.ReadBits(1));
        reader.SeekBytePosition(0);
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
    }

    /// <summary>
    /// Verifies size headers against the native source dimensions and the Rust reference inspector.
    /// </summary>
    /// <param name="file">The stored raw or fragmented codestream.</param>
    /// <param name="width">The independently verified image width.</param>
    /// <param name="height">The independently verified image height.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Splines, 2048, 2048)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0, 2048, 2048)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, 2048, 2048)]
    [InlineData(TestImages.Jxl.SquareExtendedSizeContainer, 8, 8)]
    [InlineData(TestImages.Jxl.ExifXmp, 1, 1)]
    [InlineData(TestImages.Jxl.CroppedTrafficLight, 50, 80)]
    public void ReadSizeMatchesReferenceDimensions(string file, int width, int height)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader codestream = new(buffered);
        InlineArray64<byte> readerBuffer = default;
        JxlBitReader reader = new(codestream, readerBuffer);

        Assert.Equal(0x0AFFU, reader.ReadBits(16));

        Assert.Equal(new Size(width, height), reader.ReadSize());
    }

    /// <summary>
    /// Verifies that aspect-ratio multiplication cannot wrap a large width into a signed image dimension.
    /// </summary>
    [Fact]
    public void ReadSizeRejectsWidthOutsideImageDimensionRange()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WidthExceedsInt32).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        // The stored header declares height 2^30 and ratio 2:1. Its derived width is 2^31, beyond Size's int range.
        InvalidImageContentException error = Assert.Throws<InvalidImageContentException>(() =>
        {
            InlineArray64<byte> readerBuffer = default;
            JxlBitReader reader = new(new JxlCodestreamReader(buffered), readerBuffer);
            Assert.Equal(0x0AFFU, reader.ReadBits(16));

            reader.ReadSize();
        });

        Assert.Contains("width", error.Message);
    }

    /// <summary>
    /// Verifies that unwritten reservoir bytes cannot become valid bits at the end of a codestream.
    /// </summary>
    [Fact]
    public void ReadBitsRejectsOverread()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        Assert.Throws<InvalidImageContentException>(() =>
        {
            InlineArray64<byte> readerBuffer = default;
            JxlBitReader reader = new(new JxlCodestreamReader(buffered), readerBuffer);

            // The stored codestream contains 81 bytes. Consume every actual bit, including the final short refill.
            for (int i = 0; i < 20; i++)
            {
                reader.ReadBits(32);
            }

            reader.ReadBits(8);

            Assert.Equal(648, reader.Position);
            reader.ReadBits(1);
        });
    }
}

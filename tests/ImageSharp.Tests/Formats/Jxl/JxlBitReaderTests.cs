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
    [Theory]
    [InlineData(TestImages.Jxl.Splines)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1)]
    public void ReadBitsRetainsFieldOrderAcrossRefills(string file)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader codestream = new(buffered);
        JxlBitReader reader = new(codestream);

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
        JxlBitReader reader = new(codestream);

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
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));

        // The stored header declares height 2^30 and ratio 2:1. Its derived width is 2^31, beyond Size's int range.
        InvalidImageContentException error = Assert.Throws<InvalidImageContentException>(() => reader.ReadSize());

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
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));

        // The stored codestream contains 81 bytes. Consume every actual bit, including the final short refill.
        for (int i = 0; i < 20; i++)
        {
            reader.ReadBits(32);
        }

        // Only the stored final byte, 0x03, remains. A short refill must not consume that byte on failure.
        const string boundaryError = "The JPEG XL field exceeds its section or codestream boundary.";
        InvalidImageContentException shortRead = Assert.Throws<InvalidImageContentException>(() => reader.ReadBits(32));
        Assert.Equal(boundaryError, shortRead.Message);
        Assert.Equal(640, reader.Position);

        // Prefix lookup can zero-pad its high bits, but those bits must not become valid consumption.
        Assert.Equal(0x03U, reader.PeekBits(32));
        InvalidImageContentException paddedConsumption = Assert.Throws<InvalidImageContentException>(() => reader.ConsumeBits(9));
        Assert.Equal(boundaryError, paddedConsumption.Message);
        Assert.Equal(640, reader.Position);

        reader.ReadBits(8);

        Assert.Equal(648, reader.Position);
        Assert.Throws<InvalidImageContentException>(() => reader.ReadBits(1));
    }

    /// <summary>
    /// Verifies that section lookahead hides following bytes and section completion preserves those cached bytes.
    /// </summary>
    [Fact]
    public void EndSectionRetainsFollowingLookahead()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        reader.BeginSection(1);

        // The first reservoir also contains the following 3F byte. Lookahead can expose only FA from this section.
        Assert.Equal(0xFAU, reader.PeekBits(16));
        Assert.Throws<InvalidImageContentException>(() => reader.ReadBits(16));
        Assert.Equal(16, reader.Position);
        Assert.Equal(2U, reader.ReadBits(3));
        reader.EndSection();

        Assert.Equal(24, reader.Position);
        Assert.Equal(0x3FU, reader.ReadBits(8));
    }

    /// <summary>
    /// Verifies byte fields and their remaining section budget at byte-aligned and unaligned bit positions.
    /// </summary>
    /// <param name="prefixBits">The bits consumed before the byte field.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ReadBytesRetainsSectionBudget(int prefixBits)
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        reader.BeginSection(10);
        reader.ReadBits(prefixBits);
        byte[] actual = new byte[9];
        reader.ReadBytes(actual);

        // The stored prefix is FF 0A FA 3F 01 90 D8 63 10 00. An unaligned byte field joins its bits least-significant first.
        byte[] expected = prefixBits == 0
            ? [0xFF, 0x0A, 0xFA, 0x3F, 0x01, 0x90, 0xD8, 0x63, 0x10]
            : [0x5F, 0x41, 0xFF, 0x27, 0x00, 0x12, 0x7B, 0x0C, 0x02];

        Assert.Equal(expected, actual);
        Assert.Equal(72 + prefixBits, reader.Position);
        reader.CheckBitBudget((ulong)(8 - prefixBits));
        Assert.Throws<InvalidImageContentException>(() => reader.CheckBitBudget((ulong)(9 - prefixBits)));
        Assert.Equal(0U, reader.ReadBits(8 - prefixBits));
        Assert.Throws<InvalidImageContentException>(() => reader.ReadBits(1));
        Assert.Equal(80, reader.Position);

        reader.EndSection();
        Assert.Equal(0x00E0U, reader.ReadBits(16));
    }

    /// <summary>
    /// Verifies that a partial byte skip stops at the section boundary without consuming cached following bytes.
    /// </summary>
    [Fact]
    public void TrySkipBytesStopsAtSectionBoundary()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        Assert.Equal(0x0AFFU, reader.ReadBits(16));
        reader.BeginSection(2);

        // Only FA 3F belong to this section. The same reservoir retains 01 for the following section.
        Assert.False(reader.TrySkipBytes(3));
        Assert.Equal(32, reader.Position);
        Assert.Equal(0U, reader.PeekBits(8));
        reader.EndSection();

        Assert.Equal(1U, reader.ReadBits(8));
    }

    /// <summary>
    /// Verifies that a short physical skip accounts for bytes consumed before the final codestream boundary.
    /// </summary>
    [Fact]
    public void TrySkipBytesRetainsPositionAtCodestreamEnd()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.Splines).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        reader.SeekBytePosition(79);
        reader.BeginSection(3);

        // The stored codestream has 81 bytes. A three-byte section starting at byte 79 has only two actual bytes.
        Assert.False(reader.TrySkipBytes(3));
        Assert.Equal(648, reader.Position);
        Assert.Throws<InvalidImageContentException>(() => reader.ReadBits(1));
        Assert.Equal(648, reader.Position);
    }
}

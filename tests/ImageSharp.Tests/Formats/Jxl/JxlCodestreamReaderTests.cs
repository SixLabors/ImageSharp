// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
public class JxlCodestreamReaderTests
{
    /// <summary>
    /// Verifies backward and forward section reads against the original stored codestream bytes.
    /// </summary>
    /// <param name="file">The raw or fragmented input.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Splines)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1)]
    public void SeekPreservesCodestreamAcrossFragmentBoundaries(string file)
    {
        byte[] expected = TestFile.Create(TestImages.Jxl.Splines).Bytes;
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader reader = new(buffered);
        InlineArray16<byte> storage = default;
        Span<byte> actual = storage;
        ReadOnlySpan<int> positions = [0, expected.Length / 2, 5, expected.Length - actual.Length, 0, 17];

        // This order crosses both discovered and undiscovered ranges, then returns to cached ranges.
        // The version-one fixture stores fragments out of logical order; bytes still match the unchanged raw file.
        foreach (int position in positions)
        {
            Assert.True(reader.TrySeek(position));
            int count = Math.Min(actual.Length, expected.Length - position);
            int consumed = 0;
            while (consumed < count)
            {
                int read = reader.Read(actual[consumed..count]);
                Assert.InRange(read, 1, count - consumed);
                consumed += read;
            }

            Assert.Equal(expected.AsSpan(position, count), actual[..count]);
            Assert.Equal(position + count, reader.Position);
        }

        // A valid final fragment can end before a requested section. Report that short payload
        // separately from malformed fragment headers, which still throw during traversal.
        Assert.False(reader.TrySeek(expected.Length + 1));

        reader.Complete();
        Assert.Empty(reader.Boxes);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies that completing a partially read container leaves the stream after its final physical box.
    /// </summary>
    /// <param name="file">The stored container input.</param>
    [Theory]
    [InlineData(TestImages.Jxl.SquareExtendedSizeContainer)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1)]
    public void CompleteAdvancesPastUnreadCodestream(string file)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader reader = new(buffered);
        InlineArray4<byte> storage = default;
        Span<byte> prefix = storage;

        Assert.InRange(reader.Read(prefix), 1, prefix.Length);

        reader.Complete();

        Assert.Equal(buffered.Length, buffered.Position);
    }

    /// <summary>
    /// Verifies that container framing preserves every original codestream byte.
    /// </summary>
    /// <param name="file">The raw or boxed input file.</param>
    /// <param name="rawFile">The independently stored original codestream.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Splines, TestImages.Jxl.Splines)]
    [InlineData(TestImages.Jxl.SquareExtendedSizeContainer, TestImages.Jxl.Square)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV0, TestImages.Jxl.Splines)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, TestImages.Jxl.Splines)]
    [InlineData(TestImages.Jxl.SplinesSizeZero, TestImages.Jxl.Splines)]
    public void ReadPreservesCodestreamAcrossContainerBoundaries(string file, string rawFile)
    {
        byte[] expected = TestFile.Create(rawFile).Bytes;
        byte[] actual = new byte[expected.Length];
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader reader = new(buffered);
        int offset = 0;

        // Small reads cross the signature, fragment and reservoir boundaries. The expected bytes come from the
        // original stored codestream; the test does not parse a container to manufacture its expected result.
        while (offset < actual.Length)
        {
            int count = Math.Min(7, actual.Length - offset);
            int read = reader.Read(actual.AsSpan(offset, count));
            Assert.InRange(read, 1, count);
            offset += read;
        }

        Assert.Equal(expected, actual);
        Assert.Equal(expected.Length, reader.Position);
        Assert.Equal(0, reader.Read(actual.AsSpan(0, 1)));

        reader.Complete();

        // The fragmented fixtures include a padding box. Unknown boxes must not create retained metadata entries.
        Assert.Empty(reader.Boxes);
    }

    /// <summary>
    /// Verifies fragment numbering, termination and physical file boundaries from stored malformed inputs.
    /// </summary>
    /// <param name="file">The malformed container file.</param>
    [Theory]
    [InlineData(TestImages.Jxl.SplinesDuplicateFragment)]
    [InlineData(TestImages.Jxl.SplinesMissingFinalFlag)]
    [InlineData(TestImages.Jxl.SplinesOversizedBox)]
    public void ReadRejectsInvalidContainerBoundaries(string file)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);

        Assert.Throws<InvalidImageContentException>(() =>
        {
            JxlCodestreamReader reader = new(buffered);
            InlineArray16<byte> storage = default;
            Span<byte> buffer = storage;

            // Consume the real reader to its logical end. Each fixture has exactly one container defect.
            while (reader.Read(buffer) != 0)
            {
            }

            reader.Complete();
        });
    }

    /// <summary>
    /// Verifies metadata ranges before and after the codestream, including completion without decoding unused bytes.
    /// </summary>
    /// <param name="file">The container file.</param>
    /// <param name="exifOffset">The expected EXIF payload offset.</param>
    /// <param name="xmpOffset">The expected XMP payload offset.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifXmp, 40, 312)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, 188, 460)]
    public void CompleteRetainsMetadataWithoutReadingRemainingCodestream(string file, long exifOffset, long xmpOffset)
    {
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlCodestreamReader reader = new(buffered);
        InlineArray16<byte> storage = default;
        Span<byte> prefix = storage;

        Assert.Equal(16, reader.Read(prefix));
        Assert.Equal((byte)0xFF, prefix[0]);
        Assert.Equal((byte)0x0A, prefix[1]);

        // A backward section read must not cause metadata boxes to be discovered twice during completion.
        Assert.True(reader.TrySeek(80));
        Assert.InRange(reader.Read(prefix), 1, prefix.Length);
        Assert.True(reader.TrySeek(0));
        Assert.Equal(16, reader.Read(prefix));

        reader.Complete();

        Assert.Equal(140, reader.Position);
        Assert.Equal(3, reader.Boxes.Count);
        Assert.Contains(reader.Boxes, box => box.Type == JxlConstants.ExifBox && box.Offset == exifOffset && box.Length == 264);
        Assert.Contains(reader.Boxes, box => box.Type == JxlConstants.XmlBox && box.Offset == xmpOffset && box.Length == 3447);
        Assert.Contains(reader.Boxes, box => box.Type == JxlConstants.JpegReconstructionBox && box.Length == 189);
        Assert.Equal(buffered.Length, buffered.Position);
    }
}

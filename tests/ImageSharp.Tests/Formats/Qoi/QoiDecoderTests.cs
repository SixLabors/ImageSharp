// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Qoi;

[Trait("Format", "Qoi")]
[ValidateDisposedMemoryAllocations]
public class QoiDecoderTests
{
    [Theory]
    [InlineData(TestImages.Qoi.Dice, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.EdgeCase, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.Kodim10, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.Kodim23, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.QoiLogo, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.TestCard, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.TestCardRGBA, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [InlineData(TestImages.Qoi.Wikipedia008, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    public void Identify(string imagePath, QoiChannels channels, QoiColorSpace colorSpace)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);
        QoiMetadata qoiMetadata = imageInfo.Metadata.GetQoiMetadata();

        Assert.NotNull(imageInfo);
        Assert.Equal(imageInfo.Metadata.DecodedImageFormat, QoiFormat.Instance);
        Assert.Equal(qoiMetadata.Channels, channels);
        Assert.Equal(qoiMetadata.ColorSpace, colorSpace);
    }

    [Theory]
    [WithFile(TestImages.Qoi.Dice, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.EdgeCase, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Kodim10, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Kodim23, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.QoiLogo, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.TestCard, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.TestCardRGBA, PixelTypes.Rgba32, QoiChannels.Rgba, QoiColorSpace.SrgbWithLinearAlpha)]
    [WithFile(TestImages.Qoi.Wikipedia008, PixelTypes.Rgba32, QoiChannels.Rgb, QoiColorSpace.SrgbWithLinearAlpha)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider, QoiChannels channels, QoiColorSpace colorSpace)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        QoiMetadata qoiMetadata = image.Metadata.GetQoiMetadata();
        image.DebugSave(provider);

        image.CompareToReferenceOutput(provider);
        Assert.Equal(qoiMetadata.Channels, channels);
        Assert.Equal(qoiMetadata.ColorSpace, colorSpace);
    }

    /// <summary>
    /// Verifies that a file that ends in the middle of the chunk data decodes with the default options. The complete
    /// rows match the decode of the full file, and the rows that are not decoded keep the fill value. In strict mode
    /// the decoder throws.
    /// </summary>
    /// <param name="imagePath">The test image to truncate.</param>
    [Theory]
    [InlineData(TestImages.Qoi.Kodim10)]
    [InlineData(TestImages.Qoi.Wikipedia008)]
    public void Decode_TruncatedChunkData_KeepsDecodedRows(string imagePath)
    {
        byte[] full = TestFile.Create(imagePath).Bytes;
        byte[] truncated = full[..(full.Length / 2)];

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, truncated));

        // The decoder writes the pixels of each complete chunk in row order. A partial chunk and the pixels after it
        // keep the cleared value.
        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        int decodedPixels = CountCompleteChunkPixels(truncated, expected.Width * expected.Height);
        for (int i = decodedPixels; i < expected.Width * expected.Height; i++)
        {
            expected[i % expected.Width, i / expected.Width] = default;
        }

        using Image<Rgba32> actual = Image.Load<Rgba32>(truncated);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Counts the pixels of the complete chunks in QOI data. The tag of a chunk gives its fixed length. QOI_OP_RGB is
    /// 4 bytes, QOI_OP_RGBA is 5 bytes and QOI_OP_LUMA is 2 bytes. QOI_OP_INDEX, QOI_OP_DIFF and QOI_OP_RUN are
    /// 1 byte. Only QOI_OP_RUN writes more than one pixel, with the run length in its low 6 bits, minus 1.
    /// See the QOI specification, version 1.0, chunk descriptions, https://qoiformat.org/qoi-specification.pdf.
    /// </summary>
    /// <param name="data">The QOI file. It can end inside a chunk.</param>
    /// <param name="pixelCount">The number of pixels in the image.</param>
    /// <returns>The number of pixels of the complete chunks, at most <paramref name="pixelCount"/>.</returns>
    private static int CountCompleteChunkPixels(byte[] data, int pixelCount)
    {
        // The chunks follow the 14-byte header.
        int offset = 14;
        int pixels = 0;
        while (pixels < pixelCount && offset < data.Length)
        {
            byte tag = data[offset];
            int length = tag switch
            {
                0xFE => 4,
                0xFF => 5,
                _ => (tag >> 6) == 0b10 ? 2 : 1
            };

            if (offset + length > data.Length)
            {
                break;
            }

            pixels += tag < 0xFE && (tag >> 6) == 0b11 ? (tag & 0b00111111) + 1 : 1;
            offset += length;
        }

        return Math.Min(pixels, pixelCount);
    }

    /// <summary>
    /// Verifies that a complete image with a missing or short end marker is damaged image data. A QOI file is a 14-byte
    /// header, the data chunks and an 8-byte end marker. See the QOI specification, version 1.0. In strict mode the
    /// decoder throws. With the default options it keeps the complete image.
    /// </summary>
    /// <param name="markerLength">The number of end marker bytes that the file keeps.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Decode_MissingEndMarker_FollowsIntegrityHandling(int markerLength)
    {
        byte[] full = TestFile.Create(TestImages.Qoi.Kodim10).Bytes;

        // The end marker is the last 8 bytes of the file.
        byte[] data = full[..^(8 - markerLength)];
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, data));

        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        using Image<Rgba32> actual = Image.Load<Rgba32>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Verifies that end marker bytes that are present but not valid are damaged image data. In strict mode the decoder
    /// throws. With the default options it keeps the complete image.
    /// </summary>
    [Fact]
    public void Decode_InvalidEndMarker_FollowsIntegrityHandling()
    {
        byte[] full = TestFile.Create(TestImages.Qoi.Kodim10).Bytes;
        byte[] data = [.. full];

        // The valid end marker ends with the byte 0x01.
        data[^1] = 2;
        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(strict, data));

        using Image<Rgba32> expected = Image.Load<Rgba32>(full);
        using Image<Rgba32> actual = Image.Load<Rgba32>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }
}

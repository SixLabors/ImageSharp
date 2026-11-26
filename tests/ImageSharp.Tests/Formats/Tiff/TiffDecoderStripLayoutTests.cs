// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

/// <summary>
/// Tests strip allocation and directory traversal with stored TIFF inputs.
/// </summary>
[Trait("Format", "Tiff")]
[ValidateDisposedMemoryAllocations]
public class TiffDecoderStripLayoutTests
{
    /// <summary>
    /// The ImageWidth tag.
    /// </summary>
    private const ushort TiffImageWidthTag = 256;

    /// <summary>
    /// The ImageLength tag.
    /// </summary>
    private const ushort TiffImageLengthTag = 257;

    /// <summary>
    /// The Compression tag.
    /// </summary>
    private const ushort TiffCompressionTag = 259;

    /// <summary>
    /// The RowsPerStrip tag.
    /// </summary>
    private const ushort TiffRowsPerStripTag = 278;

    /// <summary>
    /// The StripByteCounts tag.
    /// </summary>
    private const ushort TiffStripByteCountsTag = 279;

    /// <summary>
    /// The PlanarConfiguration tag.
    /// </summary>
    private const ushort TiffPlanarConfigurationTag = 284;

    /// <summary>
    /// The TileWidth tag.
    /// </summary>
    private const ushort TiffTileWidthTag = 322;

    /// <summary>
    /// The TileLength tag.
    /// </summary>
    private const ushort TiffTileLengthTag = 323;

    /// <summary>
    /// The YCbCrSubSampling tag.
    /// </summary>
    private const ushort TiffYCbCrSubSamplingTag = 530;

    /// <summary>
    /// A missing RowsPerStrip or a value greater than the image height describes one strip per plane.
    /// </summary>
    /// <param name="path">The stored TIFF input.</param>
    [Theory]
    [InlineData(ChunkyRowsPerStripMissing)]
    [InlineData(ChunkyRowsPerStripLarge)]
    [InlineData(ChunkyRowsPerStripMax)]
    [InlineData(PlanarRowsPerStripMissing)]
    [InlineData(PlanarRowsPerStripLarge)]
    [InlineData(PlanarRowsPerStripMax)]
    public void Decode_RowsPerStripMissingOrAboveHeight_DecodesAllRows(string path)
    {
        using Image<Rgb24> expected = Image.Load<Rgb24>(TestFile.Create(StripLayoutRgbReference).Bytes);
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            using Image<Rgb24> actual = Image.Load<Rgb24>(options, data);

            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }
    }

    /// <summary>
    /// Reading an uncompressed strip in rows preserves every 16-bit component.
    /// </summary>
    /// <param name="path">The stored single-strip or multiple-strip input.</param>
    [Theory]
    [InlineData(ChunkyRgb16SingleStrip)]
    [InlineData(ChunkyRgb16MultipleStrips)]
    [InlineData(PlanarRgb16SingleStrip)]
    [InlineData(PlanarRgb16MultipleStrips)]
    public void Decode_UncompressedStripAboveBufferLimit_DecodesAllRows(string path)
    {
        byte[] data = TestFile.Create(path).Bytes;
        using Image<Rgb48> expected = Image.Load<Rgb48>(data);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions { SingleBufferAllocationLimitMegabytes = 1 });

        // Each full strip exceeds 1 MiB, including each separate plane. A row fits within the limit.
        // The final strip in the multiple-strip inputs contains only the remaining 100 rows.
        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(configuration) { SegmentIntegrityHandling = integrity };
            using Image<Rgb48> actual = Image.Load<Rgb48>(options, data);

            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }
    }

    /// <summary>
    /// Planar offsets are grouped by plane, with StripsPerImage entries for each plane. See TIFF 6.0 specification,
    /// section 8, StripOffsets, page 40: https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf
    /// With one byte count missing, the last plane has no second strip. The missing strip throws in the Strict mode. In
    /// the default IgnoreImageData mode, the decoder keeps the plane stride from the image height. It decodes the first
    /// strip of each plane and leaves the rows of the second strip cleared.
    /// </summary>
    [Fact]
    public void Decode_PlanarStripByteCountMissing_DecodesStripsOfAllPlanes()
    {
        // The file is 1024 x 700 with 600 rows per strip: 2 strips for each of 3 planes. It is little-endian. The IFD
        // offset is at byte 4, and each 12-byte entry has its tag at byte 0 and its count at byte 4. StripByteCounts is
        // tag 279. The test changes the bytes, so it uses a copy of the cached file bytes.
        byte[] data = [.. TestFile.Create(PlanarRgb16MultipleStrips).Bytes];
        using Image<Rgb48> expected = Image.Load<Rgb48>(data);

        int ifd = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(ifd));
        int entry = Enumerable.Range(0, count).Select(i => ifd + 2 + (i * 12)).Single(e => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(e)) == 279);
        Assert.Equal(6, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(entry + 4)));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(entry + 4), 5);

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgb48> image = Image.Load<Rgb48>(strict, data);
        });

        using Image<Rgb48> actual = Image.Load<Rgb48>(data);

        // The first strip holds rows 0 to 599. The rows of the second strip keep the cleared image buffer.
        using Image<Rgb48> firstStrip = expected.Clone(x => x.Crop(new Rectangle(0, 0, expected.Width, 600)));
        using Image<Rgb48> partial = new(expected.Width, expected.Height);
        partial.Mutate(x => x.DrawImage(firstStrip, Point.Empty, PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.Src, 1F));
        ImageComparer.Exact.VerifySimilarity(partial, actual);
    }

    /// <summary>
    /// Subsampled chroma planes require fewer stored samples than their expanded destination buffers.
    /// </summary>
    [Fact]
    public void Decode_SubsampledPlanarYCbCr_DoesNotRequireExpandedChromaInFile()
    {
        byte[] data = TestFile.Create(PlanarYCbCrSubsampled).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            using Image<Rgb24> image = Image.Load<Rgb24>(options, data);

            // Y = 64 with neutral Cb and Cr = 128 represents gray 64 in every pixel.
            Assert.Equal(new Size(4, 2), image.Size);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Assert.Equal(new Rgb24(64, 64, 64), image[x, y]);
                }
            }
        }
    }

    /// <summary>
    /// A zero strip height is invalid in every integrity mode.
    /// </summary>
    /// <param name="path">The stored TIFF with RowsPerStrip set to zero.</param>
    [Theory]
    [InlineData(ChunkyRowsPerStripZero)]
    [InlineData(PlanarRowsPerStripZero)]
    public void Decode_ZeroRowsPerStrip_ThrowsInvalidImageContentException(string path)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws<InvalidImageContentException>(() =>
            {
                using Image<Rgb24> image = Image.Load<Rgb24>(options, data);
            });
        }
    }

    /// <summary>
    /// Short image data follows the integrity policy without reading past the declared strip or reusing old samples.
    /// </summary>
    /// <param name="path">The stored TIFF whose strip ends halfway through its second row.</param>
    /// <param name="limitBuffer">Whether to force row-by-row reads.</param>
    [Theory]
    [InlineData(ChunkyRgb16ShortStrip, false)]
    [InlineData(ChunkyRgb16ShortStrip, true)]
    [InlineData(PlanarRgb16ShortStrip, false)]
    [InlineData(PlanarRgb16ShortStrip, true)]
    public void Decode_ShortUncompressedStrip_UsesImageDataIntegrityPolicy(string path, bool limitBuffer)
    {
        byte[] data = TestFile.Create(path).Bytes;
        Configuration configuration = Configuration.Default.Clone();
        if (limitBuffer)
        {
            configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions { SingleBufferAllocationLimitMegabytes = 1 });
        }

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(configuration) { SegmentIntegrityHandling = integrity };
            if (integrity != SegmentIntegrityHandling.IgnoreImageData)
            {
                Assert.Throws<InvalidImageContentException>(() =>
                {
                    using Image<Rgb48> image = Image.Load<Rgb48>(options, data);
                });
            }
            else
            {
                using Image<Rgb48> image = Image.Load<Rgb48>(options, data);

                // The fixtures contain more bytes after StripByteCounts. Those bytes must not become image samples.
                // Samples before the declared end survive. Missing samples are zero in both read paths.
                Assert.Equal(new Rgb48(0, 13621, 27242), image[0, 0]);
                Assert.Equal(new Rgb48(25957, 39578, 53199), image[0, 1]);
                Assert.Equal(default, image[1023, 1]);
                Assert.Equal(default, image[0, 2]);
                Assert.Equal(default, image[1023, 699]);
            }
        }
    }

    /// <summary>
    /// A failed image allocation reports the declared dimensions.
    /// </summary>
    [Fact]
    public void Decode_FrameAboveAllocationLimit_ThrowsWithRealDimensions()
    {
        byte[] data = TestFile.Create(FrameAboveAllocationLimit).Bytes;
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions { AllocationLimitMegabytes = 100 });
        DecoderOptions options = new(configuration);

        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<L8> image = Image.Load<L8>(options, data);
        });

        Assert.Contains("20000x15000", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Directory links determine frame order even when their file offsets decrease.
    /// </summary>
    /// <param name="path">The stored TIFF or BigTIFF with reversed directory offsets.</param>
    [Theory]
    [InlineData(ReverseDirectories)]
    [InlineData(BigTiffReverseDirectories)]
    public void Decode_DirectoriesStoredInReverseFileOrder_DecodesAllFrames(string path)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            using Image<L8> image = Image.Load<L8>(options, data);
            ImageInfo info = Image.Identify(options, data);

            Assert.Equal(2, image.Frames.Count);
            Assert.Equal(2, info.FrameMetadataCollection.Count);
            Assert.Equal(new Size(2, 2), image.Size);
            Assert.Equal(new L8(10), image.Frames[0][0, 0]);
            Assert.Equal(new L8(11), image.Frames[0][1, 0]);
            Assert.Equal(new L8(12), image.Frames[0][0, 1]);
            Assert.Equal(new L8(13), image.Frames[0][1, 1]);
            Assert.Equal(new L8(20), image.Frames[1][0, 0]);
            Assert.Equal(new L8(21), image.Frames[1][1, 0]);
            Assert.Equal(new L8(22), image.Frames[1][0, 1]);
            Assert.Equal(new L8(23), image.Frames[1][1, 1]);
        }
    }

    /// <summary>
    /// A repeated directory offset is a structural error in every integrity mode.
    /// </summary>
    /// <param name="path">The stored TIFF or BigTIFF with a circular directory chain.</param>
    [Theory]
    [InlineData(DirectoryLoopOne)]
    [InlineData(DirectoryLoopTwo)]
    [InlineData(DirectoryLoopTwoReverse)]
    [InlineData(DirectoryLoopThreeReverse)]
    [InlineData(BigTiffDirectoryLoopOne)]
    [InlineData(BigTiffDirectoryLoopTwo)]
    [InlineData(BigTiffDirectoryLoopTwoReverse)]
    [InlineData(BigTiffDirectoryLoopThreeReverse)]
    public void Decode_DirectoryChainRevisitsDirectory_ThrowsImageFormatException(string path)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            ImageFormatException identifyError = Assert.Throws<ImageFormatException>(() => Image.Identify(options, data));
            ImageFormatException decodeError = Assert.Throws<ImageFormatException>(() =>
            {
                using Image<L8> image = Image.Load<L8>(options, data);
            });

            Assert.Contains("circular", identifyError.Message, StringComparison.Ordinal);
            Assert.Contains("circular", decodeError.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The second IFD has a strip layout that is not valid or that the decoder does not support. The Strict and IgnoreAncillary modes throw. The
    /// IgnoreImageData mode keeps the frame of the first IFD.
    /// </summary>
    /// <param name="tag">The tag of the second IFD to change.</param>
    /// <param name="value">The new value of the tag.</param>
    /// <param name="expectedException">The exception type in the Strict and IgnoreAncillary modes.</param>
    [Theory]
    [InlineData(TiffRowsPerStripTag, 0u, typeof(InvalidImageContentException))]
    [InlineData(TiffCompressionTag, 0xFFFFu, typeof(NotSupportedException))]
    [InlineData(TiffImageWidthTag, 32u, typeof(NotSupportedException))]
    public void Decode_DamagedLaterDirectory_KeepsDecodedFrames(ushort tag, uint value, Type expectedException)
    {
        using Image<Rgb24> source = CreateNoiseImage(16, 16, 1);
        using (Image<Rgb24> second = CreateNoiseImage(16, 16, 2))
        {
            source.Frames.AddFrame(second.Frames.RootFrame);
        }

        using MemoryStream stream = new();
        source.SaveAsTiff(stream);
        byte[] original = stream.ToArray();

        byte[] data = [.. original];
        SetDirectoryTagValue(data, 1, tag, value);

        foreach (SegmentIntegrityHandling integrity in new[] { SegmentIntegrityHandling.Strict, SegmentIntegrityHandling.IgnoreAncillary })
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            Assert.Throws(expectedException, () =>
            {
                using Image<Rgb24> image = Image.Load<Rgb24>(options, data);
            });
        }

        using Image<Rgb24> expected = Image.Load<Rgb24>(original);
        expected.Frames.RemoveFrame(1);

        DecoderOptions ignore = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<Rgb24> actual = Image.Load<Rgb24>(ignore, data);
        Assert.Equal(1, actual.Frames.Count);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// A zero strip height in the first IFD is a structural error. The decoder reports it before it allocates the 4096x4096 frame. This frame needs more than
    /// the 1 MB allocation limit.
    /// </summary>
    [Fact]
    public void Decode_ZeroRowsPerStrip_ThrowsBeforeFrameAllocation()
    {
        using Image<Rgb24> source = CreateNoiseImage(16, 16, 3);
        using MemoryStream stream = new();
        source.SaveAsTiff(stream);
        byte[] data = stream.ToArray();
        SetDirectoryTagValue(data, 0, TiffImageWidthTag, 4096);
        SetDirectoryTagValue(data, 0, TiffImageLengthTag, 4096);
        SetDirectoryTagValue(data, 0, TiffRowsPerStripTag, 0);

        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions { AllocationLimitMegabytes = 1 });
        DecoderOptions options = new(configuration);

        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgb24> image = Image.Load<Rgb24>(options, data);
        });
    }

    /// <summary>
    /// Verifies that the strip buffer size does not wrap around for a strip of more than ulong.MaxValue bytes. The decoder reports the strip buffer limit
    /// before it allocates the frame.
    /// </summary>
    [Fact]
    public void Identify_StripBufferAboveUInt64Range_ThrowsNotSupportedException()
    {
        using Image<RgbaVector> source = new(16, 16);
        using MemoryStream stream = new();
        TiffEncoder encoder = new() { BitsPerPixel = TiffBitsPerPixel.Bit128, SampleFormat = TiffSampleFormat.Float, Compression = TiffCompression.None };
        source.SaveAsTiff(stream, encoder);
        byte[] data = stream.ToArray();

        // A row of 2^30 pixels with 128 bits for each pixel has 2^34 bytes. A strip of 2^30 rows has 2^64 bytes. In 64 bits, that product wraps to 0.
        SetDirectoryTagValue(data, 0, TiffImageWidthTag, 1u << 30);
        SetDirectoryTagValue(data, 0, TiffImageLengthTag, 1u << 30);
        SetDirectoryTagValue(data, 0, TiffRowsPerStripTag, 1u << 30);

        const string message = "The TIFF strip buffer exceeds Int32.MaxValue bytes.";
        NotSupportedException identifyException = Assert.Throws<NotSupportedException>(() => Image.Identify(data));
        Assert.Equal(message, identifyException.Message);

        NotSupportedException decodeException = Assert.Throws<NotSupportedException>(() =>
        {
            using Image<RgbaVector> image = Image.Load<RgbaVector>(data);
        });

        Assert.Equal(message, decodeException.Message);
    }

    /// <summary>
    /// A chunky tiled IFD can store its tile layout in the strip tags. Without StripByteCounts, the tiles have no byte counts. The parser rejects the IFD
    /// before the frame exists.
    /// </summary>
    [Fact]
    public void Decode_TiledStripOffsetsWithoutByteCounts_ThrowsInvalidImageContentException()
    {
        using Image<Rgb24> source = CreateNoiseImage(16, 16, 4);
        using MemoryStream stream = new();
        source.SaveAsTiff(stream);
        byte[] data = stream.ToArray();

        // The tile tags take the places of RowsPerStrip and StripByteCounts. Thus the IFD has StripOffsets, TileWidth and TileLength, and no byte counts.
        SetDirectoryTag(data, 0, TiffRowsPerStripTag, TiffTileWidthTag);
        SetDirectoryTag(data, 0, TiffStripByteCountsTag, TiffTileLengthTag);
        SetDirectoryTagValue(data, 0, TiffTileWidthTag, 16);
        SetDirectoryTagValue(data, 0, TiffTileLengthTag, 16);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgb24> image = Image.Load<Rgb24>(data);
        });

        Assert.Equal("StripByteCounts are missing and are required for decoding the TIFF image!", exception.Message);
    }

    /// <summary>
    /// A file can hold more strips than the image height needs. The last strip of the image then has fewer rows than RowsPerStrip. The decoder reads those
    /// rows, and no extra strip, as libtiff does.
    /// </summary>
    [Fact]
    public void Decode_ChunkyStripsAfterLastRow_DecodesShortLastStrip()
    {
        // A row of 1000 Rgb24 pixels is 3000 bytes. Thus the encoder writes 2 rows in each 8 KB strip: 3 strips for 5 rows.
        using Image<Rgb24> source = CreateNoiseImage(1000, 5, 5);
        using MemoryStream stream = new();
        source.SaveAsTiff(stream, new TiffEncoder { Compression = TiffCompression.None });
        byte[] data = stream.ToArray();

        // With 3 rows, the image needs 2 strips. The second strip has 1 row. The third strip is extra.
        SetDirectoryTagValue(data, 0, TiffImageLengthTag, 3);

        using Image<Rgb24> expected = source.Clone(x => x.Crop(new Rectangle(0, 0, 1000, 3)));
        using Image<Rgb24> actual = Image.Load<Rgb24>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// "If SamplesPerPixel is 1, PlanarConfiguration is irrelevant". See TIFF 6.0 specification, section 8, page 38. A one-sample image with
    /// PlanarConfiguration 2 decodes as the same chunky image.
    /// </summary>
    [Fact]
    public void Decode_OneSamplePlanar_DecodesAsChunky()
    {
        using Image<Rgb24> source = CreateNoiseImage(16, 16, 6);
        using MemoryStream stream = new();
        TiffEncoder encoder = new() { BitsPerPixel = TiffBitsPerPixel.Bit8, PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero };
        source.SaveAsTiff(stream, encoder);
        byte[] original = stream.ToArray();

        byte[] data = [.. original];
        SetDirectoryTagValue(data, 0, TiffPlanarConfigurationTag, 2);

        using Image<Rgb24> expected = Image.Load<Rgb24>(original);
        using Image<Rgb24> actual = Image.Load<Rgb24>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// The nested JPEG decoder does not read the TIFF YCbCrSubSampling values. Thus a zero factor does not stop the decode of JPEG data. Only the TIFF YCbCr
    /// readers divide by the factors.
    /// </summary>
    [Fact]
    public void Decode_JpegWithZeroYCbCrSubSampling_DecodesImage()
    {
        byte[] original = TestFile.Create(YCbCrJpegCompressed).Bytes;
        byte[] data = [.. original];

        // The entry holds the two SHORT factors in its 4-byte value field.
        GetDirectoryEntry(data, 0, TiffYCbCrSubSamplingTag)[8..].Clear();

        using Image<Rgba32> expected = Image.Load<Rgba32>(original);
        using Image<Rgba32> actual = Image.Load<Rgba32>(data);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Creates an image of fixed pseudo-random noise.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="seed">The seed of the noise.</param>
    /// <returns>The image.</returns>
    private static Image<Rgb24> CreateNoiseImage(int width, int height, int seed)
    {
        Random random = new(seed);
        Image<Rgb24> image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            }
        }

        return image;
    }

    /// <summary>
    /// Sets the value of a SHORT or LONG tag in an IFD of a little-endian TIFF file.
    /// </summary>
    /// <remarks>
    /// The 8-byte header holds the offset of the first IFD at byte 4. An IFD holds a 2-byte entry count, the 12-byte entries, and the 4-byte offset of the next
    /// IFD. An entry holds the tag, the type, the count, and a value that fits in 4 bytes. See the TIFF 6.0 specification, section 2, "Image File Directory",
    /// pages 13-16 (https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf).
    /// </remarks>
    /// <param name="data">The TIFF file.</param>
    /// <param name="directory">The index of the IFD in the directory chain.</param>
    /// <param name="tag">The tag to change.</param>
    /// <param name="value">The new value.</param>
    private static void SetDirectoryTagValue(byte[] data, int directory, ushort tag, uint value)
    {
        Span<byte> entry = GetDirectoryEntry(data, directory, tag);

        // Type 3 is SHORT and type 4 is LONG.
        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]));
        if (type == 3)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(entry[8..], (ushort)value);
        }
        else
        {
            Assert.Equal(4, type);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], value);
        }
    }

    /// <summary>
    /// Changes the tag of an entry in an IFD of a little-endian TIFF file. The entry keeps its type, count and value.
    /// </summary>
    /// <remarks>See <see cref="SetDirectoryTagValue"/> for the IFD layout.</remarks>
    /// <param name="data">The TIFF file.</param>
    /// <param name="directory">The index of the IFD in the directory chain.</param>
    /// <param name="tag">The tag to change.</param>
    /// <param name="newTag">The new tag.</param>
    private static void SetDirectoryTag(byte[] data, int directory, ushort tag, ushort newTag)
        => BinaryPrimitives.WriteUInt16LittleEndian(GetDirectoryEntry(data, directory, tag), newTag);

    /// <summary>
    /// Gets the 12-byte entry of a tag in an IFD of a little-endian TIFF file.
    /// </summary>
    /// <remarks>See <see cref="SetDirectoryTagValue"/> for the IFD layout.</remarks>
    /// <param name="data">The TIFF file.</param>
    /// <param name="directory">The index of the IFD in the directory chain.</param>
    /// <param name="tag">The tag of the entry.</param>
    /// <returns>The entry.</returns>
    private static Span<byte> GetDirectoryEntry(byte[] data, int directory, ushort tag)
    {
        Assert.Equal((byte)'I', data[0]);
        int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
        for (int i = 0; i < directory; i++)
        {
            int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
            offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 2 + (count * 12)));
        }

        int entryCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
        for (int i = 0; i < entryCount; i++)
        {
            Span<byte> entry = data.AsSpan(offset + 2 + (i * 12), 12);
            if (BinaryPrimitives.ReadUInt16LittleEndian(entry) == tag)
            {
                return entry;
            }
        }

        Assert.Fail($"The IFD does not have the tag {tag}.");
        return default;
    }
}

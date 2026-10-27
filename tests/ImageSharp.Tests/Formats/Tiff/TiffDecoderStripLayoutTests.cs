// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
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
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
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
            DecoderOptions options = new() { Configuration = configuration, SegmentIntegrityHandling = integrity };
            using Image<Rgb48> actual = Image.Load<Rgb48>(options, data);

            ImageComparer.Exact.VerifySimilarity(expected, actual);
        }
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
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
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
    public void Decode_ZeroRowsPerStrip_ThrowsImageFormatException(string path)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            Assert.Throws<ImageFormatException>(() =>
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
            DecoderOptions options = new() { Configuration = configuration, SegmentIntegrityHandling = integrity };
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
        DecoderOptions options = new() { Configuration = configuration };

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
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
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
            DecoderOptions options = new() { SegmentIntegrityHandling = integrity };
            ImageFormatException identifyError = Assert.Throws<ImageFormatException>(() => Image.Identify(options, data));
            ImageFormatException decodeError = Assert.Throws<ImageFormatException>(() =>
            {
                using Image<L8> image = Image.Load<L8>(options, data);
            });

            Assert.Contains("circular", identifyError.Message, StringComparison.Ordinal);
            Assert.Contains("circular", decodeError.Message, StringComparison.Ordinal);
        }
    }
}

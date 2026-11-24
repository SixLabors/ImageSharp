// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrDecoderTests
{
    private static MagickReferenceDecoder ReferenceDecoder => MagickReferenceDecoder.Exr;

    [Theory]
    [InlineData(TestImages.Exr.UncompressedFloatRgb, typeof(Image<RgbaVector>))]
    [InlineData(TestImages.Exr.FloatRgbaZipFallback, typeof(Image<RgbaVectorP>))]
    [InlineData(TestImages.Exr.UncompressedRgba, typeof(Image<RgbaHalfP>))]
    [InlineData(TestImages.Exr.Rgb, typeof(Image<RgbaHalf>))]
    public void DefaultLoad_UsesFloatingStorageForExrSamples(string imagePath, Type expectedImageType)
    {
        TestFile file = TestFile.Create(imagePath);
        using MemoryStream stream = new(file.Bytes, false);
        using Image image = Image.Load(stream);

        Assert.Equal(expectedImageType, image.GetType());
    }

    [Fact]
    public void ExrDecoder_PreservesHdrSamplesFromOpenExrFile()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertHdrSamplesFromOpenExrFile,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks decoded samples from the OpenEXR fixture at positions inside the vector body and scalar tail.
    /// </summary>
    private static void AssertHdrSamplesFromOpenExrFile()
    {
        TestFile file = TestFile.Create(TestImages.Exr.OpenExrHdrHalf);
        using MemoryStream stream = new(file.Bytes, false);
        using Image image = Image.Load(stream);
        Image<RgbaHalfP> pixels = Assert.IsType<Image<RgbaHalfP>>(image);

        Assert.Equal(587, pixels.Width);
        Assert.Equal(675, pixels.Height);

        // The literals are independently read linear half samples from OpenEXR's comp_none.exr.
        // Decoding applies the public sRGB transfer curve, then rounds to the selected pixel storage.
        // Keep these checks exact: they verify the codec's integration with those two shared owners.
        ReadOnlySpan<(int X, int Y, Vector4 Linear)> samples =
        [
            (272, 180, new(319F, 423F, 501F, 1F)),
            (73, 639, new(0.001132965087890625F, -0.002262115478515625F, -0.0014476776123046875F, 1F)),
            (59, 49, new(0.91748046875F, 1.01953125F, 1.18359375F, 1F)),
            (586, 180, new(0.037841796875F, 0.021148681640625F, 0.0201263427734375F, 1F))
        ];

        foreach ((int x, int y, Vector4 linear) in samples)
        {
            RgbaHalfP expected = RgbaHalfP.FromAssociatedScaledVector4(SRgbCompanding.Compress(linear));
            Assert.Equal(expected.ToVector4(), pixels[x, y].ToVector4());
        }
    }

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgb_ExrPixelType_Half<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
        Assert.Equal(ExrPixelType.Half, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.UncompressedFloatRgb, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgb_ExrPixelType_Float<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);

        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
        Assert.Equal(ExrPixelType.Float, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.UncompressedUintRgb, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgb_ExrPixelType_Uint<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);

        // OpenEXR 3.5.3 supplies native UINT channels. Normalize them by uint.MaxValue,
        // apply the analytic IEC sRGB curve, then pack the final Rgba32 destination once.
        image.CompareToReferenceOutput(provider);
        Assert.Equal(ExrPixelType.UnsignedInt, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.UintRgba, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Uncompressed_Rgba_ExrPixelType_Uint<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        ExrMetadata exrMetaData = image.Metadata.GetExrMetadata();
        image.DebugSave(provider);

        // The OpenEXR 3.5.3 reference normalizes UINT color and alpha, restores straight RGB,
        // and applies the analytic IEC sRGB curve before final Rgba32 packing. Zero alpha retains the supplied RGB.
        // Fast Pow can change that final byte rounding by one. The measured whole-image error is
        // 0.0013%; retain the native reference and bound this image's error at 0.0014%.
        image.CompareToReferenceOutput(ImageComparer.TolerantPercentage(.0014F), provider);
        Assert.Equal(ExrPixelType.UnsignedInt, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.Rgb, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Rgb<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Gray, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Gray<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        // Fast Pow can put a component on the other side of an eight-bit rounding midpoint.
        // The measured native comparison error is 0.0032%; bound this image's error at 0.0033%.
        image.CompareToOriginal(provider, ImageComparer.TolerantPercentage(.0033F), ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zip, PixelTypes.Rgba32)]
    [WithFile(TestImages.Exr.ZipUncompressedBlock, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_ZipCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Zips, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_ZipsCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Rle, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_RunLengthCompressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Pxr24Half, PixelTypes.Rgba32)]
    [WithFile(TestImages.Exr.Pxr24Float, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Pxr24Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact, ReferenceDecoder);
    }

    [Theory]
    [WithFile(TestImages.Exr.Pxr24Uint, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_Pxr24Compressed_ExrPixelType_Uint<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);

        // OpenEXR 3.5.3 decodes PXR24 UINT independently. Normalize native channels,
        // apply the analytic IEC sRGB curve, then pack the final Rgba32 destination once.
        image.CompareToReferenceOutput(provider);
    }

    /// <summary>
    /// Reads a FLOAT scanline stored raw in a B44 image.
    /// </summary>
    [Fact]
    public void Decode_B44RawFloatBlock_PreservesPixels()
    {
        using Image<Rgba32> source = new(2, 1);
        source[0, 0] = new Rgba32(255, 0, 0);
        source[1, 0] = new Rgba32(0, 255, 0);

        using MemoryStream stream = new();
        source.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = ExrCompression.None });
        byte[] data = stream.ToArray();

        // A one-row B44 image has one scanline block. A raw block stores the original sample bytes.
        // Change only the compression attribute. Keep the encoder's raw block and offset.
        ReadOnlySpan<byte> attribute = "compression\0compression\0"u8;
        int offset = data.AsSpan().IndexOf(attribute);
        Assert.True(offset >= 0);
        data[offset + attribute.Length + sizeof(int)] = (byte)ExrCompression.B44;

        using Image<Rgba32> decoded = Image.Load<Rgba32>(data);
        Assert.Equal(source.Size, decoded.Size);
        Assert.Equal(source[0, 0], decoded[0, 0]);
        Assert.Equal(source[1, 0], decoded[1, 0]);
    }

    /// <summary>
    /// A file that ends in the middle of the pixel data decodes with the default options. The rows of the complete
    /// chunks match the full decode, and the rows of the partial and missing chunks keep the fill value. In Strict mode
    /// the decoder throws.
    /// </summary>
    /// <param name="imagePath">The test image to truncate.</param>
    [Theory]
    [InlineData(TestImages.Exr.Uncompressed)]
    [InlineData(TestImages.Exr.Zip)]
    public void Decode_TruncatedPixelData_KeepsCompleteChunks(string imagePath)
    {
        byte[] full = TestFile.Create(imagePath).Bytes;
        byte[] truncated = full[..(full.Length / 2)];

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(strict, truncated));

        // The rows of each complete chunk match the full decode. The rows of a partial chunk and of a missing chunk keep
        // the fill value.
        using Image<RgbaVector> expected = Image.Load<RgbaVector>(full);
        ClearRowsOfIncompleteChunks(expected, full, truncated.Length);

        using Image<RgbaVector> actual = Image.Load<RgbaVector>(truncated);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Clears the rows of the scanline chunks that do not end inside the first bytes of a file. The header is a list of
    /// attributes. Each attribute has a null-terminated name, a null-terminated type, a 4-byte size and its value. An empty
    /// name ends the list. The offset table follows, with one 8-byte offset for each chunk. A scanline chunk starts with
    /// the 4-byte y coordinate of its first row and the 4-byte size of its pixel data. The compression method sets the
    /// number of rows in each chunk. A chunk has 1 row for no compression, RLE and ZIPS. It has 16 rows for ZIP and
    /// PXR24, and 32 rows for PIZ, B44 and B44A. See "OpenEXR File Layout", section "Regular Scan Line Blocks",
    /// https://openexr.com/en/latest/OpenEXRFileLayout.html.
    /// </summary>
    /// <param name="image">The complete decode. The rows of the incomplete chunks are cleared.</param>
    /// <param name="data">The complete file.</param>
    /// <param name="length">The number of bytes of the file that the decoder gets.</param>
    private static void ClearRowsOfIncompleteChunks(Image<RgbaVector> image, byte[] data, int length)
    {
        // The magic number and the version field come before the attributes.
        int offset = 8;
        int yMin = 0;
        int rowsPerChunk = 1;
        while (data[offset] != 0)
        {
            int nameEnd = Array.IndexOf(data, (byte)0, offset);
            string name = System.Text.Encoding.ASCII.GetString(data, offset, nameEnd - offset);
            int typeEnd = Array.IndexOf(data, (byte)0, nameEnd + 1);
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(typeEnd + 1));
            int value = typeEnd + 5;
            if (name == "dataWindow")
            {
                // A box2i value is xMin, yMin, xMax and yMax.
                yMin = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(value + 4));
            }
            else if (name == "compression")
            {
                rowsPerChunk = data[value] switch
                {
                    3 or 5 => 16,
                    4 or 6 or 7 => 32,
                    _ => 1
                };
            }

            offset = value + size;
        }

        // The empty name of the end of the header is one byte.
        int tableStart = offset + 1;
        int chunkCount = (image.Height + rowsPerChunk - 1) / rowsPerChunk;
        for (int i = 0; i < chunkCount; i++)
        {
            int chunk = (int)BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(tableStart + (i * 8)));
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunk + 4));
            if (chunk + 8 + chunkSize <= length)
            {
                continue;
            }

            int firstRow = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunk)) - yMin;
            for (int y = firstRow; y < Math.Min(firstRow + rowsPerChunk, image.Height); y++)
            {
                image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y).Clear();
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Exr.B44, PixelTypes.Rgba32)]
    public void ExrDecoder_CanDecode_B44Compressed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(ExrDecoder.Instance);
        image.DebugSave(provider);

        // Note: There is a 0,1190% difference to the reference decoder.
        image.CompareToOriginal(provider, ImageComparer.Tolerant(0.011f), ReferenceDecoder);
    }
}

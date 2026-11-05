// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrEncoderTests
{
    protected static readonly IImageDecoder ReferenceDecoder = new MagickReferenceDecoder(ExrFormat.Instance);

    [Theory]
    [InlineData(null, ExrPixelType.Half)]
    [InlineData(ExrPixelType.Float, ExrPixelType.Float)]
    [InlineData(ExrPixelType.Half, ExrPixelType.Half)]
    [InlineData(ExrPixelType.UnsignedInt, ExrPixelType.UnsignedInt)]
    public void EncoderOptions_SetPixelType_Works(ExrPixelType? pixelType, ExrPixelType expectedPixelType)
    {
        ExrEncoder exrEncoder = new() { PixelType = pixelType };
        TestFile file = TestFile.Create(TestImages.Exr.Uncompressed);
        using Image<Rgba32> input = file.CreateRgba32Image();
        using MemoryStream memStream = new();

        input.Save(memStream, exrEncoder);

        memStream.Position = 0;
        ImageInfo output = Image.Identify(memStream);
        ExrMetadata exrMetaData = output.Metadata.GetExrMetadata();
        Assert.Equal(expectedPixelType, exrMetaData.PixelType);
    }

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithNoCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "NoCompression", compression: ExrCompression.None);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithZipCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "ZipCompression", compression: ExrCompression.Zip);

    [Theory]
    [WithFile(TestImages.Exr.Uncompressed, PixelTypes.Rgba32)]
    public void ExrEncoder_WithZipsCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel> => TestExrEncoderCore(provider, "ZipsCompression", compression: ExrCompression.Zips);

    /// <summary>
    /// Verifies that ZIP encoding stores the original pixel block when compression cannot reduce its size.
    /// </summary>
    /// <param name="provider">The stored one-pixel float RGBA input.</param>
    /// <param name="compression">The ZIP method requested from the encoder.</param>
    [Theory]
    [WithFile(TestImages.Exr.FloatRgbaZipFallback, PixelTypes.RgbaVector, ExrCompression.Zip)]
    [WithFile(TestImages.Exr.FloatRgbaZipFallback, PixelTypes.RgbaVector, ExrCompression.Zips)]
    public void ExrEncoder_Zip_WritesUncompressedBlockWhenCompressionDoesNotReduceSize(TestImageProvider<RgbaVector> provider, ExrCompression compression)
    {
        using Image<RgbaVector> input = provider.GetImage(ReferenceDecoder);
        using MemoryStream stream = new();

        input.Save(stream, new ExrEncoder { PixelType = ExrPixelType.Float, Compression = compression });

        byte[] encoded = stream.ToArray();

        // The stored input has one pixel with four distinct float channels. A zlib payload
        // is larger than its 16 pixel bytes, so ZIP must retain those bytes without zlib framing.
        // Skip the variable-length header attributes to find the scanline chunk-offset table.
        int position = 8;

        while (encoded[position] != 0)
        {
            int nameEnd = Array.IndexOf(encoded, (byte)0, position);
            ReadOnlySpan<byte> name = encoded.AsSpan(position, nameEnd - position);
            position = nameEnd + 1;
            position = Array.IndexOf(encoded, (byte)0, position) + 1;
            int length = BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(position, sizeof(int)));
            position += sizeof(int);

            if (name.SequenceEqual("compression"u8))
            {
                Assert.Equal((byte)compression, encoded[position]);
            }

            position += length;
        }

        // The first table entry follows the header's zero terminator. Each chunk starts
        // with its row coordinate and byte count; four float channels require exactly 16 bytes.
        int chunkOffset = (int)BinaryPrimitives.ReadUInt64LittleEndian(encoded.AsSpan(position + 1, sizeof(ulong)));
        int pixelBytes = BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(chunkOffset + sizeof(int), sizeof(int)));
        Assert.Equal(4 * sizeof(float), pixelBytes);

        // The native decoder checks channel order and raw payload decoding. The fixture's
        // straight RGB lies within [0,1], so Rgba32 comparison retains its visible colors.
        stream.Position = 0;
        using Image<Rgba32> decoded = ReferenceDecoder.Decode<Rgba32>(DecoderOptions.Default, stream);
        using Image<Rgba32> expected = input.CloneAs<Rgba32>();
        ImageComparer.Exact.VerifySimilarity(expected, decoded);
    }

    protected static void TestExrEncoderCore<TPixel>(
        TestImageProvider<TPixel> provider,
        object testOutputDetails,
        ExrCompression compression = ExrCompression.None,
        bool useExactComparer = true,
        float compareTolerance = 0.001f,
        IImageDecoder imageDecoder = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ExrEncoder encoder = new()
        {
            Compression = compression,
        };

        // Save the encoded output and compare its native-decoded pixels with the stored input.
        image.VerifyEncoder(
            provider,
            "exr",
            testOutputDetails: testOutputDetails,
            encoder: encoder,
            customComparer: useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance),
            referenceDecoder: imageDecoder ?? ReferenceDecoder);
    }
}

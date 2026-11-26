// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrZipDecoderTests
{
    /// <summary>
    /// Incomplete and oversized blocks are rejected unless image-data recovery is enabled.
    /// </summary>
    /// <param name="length">The inflated payload length.</param>
    /// <param name="integrity">The image-data integrity policy.</param>
    [Theory]
    [InlineData(0, SegmentIntegrityHandling.Strict)]
    [InlineData(8, SegmentIntegrityHandling.Strict)]
    [InlineData(1025, SegmentIntegrityHandling.Strict)]
    [InlineData(0, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(8, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(1025, SegmentIntegrityHandling.IgnoreAncillary)]
    public void Decode_InvalidInflatedBlock_Throws(int length, SegmentIntegrityHandling integrity)
    {
        byte[] data = BuildExr(ZlibCompress(new byte[length]), ExrPixelType.Float, 2, 0);
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };

        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(options, data));
    }

    /// <summary>
    /// Recovering an invalid image-data block must not expose partially decoded or pooled bytes. The rows of the
    /// damaged block keep the fill value, transparent black, as the rows of a missing chunk do.
    /// </summary>
    /// <param name="pixelType">The stored sample type.</param>
    /// <param name="length">The inflated payload length.</param>
    [Theory]
    [InlineData(ExrPixelType.Half, 0)]
    [InlineData(ExrPixelType.Half, 8)]
    [InlineData(ExrPixelType.Half, 1025)]
    [InlineData(ExrPixelType.Float, 0)]
    [InlineData(ExrPixelType.Float, 8)]
    [InlineData(ExrPixelType.Float, 1025)]
    [InlineData(ExrPixelType.UnsignedInt, 0)]
    [InlineData(ExrPixelType.UnsignedInt, 8)]
    [InlineData(ExrPixelType.UnsignedInt, 1025)]
    public void Decode_InvalidInflatedBlock_IgnoreImageData_ClearsPixels(ExrPixelType pixelType, int length)
    {
        byte[] data = BuildExr(ZlibCompress(new byte[length]), pixelType, 2, 0);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator(0x3F);
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };

        using Image<RgbaVector> image = Image.Load<RgbaVector>(options, data);
        using Image<RgbaVector> cleared = new(256, 1);
        ImageComparer.Exact.VerifySimilarity(cleared, image);
    }

    /// <summary>
    /// Missing, truncated, or bad zlib headers obey the image-data integrity policy. In the IgnoreImageData mode, the
    /// rows of the damaged block keep the fill value, transparent black.
    /// </summary>
    /// <param name="cmf">
    /// The first header byte. 0x78 is a valid header. 0x7F has the compression method 15, and 0x88 has the window size
    /// value 8. RFC 1950, section 2.2 "Data format", defines the method 8 as deflate and does not allow window size
    /// values above 7. EXR ZIP compression uses deflate.
    /// </param>
    /// <param name="length">The number of available zlib header bytes.</param>
    /// <param name="integrity">The image-data integrity policy.</param>
    [Theory]
    [InlineData(0x78, 0, SegmentIntegrityHandling.Strict)]
    [InlineData(0x78, 1, SegmentIntegrityHandling.Strict)]
    [InlineData(0x7F, 2, SegmentIntegrityHandling.Strict)]
    [InlineData(0x88, 2, SegmentIntegrityHandling.Strict)]
    [InlineData(0x78, 0, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(0x78, 1, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(0x7F, 2, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(0x88, 2, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(0x78, 0, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(0x78, 1, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(0x7F, 2, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(0x88, 2, SegmentIntegrityHandling.IgnoreImageData)]
    public void Decode_DamagedZlibHeader_RespectsIntegrityHandling(byte cmf, int length, SegmentIntegrityHandling integrity)
    {
        byte[] header = [cmf, 0x9C];
        byte[] data = BuildExr(header[..length], ExrPixelType.Float, 2, 0);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator(0x3F);
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = integrity };

        if (integrity == SegmentIntegrityHandling.IgnoreImageData)
        {
            using Image<RgbaVector> image = Image.Load<RgbaVector>(options, data);
            using Image<RgbaVector> cleared = new(256, 1);
            ImageComparer.Exact.VerifySimilarity(cleared, image);
        }
        else
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(options, data));
        }
    }

    /// <summary>
    /// The chunk header owns the block sizes. A byte count that ends outside the stream is damaged image data. The Strict mode throws. The IgnoreImageData mode
    /// keeps the fill value for the rows of the chunk.
    /// </summary>
    [Fact]
    public void Decode_ChunkByteCountOutsideStream_RespectsIntegrityHandling()
    {
        byte[] compressed = ZlibCompress(new byte[256 * sizeof(float)]);
        byte[] data = BuildExr(compressed, ExrPixelType.Float, 3, 0);

        // The byte count of the chunk comes immediately before the compressed data.
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(data.Length - compressed.Length - sizeof(uint)), uint.MaxValue);

        DecoderOptions strict = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(strict, data));

        DecoderOptions ignore = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using Image<RgbaVector> image = Image.Load<RgbaVector>(ignore, data);
        using Image<RgbaVector> cleared = new(256, 1);
        ImageComparer.Exact.VerifySimilarity(cleared, image);
    }

    /// <summary>
    /// Missing color channels must not inherit the allocator's previous contents.
    /// </summary>
    /// <param name="pixelType">The stored sample type.</param>
    /// <param name="compression">The ZIP compression code.</param>
    /// <param name="yMin">The first row coordinate of the data window.</param>
    [Theory]
    [InlineData(ExrPixelType.Half, 2, 0)]
    [InlineData(ExrPixelType.Float, 2, 0)]
    [InlineData(ExrPixelType.UnsignedInt, 2, 0)]
    [InlineData(ExrPixelType.Half, 3, 0)]
    [InlineData(ExrPixelType.Float, 3, 0)]
    [InlineData(ExrPixelType.UnsignedInt, 3, 0)]
    [InlineData(ExrPixelType.Half, 3, -10)]
    [InlineData(ExrPixelType.Float, 3, -10)]
    [InlineData(ExrPixelType.UnsignedInt, 3, -10)]
    [InlineData(ExrPixelType.Half, 3, 10)]
    [InlineData(ExrPixelType.Float, 3, 10)]
    [InlineData(ExrPixelType.UnsignedInt, 3, 10)]
    public void Decode_SingleRedChannel_InitializesMissingColorChannels(ExrPixelType pixelType, byte compression, int yMin)
    {
        byte[] predicted = new byte[256 * (pixelType == ExrPixelType.Half ? 2 : 4)];

        // A zero first byte followed by 128-valued differences reconstructs an all-zero sample plane.
        predicted.AsSpan(1).Fill(128);
        byte[] data = BuildExr(ZlibCompress(predicted), pixelType, compression, yMin);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator(0x3F);
        DecoderOptions options = new(configuration);

        using Image<RgbaVector> image = Image.Load<RgbaVector>(options, data);
        Assert.Equal(new Size(256, 1), image.Size);

        for (int x = 0; x < image.Width; x++)
        {
            Assert.Equal(new Vector4(0, 0, 0, 1), image[x, 0].ToVector4());
        }
    }

    /// <summary>
    /// A row holds the samples of every channel, in the order of the channel list. The decoder skips the samples of a channel that is not R, G, B, A or Y. Thus
    /// the samples of the next channel stay in place.
    /// </summary>
    [Fact]
    public void Decode_UnknownChannel_SkipsAllItsSamples()
    {
        // The FLOAT Q samples are 0xFFFFFFFF. The HALF R samples are 0x3C00, which is 1.
        byte[] chunk = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x3C, 0x00, 0x3C];
        byte[] data = BuildFloatAndHalfChannelExr(0, ExrCompression.None, chunk);

        using Image<Rgba32> decoded = Image.Load<Rgba32>(data);
        AssertTwoPixels(decoded, new Rgba32(255, 0, 0, 255));
    }

    /// <summary>
    /// PXR24 packs each channel with its own pixel type.
    /// </summary>
    [Fact]
    public void Decode_Pxr24MixedPixelTypes_UsesTypeOfEachChannel()
    {
        // A FLOAT channel keeps 3 byte planes. A HALF channel keeps 2 byte planes. Each plane holds the differences to the previous sample. Q is 0. R starts at
        // 0x3C00, which is 1, and does not change.
        byte[] packed = [0, 0, 0, 0, 0, 0, 0x3C, 0x00, 0x00, 0x00];
        byte[] data = BuildFloatAndHalfChannelExr(0, ExrCompression.Pxr24, ZlibCompress(packed));

        using Image<Rgba32> decoded = Image.Load<Rgba32>(data);
        AssertTwoPixels(decoded, new Rgba32(255, 0, 0, 255));
    }

    /// <summary>
    /// B44 stores the samples of a FLOAT channel without change. For a pLinear HALF channel, the encoder stores exp(x / 8). The decoder restores x.
    /// </summary>
    [Fact]
    public void Decode_B44FloatAndLinearChannels_RestoresSamples()
    {
        // The 8 bytes of the two FLOAT Q samples come first. The flat tile of R holds 0xBC00, the ordered form of 1. 1 is exp(0 / 8). Thus R is 0.
        byte[] chunk = [0, 0, 0, 0, 0, 0, 0, 0, 0xBC, 0x00, 0xFF];
        byte[] data = BuildFloatAndHalfChannelExr(1, ExrCompression.B44, chunk);

        using Image<Rgba32> decoded = Image.Load<Rgba32>(data);
        AssertTwoPixels(decoded, new Rgba32(0, 0, 0, 255));
    }

    /// <summary>
    /// Asserts that both pixels of a 2x1 image have the expected value.
    /// </summary>
    /// <param name="image">The decoded image.</param>
    /// <param name="pixel">The expected value of each pixel.</param>
    private static void AssertTwoPixels(Image<Rgba32> image, Rgba32 pixel)
    {
        using Image<Rgba32> expected = new(2, 1, pixel);
        ImageComparer.Exact.VerifySimilarity(expected, image);
    }

    /// <summary>
    /// Compresses the predictor bytes for a scanline block.
    /// </summary>
    /// <param name="data">The predictor bytes.</param>
    /// <returns>The zlib stream.</returns>
    private static byte[] ZlibCompress(byte[] data)
    {
        if (data.Length == 0)
        {
            // An empty write produces no output on some runtimes. Use a complete zlib stream
            // containing an empty final DEFLATE block and Adler-32 checksum instead.
            return [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01];
        }

        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Builds a single-row EXR containing only the red channel.
    /// </summary>
    /// <param name="compressed">The compressed scanline bytes.</param>
    /// <param name="pixelType">The stored sample type.</param>
    /// <param name="compression">The compression code.</param>
    /// <param name="yMin">The first row coordinate of the data window.</param>
    /// <returns>The encoded image.</returns>
    internal static byte[] BuildExr(byte[] compressed, ExrPixelType pixelType, byte compression, int yMin)
        => BuildExr(BuildChannelList(("R", pixelType, 0)), 256, compressed, compression, yMin);

    /// <summary>
    /// Builds a 2x1 EXR with a FLOAT Q channel and a HALF R channel.
    /// </summary>
    /// <param name="redLinear">The pLinear value of the R channel.</param>
    /// <param name="compression">The compression method of the chunk.</param>
    /// <param name="compressed">The stored bytes of the chunk.</param>
    /// <returns>The encoded image.</returns>
    private static byte[] BuildFloatAndHalfChannelExr(byte redLinear, ExrCompression compression, byte[] compressed)
        => BuildExr(BuildChannelList(("Q", ExrPixelType.Float, 0), ("R", ExrPixelType.Half, redLinear)), 2, compressed, (byte)compression, 0);

    /// <summary>
    /// Builds the value of a channel list attribute. Each channel has the x and y sampling 1.
    /// </summary>
    /// <param name="channels">The channels, in the order of the list.</param>
    /// <returns>The attribute value, with the null byte at its end.</returns>
    private static byte[] BuildChannelList(params (string Name, ExrPixelType PixelType, byte Linear)[] channels)
    {
        using MemoryStream channelStream = new();
        using BinaryWriter channelWriter = new(channelStream);
        foreach ((string name, ExrPixelType pixelType, byte linear) in channels)
        {
            WriteString(channelWriter, name);
            channelWriter.Write((int)pixelType);
            channelWriter.Write(linear);
            channelWriter.Write(new byte[] { 0, 0, 0 });
            channelWriter.Write(1);
            channelWriter.Write(1);
        }

        channelWriter.Write((byte)0);
        return channelStream.ToArray();
    }

    /// <summary>
    /// Builds a single-row EXR with one scanline chunk.
    /// </summary>
    /// <param name="channelList">The value of the channel list attribute.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="compressed">The compressed scanline bytes.</param>
    /// <param name="compression">The compression code.</param>
    /// <param name="yMin">The first row coordinate of the data window.</param>
    /// <returns>The encoded image.</returns>
    private static byte[] BuildExr(byte[] channelList, int width, byte[] compressed, byte compression, int yMin)
    {
        const int height = 1;

        using MemoryStream output = new();
        using BinaryWriter writer = new(output);

        writer.Write(new byte[] { 0x76, 0x2F, 0x31, 0x01 });
        writer.Write((byte)2);
        writer.Write(new byte[] { 0, 0, 0 });

        WriteAttribute(writer, "channels", "chlist", channelList);
        WriteAttribute(writer, "compression", "compression", [compression]);

        using (MemoryStream boxStream = new())
        using (BinaryWriter boxWriter = new(boxStream))
        {
            boxWriter.Write(0);
            boxWriter.Write(yMin);
            boxWriter.Write(width - 1);
            boxWriter.Write(yMin + height - 1);

            byte[] box = boxStream.ToArray();
            WriteAttribute(writer, "dataWindow", "box2i", box);
            WriteAttribute(writer, "displayWindow", "box2i", box);
        }

        WriteAttribute(writer, "lineOrder", "lineOrder", [0]);

        byte[] one = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(one, 1F);
        WriteAttribute(writer, "pixelAspectRatio", "float", one);
        WriteAttribute(writer, "screenWindowCenter", "v2f", new byte[8]);
        WriteAttribute(writer, "screenWindowWidth", "float", one);
        writer.Write((byte)0);

        long chunkStart = output.Position + sizeof(ulong);
        writer.Write((ulong)chunkStart);
        writer.Write(yMin);
        writer.Write((uint)compressed.Length);
        writer.Write(compressed);

        return output.ToArray();
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }

    private static void WriteAttribute(BinaryWriter writer, string name, string type, byte[] value)
    {
        WriteString(writer, name);
        WriteString(writer, type);
        writer.Write(value.Length);
        writer.Write(value);
    }
}

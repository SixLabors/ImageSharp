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

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrZipDecoderTests
{
    [Fact]
    public void Decode_ShortInflatedBlock_Throws()
    {
        byte[] data = BuildExr(ZlibCompress(new byte[8]), ExrPixelType.Float, 2);

        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(data));
    }

    /// <summary>
    /// Missing color channels must not inherit the allocator's previous contents.
    /// </summary>
    /// <param name="pixelType">The stored sample type.</param>
    /// <param name="compression">The ZIP compression code.</param>
    [Theory]
    [InlineData(ExrPixelType.Half, 2)]
    [InlineData(ExrPixelType.Float, 2)]
    [InlineData(ExrPixelType.UnsignedInt, 2)]
    [InlineData(ExrPixelType.Half, 3)]
    [InlineData(ExrPixelType.Float, 3)]
    [InlineData(ExrPixelType.UnsignedInt, 3)]
    public void Decode_SingleRedChannel_InitializesMissingColorChannels(ExrPixelType pixelType, byte compression)
    {
        byte[] predicted = new byte[256 * (pixelType == ExrPixelType.Half ? 2 : 4)];

        // A zero first byte followed by 128-valued differences reconstructs an all-zero sample plane.
        predicted.AsSpan(1).Fill(128);
        byte[] data = BuildExr(ZlibCompress(predicted), pixelType, compression);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = new TestMemoryAllocator(0x3F);
        DecoderOptions options = new() { Configuration = configuration };

        using Image<RgbaVector> image = Image.Load<RgbaVector>(options, data);
        Assert.Equal(new Size(256, 1), image.Size);

        for (int x = 0; x < image.Width; x++)
        {
            Assert.Equal(new Vector4(0, 0, 0, 1), image[x, 0].ToVector4());
        }
    }

    /// <summary>
    /// Compresses the predictor bytes for a scanline block.
    /// </summary>
    /// <param name="data">The predictor bytes.</param>
    /// <returns>The zlib stream.</returns>
    private static byte[] ZlibCompress(byte[] data)
    {
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
    /// <param name="compression">The ZIP compression code.</param>
    /// <returns>The encoded image.</returns>
    private static byte[] BuildExr(byte[] compressed, ExrPixelType pixelType, byte compression)
    {
        const int width = 256;
        const int height = 1;

        using MemoryStream output = new();
        using BinaryWriter writer = new(output);

        writer.Write(new byte[] { 0x76, 0x2F, 0x31, 0x01 });
        writer.Write((byte)2);
        writer.Write(new byte[] { 0, 0, 0 });

        using (MemoryStream channelStream = new())
        using (BinaryWriter channelWriter = new(channelStream))
        {
            WriteString(channelWriter, "R");
            channelWriter.Write((int)pixelType);
            channelWriter.Write((byte)0);
            channelWriter.Write(new byte[] { 0, 0, 0 });
            channelWriter.Write(1);
            channelWriter.Write(1);
            channelWriter.Write((byte)0);

            WriteAttribute(writer, "channels", "chlist", channelStream.ToArray());
        }

        WriteAttribute(writer, "compression", "compression", [compression]);

        using (MemoryStream boxStream = new())
        using (BinaryWriter boxWriter = new(boxStream))
        {
            boxWriter.Write(0);
            boxWriter.Write(0);
            boxWriter.Write(width - 1);
            boxWriter.Write(height - 1);

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
        writer.Write(0U);
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

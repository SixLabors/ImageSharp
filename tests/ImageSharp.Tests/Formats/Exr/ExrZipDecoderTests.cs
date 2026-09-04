// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Exr;

[Trait("Format", "Exr")]
[ValidateDisposedMemoryAllocations]
public class ExrZipDecoderTests
{
    [Fact]
    public void Decode_ShortInflatedBlock_Throws()
    {
        byte[] data = BuildExr(ZlibCompress(new byte[8]));

        Assert.Throws<InvalidImageContentException>(() => Image.Load<RgbaVector>(data));
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] BuildExr(byte[] compressed)
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
            channelWriter.Write(2);
            channelWriter.Write((byte)0);
            channelWriter.Write(new byte[] { 0, 0, 0 });
            channelWriter.Write(1);
            channelWriter.Write(1);
            channelWriter.Write((byte)0);

            WriteAttribute(writer, "channels", "chlist", channelStream.ToArray());
        }

        WriteAttribute(writer, "compression", "compression", [2]);

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

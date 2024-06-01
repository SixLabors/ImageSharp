// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class ObuFrameHeaderTests
{
    [Theory]
    // [InlineData(TestImages.Heif.IrvineAvif, 0x0102, 0x000D)]
    // [InlineData(TestImages.Heif.IrvineAvif, 0x0198, 0x6BD1)]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    public void ReadFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1Decoder decoder = new();

        // Act
        decoder.Decode(span);

        // Assert
        Assert.True(decoder.SequenceHeaderDone);
        Assert.False(decoder.SeenFrameHeader);
    }

    /* [Theory]
    // [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    // public void BinaryIdenticalRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    // {
    //    // Assign
    //    string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
    //    byte[] content = File.ReadAllBytes(filePath);
    //    Span<byte> span = content.AsSpan(fileOffset, blockSize);
    //    IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();
    //    Av1BitStreamReader reader = new(span);

    //    // Act 1
    //    ObuReader.Read(ref reader, blockSize, tileDecoder);

    //    // Assign 2
    //    MemoryStream encoded = new();

    //    // Act 2
    //    ObuWriter.Write(encoded, tileDecoder);

    //    // Assert
    //    Assert.Equal(span, encoded.ToArray());
    //}
    */

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    public void ThreeTimeRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        IAv1TileDecoder tileDecoder = new Av1TileDecoderStub();
        Av1BitStreamReader reader = new(span);

        // Act 1
        ObuReader.Read(ref reader, blockSize, tileDecoder);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter.Write(encoded, tileDecoder);

        // Assign 2
        Span<byte> encodedBuffer = encoded.ToArray();
        IAv1TileDecoder tileDecoder2 = new Av1TileDecoderStub();
        Av1BitStreamReader reader2 = new(span);

        // Act 2
        ObuReader.Read(ref reader2, encodedBuffer.Length, tileDecoder2);

        // Assert
        Assert.Equal(PrettyPrintProperties(tileDecoder.SequenceHeader.ColorConfig), PrettyPrintProperties(tileDecoder2.SequenceHeader.ColorConfig));
        Assert.Equal(PrettyPrintProperties(tileDecoder.SequenceHeader), PrettyPrintProperties(tileDecoder2.SequenceHeader));
        Assert.Equal(PrettyPrintProperties(tileDecoder.FrameInfo), PrettyPrintProperties(tileDecoder2.FrameInfo));
        Assert.Equal(PrettyPrintProperties(tileDecoder.TileInfo), PrettyPrintProperties(tileDecoder2.TileInfo));
    }

    private static string PrettyPrintProperties(object obj)
    {
        StringBuilder builder = new();
        builder.Append(obj.GetType().Name);
        builder.AppendLine("{");
        MemberInfo[] properties = obj.GetType().FindMembers(MemberTypes.Property, BindingFlags.Instance | BindingFlags.Public, null, null);
        foreach (MemberInfo member in properties)
        {
            if (member is PropertyInfo property)
            {
                builder.Append(property.Name);
                builder.Append(" = ");
                object value = property.GetValue(obj) ?? "NULL";
                builder.AppendLine(value.ToString());
            }
        }

        builder.AppendLine("}");
        return builder.ToString();
    }
}

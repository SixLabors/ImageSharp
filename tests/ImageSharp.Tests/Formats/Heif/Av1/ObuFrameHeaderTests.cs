// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

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
        Av1BitStreamReader reader = new(span);
        IAv1TileDecoder decoder = new Av1TileDecoderStub();
        ObuReader obuReader = new();

        // Act
        obuReader.Read(ref reader, blockSize, decoder);

        // Assert
        Assert.NotNull(obuReader.SequenceHeader);
        Assert.NotNull(obuReader.FrameHeader);
        Assert.NotNull(obuReader.FrameHeader.TilesInfo);
        Assert.Equal(reader.Length * Av1BitStreamReader.WordSize, reader.BitPosition);
        Assert.Equal(reader.Length * 4, blockSize);
    }

    /*
    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC)]
    public void BinaryIdenticalRoundTripFrameHeader(string filename, int fileOffset, int blockSize)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> span = content.AsSpan(fileOffset, blockSize);
        Av1TileDecoderStub tileDecoder = new();
        Av1BitStreamReader reader = new(span);
        ObuReader obuReader = new();

        // Act 1
        obuReader.Read(ref reader, blockSize, tileDecoder);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter obuWriter = new();
        ObuWriter.Write(encoded, obuReader.SequenceHeader, obuReader.FrameHeader);

        // Assert
        Assert.Equal(span, encoded.ToArray());
    }
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
        ObuReader obuReader1 = new();

        // Act 1
        obuReader1.Read(ref reader, blockSize, tileDecoder);

        // Assign 2
        MemoryStream encoded = new();

        // Act 2
        ObuWriter.Write(encoded, obuReader1.SequenceHeader, obuReader1.FrameHeader);

        // Assign 2
        Span<byte> encodedBuffer = encoded.ToArray();
        IAv1TileDecoder tileDecoder2 = new Av1TileDecoderStub();
        Av1BitStreamReader reader2 = new(span);
        ObuReader obuReader2 = new();

        // Act 2
        obuReader2.Read(ref reader2, encodedBuffer.Length, tileDecoder2);

        // Assert
        Assert.Equal(PrettyPrintProperties(obuReader1.SequenceHeader.ColorConfig), PrettyPrintProperties(obuReader2.SequenceHeader.ColorConfig));
        Assert.Equal(PrettyPrintProperties(obuReader1.SequenceHeader), PrettyPrintProperties(obuReader2.SequenceHeader));
        Assert.Equal(PrettyPrintProperties(obuReader1.FrameHeader), PrettyPrintProperties(obuReader2.FrameHeader));
        Assert.Equal(PrettyPrintProperties(obuReader1.FrameHeader.TilesInfo), PrettyPrintProperties(obuReader2.FrameHeader.TilesInfo));
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

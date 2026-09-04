// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

public partial class PngDecoderTests
{
    [Fact]
    public void Decode_IccLutExceedsVectorChannelCount_Throws()
    {
        byte[] profileData = BuildLut16Profile(3, 15, 2, 2, 2);
        byte[] pngData = BuildPng(profileData);
        DecoderOptions options = new() { ColorProfileHandling = ColorProfileHandling.Convert };

        Assert.Throws<InvalidIccProfileException>(() => Image.Load(options, pngData));
    }

    private static byte[] BuildLut16Profile(int inputChannels, int outputChannels, int clutPoints, int inputTableLength, int outputTableLength)
    {
        using MemoryStream stream = new();

        byte[] header = new byte[128];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), 0x04300000U);
        Encoding.ASCII.GetBytes("mntr").CopyTo(header, 12);
        Encoding.ASCII.GetBytes("RGB ").CopyTo(header, 16);
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(header, 20);
        stream.Write(header);

        WriteUInt32(1);
        stream.Write(Encoding.ASCII.GetBytes("A2B0"));
        long offsetPosition = stream.Position;
        WriteUInt32(0);
        long sizePosition = stream.Position;
        WriteUInt32(0);

        long tagStart = stream.Position;
        stream.Write(Encoding.ASCII.GetBytes("mft2"));
        WriteUInt32(0);
        stream.WriteByte((byte)inputChannels);
        stream.WriteByte((byte)outputChannels);
        stream.WriteByte((byte)clutPoints);
        stream.WriteByte(0);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                WriteFix16(x == y ? 1D : 0D);
            }
        }

        WriteUInt16((ushort)inputTableLength);
        WriteUInt16((ushort)outputTableLength);

        for (int channel = 0; channel < inputChannels; channel++)
        {
            for (int i = 0; i < inputTableLength; i++)
            {
                WriteUInt16((ushort)(i == 0 ? 0 : ushort.MaxValue));
            }
        }

        int clutLength = (int)Math.Pow(clutPoints, inputChannels);
        for (int i = 0; i < clutLength; i++)
        {
            for (int channel = 0; channel < outputChannels; channel++)
            {
                WriteUInt16(0x8000);
            }
        }

        for (int channel = 0; channel < outputChannels; channel++)
        {
            for (int i = 0; i < outputTableLength; i++)
            {
                WriteUInt16((ushort)(i == 0 ? 0 : ushort.MaxValue));
            }
        }

        long tagEnd = stream.Position;
        byte[] result = stream.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan((int)offsetPosition), (uint)tagStart);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan((int)sizePosition), (uint)(tagEnd - tagStart));
        BinaryPrimitives.WriteUInt32BigEndian(result, (uint)result.Length);
        return result;

        void WriteUInt32(uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            stream.Write(buffer);
        }

        void WriteUInt16(ushort value)
        {
            Span<byte> buffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            stream.Write(buffer);
        }

        void WriteFix16(double value)
        {
            int rawValue = (int)Math.Round(value * 65536D);
            WriteUInt32(unchecked((uint)rawValue));
        }
    }

    private static byte[] BuildPng(byte[] profileData)
    {
        using Image<Rgb24> image = new(16, 16);
        image.Metadata.IccProfile = new IccProfile(profileData);

        using MemoryStream stream = new();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}

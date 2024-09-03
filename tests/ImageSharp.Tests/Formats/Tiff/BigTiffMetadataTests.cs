// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Writers;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

[Trait("Format", "Tiff")]
public class BigTiffMetadataTests
{
    [Fact]
    public void ExifLong8()
    {
        ExifLong8 long8 = new ExifLong8(ExifTagValue.StripByteCounts);

        Assert.True(long8.TrySetValue(0));
        Assert.Equal(0UL, long8.GetValue());

        Assert.True(long8.TrySetValue(100u));
        Assert.Equal(100UL, long8.GetValue());

        Assert.True(long8.TrySetValue(ulong.MaxValue));
        Assert.Equal(ulong.MaxValue, long8.GetValue());

        Assert.False(long8.TrySetValue(-65));
        Assert.Equal(ulong.MaxValue, long8.GetValue());
    }

    [Fact]
    public void ExifSignedLong8()
    {
        ExifSignedLong8 long8 = new ExifSignedLong8(ExifTagValue.ImageID);

        Assert.False(long8.TrySetValue(0));

        Assert.True(long8.TrySetValue(0L));
        Assert.Equal(0L, long8.GetValue());

        Assert.True(long8.TrySetValue(-100L));
        Assert.Equal(-100L, long8.GetValue());
        Assert.Equal(ExifDataType.SignedLong8, long8.DataType);

        Assert.True(long8.TrySetValue(long.MaxValue));
        Assert.Equal(long.MaxValue, long8.GetValue());
        Assert.Equal(ExifDataType.SignedLong8, long8.DataType);
    }

    [Fact]
    public void ExifLong8Array()
    {
        ExifLong8Array long8 = new ExifLong8Array(ExifTagValue.StripOffsets);

        Assert.True(long8.TrySetValue((short)-123));
        Assert.Equal(new[] { 0UL }, long8.GetValue());

        Assert.True(long8.TrySetValue((ushort)123));
        Assert.Equal(new[] { 123UL }, long8.GetValue());

        Assert.True(long8.TrySetValue((short)123));
        Assert.Equal(new[] { 123UL }, long8.GetValue());

        Assert.True(long8.TrySetValue(123));
        Assert.Equal(new[] { 123UL }, long8.GetValue());

        Assert.True(long8.TrySetValue(123u));
        Assert.Equal(new[] { 123UL }, long8.GetValue());

        Assert.True(long8.TrySetValue(123L));
        Assert.Equal(new[] { 123UL }, long8.GetValue());

        Assert.True(long8.TrySetValue(123UL));
        Assert.Equal(new[] { 123UL }, long8.GetValue());

        Assert.True(long8.TrySetValue(new short[] { -1, 2, -3, 4 }));
        Assert.Equal(new ulong[] { 0, 2UL, 0, 4UL }, long8.GetValue());

        Assert.True(long8.TrySetValue(new[] { 1, 2, 3, 4 }));
        Assert.Equal(new[] { 1UL, 2UL, 3UL, 4UL }, long8.GetValue());
        Assert.Equal(ExifDataType.Long, long8.DataType);

        Assert.True(long8.TrySetValue(new[] { 1, 2, 3, 4, long.MaxValue }));
        Assert.Equal(new[] { 1UL, 2UL, 3UL, 4UL, (ulong)long.MaxValue }, long8.GetValue());
        Assert.Equal(ExifDataType.Long8, long8.DataType);
    }

    [Fact]
    public void ExifSignedLong8Array()
    {
        ExifSignedLong8Array long8 = new ExifSignedLong8Array(ExifTagValue.StripOffsets);

        Assert.True(long8.TrySetValue(new[] { 0L }));
        Assert.Equal(new[] { 0L }, long8.GetValue());
        Assert.Equal(ExifDataType.SignedLong8, long8.DataType);

        Assert.True(long8.TrySetValue(new[] { -1L, 2L, long.MinValue, 4L }));
        Assert.Equal(new[] { -1L, 2L, long.MinValue, 4L }, long8.GetValue());
        Assert.Equal(ExifDataType.SignedLong8, long8.DataType);
    }

    [Fact]
    public void NotCoveredTags()
    {
        using Image<Rgba32> input = new Image<Rgba32>(10, 10);

        Dictionary<ExifTag, (ExifDataType DataType, object Value)> testTags = new Dictionary<ExifTag, (ExifDataType DataType, object Value)>
        {
            { new ExifTag<float[]>((ExifTagValue)0xdd01), (ExifDataType.SingleFloat, new float[] { 1.2f, 2.3f, 4.5f }) },
            { new ExifTag<float>((ExifTagValue)0xdd02), (ExifDataType.SingleFloat, 2.345f) },
            { new ExifTag<double[]>((ExifTagValue)0xdd03), (ExifDataType.DoubleFloat, new double[] { 4.5, 6.7 }) },
            { new ExifTag<double>((ExifTagValue)0xdd04), (ExifDataType.DoubleFloat, 8.903) },
            { new ExifTag<sbyte>((ExifTagValue)0xdd05), (ExifDataType.SignedByte, (sbyte)-3) },
            { new ExifTag<sbyte[]>((ExifTagValue)0xdd06), (ExifDataType.SignedByte, new sbyte[] { -3, 0, 5 }) },
            { new ExifTag<int[]>((ExifTagValue)0xdd07), (ExifDataType.SignedLong, new int[] { int.MinValue, 1, int.MaxValue }) },
            { new ExifTag<uint[]>((ExifTagValue)0xdd08), (ExifDataType.Long, new uint[] { 0, 1, uint.MaxValue }) },
            { new ExifTag<short>((ExifTagValue)0xdd09), (ExifDataType.SignedShort, (short)-1234) },
            { new ExifTag<ushort>((ExifTagValue)0xdd10), (ExifDataType.Short, (ushort)1234) },
        };

        // arrange
        List<IExifValue> values = new List<IExifValue>();
        foreach (KeyValuePair<ExifTag, (ExifDataType DataType, object Value)> tag in testTags)
        {
            ExifValue newExifValue = ExifValues.Create((ExifTagValue)(ushort)tag.Key, tag.Value.DataType, tag.Value.Value is Array);

            Assert.True(newExifValue.TrySetValue(tag.Value.Value));
            values.Add(newExifValue);
        }

        input.Frames.RootFrame.Metadata.ExifProfile = new ExifProfile(values, Array.Empty<ExifTag>());

        // act
        TiffEncoder encoder = new TiffEncoder();
        using MemoryStream memStream = new MemoryStream();
        input.Save(memStream, encoder);

        // assert
        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageFrameMetadata loadedFrameMetadata = output.Frames.RootFrame.Metadata;
        foreach (KeyValuePair<ExifTag, (ExifDataType DataType, object Value)> tag in testTags)
        {
            IExifValue exifValue = loadedFrameMetadata.ExifProfile.GetValueInternal(tag.Key);
            Assert.NotNull(exifValue);
            object value = exifValue.GetValue();

            Assert.Equal(tag.Value.DataType, exifValue.DataType);
            {
                Assert.Equal(value, tag.Value.Value);
            }
        }
    }

    [Fact]
    public void NotCoveredTags64bit()
    {
        Dictionary<ExifTag, (ExifDataType DataType, object Value)> testTags = new Dictionary<ExifTag, (ExifDataType DataType, object Value)>
        {
            { new ExifTag<ulong>((ExifTagValue)0xdd11), (ExifDataType.Long8, ulong.MaxValue) },
            { new ExifTag<long>((ExifTagValue)0xdd12), (ExifDataType.SignedLong8, long.MaxValue) },
            //// WriteIfdTags64Bit: arrays aren't support (by our code)
            ////{ new ExifTag<ulong[]>((ExifTagValue)0xdd13), (ExifDataType.Long8, new ulong[] { 0, 1234, 56789UL, ulong.MaxValue }) },
            ////{ new ExifTag<long[]>((ExifTagValue)0xdd14), (ExifDataType.SignedLong8, new long[] { -1234, 56789L, long.MaxValue }) },
        };

        List<IExifValue> values = new List<IExifValue>();
        foreach (KeyValuePair<ExifTag, (ExifDataType DataType, object Value)> tag in testTags)
        {
            ExifValue newExifValue = ExifValues.Create((ExifTagValue)(ushort)tag.Key, tag.Value.DataType, tag.Value.Value is Array);

            Assert.True(newExifValue.TrySetValue(tag.Value.Value));
            values.Add(newExifValue);
        }

        // act
        byte[] inputBytes = WriteIfdTags64Bit(values);
        Configuration config = Configuration.Default;
        EntryReader reader = new EntryReader(
            new MemoryStream(inputBytes),
            BitConverter.IsLittleEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian,
            config.MemoryAllocator);

        reader.ReadTags(true, 0);

        List<IExifValue> outputTags = reader.Values;

        // assert
        foreach (KeyValuePair<ExifTag, (ExifDataType DataType, object Value)> tag in testTags)
        {
            IExifValue exifValue = outputTags.Find(t => t.Tag == tag.Key);
            Assert.NotNull(exifValue);
            object value = exifValue.GetValue();

            Assert.Equal(tag.Value.DataType, exifValue.DataType);
            {
                Assert.Equal(value, tag.Value.Value);
            }
        }
    }

    private static byte[] WriteIfdTags64Bit(List<IExifValue> values)
    {
        byte[] buffer = new byte[8];
        MemoryStream ms = new MemoryStream();
        TiffStreamWriter writer = new TiffStreamWriter(ms);
        WriteLong8(writer, buffer, (ulong)values.Count);

        foreach (IExifValue entry in values)
        {
            writer.Write((ushort)entry.Tag, buffer);
            writer.Write((ushort)entry.DataType, buffer);
            WriteLong8(writer, buffer, ExifWriter.GetNumberOfComponents(entry));

            uint length = ExifWriter.GetLength(entry);

            Assert.True(length <= 8);

            if (length <= 8)
            {
                int sz = ExifWriter.WriteValue(entry, buffer, 0);
                DebugGuard.IsTrue(sz == length, "Incorrect number of bytes written");

                // write padded
                writer.BaseStream.Write(buffer.AsSpan(0, sz));
                int d = sz % 8;
                if (d != 0)
                {
                    writer.BaseStream.Write(new byte[d]);
                }
            }
        }

        WriteLong8(writer, buffer, 0);

        return ms.ToArray();
    }

    private static void WriteLong8(TiffStreamWriter writer, byte[] buffer, ulong value)
    {
        if (TiffStreamWriter.IsLittleEndian)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        }

        writer.BaseStream.Write(buffer);
    }
}

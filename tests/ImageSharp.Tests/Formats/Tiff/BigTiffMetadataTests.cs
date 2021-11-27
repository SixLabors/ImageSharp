// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class BigTiffMetadataTests
    {
        private static TiffDecoder TiffDecoder => new TiffDecoder();

        [Fact]
        public void ExifLong8()
        {
            var long8 = new ExifLong8(ExifTagValue.StripByteCounts);

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
            var long8 = new ExifSignedLong8(ExifTagValue.ImageID);

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
            var long8 = new ExifLong8Array(ExifTagValue.StripOffsets);

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
            var long8 = new ExifSignedLong8Array(ExifTagValue.StripOffsets);

            Assert.True(long8.TrySetValue(new[] { 0L }));
            Assert.Equal(new[] { 0L }, long8.GetValue());
            Assert.Equal(ExifDataType.SignedLong8, long8.DataType);

            Assert.True(long8.TrySetValue(new[] { -1L, 2L, long.MinValue, 4L }));
            Assert.Equal(new[] { -1L, 2L, long.MinValue, 4L }, long8.GetValue());
            Assert.Equal(ExifDataType.SignedLong8, long8.DataType);
        }

        [Theory]
        [WithFile(RgbUncompressed, PixelTypes.Rgb24)]
        public void ExifTags<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> input = provider.GetImage();

            var testTags = new Dictionary<ExifTag, (ExifDataType DataType, object Value)>
            {
                { new ExifTag<float[]>((ExifTagValue)0xdd01), (ExifDataType.SingleFloat, new float[] { 1.2f, 2.3f, 4.5f }) },
                { new ExifTag<double[]>((ExifTagValue)0xdd02), (ExifDataType.DoubleFloat, new double[] { 4.5, 6.7 }) },
                { new ExifTag<double>((ExifTagValue)0xdd03), (ExifDataType.DoubleFloat, 8.903) },
                { new ExifTag<sbyte>((ExifTagValue)0xdd04), (ExifDataType.SignedByte, (sbyte)-3) },
                { new ExifTag<sbyte[]>((ExifTagValue)0xdd05), (ExifDataType.SignedByte, new sbyte[] { -3, 0, 5 }) },
                { new ExifTag<int[]>((ExifTagValue)0xdd06), (ExifDataType.SignedLong, new int[] { int.MinValue, 1, int.MaxValue }) },
                { new ExifTag<uint[]>((ExifTagValue)0xdd07), (ExifDataType.Long, new uint[] { 0, 1, uint.MaxValue }) },
                { new ExifTag<ushort>((ExifTagValue)0xdd08), (ExifDataType.Short, (ushort)1234) },
                //{ new ExifTag<ulong>((ExifTagValue)0xdd09), (ExifDataType.Long8, ulong.MaxValue) },
            };

            // arrange
            var values = new List<IExifValue>();
            foreach (KeyValuePair<ExifTag, (ExifDataType DataType, object Value)> tag in testTags)
            {
                ExifValue newExifValue = ExifValues.Create((ExifTagValue)(ushort)tag.Key, tag.Value.DataType, tag.Value.Value is Array);

                Assert.True(newExifValue.TrySetValue(tag.Value.Value));
                values.Add(newExifValue);
            }

            input.Frames.RootFrame.Metadata.ExifProfile = new ExifProfile(values, Array.Empty<ExifTag>());

            // act
            var encoder = new TiffEncoder();
            using var memStream = new MemoryStream();
            input.Save(memStream, encoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            ImageFrameMetadata loadedFrameMetadata = output.Frames.RootFrame.Metadata;
            foreach (KeyValuePair<ExifTag, (ExifDataType DataType, object Value)> tag in testTags)
            {
                IExifValue exifValue = loadedFrameMetadata.ExifProfile.GetValueInternal(tag.Key);
                Assert.NotNull(exifValue);
                object value = exifValue.GetValue();

                Assert.Equal(tag.Value.DataType, exifValue.DataType);
                if (value is Array array)
                {
                    Assert.Equal(array, tag.Value.Value as Array);
                }
                else
                {
                    Assert.Equal(value, tag.Value.Value);
                }
            }
        }
    }
}

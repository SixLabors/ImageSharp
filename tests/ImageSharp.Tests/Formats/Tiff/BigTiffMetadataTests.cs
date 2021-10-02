// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

using Xunit;

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

            Assert.True(long8.TrySetValue(100L));
            Assert.Equal(100L, long8.GetValue());
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

            Assert.True(long8.TrySetValue(123L));
            Assert.Equal(new[] { 123UL }, long8.GetValue());

            Assert.True(long8.TrySetValue(123UL));
            Assert.Equal(new[] { 123UL }, long8.GetValue());

            Assert.True(long8.TrySetValue(new[] { 1, 2, 3, 4 }));
            Assert.Equal(new[] { 1UL, 2UL, 3UL, 4UL }, long8.GetValue());
        }

        [Fact]
        public void ExifSignedLong8Array()
        {
            var long8 = new ExifSignedLong8Array(ExifTagValue.StripOffsets);

            Assert.True(long8.TrySetValue(new[] { 0L }));
            Assert.Equal(new[] { 0L }, long8.GetValue());

            Assert.True(long8.TrySetValue(new[] { -1L, 2L, -3L, 4L }));
            Assert.Equal(new[] { -1L, 2L, -3L, 4L }, long8.GetValue());
        }
    }
}

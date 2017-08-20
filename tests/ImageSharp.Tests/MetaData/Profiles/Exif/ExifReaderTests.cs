// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifReaderTests
    {
        [Fact]
        public void Read_DataIsEmpty_ReturnsEmptyCollection()
        {
            ExifReader reader = new ExifReader();
            byte[] data = new byte[] { };

            Collection<ExifValue> result = reader.Read(data);

            Assert.Equal(0, result.Count);
        }

        [Fact]
        public void Read_DataIsMinimal_ReturnsEmptyCollection()
        {
            ExifReader reader = new ExifReader();
            byte[] data = new byte[] { 69, 120, 105, 102, 0, 0 };

            Collection<ExifValue> result = reader.Read(data);

            Assert.Equal(0, result.Count);
        }
    }
}

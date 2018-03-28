// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ExifReaderTests
    {
        [Fact]
        public void Read_DataIsEmpty_ReturnsEmptyCollection()
        {
            var reader = new ExifReader(new byte[] { });

            IList<ExifValue> result = reader.ReadValues();

            Assert.Equal(0, result.Count);
        }

        [Fact]
        public void Read_DataIsMinimal_ReturnsEmptyCollection()
        {
            var reader = new ExifReader(new byte[] { 69, 120, 105, 102, 0, 0 });

            IList<ExifValue> result = reader.ReadValues();

            Assert.Equal(0, result.Count);
        }
    }
}

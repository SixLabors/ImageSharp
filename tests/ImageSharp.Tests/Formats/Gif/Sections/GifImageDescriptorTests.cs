// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Gif;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifImageDescriptorTests
    {
        [Fact]
        public void TestPackedValue()
        {
            Assert.Equal(129, GifImageDescriptor.GetPackedValue(true, false, false, 1));  // localColorTable
            Assert.Equal(65,  GifImageDescriptor.GetPackedValue(false, true, false, 1));  // interfaceFlag
            Assert.Equal(33,  GifImageDescriptor.GetPackedValue(false, false, true, 1));  // sortFlag
            Assert.Equal(225, GifImageDescriptor.GetPackedValue(true,  true,  true, 1));  // all
            Assert.Equal(8,   GifImageDescriptor.GetPackedValue(false, false, false, 8));
            Assert.Equal(228, GifImageDescriptor.GetPackedValue(true, true, true, 4));
            Assert.Equal(232, GifImageDescriptor.GetPackedValue(true, true, true, 8));
        }
    }
}
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
            Assert.Equal(128, GifImageDescriptor.GetPackedValue(true, false, false, 1));  // localColorTable
            Assert.Equal(64,  GifImageDescriptor.GetPackedValue(false, true, false, 1));  // interfaceFlag
            Assert.Equal(32,  GifImageDescriptor.GetPackedValue(false, false, true, 1));  // sortFlag
            Assert.Equal(224, GifImageDescriptor.GetPackedValue(true,  true,  true, 1));  // all
            Assert.Equal(7,   GifImageDescriptor.GetPackedValue(false, false, false, 8));
            Assert.Equal(227, GifImageDescriptor.GetPackedValue(true, true, true, 4));
            Assert.Equal(231, GifImageDescriptor.GetPackedValue(true, true, true, 8));
        }
    }
}
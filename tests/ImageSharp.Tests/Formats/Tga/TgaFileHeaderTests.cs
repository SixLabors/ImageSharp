// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tga
{
    public class TgaFileHeaderTests
    {
        private static readonly byte[] Data = { 0, 0, 0 };

        private MemoryStream Stream { get; } = new MemoryStream(Data);

        [Fact]
        public void ImageLoad_WithoutEnoughData_Throws_UnknownImageFormatException()
        {
            Assert.Throws<UnknownImageFormatException>(() =>
            {
                using (Image.Load(Configuration.Default, this.Stream, out IImageFormat _))
                {
                }
            });
        }
    }
}

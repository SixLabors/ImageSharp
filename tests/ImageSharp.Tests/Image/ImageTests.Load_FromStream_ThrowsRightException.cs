// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromStream_Throws : IDisposable
        {
            private static readonly byte[] Data = new byte[] { 0x01 };

            private MemoryStream Stream { get; } = new MemoryStream(Data);

            [Fact]
            public void Image_Load_Throws_UnknownImageFormatException()
            {
                Assert.Throws<UnknownImageFormatException>(() =>
                {
                    using (Image.Load(Configuration.Default, this.Stream, out IImageFormat format))
                    {
                    }
                });
            }

            [Fact]
            public void Image_Load_T_Throws_UnknownImageFormatException()
            {
                Assert.Throws<UnknownImageFormatException>(() =>
                {
                    using (Image.Load<Rgba32>(Configuration.Default, this.Stream, out IImageFormat format))
                    {
                    }
                });
            }

            public void Dispose()
            {
                this.Stream?.Dispose();
            }
        }
    }
}

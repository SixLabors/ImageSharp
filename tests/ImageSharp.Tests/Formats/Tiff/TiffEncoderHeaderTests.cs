// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Category", "Tiff")]
    public class TiffEncoderHeaderTests
    {
        private static readonly MemoryAllocator MemoryAllocator = new ArrayPoolMemoryAllocator();
        private static readonly Configuration Configuration = Configuration.Default;

        [Fact]
        public void WriteHeader_WritesValidHeader()
        {
            var stream = new MemoryStream();
            var encoder = new TiffEncoderCore(null, MemoryAllocator);

            using (var writer = new TiffWriter(stream, MemoryAllocator, Configuration))
            {
                long firstIfdMarker = encoder.WriteHeader(writer);
            }

            Assert.Equal(new byte[] { 0x49, 0x49, 42, 0, 0x00, 0x00, 0x00, 0x00 }, stream.ToArray());
        }

        [Fact]
        public void WriteHeader_ReturnsFirstIfdMarker()
        {
            var stream = new MemoryStream();
            var encoder = new TiffEncoderCore(null, MemoryAllocator);

            using (var writer = new TiffWriter(stream, MemoryAllocator, Configuration))
            {
                long firstIfdMarker = encoder.WriteHeader(writer);
                Assert.Equal(4, firstIfdMarker);
            }
        }
    }
}

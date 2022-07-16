// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.Writers;
using SixLabors.ImageSharp.Memory;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    [Trait("Format", "Tiff")]
    public class TiffEncoderHeaderTests
    {
        private static readonly MemoryAllocator MemoryAllocator = MemoryAllocator.Create();
        private static readonly Configuration Configuration = Configuration.Default;
        private static readonly ITiffEncoderOptions Options = new TiffEncoder();

        [Fact]
        public void WriteHeader_WritesValidHeader()
        {
            using var stream = new MemoryStream();
            var encoder = new TiffEncoderCore(Options, MemoryAllocator);

            using (var writer = new TiffStreamWriter(stream))
            {
                long firstIfdMarker = encoder.WriteHeader(writer);
            }

            Assert.Equal(new byte[] { 0x49, 0x49, 42, 0, 0x00, 0x00, 0x00, 0x00 }, stream.ToArray());
        }

        [Fact]
        public void WriteHeader_ReturnsFirstIfdMarker()
        {
            using var stream = new MemoryStream();
            var encoder = new TiffEncoderCore(Options, MemoryAllocator);

            using (var writer = new TiffStreamWriter(stream))
            {
                long firstIfdMarker = encoder.WriteHeader(writer);
                Assert.Equal(4, firstIfdMarker);
            }
        }
    }
}

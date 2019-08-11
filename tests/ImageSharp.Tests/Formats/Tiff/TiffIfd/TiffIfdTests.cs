// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using Xunit;

    using ImageSharp.Formats.Tiff;

    [Trait("Category", "Tiff")]
    public class TiffIfdTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var entries = new TiffIfdEntry[10];
            var ifd = new TiffIfd(entries, 1234u);

            Assert.Equal(entries, ifd.Entries);
            Assert.Equal(1234u, ifd.NextIfdOffset);
        }

        [Fact]
        public void GetIfdEntry_ReturnsIfdIfExists()
        {
            var entries = new[]
                    {
                        new TiffIfdEntry(10, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(20, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(30, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(40, TiffType.Short, 20, new byte[4])
                    };
            var ifd = new TiffIfd(entries, 1234u);

            TiffIfdEntry? entry = ifd.GetIfdEntry(30);

            Assert.True(entry.HasValue);
            Assert.Equal(30, entry.Value.Tag);
        }

        [Fact]
        public void GetIfdEntry_ReturnsNullOtherwise()
        {
            var entries = new[]
                    {
                        new TiffIfdEntry(10, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(20, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(30, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(40, TiffType.Short, 20, new byte[4])
                    };
            var ifd = new TiffIfd(entries, 1234u);

            TiffIfdEntry? entry = ifd.GetIfdEntry(25);

            Assert.False(entry.HasValue);
        }

        [Fact]
        public void TryGetIfdEntry_ReturnsIfdIfExists()
        {
            var entries = new[]
                    {
                        new TiffIfdEntry(10, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(20, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(30, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(40, TiffType.Short, 20, new byte[4])
                    };
            var ifd = new TiffIfd(entries, 1234u);

            bool success = ifd.TryGetIfdEntry(30, out var entry);

            Assert.True(success);
            Assert.Equal(30, entry.Tag);
        }

        [Fact]
        public void TryGetIfdEntry_ReturnsFalseOtherwise()
        {
            var entries = new[]
                    {
                        new TiffIfdEntry(10, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(20, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(30, TiffType.Short, 20, new byte[4]),
                        new TiffIfdEntry(40, TiffType.Short, 20, new byte[4])
                    };
            var ifd = new TiffIfd(entries, 1234u);

            bool success = ifd.TryGetIfdEntry(25, out var entry);

            Assert.False(success);
            Assert.Equal(0, entry.Tag);
        }
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;

    using Xunit;

    public class JFifMarkerTests
    {
        // Taken from actual test image
        private readonly byte[] bytes = { 0x4A, 0x46, 0x49, 0x46, 0x0, 0x1, 0x1, 0x1, 0x0, 0x60, 0x0, 0x60, 0x0 };

        // Altered components
        private readonly byte[] bytes2 = { 0x4A, 0x46, 0x49, 0x46, 0x0, 0x1, 0x1, 0x1, 0x0, 0x48, 0x0, 0x48, 0x0 };

        // Incorrect density values. Zero is invalid.
        private readonly byte[] bytes3 = { 0x4A, 0x46, 0x49, 0x46, 0x0, 0x1, 0x1, 0x1, 0x0, 0x0, 0x0, 0x0, 0x0 };

        [Fact]
        public void MarkerLengthIsCorrect()
        {
            Assert.Equal(13, JFifMarker.Length);
        }

        [Fact]
        public void MarkerReturnsCorrectParsedValue()
        {
            bool isJFif = JFifMarker.TryParse(this.bytes, out var marker);

            Assert.True(isJFif);
            Assert.Equal(1, marker.MajorVersion);
            Assert.Equal(1, marker.MinorVersion);
            Assert.Equal(1, marker.DensityUnits);
            Assert.Equal(96, marker.XDensity);
            Assert.Equal(96, marker.YDensity);
        }

        [Fact]
        public void MarkerIgnoresIncorrectValue()
        {
            bool isJFif = JFifMarker.TryParse(new byte[] { 0, 0, 0, 0 }, out var marker);

            Assert.False(isJFif);
            Assert.Equal(default(JFifMarker), marker);
        }

        [Fact]
        public void MarkerIgnoresCorrectHeaderButInvalidDensities()
        {
            bool isJFif = JFifMarker.TryParse(this.bytes3, out var marker);

            Assert.False(isJFif);
            Assert.Equal(default(JFifMarker), marker);
        }

        [Fact]
        public void MarkerEqualityIsCorrect()
        {
            JFifMarker.TryParse(this.bytes, out var marker);
            JFifMarker.TryParse(this.bytes, out var marker2);

            Assert.True(marker.Equals(marker2));
        }

        [Fact]
        public void MarkerInEqualityIsCorrect()
        {
            JFifMarker.TryParse(this.bytes, out var marker);
            JFifMarker.TryParse(this.bytes2, out var marker2);

            Assert.False(marker.Equals(marker2));
        }

        [Fact]
        public void MarkerHashCodeIsReplicable()
        {
            JFifMarker.TryParse(this.bytes, out var marker);
            JFifMarker.TryParse(this.bytes, out var marker2);

            Assert.True(marker.GetHashCode().Equals(marker2.GetHashCode()));
        }

        [Fact]
        public void MarkerHashCodeIsUnique()
        {
            JFifMarker.TryParse(this.bytes, out var marker);
            JFifMarker.TryParse(this.bytes2, out var marker2);

            Assert.False(marker.GetHashCode().Equals(marker2.GetHashCode()));
        }
    }
}
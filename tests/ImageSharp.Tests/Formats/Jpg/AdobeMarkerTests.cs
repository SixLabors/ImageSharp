// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class AdobeMarkerTests
    {
        // Taken from actual test image
        private readonly byte[] bytes = { 0x41, 0x64, 0x6F, 0x62, 0x65, 0x0, 0x64, 0x0, 0x0, 0x0, 0x0, 0x2 };

        // Altered components
        private readonly byte[] bytes2 = { 0x41, 0x64, 0x6F, 0x62, 0x65, 0x0, 0x64, 0x0, 0x0, 0x1, 0x1, 0x1 };

        [Fact]
        public void MarkerLengthIsCorrect()
        {
            Assert.Equal(12, AdobeMarker.Length);
        }

        [Fact]
        public void MarkerReturnsCorrectParsedValue()
        {
            bool isAdobe = AdobeMarker.TryParse(this.bytes, out AdobeMarker marker);

            Assert.True(isAdobe);
            Assert.Equal(100, marker.DCTEncodeVersion);
            Assert.Equal(0, marker.APP14Flags0);
            Assert.Equal(0, marker.APP14Flags1);
            Assert.Equal(JpegConstants.Adobe.ColorTransformYcck, marker.ColorTransform);
        }

        [Fact]
        public void MarkerIgnoresIncorrectValue()
        {
            bool isAdobe = AdobeMarker.TryParse(new byte[] { 0, 0, 0, 0 }, out AdobeMarker marker);

            Assert.False(isAdobe);
            Assert.Equal(default, marker);
        }

        [Fact]
        public void MarkerEqualityIsCorrect()
        {
            AdobeMarker.TryParse(this.bytes, out AdobeMarker marker);
            AdobeMarker.TryParse(this.bytes, out AdobeMarker marker2);

            Assert.True(marker.Equals(marker2));
        }

        [Fact]
        public void MarkerInEqualityIsCorrect()
        {
            AdobeMarker.TryParse(this.bytes, out AdobeMarker marker);
            AdobeMarker.TryParse(this.bytes2, out AdobeMarker marker2);

            Assert.False(marker.Equals(marker2));
        }

        [Fact]
        public void MarkerHashCodeIsReplicable()
        {
            AdobeMarker.TryParse(this.bytes, out AdobeMarker marker);
            AdobeMarker.TryParse(this.bytes, out AdobeMarker marker2);

            Assert.True(marker.GetHashCode().Equals(marker2.GetHashCode()));
        }

        [Fact]
        public void MarkerHashCodeIsUnique()
        {
            AdobeMarker.TryParse(this.bytes, out AdobeMarker marker);
            AdobeMarker.TryParse(this.bytes2, out AdobeMarker marker2);

            Assert.False(marker.GetHashCode().Equals(marker2.GetHashCode()));
        }
    }
}
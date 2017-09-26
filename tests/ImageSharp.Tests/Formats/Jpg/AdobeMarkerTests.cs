// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;

    using Xunit;

    public class AdobeMarkerTests
    {
        // Taken from actual test image
        private readonly byte[] bytes = { 0x41, 0x64, 0x6F, 0x62, 0x65, 0x0, 0x64, 0x0, 0x0, 0x0, 0x0, 0x2 };

        [Fact]
        public void MarkerLengthIsCorrect()
        {
            Assert.Equal(12, AdobeMarker.Length);
        }

        [Fact]
        public void MarkerReturnsCorrectParsedValue()
        {
            bool isAdobe = AdobeMarker.TryParse(this.bytes, out var marker);

            Assert.True(isAdobe);
            Assert.Equal(100, marker.DCTEncodeVersion);
            Assert.Equal(0, marker.APP14Flags0);
            Assert.Equal(0, marker.APP14Flags1);
            Assert.Equal(OrigJpegConstants.Adobe.ColorTransformYcck, marker.ColorTransform);
        }

        [Fact]
        public void MarkerIgnoresIncorrectValue()
        {
            bool isAdobe = AdobeMarker.TryParse(new byte[] { 0, 0, 0, 0 }, out var marker);

            Assert.False(isAdobe);
            Assert.Equal(default(AdobeMarker), marker);
        }
    }
}
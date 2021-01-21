// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Text;

using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public class ProfileResolverTests
    {
        private static readonly byte[] JFifMarker = Encoding.ASCII.GetBytes("JFIF\0");
        private static readonly byte[] ExifMarker = Encoding.ASCII.GetBytes("Exif\0\0");
        private static readonly byte[] IccMarker = Encoding.ASCII.GetBytes("ICC_PROFILE\0");
        private static readonly byte[] AdobeMarker = Encoding.ASCII.GetBytes("Adobe");

        [Fact]
        public void ProfileResolverHasCorrectJFifMarker()
        {
            Assert.Equal(JFifMarker, ProfileResolver.JFifMarker.ToArray());
        }

        [Fact]
        public void ProfileResolverHasCorrectExifMarker()
        {
            Assert.Equal(ExifMarker, ProfileResolver.ExifMarker.ToArray());
        }

        [Fact]
        public void ProfileResolverHasCorrectIccMarker()
        {
            Assert.Equal(IccMarker, ProfileResolver.IccMarker.ToArray());
        }

        [Fact]
        public void ProfileResolverHasCorrectAdobeMarker()
        {
            Assert.Equal(AdobeMarker, ProfileResolver.AdobeMarker.ToArray());
        }

        [Fact]
        public void ProfileResolverCanResolveJFifMarker()
        {
            Assert.True(ProfileResolver.IsProfile(JFifMarker, ProfileResolver.JFifMarker));
        }

        [Fact]
        public void ProfileResolverCanResolveExifMarker()
        {
            Assert.True(ProfileResolver.IsProfile(ExifMarker, ProfileResolver.ExifMarker));
        }

        [Fact]
        public void ProfileResolverCanResolveIccMarker()
        {
            Assert.True(ProfileResolver.IsProfile(IccMarker, ProfileResolver.IccMarker));
        }

        [Fact]
        public void ProfileResolverCanResolveAdobeMarker()
        {
            Assert.True(ProfileResolver.IsProfile(AdobeMarker, ProfileResolver.AdobeMarker));
        }

        [Fact]
        public void ProfileResolverCorrectlyReportsNonMarker()
        {
            Assert.False(ProfileResolver.IsProfile(IccMarker, ProfileResolver.AdobeMarker));
        }

        [Fact]
        public void ProfileResolverCanHandleIncorrectLength()
        {
            Assert.False(ProfileResolver.IsProfile(AdobeMarker, ProfileResolver.IccMarker));
        }
    }
}

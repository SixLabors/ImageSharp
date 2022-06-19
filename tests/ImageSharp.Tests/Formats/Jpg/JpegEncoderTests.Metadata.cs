// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public partial class JpegEncoderTests
    {
        [Fact]
        public void Encode_PreservesIptcProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.IptcProfile = new IptcProfile();
            input.Metadata.IptcProfile.SetValue(IptcTag.Byline, "unit_test");

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            IptcProfile actual = output.Metadata.IptcProfile;
            Assert.NotNull(actual);
            IEnumerable<IptcValue> values = input.Metadata.IptcProfile.Values;
            Assert.Equal(values, actual.Values);
        }

        [Fact]
        public void Encode_PreservesExifProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.ExifProfile = new ExifProfile();
            input.Metadata.ExifProfile.SetValue(ExifTag.Software, "unit_test");

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            ExifProfile actual = output.Metadata.ExifProfile;
            Assert.NotNull(actual);
            IReadOnlyList<IExifValue> values = input.Metadata.ExifProfile.Values;
            Assert.Equal(values, actual.Values);
        }

        [Fact]
        public void Encode_PreservesIccProfile()
        {
            // arrange
            using var input = new Image<Rgba32>(1, 1);
            input.Metadata.IccProfile = new IccProfile(IccTestDataProfiles.Profile_Random_Array);

            // act
            using var memStream = new MemoryStream();
            input.Save(memStream, JpegEncoder);

            // assert
            memStream.Position = 0;
            using var output = Image.Load<Rgba32>(memStream);
            IccProfile actual = output.Metadata.IccProfile;
            Assert.NotNull(actual);
            IccProfile values = input.Metadata.IccProfile;
            Assert.Equal(values.Entries, actual.Entries);
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Issues.ValidExifArgumentNullExceptionOnEncode, PixelTypes.Rgba32)]
        public void Encode_WithValidExifProfile_DoesNotThrowException<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Exception ex = Record.Exception(() =>
            {
                var encoder = new JpegEncoder();
                var stream = new MemoryStream();

                using Image<TPixel> image = provider.GetImage(JpegDecoder);
                image.Save(stream, encoder);
            });

            Assert.Null(ex);
        }
    }
}

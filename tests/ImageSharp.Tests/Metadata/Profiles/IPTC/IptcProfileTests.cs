// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.IPTC
{
    public class IptcProfileTests
    {
        private static JpegDecoder JpegDecoder => new JpegDecoder() { IgnoreMetadata = false };

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Iptc, PixelTypes.Rgba32)]
        public void ReadIptcProfile<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(JpegDecoder))
            {
                Assert.NotNull(image.Metadata.IptcProfile);
                IEnumerable<IptcValue> iptcValues = image.Metadata.IptcProfile.Values;
                ContainsIptcValue(iptcValues, IptcTag.Caption, "description");
                ContainsIptcValue(iptcValues, IptcTag.CaptionWriter, "description writer");
                ContainsIptcValue(iptcValues, IptcTag.Headline, "headline");
                ContainsIptcValue(iptcValues, IptcTag.SpecialInstructions, "special instructions");
                ContainsIptcValue(iptcValues, IptcTag.Byline, "author");
                ContainsIptcValue(iptcValues, IptcTag.BylineTitle, "author title");
                ContainsIptcValue(iptcValues, IptcTag.Credit, "credits");
                ContainsIptcValue(iptcValues, IptcTag.Source, "source");
                ContainsIptcValue(iptcValues, IptcTag.Title, "title");
                ContainsIptcValue(iptcValues, IptcTag.CreatedDate, "20200414");
                ContainsIptcValue(iptcValues, IptcTag.City, "city");
                ContainsIptcValue(iptcValues, IptcTag.SubLocation, "sublocation");
                ContainsIptcValue(iptcValues, IptcTag.ProvinceState, "province-state");
                ContainsIptcValue(iptcValues, IptcTag.Country, "country");
                ContainsIptcValue(iptcValues, IptcTag.Category, "category");
                ContainsIptcValue(iptcValues, IptcTag.Priority, "1");
                ContainsIptcValue(iptcValues, IptcTag.Keyword, "keywords");
                ContainsIptcValue(iptcValues, IptcTag.CopyrightNotice, "copyright");
            }
        }

        [Fact]
        public void IptcProfile_CloneIsDeep()
        {
            // arrange
            var profile = new IptcProfile();
            var captionWriter = "unittest";
            var caption = "test";
            profile.SetValue(IptcTag.CaptionWriter, captionWriter);
            profile.SetValue(IptcTag.Caption, caption);

            // act
            IptcProfile clone = profile.DeepClone();
            clone.SetValue(IptcTag.Caption, "changed");

            // assert
            Assert.Equal(2, clone.Values.Count());
            ContainsIptcValue(clone.Values, IptcTag.CaptionWriter, captionWriter);
            ContainsIptcValue(clone.Values, IptcTag.Caption, "changed");
            ContainsIptcValue(profile.Values, IptcTag.Caption, caption);
        }

        [Fact]
        public void IptcValue_CloneIsDeep()
        {
            // arrange
            var iptcValue = new IptcValue(IptcTag.Caption, System.Text.Encoding.UTF8, "test");

            // act
            IptcValue clone = iptcValue.DeepClone();
            clone.Value = "changed";

            // assert
            Assert.NotEqual(iptcValue.Value, clone.Value);
        }

        private static void ContainsIptcValue(IEnumerable<IptcValue> values, IptcTag tag, string value)
        {
            Assert.True(values.Any(val => val.Tag == tag), $"Missing iptc tag {tag}");
            Assert.True(values.Contains(new IptcValue(tag, System.Text.Encoding.UTF8.GetBytes(value))), $"expected iptc value '{value}' was not found for tag '{tag}'");
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.ObjectModel;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class JpegMetadataTests
{
    [Fact]
    public void CloneIsDeep()
    {
        var meta = new JpegMetadata { ColorType = JpegColorType.Luminance };
        var clone = (JpegMetadata)meta.DeepClone();

        clone.ColorType = JpegColorType.YCbCrRatio420;

        Assert.False(meta.ColorType.Equals(clone.ColorType));
    }

    [Fact]
    public void Quality_DefaultQuality()
    {
        var meta = new JpegMetadata();

        Assert.Equal(meta.Quality, ImageSharp.Formats.Jpeg.Components.Quantization.DefaultQualityFactor);
    }

    [Fact]
    public void Quality_LuminanceOnlyQuality()
    {
        int quality = 50;

        var meta = new JpegMetadata { LuminanceQuality = quality };

        Assert.Equal(meta.Quality, quality);
    }

    [Fact]
    public void Quality_BothComponentsQuality()
    {
        int quality = 50;

        var meta = new JpegMetadata { LuminanceQuality = quality, ChrominanceQuality = quality };

        Assert.Equal(meta.Quality, quality);
    }

    [Fact]
    public void Quality_ReturnsMaxQuality()
    {
        int qualityLuma = 50;
        int qualityChroma = 30;

        var meta = new JpegMetadata { LuminanceQuality = qualityLuma, ChrominanceQuality = qualityChroma };

        Assert.Equal(meta.Quality, qualityLuma);
    }

    [Fact]
    public void Comment_EmptyComment()
    {
        var meta = new JpegMetadata();

        Assert.True(Array.Empty<JpegComData>().SequenceEqual(meta.Comments));
    }

    [Fact]
    public void Comment_OnlyComment()
    {
        string comment = "test comment";
        var expectedCollection = new Collection<string> { comment };

        var meta = new JpegMetadata();
        meta.Comments.Add(JpegComData.FromString(comment));

        Assert.Equal(1, meta.Comments.Count);
        Assert.True(expectedCollection.FirstOrDefault() == meta.Comments.FirstOrDefault().ToString());
    }
}

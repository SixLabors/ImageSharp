// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
public class HeifFrameMetadataTests
{
    [Fact]
    public void FormatCreatesFrameMetadata()
    {
        HeifFrameMetadata metadata = HeifFormat.Instance.CreateDefaultFormatFrameMetadata();

        Assert.Equal(new Rational(0), metadata.FrameDelay);
    }

    [Fact]
    public void DeepCloneCopiesFrameDelay()
    {
        HeifFrameMetadata metadata = new() { FrameDelay = new Rational(1, 4) };

        HeifFrameMetadata clone = metadata.DeepClone();
        clone.FrameDelay = new Rational(1, 2);

        Assert.Equal(new Rational(1, 4), metadata.FrameDelay);
        Assert.Equal(new Rational(1, 2), clone.FrameDelay);
    }

    [Fact]
    public void DurationRoundTripsFormatConnectingMetadata()
    {
        FormatConnectingFrameMetadata connectingMetadata = new() { Duration = TimeSpan.FromMilliseconds(250) };

        HeifFrameMetadata metadata = HeifFrameMetadata.FromFormatConnectingFrameMetadata(connectingMetadata);
        FormatConnectingFrameMetadata result = metadata.ToFormatConnectingFrameMetadata();

        Assert.Equal(0.25, metadata.FrameDelay.ToDouble(), 10);
        Assert.Equal(TimeSpan.FromMilliseconds(250), result.Duration);
        Assert.Equal(FrameBlendMode.Source, result.BlendMode);
        Assert.Equal(FrameDisposalMode.DoNotDispose, result.DisposalMode);
    }

    [Fact]
    public void ZeroDenominatorConvertsToUnspecifiedDuration()
    {
        HeifFrameMetadata metadata = new() { FrameDelay = new Rational(1, 0) };

        FormatConnectingFrameMetadata result = metadata.ToFormatConnectingFrameMetadata();

        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void ImageFrameMetadataExtensionsReturnAndCloneHeifMetadata()
    {
        using Image<Rgba32> image = new(1, 1);
        HeifFrameMetadata metadata = image.Frames.RootFrame.Metadata.GetHeifMetadata();
        metadata.FrameDelay = new Rational(1, 5);

        HeifFrameMetadata clone = image.Frames.RootFrame.Metadata.CloneHeifMetadata();

        Assert.NotSame(metadata, clone);
        Assert.Equal(metadata.FrameDelay, clone.FrameDelay);
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Heif;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

[Trait("Format", "Heif")]
public class HeifFrameMetadataTests
{
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

        // A zero denominator has no defined duration.
        HeifFrameMetadata unspecified = new() { FrameDelay = new Rational(1, 0) };
        Assert.Equal(TimeSpan.Zero, unspecified.ToFormatConnectingFrameMetadata().Duration);
    }
}

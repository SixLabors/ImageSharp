// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

/// <summary>
/// Verifies parsing and interpretation of HEIF image-item property payloads.
/// </summary>
[Trait("Format", "Heif")]
public class HeifPropertyParserTests
{
    /// <summary>
    /// Verifies the AV1 operating-point and spatial-layer selector ranges, including the progressive final-layer
    /// selector, and rejects values the sequence header or OBU extension cannot address.
    /// </summary>
    [Fact]
    public void ParseAv1SelectorsAcceptOnlyAddressableValues()
    {
        Assert.Equal((byte)0, HeifPropertyParser.ParseAv1OperatingPointSelector([0]).Index);
        Assert.Equal((byte)31, HeifPropertyParser.ParseAv1OperatingPointSelector([31]).Index);
        Assert.Throws<InvalidImageContentException>(() => HeifPropertyParser.ParseAv1OperatingPointSelector([32]));

        Assert.Equal((ushort)0, HeifPropertyParser.ParseAv1LayerSelector([0, 0]).LayerId);
        Assert.Equal((ushort)3, HeifPropertyParser.ParseAv1LayerSelector([0, 3]).LayerId);
        Assert.Equal(Av1LayerSelector.AllLayers, HeifPropertyParser.ParseAv1LayerSelector([255, 255]).LayerId);
        Assert.Throws<InvalidImageContentException>(() => HeifPropertyParser.ParseAv1LayerSelector([0, 4]));
    }

    /// <summary>
    /// Verifies the compact 16-bit and large 32-bit layered-image index layouts and rejects reserved flag bits.
    /// </summary>
    [Fact]
    public void ParseAv1LayeredImageIndexReadsBothFieldSizes()
    {
        Av1LayeredImageIndex small = HeifPropertyParser.ParseAv1LayeredImageIndex([0, 0, 55, 0, 17, 1, 2]);
        Assert.Equal(55U, small.FirstLayerSize);
        Assert.Equal(17U, small.SecondLayerSize);
        Assert.Equal(258U, small.ThirdLayerSize);

        Av1LayeredImageIndex large = HeifPropertyParser.ParseAv1LayeredImageIndex(
            [1, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0]);
        Assert.Equal(65536U, large.FirstLayerSize);
        Assert.Equal(131072U, large.SecondLayerSize);
        Assert.Equal(196608U, large.ThirdLayerSize);

        Assert.Throws<InvalidImageContentException>(() => HeifPropertyParser.ParseAv1LayeredImageIndex([2, 0, 1, 0, 0, 0, 0]));
    }

    /// <summary>
    /// Verifies cumulative layer selection through the two-layer payload used by the libavif progressive fixture,
    /// and rejects absent layers and explicit boundaries that leave no bytes for the implicit final layer.
    /// </summary>
    [Fact]
    public void GetPayloadLengthSelectsCumulativeLayerBytes()
    {
        Av1LayeredImageIndex index = new(55, 0, 0);

        Assert.Equal(55, index.GetPayloadLength(72, new Av1LayerSelector(0)));
        Assert.Equal(72, index.GetPayloadLength(72, new Av1LayerSelector(1)));
        Assert.Equal(72, index.GetPayloadLength(72, new Av1LayerSelector(Av1LayerSelector.AllLayers)));
        Assert.Throws<InvalidImageContentException>(() => index.GetPayloadLength(72, new Av1LayerSelector(2)));
        Assert.Throws<InvalidImageContentException>(() => new Av1LayeredImageIndex(72, 0, 0).GetPayloadLength(72, null));
    }
}

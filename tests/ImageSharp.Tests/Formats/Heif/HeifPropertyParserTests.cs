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
    /// Verifies that every AV1 operating-point index representable by a sequence header is accepted.
    /// </summary>
    /// <param name="index">The zero-based operating-point index.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void ParseAv1OperatingPointSelectorAcceptsSequenceHeaderRange(byte index)
    {
        Av1OperatingPointSelector selector = HeifPropertyParser.ParseAv1OperatingPointSelector([index]);

        Assert.Equal(index, selector.Index);
    }

    /// <summary>
    /// Verifies that an AV1 operating-point index outside the sequence-header range is rejected.
    /// </summary>
    [Fact]
    public void ParseAv1OperatingPointSelectorRejectsOutOfRangeIndex()
        => Assert.Throws<InvalidImageContentException>(() => HeifPropertyParser.ParseAv1OperatingPointSelector([32]));

    /// <summary>
    /// Verifies the four AV1 spatial-layer identifiers and the progressive final-layer selector.
    /// </summary>
    /// <param name="highByte">The most-significant selector byte.</param>
    /// <param name="lowByte">The least-significant selector byte.</param>
    /// <param name="expected">The parsed layer identifier.</param>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 3, 3)]
    [InlineData(255, 255, Av1LayerSelector.AllLayers)]
    public void ParseAv1LayerSelectorAcceptsDefinedValues(byte highByte, byte lowByte, ushort expected)
    {
        Av1LayerSelector selector = HeifPropertyParser.ParseAv1LayerSelector([highByte, lowByte]);

        Assert.Equal(expected, selector.LayerId);
    }

    /// <summary>
    /// Verifies that an AV1 spatial-layer identifier wider than the OBU extension field is rejected.
    /// </summary>
    [Fact]
    public void ParseAv1LayerSelectorRejectsOutOfRangeLayer()
        => Assert.Throws<InvalidImageContentException>(() => HeifPropertyParser.ParseAv1LayerSelector([0, 4]));

    /// <summary>
    /// Verifies the compact 16-bit representation of the three explicit layered-image boundaries.
    /// </summary>
    [Fact]
    public void ParseAv1LayeredImageIndexReadsSmallSizes()
    {
        Av1LayeredImageIndex index = HeifPropertyParser.ParseAv1LayeredImageIndex([0, 0, 55, 0, 17, 1, 2]);

        Assert.Equal(55U, index.FirstLayerSize);
        Assert.Equal(17U, index.SecondLayerSize);
        Assert.Equal(258U, index.ThirdLayerSize);
    }

    /// <summary>
    /// Verifies the 32-bit representation used when a layered-image boundary exceeds 16 bits.
    /// </summary>
    [Fact]
    public void ParseAv1LayeredImageIndexReadsLargeSizes()
    {
        Av1LayeredImageIndex index = HeifPropertyParser.ParseAv1LayeredImageIndex(
            [1, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0]);

        Assert.Equal(65536U, index.FirstLayerSize);
        Assert.Equal(131072U, index.SecondLayerSize);
        Assert.Equal(196608U, index.ThirdLayerSize);
    }

    /// <summary>
    /// Verifies that reserved layered-image flag bits cannot alter the payload interpretation.
    /// </summary>
    [Fact]
    public void ParseAv1LayeredImageIndexRejectsReservedBits()
        => Assert.Throws<InvalidImageContentException>(() => HeifPropertyParser.ParseAv1LayeredImageIndex([2, 0, 1, 0, 0, 0, 0]));

    /// <summary>
    /// Verifies cumulative selection through the two-layer payload used by the libavif progressive fixture.
    /// </summary>
    /// <param name="layerId">The selected spatial layer.</param>
    /// <param name="expectedLength">The cumulative payload length through that layer.</param>
    [Theory]
    [InlineData(0, 55)]
    [InlineData(1, 72)]
    [InlineData(Av1LayerSelector.AllLayers, 72)]
    public void GetPayloadLengthSelectsCumulativeLayerBytes(ushort layerId, int expectedLength)
    {
        Av1LayeredImageIndex index = new(55, 0, 0);
        Av1LayerSelector selector = new(layerId);

        Assert.Equal(expectedLength, index.GetPayloadLength(72, selector));
    }

    /// <summary>
    /// Verifies that a selector cannot address a layer absent from the indexed item payload.
    /// </summary>
    [Fact]
    public void GetPayloadLengthRejectsAbsentLayer()
    {
        Av1LayeredImageIndex index = new(55, 0, 0);
        Av1LayerSelector selector = new(2);

        Assert.Throws<InvalidImageContentException>(() => index.GetPayloadLength(72, selector));
    }

    /// <summary>
    /// Verifies that every explicit layer boundary leaves bytes for the following implicit layer.
    /// </summary>
    [Fact]
    public void GetPayloadLengthRejectsBoundaryAtItemEnd()
    {
        Av1LayeredImageIndex index = new(72, 0, 0);

        Assert.Throws<InvalidImageContentException>(() => index.GetPayloadLength(72, null));
    }
}

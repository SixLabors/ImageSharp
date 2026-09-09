// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the frame-level derivation of AV1 skip-mode reference pairs.
/// </summary>
[Trait("Format", "Avif")]
public class ObuSkipModeParametersTests
{
    /// <summary>
    /// The public theory-data representation of <see cref="ObuFrameType.KeyFrame"/>.
    /// </summary>
    private const int KeyFrameValue = (int)ObuFrameType.KeyFrame;

    /// <summary>
    /// The public theory-data representation of <see cref="ObuFrameType.InterFrame"/>.
    /// </summary>
    private const int InterFrameValue = (int)ObuFrameType.InterFrame;

    /// <summary>
    /// The public theory-data representation of <see cref="ObuReferenceMode.SingleReference"/>.
    /// </summary>
    private const int SingleReferenceValue = (int)ObuReferenceMode.SingleReference;

    /// <summary>
    /// The public theory-data representation of <see cref="ObuReferenceMode.ReferenceModeSelect"/>.
    /// </summary>
    private const int ReferenceModeSelectValue = (int)ObuReferenceMode.ReferenceModeSelect;

    /// <summary>
    /// Verifies signed order-hint distances across the modulo-domain boundary.
    /// </summary>
    [Fact]
    public void GetRelativeDistanceWrapsWithinConfiguredDomain()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();

        Assert.Equal(-2, orderHintInfo.GetRelativeDistance(15, 1));
        Assert.Equal(2, orderHintInfo.GetRelativeDistance(1, 15));
    }

    /// <summary>
    /// Verifies that disabled order hints have no temporal ordering.
    /// </summary>
    [Fact]
    public void GetRelativeDistanceReturnsZeroWhenOrderHintsAreDisabled()
    {
        ObuOrderHintInfo orderHintInfo = new();

        Assert.Equal(0, orderHintInfo.GetRelativeDistance(15, 1));
    }

    /// <summary>
    /// Verifies that skip mode selects the nearest past and future canonical reference roles.
    /// </summary>
    [Fact]
    public void DeriveSelectsNearestForwardAndBackwardReferences()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(8, [7, 3, 6, 2, 10, 12, 15]);

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.True(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.Last, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.Backward, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies that the derived pair identifies canonical roles rather than their physical reference-map slots.
    /// </summary>
    [Fact]
    public void DeriveOrdersCanonicalRolesIndependentlyOfMappedSlots()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(8, [7, 3, 6, 2, 10, 12, 15]);
        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        Span<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();

        // Several canonical roles deliberately share physical slot seven. The first matching role remains LAST, while
        // the future BWDREF role maps to slot four; neither physical slot number becomes part of the derived pair.
        referenceFrameIndices.Fill(7);
        referenceFrameIndices[(int)Av1ReferenceFrameType.Backward - 1] = 4;
        referenceOrderHints[7] = 7;

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.True(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.Last, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.Backward, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies that a frame with only future references cannot use skip mode.
    /// </summary>
    [Fact]
    public void DeriveDisallowsSkipModeWithoutForwardReference()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(8, [9, 10, 11, 12, 13, 14, 15]);

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.False(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies that a forward-only frame selects the two closest distinct past reference orders.
    /// </summary>
    [Fact]
    public void SelectsTwoForwardReferencesWithoutBackwardReference()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(8, [7, 3, 6, 2, 1, 5, 4]);

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.True(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.Last, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.Last3, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies that modulo wraparound participates in nearest-reference selection.
    /// </summary>
    [Fact]
    public void DeriveSelectsReferencesAcrossOrderHintWraparound()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(1, [12, 15, 11, 10, 2, 5, 7]);

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.True(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.Last2, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.Backward, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies that skip mode remains unavailable without two temporally distinct usable reference orders.
    /// </summary>
    [Fact]
    public void DeriveDisallowsSkipModeWithoutReferencePair()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(8, [7, 8, 8, 8, 8, 8, 8]);

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.False(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies that deriving an ineligible frame clears a reference pair retained by an earlier derivation.
    /// </summary>
    [Fact]
    public void DeriveClearsPreviousReferencePair()
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        ObuFrameHeader frameHeader = CreateInterFrame(8, [7, 3, 6, 2, 10, 12, 15]);
        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        frameHeader.ReferenceMode = ObuReferenceMode.SingleReference;
        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.False(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Verifies the frame modes for which the AV1 syntax forbids skip-mode signaling.
    /// </summary>
    /// <param name="enableOrderHint">Whether the sequence enables order hints.</param>
    /// <param name="frameTypeValue">The numeric coded-frame-type value.</param>
    /// <param name="referenceModeValue">The numeric frame-level reference-mode value.</param>
    [Theory]
    [InlineData(false, InterFrameValue, ReferenceModeSelectValue)]
    [InlineData(true, KeyFrameValue, ReferenceModeSelectValue)]
    [InlineData(true, InterFrameValue, SingleReferenceValue)]
    public void DeriveDisallowsSkipModeForIneligibleFrameSyntax(
        bool enableOrderHint,
        int frameTypeValue,
        int referenceModeValue)
    {
        ObuOrderHintInfo orderHintInfo = CreateOrderHintInfo();
        orderHintInfo.EnableOrderHint = enableOrderHint;
        ObuFrameHeader frameHeader = CreateInterFrame(8, [7, 3, 6, 2, 10, 12, 15]);
        frameHeader.FrameType = (ObuFrameType)frameTypeValue;
        frameHeader.ReferenceMode = (ObuReferenceMode)referenceModeValue;

        frameHeader.SkipModeParameters.Derive(orderHintInfo, frameHeader);

        Assert.False(frameHeader.SkipModeParameters.SkipModeAllowed);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.FirstReferenceFrame);
        Assert.Equal(Av1ReferenceFrameType.None, frameHeader.SkipModeParameters.SecondReferenceFrame);
    }

    /// <summary>
    /// Creates the four-bit modulo order-hint configuration used by the derivation scenarios.
    /// </summary>
    /// <returns>The enabled order-hint configuration.</returns>
    private static ObuOrderHintInfo CreateOrderHintInfo()
        => new()
        {
            EnableOrderHint = true,
            OrderHintBits = 4,
        };

    /// <summary>
    /// Creates an inter frame whose seven canonical roles map directly to slots zero through six.
    /// </summary>
    /// <param name="currentOrderHint">The current frame order hint.</param>
    /// <param name="referenceOrderHints">The order hint selected by each canonical role.</param>
    /// <returns>The initialized inter-frame header.</returns>
    private static ObuFrameHeader CreateInterFrame(uint currentOrderHint, ReadOnlySpan<uint> referenceOrderHints)
    {
        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            OrderHint = currentOrderHint,
            ReferenceMode = ObuReferenceMode.ReferenceModeSelect,
        };

        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        Span<uint> referenceMapOrderHints = frameHeader.GetReferenceOrderHints();

        for (int referenceIndex = 0; referenceIndex < Av1Constants.ReferencesPerFrame; referenceIndex++)
        {
            referenceFrameIndices[referenceIndex] = (uint)referenceIndex;
            referenceMapOrderHints[referenceIndex] = referenceOrderHints[referenceIndex];
        }

        return frameHeader;
    }
}

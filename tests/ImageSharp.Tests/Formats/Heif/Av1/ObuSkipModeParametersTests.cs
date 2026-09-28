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

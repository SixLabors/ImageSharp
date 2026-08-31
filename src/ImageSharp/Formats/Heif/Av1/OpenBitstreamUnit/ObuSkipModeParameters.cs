// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the availability and enabled state of AV1 skip mode for a frame.
/// </summary>
internal class ObuSkipModeParameters
{
    /// <summary>
    /// Gets a value indicating whether the frame is permitted to use skip mode.
    /// </summary>
    public bool SkipModeAllowed { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether skip mode is enabled for the frame.
    /// </summary>
    public bool SkipModeFlag { get; set; }

    /// <summary>
    /// Gets the first canonical inter-reference type selected for skip-mode blocks.
    /// </summary>
    public Av1ReferenceFrameType FirstReferenceFrame { get; private set; } = Av1ReferenceFrameType.None;

    /// <summary>
    /// Gets the second canonical inter-reference type selected for skip-mode blocks.
    /// </summary>
    public Av1ReferenceFrameType SecondReferenceFrame { get; private set; } = Av1ReferenceFrameType.None;

    /// <summary>
    /// Derives skip-mode availability and its reference pair from the current frame's retained-reference mapping.
    /// </summary>
    /// <param name="orderHintInfo">The sequence-level order-hint configuration.</param>
    /// <param name="frameHeader">The current frame header and its seven canonical inter-reference mappings.</param>
    public void Derive(ObuOrderHintInfo orderHintInfo, ObuFrameHeader frameHeader)
    {
        this.SkipModeAllowed = false;
        this.FirstReferenceFrame = Av1ReferenceFrameType.None;
        this.SecondReferenceFrame = Av1ReferenceFrameType.None;

        if (!orderHintInfo.EnableOrderHint || frameHeader.IsIntra || frameHeader.ReferenceMode == ObuReferenceMode.SingleReference)
        {
            return;
        }

        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        ReadOnlySpan<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
        int nearestForwardOrderHint = -1;
        int nearestBackwardOrderHint = int.MaxValue;
        int nearestForwardReferenceIndex = -1;
        int nearestBackwardReferenceIndex = -1;

        // The seven entries are canonical roles, while each value selects one physical reference-map slot. Compare
        // the selected slot's order hint so duplicate roles retain the same deterministic ordering as the reference decoder.
        for (int referenceIndex = 0; referenceIndex < Av1Constants.ReferencesPerFrame; referenceIndex++)
        {
            uint referenceOrderHint = referenceOrderHints[(int)referenceFrameIndices[referenceIndex]];
            int distanceFromCurrent = orderHintInfo.GetRelativeDistance(referenceOrderHint, frameHeader.OrderHint);

            if (distanceFromCurrent < 0 &&
                (nearestForwardOrderHint == -1 || orderHintInfo.GetRelativeDistance(referenceOrderHint, (uint)nearestForwardOrderHint) > 0))
            {
                // Among past frames, the greatest relative order is the closest frame before the current one.
                nearestForwardOrderHint = (int)referenceOrderHint;
                nearestForwardReferenceIndex = referenceIndex;
            }
            else if (distanceFromCurrent > 0 &&
                (nearestBackwardOrderHint == int.MaxValue || orderHintInfo.GetRelativeDistance(referenceOrderHint, (uint)nearestBackwardOrderHint) < 0))
            {
                // Among future frames, the smallest relative order is the closest frame after the current one.
                nearestBackwardOrderHint = (int)referenceOrderHint;
                nearestBackwardReferenceIndex = referenceIndex;
            }
        }

        if (nearestForwardReferenceIndex >= 0 && nearestBackwardReferenceIndex < 0)
        {
            nearestBackwardOrderHint = -1;

            // A forward-only sequence pairs the nearest past frame with the closest distinct frame preceding it.
            for (int referenceIndex = 0; referenceIndex < Av1Constants.ReferencesPerFrame; referenceIndex++)
            {
                uint referenceOrderHint = referenceOrderHints[(int)referenceFrameIndices[referenceIndex]];
                bool precedesNearestForward = orderHintInfo.GetRelativeDistance(referenceOrderHint, (uint)nearestForwardOrderHint) < 0;

                if (precedesNearestForward &&
                    (nearestBackwardOrderHint == -1 || orderHintInfo.GetRelativeDistance(referenceOrderHint, (uint)nearestBackwardOrderHint) > 0))
                {
                    nearestBackwardOrderHint = (int)referenceOrderHint;
                    nearestBackwardReferenceIndex = referenceIndex;
                }
            }
        }

        if (nearestForwardReferenceIndex < 0 || nearestBackwardReferenceIndex < 0)
        {
            return;
        }

        int firstReferenceIndex = Math.Min(nearestForwardReferenceIndex, nearestBackwardReferenceIndex);
        int secondReferenceIndex = Math.Max(nearestForwardReferenceIndex, nearestBackwardReferenceIndex);
        this.FirstReferenceFrame = (Av1ReferenceFrameType)(firstReferenceIndex + (int)Av1ReferenceFrameType.Last);
        this.SecondReferenceFrame = (Av1ReferenceFrameType)(secondReferenceIndex + (int)Av1ReferenceFrameType.Last);
        this.SkipModeAllowed = true;
    }
}

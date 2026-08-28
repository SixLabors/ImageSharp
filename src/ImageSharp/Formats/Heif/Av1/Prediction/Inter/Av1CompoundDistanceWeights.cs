// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Derives AV1 compound weights from retained reference display distances.
/// </summary>
internal static class Av1CompoundDistanceWeights
{
    private const int MaximumFrameDistance = 31;

    /// <summary>
    /// Derives the weights applied to the first and second predictors.
    /// </summary>
    public static void Derive(
        ObuOrderHintInfo orderHintInfo,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType firstReference,
        Av1ReferenceFrameType secondReference,
        out int firstWeight,
        out int secondWeight)
    {
        ReadOnlySpan<int> quantizedDistanceWeights = [2, 3, 2, 5, 2, 7, 1, MaximumFrameDistance];
        ReadOnlySpan<int> quantizedDistanceLookup = [9, 7, 11, 5, 12, 4, 13, 3];
        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        ReadOnlySpan<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
        int firstCanonicalIndex = (int)firstReference - (int)Av1ReferenceFrameType.Last;
        int secondCanonicalIndex = (int)secondReference - (int)Av1ReferenceFrameType.Last;
        uint firstOrderHint = referenceOrderHints[(int)referenceFrameIndices[firstCanonicalIndex]];
        uint secondOrderHint = referenceOrderHints[(int)referenceFrameIndices[secondCanonicalIndex]];
        int secondDistance = Av1Math.Clip3(
            0,
            MaximumFrameDistance,
            Math.Abs(orderHintInfo.GetRelativeDistance(secondOrderHint, frameHeader.OrderHint)));

        int firstDistance = Av1Math.Clip3(
            0,
            MaximumFrameDistance,
            Math.Abs(orderHintInfo.GetRelativeDistance(frameHeader.OrderHint, firstOrderHint)));

        int order = secondDistance <= firstDistance ? 1 : 0;
        int weightClass = 3;
        if (secondDistance != 0 && firstDistance != 0)
        {
            for (weightClass = 0; weightClass < 3; weightClass++)
            {
                int secondScaledDistance = secondDistance * quantizedDistanceWeights[(weightClass * 2) + order];
                int firstScaledDistance = firstDistance * quantizedDistanceWeights[(weightClass * 2) + (1 - order)];
                if ((secondDistance > firstDistance && secondScaledDistance < firstScaledDistance) ||
                    (secondDistance <= firstDistance && secondScaledDistance > firstScaledDistance))
                {
                    break;
                }
            }
        }

        firstWeight = quantizedDistanceLookup[(weightClass * 2) + order];
        secondWeight = quantizedDistanceLookup[(weightClass * 2) + (1 - order)];
    }
}

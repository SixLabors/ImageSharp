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
    /// <param name="orderHintInfo">The order hint settings of the sequence.</param>
    /// <param name="frameHeader">The header of the current frame.</param>
    /// <param name="firstReference">The reference frame of the first predictor.</param>
    /// <param name="secondReference">The reference frame of the second predictor.</param>
    /// <param name="firstWeight">The weight of the first predictor. The two weights add up to 16.</param>
    /// <param name="secondWeight">The weight of the second predictor.</param>
    public static void Derive(
        ObuOrderHintInfo orderHintInfo,
        ObuFrameHeader frameHeader,
        Av1ReferenceFrameType firstReference,
        Av1ReferenceFrameType secondReference,
        out int firstWeight,
        out int secondWeight)
    {
        // Each weight class uses one pair in both tables. The first table holds the distance ratio limit of the class.
        // The second table holds the larger and smaller weight of the class. Each pair adds up to 16.
        ReadOnlySpan<int> quantizedDistanceWeights = [2, 3, 2, 5, 2, 7, 1, MaximumFrameDistance];
        ReadOnlySpan<int> quantizedDistanceLookup = [9, 7, 11, 5, 12, 4, 13, 3];
        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        ReadOnlySpan<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
        int firstCanonicalIndex = (int)firstReference - (int)Av1ReferenceFrameType.Last;
        int secondCanonicalIndex = (int)secondReference - (int)Av1ReferenceFrameType.Last;
        uint firstOrderHint = referenceOrderHints[(int)referenceFrameIndices[firstCanonicalIndex]];
        uint secondOrderHint = referenceOrderHints[(int)referenceFrameIndices[secondCanonicalIndex]];

        // Each distance is the absolute order hint difference between the current frame and the reference, clamped to MaximumFrameDistance.
        int secondDistance = Av1Math.Clip3(
            0,
            MaximumFrameDistance,
            Math.Abs(orderHintInfo.GetRelativeDistance(secondOrderHint, frameHeader.OrderHint)));

        int firstDistance = Av1Math.Clip3(
            0,
            MaximumFrameDistance,
            Math.Abs(orderHintInfo.GetRelativeDistance(frameHeader.OrderHint, firstOrderHint)));

        // When order is 1, the second reference is not farther than the first. The nearer reference gets the larger weight.
        // On a tie, the second reference gets the larger weight.
        int order = secondDistance <= firstDistance ? 1 : 0;
        int weightClass = 3;
        if (secondDistance != 0 && firstDistance != 0)
        {
            // The loop stops at the first class whose ratio limit (3/2, 5/2, 7/2) is more than the far distance divided by the near distance.
            // Later classes give more weight to the nearer reference. Class 3 applies when no class stops the loop or when one distance is zero.
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

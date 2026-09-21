// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Maintains frame-role interpolation probabilities and reference-frame filter usage.
/// </summary>
internal static class Av1InterpolationProbabilities
{
    /// <summary>The number of selectable interpolation filters.</summary>
    public const int FilterCount = Av1SymbolContextHelper.SwitchableInterpolationFilterCount;

    /// <summary>The direction, reference-count, and neighboring-filter contexts.</summary>
    public const int ContextCount = 2 * 2 * (FilterCount + 1);

    /// <summary>The probability entries for one frame update role.</summary>
    public const int FrameLength = ContextCount * FilterCount;

    /// <summary>The probability entries for all frame update roles.</summary>
    public const int ProbabilityLength = (int)Av1FrameUpdateType.Count * FrameLength;

    /// <summary>The filter counts retained for each decoded reference slot.</summary>
    public const int ReferenceUsageLength = Av1Constants.ReferenceFrameCount * FilterCount;

    /// <summary>The combined probabilities, context counts, output counts, and reference usage.</summary>
    public const int StorageLength = ProbabilityLength + FrameLength + FilterCount + ReferenceUsageLength;

    /// <summary>
    /// Updates one frame role from the final selected blocks' context counts.
    /// </summary>
    /// <param name="probabilities">The current frame role's probability rows.</param>
    /// <param name="counts">The completed frame's context counts.</param>
    public static void Update(Span<int> probabilities, ReadOnlySpan<int> counts)
    {
        const int totalProbability = 1536;
        for (int context = 0; context < ContextCount; context++)
        {
            int offset = context * FilterCount;
            int sum = 0;
            for (int filter = 0; filter < FilterCount; filter++)
            {
                sum += counts[offset + filter];
            }

            // Keep the fixed probability total after averaging; integer truncation's remainder
            // belongs to the regular filter, including contexts not encountered in this frame.
            int remainder = totalProbability;
            for (int filter = FilterCount - 1; filter >= 0; filter--)
            {
                int observed = sum != 0 ? totalProbability * counts[offset + filter] / sum : filter == 0 ? totalProbability : 0;
                int probability = (probabilities[offset + filter] + observed) >> 1;
                remainder -= probability;
                probabilities[offset + filter] = filter == 0 ? probability + remainder : probability;
            }
        }
    }

    /// <summary>
    /// Selects the non-dual filters worth searching from preceding reference-frame usage.
    /// </summary>
    /// <param name="usage">The selected filter counts by decoded reference slot.</param>
    /// <param name="referenceSlots">The named references' decoded-slot mapping.</param>
    /// <returns>A three-bit filter mask.</returns>
    public static int GetSearchMask(ReadOnlySpan<int> usage, ReadOnlySpan<uint> referenceSlots)
    {
        InlineArray8<int> totals = default;
        int otherTotal = 0;
        for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
        {
            int offset = (int)referenceSlots[reference - 1] * FilterCount;
            for (int filter = 0; filter < FilterCount; filter++)
            {
                totals[reference] += usage[offset + filter];
            }

            if (reference != (int)Av1ReferenceFrameType.Last)
            {
                otherTotal += totals[reference];
            }
        }

        int mask = (1 << FilterCount) - 1;
        int lastOffset = (int)referenceSlots[(int)Av1ReferenceFrameType.Last - 1] * FilterCount;
        for (int filter = 0; filter < FilterCount; filter++)
        {
            if (totals[(int)Av1ReferenceFrameType.Last] != 0 &&
                usage[lastOffset + filter] * 30 <= totals[(int)Av1ReferenceFrameType.Last])
            {
                int score = 0;
                for (int reference = (int)Av1ReferenceFrameType.Last2; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
                {
                    int weight = reference <= (int)Av1ReferenceFrameType.Golden ? 20 : 10;
                    score += usage[((int)referenceSlots[reference - 1] * FilterCount) + filter] * weight;
                }

                if (score < otherTotal)
                {
                    mask &= ~(1 << filter);
                }
            }
        }

        return mask;
    }
}

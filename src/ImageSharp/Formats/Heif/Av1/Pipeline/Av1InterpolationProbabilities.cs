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

    /// <summary>The combined probabilities, context counts, and output counts.</summary>
    public const int StorageLength = ProbabilityLength + FrameLength + FilterCount;

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
}

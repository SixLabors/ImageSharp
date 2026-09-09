// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

/// <summary>
/// Selects and applies directional enhancement to the encoder reconstruction.
/// </summary>
internal static partial class Av1CdefEncoder
{
    /// <summary>
    /// The number of packed primary and secondary strength combinations.
    /// </summary>
    private const int MaximumStrengthCount = 64;

    /// <summary>
    /// Gets the ordered strength candidates for the frame's search policy.
    /// </summary>
    /// <param name="speed">The configured encoding speed.</param>
    /// <param name="stillPicture">Whether the sequence contains one still image.</param>
    /// <param name="size">The visible luma dimensions.</param>
    /// <returns>The packed strengths, or an empty span when strengths are predicted from quantization.</returns>
    private static ReadOnlySpan<byte> GetCandidateStrengths(HeifEncodingSpeed speed, bool stillPicture, Size size)
    {
        if (stillPicture && speed >= HeifEncodingSpeed.Level7)
        {
            return [];
        }

        // Candidate order determines the winner when errors are equal. Each byte packs the primary
        // strength in its upper four bits and the two-bit secondary strength in its lower bits.
        if (speed >= HeifEncodingSpeed.Level6)
        {
            return [0, 2, 44, 46];
        }

        if (speed >= HeifEncodingSpeed.Level4)
        {
            return [0, 2, 8, 10, 16, 18, 32, 34, 56, 58];
        }

        if (!stillPicture && speed >= HeifEncodingSpeed.Level3 && Math.Min(size.Width, size.Height) < 720)
        {
            return [0, 1, 2, 3, 8, 9, 10, 11, 16, 17, 18, 19, 32, 33, 34, 35, 56, 57, 58, 59];
        }

        if (speed >= HeifEncodingSpeed.Level1)
        {
            return [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 20, 21, 22, 23, 28, 29, 30, 31, 40, 41, 42, 43, 52, 53, 54, 55];
        }

        return
        [
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
            32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47,
            48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63
        ];
    }

    /// <summary>
    /// Selects the frame's strength palette and assigns a palette entry to each measured filter unit.
    /// </summary>
    /// <param name="picture">The retained block decisions receiving strength indices.</param>
    /// <param name="candidates">The ordered packed strength candidates.</param>
    /// <param name="lumaErrors">The normalized luma errors, with 64 entries per measured unit.</param>
    /// <param name="chromaErrors">The combined normalized chroma errors, with 64 entries per measured unit.</param>
    /// <param name="unitIndices">The mode-grid index of each measured unit.</param>
    /// <param name="totals">The reusable accumulator for all candidate pairs.</param>
    /// <param name="rateMultiplier">The frame's rate weight.</param>
    private static void SelectStrengths(
        Av1PictureControlSet picture,
        ReadOnlySpan<byte> candidates,
        ReadOnlySpan<ulong> lumaErrors,
        ReadOnlySpan<ulong> chromaErrors,
        ReadOnlySpan<int> unitIndices,
        Span<ulong> totals,
        int rateMultiplier)
    {
        ObuConstraintDirectionalEnhancementFilterParameters parameters = picture.Parent.FrameHeader.CdefParameters;
        bool color = picture.Sequence.SequenceHeader.ColorConfig.PlaneCount > 1;
        int candidateCount = candidates.Length;
        int pairCount = color ? candidateCount * candidateCount : candidateCount;
        int maximumBits = Math.Min(3, pairCount == 1 ? 0 : Av1Math.MostSignificantBit((uint)(pairCount - 1)) + 1);
        Span<int> luma = stackalloc int[8];
        Span<int> chroma = stackalloc int[8];
        long bestCost = long.MaxValue;

        for (int bits = 0; bits <= maximumBits; bits++)
        {
            int strengthCount = 1 << bits;
            ulong error = 0;
            for (int count = 0; count < strengthCount; count++)
            {
                error = AddStrength(luma, chroma, count, lumaErrors, chromaErrors, unitIndices.Length, candidateCount, color, totals);
            }

            // Reconsider each selected entry while retaining the others. Chroma always uses four
            // complete refinement rounds; reduced monochrome searches keep their greedy selection.
            if (color || candidateCount == MaximumStrengthCount)
            {
                for (int iteration = 0; iteration < 4 * strengthCount; iteration++)
                {
                    luma.Slice(1, strengthCount - 1).CopyTo(luma);
                    chroma.Slice(1, strengthCount - 1).CopyTo(chroma);
                    error = AddStrength(
                        luma, chroma, strengthCount - 1, lumaErrors, chromaErrors, unitIndices.Length, candidateCount, color, totals);
                }
            }

            // Every unit codes its palette index. Each palette entry also costs six bits per plane
            // group. Error is normalized to eight-bit samples and scaled to the transform RD domain.
            int literalBits = (unitIndices.Length * bits) + (strengthCount * 6 * (color ? 2 : 1));
            long cost = Av1RateDistortion.GetCost(rateMultiplier, literalBits << Av1ProbabilityCost.CostShift, (long)error * 16);
            if (cost < bestCost)
            {
                bestCost = cost;
                parameters.BitCount = bits;
                luma[..strengthCount].CopyTo(parameters.YStrength);
                if (color)
                {
                    chroma[..strengthCount].CopyTo(parameters.UvStrength);
                }
            }
        }

        int selectedCount = 1 << parameters.BitCount;
        for (int unit = 0; unit < unitIndices.Length; unit++)
        {
            int offset = unit * MaximumStrengthCount;
            ulong bestError = ulong.MaxValue;
            int bestIndex = 0;
            for (int index = 0; index < selectedCount; index++)
            {
                ulong error = lumaErrors[offset + parameters.YStrength[index]];
                if (color)
                {
                    error += chromaErrors[offset + parameters.UvStrength[index]];
                }

                if (error < bestError)
                {
                    bestError = error;
                    bestIndex = index;
                }
            }

            int blockIndex = picture.ModeInfoGrid.Span[unitIndices[unit]];
            picture.ModeInfoAllocation.Span[blockIndex].CdefStrength = bestIndex;
        }

        // Error tables use compact candidate indices. Convert only after all units have selected
        // their palette entries so both signaling and filtering receive actual packed strengths.
        for (int index = 0; index < selectedCount; index++)
        {
            parameters.YStrength[index] = candidates[parameters.YStrength[index]];
            if (color)
            {
                parameters.UvStrength[index] = candidates[parameters.UvStrength[index]];
            }
        }
    }

    /// <summary>
    /// Adds the candidate that minimizes total error alongside the already selected entries.
    /// </summary>
    /// <param name="luma">The selected luma candidate indices.</param>
    /// <param name="chroma">The selected chroma candidate indices.</param>
    /// <param name="selectedCount">The number of entries retained during this search.</param>
    /// <param name="lumaErrors">The measured luma errors.</param>
    /// <param name="chromaErrors">The measured combined chroma errors.</param>
    /// <param name="unitCount">The number of measured filter units.</param>
    /// <param name="candidateCount">The number of candidates per plane group.</param>
    /// <param name="color">Whether chroma participates in selection.</param>
    /// <param name="totals">The reusable accumulator for candidate errors.</param>
    /// <returns>The total error with the new entry selected.</returns>
    private static ulong AddStrength(
        Span<int> luma,
        Span<int> chroma,
        int selectedCount,
        ReadOnlySpan<ulong> lumaErrors,
        ReadOnlySpan<ulong> chromaErrors,
        int unitCount,
        int candidateCount,
        bool color,
        Span<ulong> totals)
    {
        int chromaCount = color ? candidateCount : 1;
        totals[..(candidateCount * chromaCount)].Clear();
        for (int unit = 0; unit < unitCount; unit++)
        {
            int offset = unit * MaximumStrengthCount;
            ulong selectedError = 1UL << 63;
            for (int index = 0; index < selectedCount; index++)
            {
                ulong error = lumaErrors[offset + luma[index]];
                if (color)
                {
                    error += chromaErrors[offset + chroma[index]];
                }

                selectedError = Math.Min(selectedError, error);
            }

            for (int y = 0; y < candidateCount; y++)
            {
                ulong yError = lumaErrors[offset + y];
                for (int uv = 0; uv < chromaCount; uv++)
                {
                    ulong error = yError + (color ? chromaErrors[offset + uv] : 0);
                    totals[(y * chromaCount) + uv] += Math.Min(selectedError, error);
                }
            }
        }

        ulong bestError = 1UL << 63;
        int bestIndex = 0;
        for (int index = 0; index < candidateCount * chromaCount; index++)
        {
            if (totals[index] < bestError)
            {
                bestError = totals[index];
                bestIndex = index;
            }
        }

        luma[selectedCount] = bestIndex / chromaCount;
        chroma[selectedCount] = bestIndex % chromaCount;
        return bestError;
    }
}

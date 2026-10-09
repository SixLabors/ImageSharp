// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Holds a mode-loop winner. The encoder searches the other motion modes of the winner after the loop.
/// </summary>
internal struct Av1MotionModeWinner
{
    /// <summary>The largest number of retained winners.</summary>
    public const int Capacity = 10;

    /// <summary>The rate-distortion cost of the simple-translation result.</summary>
    public long Cost;

    /// <summary>Whether the winner predicts from two references, which have no other motion mode.</summary>
    public bool IsCompound;

    /// <summary>The block decisions of the simple-translation result.</summary>
    public Av1MacroBlockModeInfo ModeInfo;

    /// <summary>The block state of the simple-translation result.</summary>
    public Av1EncoderBlockStruct Block;

    /// <summary>The motion vector of the simple-translation result.</summary>
    public Av1MotionVector Vector;

    /// <summary>
    /// Inserts a winner by cost, after any winner of equal cost, and drops the costliest winner past the limit.
    /// </summary>
    /// <param name="winners">The winners in increasing cost.</param>
    /// <param name="count">The number of winners, updated on insertion.</param>
    /// <param name="limit">The largest number of retained winners.</param>
    /// <param name="winner">The new winner.</param>
    public static void Insert(Span<Av1MotionModeWinner> winners, ref int count, int limit, Av1MotionModeWinner winner)
    {
        int location = count;
        for (int index = 0; index < count; index++)
        {
            if (winner.Cost < winners[index].Cost)
            {
                location = index;
                break;
            }
        }

        if (location >= limit)
        {
            return;
        }

        int moved = Math.Min(count, limit - 1) - location;
        if (moved > 0)
        {
            winners.Slice(location, moved).CopyTo(winners.Slice(location + 1, moved));
        }

        winners[location] = winner;
        count = Math.Min(limit, count + 1);
    }
}

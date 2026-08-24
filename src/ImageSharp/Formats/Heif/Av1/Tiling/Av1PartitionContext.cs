// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the above and left five-bit AV1 partition contexts for a mode-information position.
/// </summary>
/// <remarks>
/// Each set bit records a split at one block-size level. For example, <c>11111</c> records splits from
/// 128 by 128 through 8 by 8, while <c>10000</c> records only the 128 by 128 split.
/// </remarks>
internal struct Av1PartitionContext : IMinMaxValue<Av1PartitionContext>
{
    /// <summary>
    /// Maps each block size to the five-bit context stored for an above neighbor.
    /// </summary>
    private static readonly int[] AboveLookup =
        [31, 31, 30, 30, 30, 28, 28, 28, 24, 24, 24, 16, 16, 16, 0, 0, 31, 28, 30, 24, 28, 16];

    /// <summary>
    /// Maps each block size to the five-bit context stored for a left neighbor.
    /// </summary>
    private static readonly int[] LeftLookup =
        [31, 30, 31, 30, 28, 30, 28, 24, 28, 24, 16, 24, 16, 0, 16, 0, 28, 31, 24, 30, 16, 28];

    /// <summary>
    /// The mask used to convert a frame mode-information row to its position within a 128-sample superblock.
    /// </summary>
    public const int Mask = (1 << (7 - 2)) - 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1PartitionContext"/> struct.
    /// </summary>
    /// <param name="above">The context stored for blocks below this block.</param>
    /// <param name="left">The context stored for blocks to the right of this block.</param>
    public Av1PartitionContext(byte above, byte left)
    {
        this.Above = above;
        this.Left = left;
    }

    /// <summary>
    /// Gets the maximum representable partition context.
    /// </summary>
    public static Av1PartitionContext MaxValue => throw new NotImplementedException();

    /// <summary>
    /// Gets the minimum representable partition context.
    /// </summary>
    public static Av1PartitionContext MinValue => throw new NotImplementedException();

    /// <summary>
    /// Gets or sets the five-bit context derived from the left neighbor.
    /// </summary>
    public byte Left { get; internal set; }

    /// <summary>
    /// Gets or sets the five-bit context derived from the above neighbor.
    /// </summary>
    public byte Above { get; internal set; }

    /// <summary>
    /// Gets the above-neighbor partition context for the specified block size.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The five-bit above-neighbor context.</returns>
    public static int GetAboveContext(Av1BlockSize blockSize) => AboveLookup[(int)blockSize];

    /// <summary>
    /// Gets the left-neighbor partition context for the specified block size.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The five-bit left-neighbor context.</returns>
    public static int GetLeftContext(Av1BlockSize blockSize) => LeftLookup[(int)blockSize];
}

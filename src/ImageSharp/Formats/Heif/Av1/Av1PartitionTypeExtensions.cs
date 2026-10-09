// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Provides child-block geometry for AV1 partition types.
/// </summary>
internal static class Av1PartitionTypeExtensions
{
    /// <summary>
    /// Maps each partition type and parent block size to the size of its component blocks.
    /// </summary>
    private static readonly Av1BlockSize[][] PartitionSubSize = [
        [
        Av1BlockSize.Block4x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block128x128,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block128x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block4x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x128,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Block4x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block4x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block128x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block128x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block4x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x128,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block4x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x128,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x4,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block32x8,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        ], [
        Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block4x16,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block8x32,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block16x64,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid,
        ]
    ];

    /// <summary>
    /// Gets the number of children of each partition type, indexed by <see cref="Av1PartitionType"/>.
    /// </summary>
    private static ReadOnlySpan<byte> ChildCounts => [1, 2, 2, 4, 3, 3, 3, 3, 4, 4];

    /// <summary>
    /// Gets the child origins of each partition type in coding order, indexed by <see cref="Av1PartitionType"/>.
    /// </summary>
    /// <remarks>
    /// Each partition type owns eight entries: a column offset and a row offset for each of at most four children. The offsets count
    /// quarters of the node size, because the four-strip partitions step by a quarter and all other partitions step by a half. Entries
    /// after the last child of a partition type are zero and are never read.
    /// </remarks>
    private static ReadOnlySpan<byte> ChildOffsets =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, // None: one block that covers the node.
        0, 0, 0, 2, 0, 0, 0, 0, // Horizontal: the top half, then the bottom half.
        0, 0, 2, 0, 0, 0, 0, 0, // Vertical: the left half, then the right half.
        0, 0, 2, 0, 0, 2, 2, 2, // Split: the four quarters in raster order.
        0, 0, 2, 0, 0, 2, 0, 0, // HorizontalA: the top-left and top-right quarters, then the bottom half.
        0, 0, 0, 2, 2, 2, 0, 0, // HorizontalB: the top half, then the bottom-left and bottom-right quarters.
        0, 0, 0, 2, 2, 0, 0, 0, // VerticalA: the top-left and bottom-left quarters, then the right half.
        0, 0, 2, 0, 2, 2, 0, 0, // VerticalB: the left half, then the top-right and bottom-right quarters.
        0, 0, 0, 1, 0, 2, 0, 3, // Horizontal4: four horizontal strips from top to bottom.
        0, 0, 1, 0, 2, 0, 3, 0, // Vertical4: four vertical strips from left to right.
    ];

    /// <summary>
    /// Gets, for each partition type, a bit mask of the children that are square quarters of the node, indexed by
    /// <see cref="Av1PartitionType"/>.
    /// </summary>
    /// <remarks>
    /// Bit n is set when child n in coding order has the size of a split child. Only the three-block partitions mix quarters with a
    /// half. The children of every other partition type all have the component size of that partition type, which for a split is the
    /// quarter size, so their masks are zero.
    /// </remarks>
    private static ReadOnlySpan<byte> QuarterChildMasks => [0, 0, 0, 0, 0b011, 0b110, 0b011, 0b110, 0, 0];

    /// <summary>
    /// Gets the component block size produced by a partition operation.
    /// </summary>
    /// <param name="partition">The partition operation.</param>
    /// <param name="blockSize">The parent block size.</param>
    /// <returns>The component block size, or <see cref="Av1BlockSize.Invalid"/> when the partition is not permitted.</returns>
    public static Av1BlockSize GetBlockSubSize(this Av1PartitionType partition, Av1BlockSize blockSize)
        => PartitionSubSize[(int)partition][(int)blockSize];

    /// <summary>
    /// Gets the number of child blocks of a partition operation.
    /// </summary>
    /// <param name="partition">The partition operation.</param>
    /// <returns>The number of child blocks.</returns>
    public static int GetChildCount(this Av1PartitionType partition)
        => ChildCounts[(int)partition];

    /// <summary>
    /// Gets the child origins of a partition operation in coding order.
    /// </summary>
    /// <param name="partition">The partition operation.</param>
    /// <returns>
    /// A column offset and a row offset for each child, in quarters of the node size. The span has two entries for each child.
    /// </returns>
    public static ReadOnlySpan<byte> GetChildOffsets(this Av1PartitionType partition)
        => ChildOffsets.Slice((int)partition * 8, ChildCounts[(int)partition] * 2);

    /// <summary>
    /// Gets the origin of one child block of a partition operation.
    /// </summary>
    /// <param name="partition">The partition operation.</param>
    /// <param name="blockOrigin">The origin of the partitioned node.</param>
    /// <param name="blockSize">The size of the partitioned node.</param>
    /// <param name="childIndex">The index of the child in coding order. It is less than <see cref="GetChildCount"/>.</param>
    /// <returns>The origin of the child.</returns>
    public static Point GetChildOrigin(this Av1PartitionType partition, Point blockOrigin, Av1BlockSize blockSize, int childIndex)
    {
        // The offsets count quarters of the node, so each axis scales by a quarter of the node extent on that axis.
        int offsetIndex = ((int)partition * 8) + (childIndex * 2);
        ReadOnlySpan<byte> offsets = ChildOffsets;
        return blockOrigin + new Size(offsets[offsetIndex] * (blockSize.GetWidth() >> 2), offsets[offsetIndex + 1] * (blockSize.GetHeight() >> 2));
    }

    /// <summary>
    /// Gets the size of one child block of a partition operation.
    /// </summary>
    /// <param name="partition">The partition operation.</param>
    /// <param name="blockSize">The size of the partitioned node.</param>
    /// <param name="childIndex">The index of the child in coding order. It is less than <see cref="GetChildCount"/>.</param>
    /// <returns>The size of the child.</returns>
    public static Av1BlockSize GetChildBlockSize(this Av1PartitionType partition, Av1BlockSize blockSize, int childIndex)
        => ((QuarterChildMasks[(int)partition] >> childIndex) & 1) != 0
            ? Av1PartitionType.Split.GetBlockSubSize(blockSize)
            : partition.GetBlockSubSize(blockSize);
}

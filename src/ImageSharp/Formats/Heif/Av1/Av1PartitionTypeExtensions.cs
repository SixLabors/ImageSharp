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
    /// Gets the component block size produced by a partition operation.
    /// </summary>
    /// <param name="partition">The partition operation.</param>
    /// <param name="blockSize">The parent block size.</param>
    /// <returns>The component block size, or <see cref="Av1BlockSize.Invalid"/> when the partition is not permitted.</returns>
    public static Av1BlockSize GetBlockSubSize(this Av1PartitionType partition, Av1BlockSize blockSize)
        => PartitionSubSize[(int)partition][(int)blockSize];
}

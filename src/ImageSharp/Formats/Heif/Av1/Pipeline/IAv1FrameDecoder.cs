// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Defines reconstruction of AV1 coding blocks as their syntax is parsed within a frame.
/// </summary>
internal interface IAv1FrameDecoder
{
    /// <summary>
    /// Begins reconstruction of a superblock before its first coding block is parsed.
    /// </summary>
    /// <param name="superblockInfo">The transform and coefficient storage belonging to the superblock.</param>
    void BeginSuperblock(Av1SuperblockInfo superblockInfo);

    /// <summary>
    /// Prepares a published coding block before its residual syntax is read.
    /// </summary>
    /// <param name="partitionInfo">The current block modes, geometry, and available neighbors.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    void BeginBlock(ref Av1PartitionInfo partitionInfo, Av1TileInfo tileInfo);

    /// <summary>
    /// Reconstructs a parsed transform before the following transform's coefficients are read.
    /// </summary>
    /// <param name="partitionInfo">The current block modes, geometry, and available neighbors.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="transformInfo">The parsed transform geometry and residual metadata.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    void DecodeTransform(ref Av1PartitionInfo partitionInfo, int plane, ref Av1TransformInfo transformInfo, Av1TileInfo tileInfo);

    /// <summary>
    /// Completes reconstruction after all residuals of the coding block have been read.
    /// </summary>
    /// <param name="partitionInfo">The reconstructed block modes and geometry.</param>
    void EndBlock(ref Av1PartitionInfo partitionInfo);
}

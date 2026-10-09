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
    /// Gets the inverse-transform and prediction storage. The tile reader reads it once per tile and passes it to
    /// <see cref="BeginBlock"/> and <see cref="DecodeTransform"/>.
    /// </summary>
    Span<short> Workspace { get; }

    /// <summary>
    /// Gets the samples of one plane of the reconstructed frame. The tile reader reads each plane once per tile and passes it to
    /// <see cref="BeginBlock"/>, <see cref="DecodeTransform"/> and <see cref="EndBlock"/>.
    /// </summary>
    /// <param name="plane">The plane.</param>
    /// <returns>The plane samples, or an empty span for a chroma plane of a monochrome frame.</returns>
    Span<byte> GetFramePlane(Av1Plane plane);

    /// <summary>
    /// Begins reconstruction of a superblock before its first coding block is parsed.
    /// </summary>
    /// <param name="superblockInfo">The transform and coefficient storage belonging to the superblock.</param>
    void BeginSuperblock(Av1SuperblockInfo superblockInfo);

    /// <summary>
    /// Prepares a published coding block before its residual syntax is read.
    /// </summary>
    /// <param name="partitionInfo">The current block modes, geometry, and available neighbors.</param>
    /// <param name="workspace">The inverse-transform and prediction storage, from <see cref="Workspace"/>.</param>
    /// <param name="frameLuma">The luma samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameBlue">The blue-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameRed">The red-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    void BeginBlock(
        ref Av1PartitionInfo partitionInfo,
        Span<short> workspace,
        Span<byte> frameLuma,
        Span<byte> frameBlue,
        Span<byte> frameRed,
        Av1TileInfo tileInfo);

    /// <summary>
    /// Reconstructs a parsed transform before the following transform's coefficients are read.
    /// </summary>
    /// <param name="partitionInfo">The current block modes, geometry, and available neighbors.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="transformInfo">The parsed transform geometry and residual metadata.</param>
    /// <param name="workspace">The inverse-transform and prediction storage, from <see cref="Workspace"/>.</param>
    /// <param name="frameLuma">The luma samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameBlue">The blue-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameRed">The red-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="planeCoefficients">The coefficient scratch of the plane in the superblock, read once by the caller.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    void DecodeTransform(
        ref Av1PartitionInfo partitionInfo,
        int plane,
        ref Av1TransformInfo transformInfo,
        Span<short> workspace,
        Span<byte> frameLuma,
        Span<byte> frameBlue,
        Span<byte> frameRed,
        Span<int> planeCoefficients,
        Av1TileInfo tileInfo);

    /// <summary>
    /// Completes reconstruction after the tile reader reads all residuals of the coding block.
    /// </summary>
    /// <param name="partitionInfo">The reconstructed block modes and geometry.</param>
    /// <param name="workspace">The inverse-transform and prediction storage, from <see cref="Workspace"/>.</param>
    /// <param name="frameLuma">The luma samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    void EndBlock(ref Av1PartitionInfo partitionInfo, Span<short> workspace, Span<byte> frameLuma);
}

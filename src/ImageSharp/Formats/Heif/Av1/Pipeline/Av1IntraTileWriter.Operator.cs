// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the sample-storage operations used by single-tile intra encoding.
/// </content>
internal sealed partial class Av1IntraTileWriter
{
    /// <summary>
    /// Defines type-specific superblock encoding without coupling tile traversal to sample storage width.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    private interface ITileEncodingOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Encodes and reconstructs one superblock.
        /// </summary>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reconstruction">The reconstructed frame updated by the block transforms.</param>
        /// <param name="picture">The frame coding and mode-information state.</param>
        /// <param name="superblock">The reusable partition and final-block decisions.</param>
        /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
        /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
        public static abstract void EncodeSuperblock(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace);
    }

    /// <summary>
    /// Encodes tiles stored as eight-bit samples.
    /// </summary>
    private readonly struct ByteOperator : ITileEncodingOperator<byte>
    {
        /// <inheritdoc/>
        public static void EncodeSuperblock(
            Av1EncoderFrame<byte> source,
            Av1EncoderFrame<byte> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace)
            => Av1IntraSuperblockEncoder.Encode(
                source,
                reconstruction,
                picture,
                superblock,
                coefficientBuffer,
                blockWorkspace);
    }

    /// <summary>
    /// Encodes tiles stored as high-bit-depth samples.
    /// </summary>
    private readonly struct UInt16Operator : ITileEncodingOperator<ushort>
    {
        /// <inheritdoc/>
        public static void EncodeSuperblock(
            Av1EncoderFrame<ushort> source,
            Av1EncoderFrame<ushort> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace)
            => Av1IntraSuperblockEncoder.Encode(
                source,
                reconstruction,
                picture,
                superblock,
                coefficientBuffer,
                blockWorkspace);
    }
}

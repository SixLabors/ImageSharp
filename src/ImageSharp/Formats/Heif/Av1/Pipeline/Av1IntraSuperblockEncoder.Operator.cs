// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the sample-storage operations used by fixed intra superblock traversal.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Defines type-specific block encoding without coupling traversal to sample storage width.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    internal interface IBlockEncodingOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Gets temporary contiguous storage for left reference samples.
        /// </summary>
        /// <param name="residual">The reusable residual workspace.</param>
        /// <param name="length">The number of reference samples.</param>
        /// <returns>The writable reference span.</returns>
        public static abstract Span<TSample> GetLeftReference(Span<short> residual, int length);

        /// <summary>
        /// Encodes and reconstructs one DC intra transform block.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="reconstruction">The coded reconstruction plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="above">The top reference samples.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The retained transform state.</param>
        public static abstract void Encode(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Buffer2DRegion<TSample> reconstruction,
            Point blockOrigin,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);

        /// <summary>
        /// Encodes one luma candidate into contiguous decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
        /// <param name="above">The top reference samples.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Span<TSample> reconstruction,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);
    }

    /// <summary>
    /// Encodes blocks stored as eight-bit samples.
    /// </summary>
    internal readonly struct ByteOperator : IBlockEncodingOperator<byte>
    {
        /// <inheritdoc/>
        public static Span<byte> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.AsBytes(residual)[..length];

        /// <inheritdoc/>
        public static void Encode(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Buffer2DRegion<byte> reconstruction,
            Point blockOrigin,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraDcLossy(
                workspace,
                source,
                reconstruction,
                blockOrigin,
                above,
                left,
                hasLeft,
                hasAbove,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);

        /// <inheritdoc/>
        public static long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Span<byte> reconstruction,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
                workspace,
                source,
                blockOrigin,
                reconstruction,
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                Av1Plane.Y,
                ref state);
    }

    /// <summary>
    /// Encodes blocks stored as high-bit-depth samples.
    /// </summary>
    internal readonly struct UInt16Operator : IBlockEncodingOperator<ushort>
    {
        /// <inheritdoc/>
        public static Span<ushort> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.Cast<short, ushort>(residual)[..length];

        /// <inheritdoc/>
        public static void Encode(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Buffer2DRegion<ushort> reconstruction,
            Point blockOrigin,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraDcLossy(
                workspace,
                source,
                reconstruction,
                blockOrigin,
                above,
                left,
                hasLeft,
                hasAbove,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                bitDepth,
                ref state);

        /// <inheritdoc/>
        public static long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Span<ushort> reconstruction,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
                workspace,
                source,
                blockOrigin,
                reconstruction,
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                Av1Plane.Y,
                bitDepth,
                ref state);
    }
}

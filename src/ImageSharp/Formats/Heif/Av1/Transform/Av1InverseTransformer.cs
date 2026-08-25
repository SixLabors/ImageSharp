// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Reconstructs decoded AV1 transform coefficients into prediction sample buffers.
/// </summary>
internal class Av1InverseTransformer
{
    /// <summary>
    /// Reconstructs an eight-bit transform block in place by adding its inverse-transform residual.
    /// </summary>
    /// <param name="coefficientsBuffer">The dequantized transform coefficients.</param>
    /// <param name="reconstructionBuffer">The predicted samples and reconstruction destination.</param>
    /// <param name="reconstructionStride">The number of samples between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="plane">The zero-based Y, U, or V plane index.</param>
    /// <param name="numberOfCoefficients">The decoded coefficient end position.</param>
    /// <param name="isLossless">Whether the segment uses lossless transform rules.</param>
    /// <param name="workspace">The reusable transform workspace for the containing block decode.</param>
    public static void Reconstruct8Bit(
        Span<int> coefficientsBuffer,
        Span<byte> reconstructionBuffer,
        int reconstructionStride,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int plane,
        int numberOfCoefficients,
        bool isLossless,
        Span<int> workspace)
    {
        Av1TransformFunctionParameters transformFunctionParameters = new()
        {
            TransformType = transformType,
            TransformSize = transformSize,
            EndOfBuffer = numberOfCoefficients,
            IsLossless = isLossless,
            BitDepth = 8,
            Is16BitPipeline = false
        };

        Av1InverseTransformerFactory.InverseTransformAdd(
            coefficientsBuffer, reconstructionBuffer, reconstructionStride, reconstructionBuffer, reconstructionStride, transformFunctionParameters, workspace);
    }

    /// <summary>
    /// Reconstructs an eight-bit transform block from a separate prediction buffer.
    /// </summary>
    /// <param name="coefficientsBuffer">The dequantized transform coefficients.</param>
    /// <param name="reconstructionBufferRead">The predicted samples read by reconstruction.</param>
    /// <param name="reconstructionReadStride">The number of prediction samples between rows.</param>
    /// <param name="reconstructionBufferWrite">The destination reconstructed samples.</param>
    /// <param name="reconstructionWriteStride">The number of destination samples between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="plane">The zero-based Y, U, or V plane index.</param>
    /// <param name="numberOfCoefficients">The decoded coefficient end position.</param>
    /// <param name="isLossless">Whether the segment uses lossless transform rules.</param>
    /// <param name="workspace">The reusable transform workspace for the containing block decode.</param>
    public static void Reconstruct8Bit(
        Span<int> coefficientsBuffer,
        Span<byte> reconstructionBufferRead,
        int reconstructionReadStride,
        Span<byte> reconstructionBufferWrite,
        int reconstructionWriteStride,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int plane,
        int numberOfCoefficients,
        bool isLossless,
        Span<int> workspace)
    {
        Av1TransformFunctionParameters transformFunctionParameters = new()
        {
            TransformType = transformType,
            TransformSize = transformSize,
            EndOfBuffer = numberOfCoefficients,
            IsLossless = isLossless,
            BitDepth = 8,
            Is16BitPipeline = false
        };

        // Separate prediction and destination buffers require every sample to be copied or reconstructed. Restricting
        // traversal to the coded coefficient end position would leave the untouched prediction region unwritten.
        transformFunctionParameters.EndOfBuffer = Av1InverseTransformMath.GetMaxEndOfBuffer(transformSize);

        Av1InverseTransformerFactory.InverseTransformAdd(
            coefficientsBuffer,
            reconstructionBufferRead,
            reconstructionReadStride,
            reconstructionBufferWrite,
            reconstructionWriteStride,
            transformFunctionParameters,
            workspace);
    }

    /// <summary>
    /// Reconstructs a high-bit-depth transform block in place by adding its inverse-transform residual.
    /// </summary>
    /// <param name="coefficientsBuffer">The dequantized transform coefficients.</param>
    /// <param name="reconstructionBuffer">The predicted samples and reconstruction destination.</param>
    /// <param name="reconstructionStride">The number of logical samples between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="plane">The zero-based Y, U, or V plane index.</param>
    /// <param name="numberOfCoefficients">The decoded coefficient end position.</param>
    /// <param name="isLossless">Whether the segment uses lossless transform rules.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="workspace">The reusable transform workspace for the containing block decode.</param>
    /// <remarks>Implements the reconstruction operation in AV1 section 7.11.2.</remarks>
    public static void ReconstructHighBitDepth(
        Span<int> coefficientsBuffer,
        Span<short> reconstructionBuffer,
        int reconstructionStride,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int plane,
        int numberOfCoefficients,
        bool isLossless,
        Av1BitDepth bitDepth,
        Span<int> workspace)
    {
        Av1TransformFunctionParameters transformFunctionParameters = new()
        {
            TransformType = transformType,
            TransformSize = transformSize,
            EndOfBuffer = numberOfCoefficients,
            IsLossless = isLossless,
            BitDepth = bitDepth.GetBitCount(),
            Is16BitPipeline = true
        };

        Av1InverseTransformerFactory.InverseTransformAdd(
            coefficientsBuffer, reconstructionBuffer, reconstructionStride, reconstructionBuffer, reconstructionStride, transformFunctionParameters, workspace);
    }
}

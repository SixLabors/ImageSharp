// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Predicts, transforms, quantizes, and reconstructs finalized AV1 blocks.
/// </summary>
internal static class Av1TransformBlockEncoder
{
    /// <summary>
    /// Encodes and reconstructs one eight-bit lossy DC intra block in contiguous encoder planes.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="reconstruction">The coded reconstruction plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="above">The contiguous top reference samples.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeIntraDcLossy(
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
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);
        Span<byte> reconstructionSamples = GetPlaneSpan(reconstruction, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstructionSamples,
            reconstruction.Stride,
            above,
            left,
            hasLeft,
            hasAbove,
            Av1PredictionMode.DC,
            0,
            false,
            false,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            ref state);
    }

    /// <summary>
    /// Encodes one eight-bit intra candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
    /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeIntraLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Point blockOrigin,
        Span<byte> reconstruction,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        bool enableIntraEdgeFilter,
        bool smoothIntraEdges,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            angleDelta,
            enableIntraEdgeFilter,
            smoothIntraEdges,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            ref state);

        long distortion = Av1ResidualBuilder.SumSquaredError(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            width,
            height);

        return distortion << 4;
    }

    /// <summary>
    /// Encodes one eight-bit candidate from a cached prediction and source residual.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="prediction">The contiguous prediction samples.</param>
    /// <param name="residual">The contiguous source-minus-prediction samples.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodePredictionLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Point blockOrigin,
        ReadOnlySpan<byte> prediction,
        ReadOnlySpan<short> residual,
        Span<byte> reconstruction,
        int reconstructionStride,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int sampleCount = transformSize.GetSize2d();
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);

        // Each transform trial overwrites reconstruction but consumes the prepared residual read-only.
        // Row copies preserve a larger candidate surface without materializing a second compact block.
        for (int row = 0; row < height; row++)
        {
            prediction.Slice(row * width, width).CopyTo(reconstruction.Slice(row * reconstructionStride, width));
        }

        EncodeLossy(
            workspace,
            residual[..sampleCount],
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.Reconstruct8Bit(
                workspace.DequantizedCoefficients,
                reconstruction,
                reconstructionStride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                qIndex == 0,
                workspace.TransformWorkspace);
        }

        long distortion = Av1ResidualBuilder.SumSquaredError(
            sourceSamples,
            source.Stride,
            reconstruction,
            reconstructionStride,
            width,
            height);

        return distortion << 4;
    }

    /// <summary>
    /// Encodes one eight-bit chroma-from-luma candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="dc">The cached DC predictor sample shared by every alpha.</param>
    /// <param name="lumaQ3">The zero-mean reconstructed-luma predictor surface.</param>
    /// <param name="alphaQ3">The signed chroma-from-luma multiplier.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected chroma transform dimensions.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeChromaFromLumaLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<byte> source,
        Point blockOrigin,
        Span<byte> reconstruction,
        byte dc,
        ReadOnlySpan<short> lumaQ3,
        int alphaQ3,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<byte> sourceSamples = GetPlaneSpan(source, blockOrigin);
        reconstruction[..transformSize.GetSize2d()].Fill(dc);

        // CfL adds its scaled reconstructed-luma AC contribution to the cached DC predictor before residual coding.
        Av1ChromaFromLumaPredictor.Predict(lumaQ3, reconstruction, width, alphaQ3, width, height);
        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            workspace.Residual,
            width,
            width,
            height);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            Av1TransformType.DctDct,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.Reconstruct8Bit(
                workspace.DequantizedCoefficients,
                reconstruction,
                width,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                qIndex == 0,
                workspace.TransformWorkspace);
        }

        // Final distortion is measured against the samples a decoder reconstructs, not the unquantized predictor.
        long distortion = Av1ResidualBuilder.SumSquaredError(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            width,
            height);

        return distortion << 4;
    }

    /// <summary>
    /// Encodes and reconstructs one high-bit-depth lossy DC intra block in contiguous encoder planes.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="reconstruction">The coded reconstruction plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="above">The contiguous top reference samples.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeIntraDcLossy(
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
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);
        Span<ushort> reconstructionSamples = GetPlaneSpan(reconstruction, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstructionSamples,
            reconstruction.Stride,
            above,
            left,
            hasLeft,
            hasAbove,
            Av1PredictionMode.DC,
            0,
            false,
            false,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            bitDepth,
            ref state);
    }

    /// <summary>
    /// Encodes one high-bit-depth intra candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
    /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeIntraLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Point blockOrigin,
        Span<ushort> reconstruction,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        bool enableIntraEdgeFilter,
        bool smoothIntraEdges,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);

        EncodeIntraLossyContiguous(
            workspace,
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            angleDelta,
            enableIntraEdgeFilter,
            smoothIntraEdges,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            plane,
            bitDepth,
            ref state);

        long distortion = Av1ResidualBuilder.SumSquaredError(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            width,
            height);

        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        return normalizedDistortion << 4;
    }

    /// <summary>
    /// Encodes one high-bit-depth candidate from a cached prediction and source residual.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="prediction">The contiguous prediction samples.</param>
    /// <param name="residual">The contiguous source-minus-prediction samples.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodePredictionLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Point blockOrigin,
        ReadOnlySpan<ushort> prediction,
        ReadOnlySpan<short> residual,
        Span<ushort> reconstruction,
        int reconstructionStride,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int sampleCount = transformSize.GetSize2d();
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);

        // Each transform trial overwrites reconstruction but consumes the prepared residual read-only.
        // Row copies preserve a larger candidate surface without materializing a second compact block.
        for (int row = 0; row < height; row++)
        {
            prediction.Slice(row * width, width).CopyTo(reconstruction.Slice(row * reconstructionStride, width));
        }

        EncodeLossy(
            workspace,
            residual[..sampleCount],
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                reconstructionStride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                qIndex == 0,
                bitDepth,
                workspace.TransformWorkspace);
        }

        long distortion = Av1ResidualBuilder.SumSquaredError(
            sourceSamples,
            source.Stride,
            reconstruction,
            reconstructionStride,
            width,
            height);

        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        return normalizedDistortion << 4;
    }

    /// <summary>
    /// Encodes one high-bit-depth chroma-from-luma candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
    /// <param name="dc">The cached DC predictor sample shared by every alpha.</param>
    /// <param name="lumaQ3">The zero-mean reconstructed-luma predictor surface.</param>
    /// <param name="alphaQ3">The signed chroma-from-luma multiplier.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected chroma transform dimensions.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long EncodeChromaFromLumaLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Buffer2DRegion<ushort> source,
        Point blockOrigin,
        Span<ushort> reconstruction,
        ushort dc,
        ReadOnlySpan<short> lumaQ3,
        int alphaQ3,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        ReadOnlySpan<ushort> sourceSamples = GetPlaneSpan(source, blockOrigin);
        Span<short> signedReconstruction = MemoryMarshal.Cast<ushort, short>(reconstruction);
        reconstruction[..transformSize.GetSize2d()].Fill(dc);

        Av1ChromaFromLumaPredictor.Predict(
            lumaQ3,
            signedReconstruction,
            width,
            alphaQ3,
            bitDepth.GetBitCount(),
            width,
            height);

        Av1ResidualBuilder.Subtract(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            workspace.Residual,
            width,
            width,
            height);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            Av1TransformType.DctDct,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                signedReconstruction,
                width,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                qIndex == 0,
                bitDepth,
                workspace.TransformWorkspace);
        }

        long distortion = Av1ResidualBuilder.SumSquaredError(
            sourceSamples,
            source.Stride,
            reconstruction,
            width,
            width,
            height);

        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        return normalizedDistortion << 4;
    }

    /// <summary>
    /// Builds an eight-bit intra prediction and its compact source residual.
    /// </summary>
    /// <param name="workspace">The reusable prediction scratch.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="prediction">The prediction destination.</param>
    /// <param name="predictionStride">The number of prediction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
    /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
    /// <param name="residual">The compact source-minus-prediction destination.</param>
    /// <param name="transformSize">The prediction dimensions.</param>
    public static void PrepareIntraPrediction(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> prediction,
        int predictionStride,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        bool enableIntraEdgeFilter,
        bool smoothIntraEdges,
        Span<short> residual,
        Av1TransformSize transformSize)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Prediction remains in the specialized SIMD-first kernels. This boundary only shares the prepared
        // samples and residual across transform trials that differ in transform size or type.
        if (mode == Av1PredictionMode.DC)
        {
            Av1DcIntraPredictor.Predict(hasLeft, hasAbove, prediction, predictionStride, above, left, width, height);
        }
        else if (mode.IsDirectional())
        {
            int angle = mode.ToAngle() + (angleDelta * Av1Constants.AngleStep);
            Span<byte> scratch = MemoryMarshal.AsBytes(workspace.TransformWorkspace);
            int predictionLength = width * height;
            Span<byte> directionalScratch = scratch[..predictionLength];
            bool upsampleAbove = false;
            bool upsampleLeft = false;
            if (enableIntraEdgeFilter)
            {
                // Mode trials share raw references. Prepare private edge copies after the directional scratch;
                // this entire transform workspace is reusable once prediction and residual formation finish.
                int edgeLength = Av1IntraEdgePreparation.ReferenceBufferLength;
                int prefixLength = Av1IntraEdgePreparation.ReferencePrefixLength;
                Span<byte> aboveStorage = scratch.Slice(predictionLength, edgeLength);
                Span<byte> leftStorage = scratch.Slice(predictionLength + edgeLength, edgeLength);
                aboveStorage.Fill(127);
                leftStorage.Fill(129);
                if (angle < 180)
                {
                    above.CopyTo(aboveStorage[prefixLength..]);
                    aboveStorage[prefixLength - 1] = Unsafe.Subtract(ref MemoryMarshal.GetReference(above), 1);
                }

                if (angle > 90)
                {
                    left.CopyTo(leftStorage[prefixLength..]);
                    leftStorage[prefixLength - 1] = Unsafe.Subtract(ref MemoryMarshal.GetReference(left), 1);
                }

                Span<byte> filteredAbove = aboveStorage[prefixLength..];
                Span<byte> filteredLeft = leftStorage[prefixLength..];
                Av1IntraEdgePreparation.Prepare(
                    filteredAbove,
                    filteredLeft,
                    width,
                    height,
                    angle,
                    hasAbove ? width : 0,
                    hasLeft ? height : 0,
                    smoothIntraEdges,
                    8,
                    scratch.Slice(predictionLength + (2 * edgeLength), Av1IntraEdgeFilter.ScratchLength),
                    out upsampleAbove,
                    out upsampleLeft);

                above = filteredAbove;
                left = filteredLeft;
            }

            Av1DirectionalIntraPredictor.Predict(
                prediction,
                predictionStride,
                transformSize,
                above,
                left,
                upsampleAbove,
                upsampleLeft,
                angle,
                directionalScratch);
        }
        else
        {
            Av1NonDirectionalIntraPredictorBase.GetPredictor(mode)
                .Predict(prediction, predictionStride, above, left, width, height);
        }

        Av1ResidualBuilder.Subtract(
            source,
            sourceStride,
            prediction,
            predictionStride,
            residual,
            width,
            width,
            height);
    }

    /// <summary>
    /// Builds a high-bit-depth intra prediction and its compact source residual.
    /// </summary>
    /// <param name="workspace">The reusable prediction scratch.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="prediction">The prediction destination.</param>
    /// <param name="predictionStride">The number of prediction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
    /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
    /// <param name="residual">The compact source-minus-prediction destination.</param>
    /// <param name="transformSize">The prediction dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static void PrepareIntraPrediction(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> prediction,
        int predictionStride,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        bool enableIntraEdgeFilter,
        bool smoothIntraEdges,
        Span<short> residual,
        Av1TransformSize transformSize,
        Av1BitDepth bitDepth)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Valid high-bit-depth samples remain below the sign bit, so the predictor kernels can share
        // the unsigned frame storage with their signed transform-domain implementation.
        Span<short> signedPrediction = MemoryMarshal.Cast<ushort, short>(prediction);
        ReadOnlySpan<short> signedAbove = MemoryMarshal.Cast<ushort, short>(above);
        ReadOnlySpan<short> signedLeft = MemoryMarshal.Cast<ushort, short>(left);
        if (mode == Av1PredictionMode.DC)
        {
            Av1DcIntraPredictor.Predict(
                hasLeft,
                hasAbove,
                signedPrediction,
                predictionStride,
                signedAbove,
                signedLeft,
                width,
                height,
                bitDepth.GetBitCount());
        }
        else if (mode.IsDirectional())
        {
            int angle = mode.ToAngle() + (angleDelta * Av1Constants.AngleStep);
            Span<short> scratch = MemoryMarshal.Cast<int, short>(workspace.TransformWorkspace);
            int predictionLength = width * height;
            Span<short> directionalScratch = scratch[..predictionLength];
            bool upsampleAbove = false;
            bool upsampleLeft = false;
            if (enableIntraEdgeFilter)
            {
                // Mode trials share raw references. Prepare private edge copies after the directional scratch;
                // this entire transform workspace is reusable once prediction and residual formation finish.
                int edgeLength = Av1IntraEdgePreparation.ReferenceBufferLength;
                int prefixLength = Av1IntraEdgePreparation.ReferencePrefixLength;
                Span<short> aboveStorage = scratch.Slice(predictionLength, edgeLength);
                Span<short> leftStorage = scratch.Slice(predictionLength + edgeLength, edgeLength);
                int midpoint = 128 << (bitDepth.GetBitCount() - 8);
                aboveStorage.Fill((short)(midpoint - 1));
                leftStorage.Fill((short)(midpoint + 1));
                if (angle < 180)
                {
                    signedAbove.CopyTo(aboveStorage[prefixLength..]);
                    aboveStorage[prefixLength - 1] = Unsafe.Subtract(ref MemoryMarshal.GetReference(signedAbove), 1);
                }

                if (angle > 90)
                {
                    signedLeft.CopyTo(leftStorage[prefixLength..]);
                    leftStorage[prefixLength - 1] = Unsafe.Subtract(ref MemoryMarshal.GetReference(signedLeft), 1);
                }

                Span<short> filteredAbove = aboveStorage[prefixLength..];
                Span<short> filteredLeft = leftStorage[prefixLength..];
                Av1IntraEdgePreparation.Prepare(
                    filteredAbove,
                    filteredLeft,
                    width,
                    height,
                    angle,
                    hasAbove ? width : 0,
                    hasLeft ? height : 0,
                    smoothIntraEdges,
                    bitDepth.GetBitCount(),
                    scratch.Slice(predictionLength + (2 * edgeLength), Av1IntraEdgeFilter.ScratchLength),
                    out upsampleAbove,
                    out upsampleLeft);

                signedAbove = filteredAbove;
                signedLeft = filteredLeft;
            }

            Av1DirectionalIntraPredictor.Predict(
                signedPrediction,
                predictionStride,
                transformSize,
                signedAbove,
                signedLeft,
                upsampleAbove,
                upsampleLeft,
                angle,
                directionalScratch);
        }
        else
        {
            Av1NonDirectionalIntraPredictorBase.GetPredictor(mode)
                .Predict(signedPrediction, predictionStride, signedAbove, signedLeft, width, height);
        }

        Av1ResidualBuilder.Subtract(
            source,
            sourceStride,
            prediction,
            predictionStride,
            residual,
            width,
            width,
            height);
    }

    /// <summary>
    /// Encodes and reconstructs one eight-bit lossy intra block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="reconstruction">The reconstructed frame samples and prediction destination.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
    /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    private static void EncodeIntraLossyContiguous(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> reconstruction,
        int reconstructionStride,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        bool enableIntraEdgeFilter,
        bool smoothIntraEdges,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        PrepareIntraPrediction(
            workspace,
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            angleDelta,
            enableIntraEdgeFilter,
            smoothIntraEdges,
            workspace.Residual,
            transformSize);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            ref state);

        if (state.EndOfBlock > 0)
        {
            // Reconstructing the quantized result makes later predictions use exactly the samples a decoder will reproduce.
            Av1InverseTransformer.Reconstruct8Bit(
                workspace.DequantizedCoefficients,
                reconstruction,
                reconstructionStride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                qIndex == 0,
                workspace.TransformWorkspace);
        }
    }

    /// <summary>
    /// Encodes and reconstructs one high-bit-depth lossy intra block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The number of source samples between rows.</param>
    /// <param name="reconstruction">The reconstructed frame samples and prediction destination.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="above">The contiguous top reference samples, with prefix storage for the shared corner.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
    /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    private static void EncodeIntraLossyContiguous(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Span<ushort> reconstruction,
        int reconstructionStride,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Av1PredictionMode mode,
        int angleDelta,
        bool enableIntraEdgeFilter,
        bool smoothIntraEdges,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        PrepareIntraPrediction(
            workspace,
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            above,
            left,
            hasLeft,
            hasAbove,
            mode,
            angleDelta,
            enableIntraEdgeFilter,
            smoothIntraEdges,
            workspace.Residual,
            transformSize,
            bitDepth);

        EncodeLossy(
            workspace,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            ref state);

        if (state.EndOfBlock > 0)
        {
            // Reconstructing the quantized result makes later predictions use exactly the samples a decoder will reproduce.
            Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                reconstructionStride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                qIndex == 0,
                bitDepth,
                workspace.TransformWorkspace);
        }
    }

    /// <summary>
    /// Applies a lossy forward transform and quantization to one residual block.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeLossy(
        Av1EncoderBlockWorkspace workspace,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
        => EncodeLossy(
            workspace,
            workspace.Residual,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            ref state);

    private static void EncodeLossy(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<short> residual,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = workspace.TransformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = workspace.DequantizedCoefficients[..coefficientCount];

        if (qIndex == 0)
        {
            // A coded-lossless frame fixes every transform block at 4x4 and uses the reversible transform. Its
            // quantizer removes only the transform's fixed scale, leaving reconstruction coefficients unchanged.
            Av1ForwardTransformer.TransformLossless4x4(residual, transformed, (uint)transformSize.GetWidth());
            state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossless(transformed, quantized, dequantized, bitDepth);
            state.TransformType = Av1TransformType.DctDct;
            return;
        }

        // The forward transform reads the prepared residual without changing it, so every type candidate can
        // reuse one source-minus-prediction block. Separate coefficient spans preserve each later representation.
        Av1ForwardTransformer.Transform2d(
            residual,
            transformed,
            (uint)transformSize.GetWidth(),
            transformType,
            transformSize,
            bitDepth.GetBitCount(),
            workspace.TransformWorkspace);

        state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossy(
            transformed,
            quantized,
            dequantized,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth);

        state.TransformType = transformType;
    }

    /// <summary>
    /// Estimates the luma transform rate and distortion of a prepared inter prediction.
    /// </summary>
    /// <param name="workspace">The reusable transform storage, overwritten for each transform block.</param>
    /// <param name="residual">The padded source-minus-prediction block.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="quantizedCoefficients">Scratch for one transform's entropy-coding coefficients.</param>
    /// <param name="writer">The current tile probability state; estimation does not adapt it.</param>
    /// <param name="aboveContexts">The block's top coefficient contexts in four-sample units.</param>
    /// <param name="leftContexts">The block's left coefficient contexts in four-sample units.</param>
    /// <param name="blockSize">The containing prediction block size.</param>
    /// <param name="activeSize">The coded extent controlling which padded transform blocks are visited.</param>
    /// <param name="transformSize">The transform size selected for the estimate.</param>
    /// <param name="qIndex">The effective segment quantizer index.</param>
    /// <param name="dcDeltaQ">The luma DC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="sharpness">The quantization sharpness setting.</param>
    /// <param name="lossless">Whether the segment uses reversible transforms and lossless quantization.</param>
    /// <param name="rateMultiplier">The current block's rate-distortion multiplier.</param>
    /// <param name="transformSizeRate">The rate for signaling the selected transform partition.</param>
    /// <param name="noSkipRate">The rate for signaling a non-skipped prediction block.</param>
    /// <param name="skipRate">The rate for signaling a skipped prediction block.</param>
    /// <param name="bestCost">The current winning cost used for partial-block termination.</param>
    /// <param name="statistics">The aggregate estimate, excluding the prediction block's skip flag rate.</param>
    /// <param name="sumOfSquares">The normalized transform energy before quantization.</param>
    /// <param name="skip">Whether the aggregate estimate selects transform skip.</param>
    /// <returns>The decision cost including the skip flag, or the invalid cost for an incomplete estimate.</returns>
    public static long EstimateInterTransform(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<short> residual,
        int residualStride,
        Span<int> quantizedCoefficients,
        Av1SymbolEncoder writer,
        ReadOnlySpan<byte> aboveContexts,
        ReadOnlySpan<byte> leftContexts,
        Av1BlockSize blockSize,
        Size activeSize,
        Av1TransformSize transformSize,
        int qIndex,
        int dcDeltaQ,
        Av1BitDepth bitDepth,
        int sharpness,
        bool lossless,
        int rateMultiplier,
        int transformSizeRate,
        int noSkipRate,
        int skipRate,
        long bestCost,
        out Av1RateDistortionStatistics statistics,
        out long sumOfSquares,
        out bool skip)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int widthUnits = width >> Av1Constants.ModeInfoSizeLog2;
        int heightUnits = height >> Av1Constants.ModeInfoSizeLog2;
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = workspace.TransformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = workspace.DequantizedCoefficients[..coefficientCount];

        // A prediction trial changes only its local edge contexts. At most 32 four-sample units lie along
        // either edge of a 128-sample block; subsequent transforms see earlier transforms from this trial.
        Span<byte> above = stackalloc byte[32];
        Span<byte> left = stackalloc byte[32];
        aboveContexts.CopyTo(above);
        leftContexts.CopyTo(left);
        int rate = 0;
        long distortion = 0;
        sumOfSquares = 0;
        skip = true;
        long currentCost = Math.Min(
            Av1RateDistortion.GetCost(rateMultiplier, noSkipRate + transformSizeRate, 0),
            Av1RateDistortion.GetCost(rateMultiplier, skipRate, 0));

        bool exitEarly = false;
        for (int y = 0; y < activeSize.Height; y += height)
        {
            for (int x = 0; x < activeSize.Width; x += width)
            {
                // A threshold crossing on the final transform still leaves a complete estimate. Only a
                // subsequent unvisited transform invalidates it, so preserve the completed block's statistics.
                if (exitEarly)
                {
                    statistics = Av1RateDistortionStatistics.Invalid;
                    return long.MaxValue;
                }

                Span<byte> top = above.Slice(x >> Av1Constants.ModeInfoSizeLog2, widthUnits);
                Span<byte> side = left.Slice(y >> Av1Constants.ModeInfoSizeLog2, heightUnits);
                Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
                    Av1ComponentType.Luminance, top, side, blockSize, transformSize);

                ReadOnlySpan<short> transformResidual = residual[((y * residualStride) + x)..];
                ushort endOfBlock;
                if (lossless)
                {
                    Av1ForwardTransformer.TransformLossless4x4(transformResidual, transformed, (uint)residualStride);
                    endOfBlock = Av1ForwardQuantizer.QuantizeLossless(transformed, quantized, dequantized, bitDepth);
                }
                else
                {
                    Av1ForwardTransformer.Transform2d(
                        transformResidual,
                        transformed,
                        (uint)residualStride,
                        Av1TransformType.DctDct,
                        transformSize,
                        bitDepth.GetBitCount(),
                        workspace.TransformWorkspace);

                    endOfBlock = Av1ForwardQuantizer.QuantizeRegular(
                        transformed, quantized, dequantized, transformSize, Av1TransformType.DctDct, qIndex, dcDeltaQ, 0, bitDepth, sharpness);
                }

                int transformRate = writer.GetCoefficientCost(
                    transformSize,
                    Av1TransformType.DctDct,
                    Av1PredictionMode.DC,
                    quantized,
                    Av1ComponentType.Luminance,
                    context,
                    endOfBlock,
                    useReducedTransformSet: false,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: true);

                long transformDistortion = GetTransformError(transformed, dequantized, transformSize, bitDepth, out long transformEnergy);
                rate += transformRate;
                distortion += transformDistortion;
                sumOfSquares += transformEnergy;
                skip &= endOfBlock == 0;

                // The running bound chooses the cheaper coded or skipped contribution for each transform.
                // The final decision below chooses one skip flag for the whole prediction block.
                currentCost += Math.Min(
                    Av1RateDistortion.GetCost(rateMultiplier, transformRate, transformDistortion),
                    Av1RateDistortion.GetCost(rateMultiplier, 0, transformEnergy));

                if (currentCost > bestCost)
                {
                    exitEarly = true;
                    break;
                }

                byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                    quantized, transformSize, Av1TransformType.DctDct, endOfBlock);

                top.Fill(coefficientContext);
                side.Fill(coefficientContext);
            }
        }

        long cost;
        if (skip)
        {
            // Empty transforms retain their coefficient-skip rate in the estimate. The block header cost is
            // used for this decision but is excluded from the returned rate so the caller can combine planes.
            cost = Av1RateDistortion.GetCost(rateMultiplier, skipRate, sumOfSquares);
        }
        else
        {
            cost = Av1RateDistortion.GetCost(rateMultiplier, rate + noSkipRate + transformSizeRate, distortion);
            rate += transformSizeRate;
            if (!lossless)
            {
                long skipCost = Av1RateDistortion.GetCost(rateMultiplier, skipRate, sumOfSquares);
                if (skipCost <= cost)
                {
                    cost = skipCost;
                    rate = 0;
                    distortion = sumOfSquares;
                    skip = true;
                }
            }
        }

        statistics = new Av1RateDistortionStatistics(rateMultiplier, rate, distortion);
        return cost;
    }

    /// <summary>
    /// Measures quantization error and unquantized energy in the transform distortion domain.
    /// </summary>
    /// <param name="coefficients">The original transform coefficients.</param>
    /// <param name="dequantized">The reconstructed transform coefficients.</param>
    /// <param name="transformSize">The transform dimensions controlling coefficient scaling.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="sumOfSquares">The normalized energy of the original coefficients.</param>
    /// <returns>The normalized squared quantization error.</returns>
    public static long GetTransformError(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<int> dequantized,
        Av1TransformSize transformSize,
        Av1BitDepth bitDepth,
        out long sumOfSquares)
    {
        long error = 0;
        long energy = 0;
        int i = 0;
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantized);

        // Each Int32 lane holds one coefficient in raster order. Widen before squaring: twelve-bit
        // transforms can exceed the signed Int32 square range even though each coefficient and difference fits.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<long> errors = Vector512<long>.Zero;
            Vector512<long> energies = Vector512<long>.Zero;
            for (; i <= coefficients.Length - Vector512<int>.Count; i += Vector512<int>.Count)
            {
                Vector512<int> values = Vector512.LoadUnsafe(ref coefficientBase, (nuint)i);
                Vector512<int> differences = values - Vector512.LoadUnsafe(ref dequantizedBase, (nuint)i);
                (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(values);
                (Vector512<long> lowerDifference, Vector512<long> upperDifference) = Vector512.Widen(differences);
                energies += (lower * lower) + (upper * upper);
                errors += (lowerDifference * lowerDifference) + (upperDifference * upperDifference);
            }

            energy += Vector512.Sum(energies);
            error += Vector512.Sum(errors);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<long> errors = Vector256<long>.Zero;
            Vector256<long> energies = Vector256<long>.Zero;
            for (; i <= coefficients.Length - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                Vector256<int> values = Vector256.LoadUnsafe(ref coefficientBase, (nuint)i);
                Vector256<int> differences = values - Vector256.LoadUnsafe(ref dequantizedBase, (nuint)i);
                (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(values);
                (Vector256<long> lowerDifference, Vector256<long> upperDifference) = Vector256.Widen(differences);
                energies += (lower * lower) + (upper * upper);
                errors += (lowerDifference * lowerDifference) + (upperDifference * upperDifference);
            }

            energy += Vector256.Sum(energies);
            error += Vector256.Sum(errors);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<long> errors = Vector128<long>.Zero;
            Vector128<long> energies = Vector128<long>.Zero;
            for (; i <= coefficients.Length - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                Vector128<int> values = Vector128.LoadUnsafe(ref coefficientBase, (nuint)i);
                Vector128<int> differences = values - Vector128.LoadUnsafe(ref dequantizedBase, (nuint)i);
                (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(values);
                (Vector128<long> lowerDifference, Vector128<long> upperDifference) = Vector128.Widen(differences);
                energies += (lower * lower) + (upper * upper);
                errors += (lowerDifference * lowerDifference) + (upperDifference * upperDifference);
            }

            energy += Vector128.Sum(energies);
            error += Vector128.Sum(errors);
        }

        for (; i < coefficients.Length; i++)
        {
            long value = coefficients[i];
            long difference = value - dequantized[i];
            energy += value * value;
            error += difference * difference;
        }

        // Normalize high-bit-depth squared values first, rounding once at the accumulated-block boundary.
        // Transform scale zero then divides by four, scale one is unchanged, and scale two multiplies by four.
        int precisionShift = 2 * (bitDepth.GetBitCount() - 8);
        long rounding = (1L << precisionShift) >> 1;
        error = (error + rounding) >> precisionShift;
        energy = (energy + rounding) >> precisionShift;
        int scaleShift = (1 - transformSize.GetScale()) * 2;
        sumOfSquares = scaleShift >= 0 ? energy >> scaleShift : energy << -scaleShift;
        return scaleShift >= 0 ? error >> scaleShift : error << -scaleShift;
    }

    public static Span<TSample> GetPlaneSpan<TSample>(Buffer2DRegion<TSample> plane, Point blockOrigin)
        where TSample : unmanaged
    {
        int offset =
            ((plane.Bounds.Y + blockOrigin.Y) * plane.Stride) +
            plane.Bounds.X +
            blockOrigin.X;

        // Encoder planes wrap one contiguous frame owner, so direct segment access retains physical strides without an enumerator or row copy.
        return plane.Buffer.FastMemoryGroup[0].Span[offset..];
    }
}

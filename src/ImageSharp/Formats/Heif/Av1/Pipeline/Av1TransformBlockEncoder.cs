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
internal static partial class Av1TransformBlockEncoder
{
    /// <summary>
    /// Gets the Q12 terms that normalize a DC coefficient for the shape of its transform.
    /// </summary>
    /// <remarks>
    /// This is <c>dc_coeff_scale</c>. Rectangles of 1:2 carry a square-root of two,
    /// rectangles of 1:4 a factor of two, transforms of 8x8 and below another factor of two, and the
    /// 64-point transforms have no entry because they are excluded from this prediction.
    /// </remarks>
    private static ReadOnlySpan<ushort> DcCoefficientScale =>
    [
        1024, 2048, 4096, 4096, 0, 1448, 1448, 2896, 2896, 2896,
        2896, 0, 0, 2048, 2048, 4096, 4096, 0, 0
    ];

    /// <summary>
    /// Encodes and reconstructs one eight-bit lossy DC intra block in contiguous encoder planes.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
    /// <param name="reconstruction">The coded reconstruction plane.</param>
    /// <param name="reconstructionSamples">The samples of the complete reconstruction plane, read once by the caller.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="above">The contiguous top reference samples.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeIntraDcLossy(
        Av1EncoderBlockWorkspace workspace,
        Av1PlaneRegion<byte> source,
        ReadOnlySpan<byte> sourceSamples,
        Av1PlaneRegion<byte> reconstruction,
        Span<byte> reconstructionSamples,
        Point blockOrigin,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool hasLeft,
        bool hasAbove,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        EncodeIntraLossyContiguous(
            workspace,
            GetPlaneSpan(sourceSamples, source, blockOrigin),
            source.Stride,
            GetPlaneSpan(reconstructionSamples, reconstruction, blockOrigin),
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
            lossless,
            dcDeltaQ,
            acDeltaQ,
            plane,
            ref state);
    }

    /// <summary>
    /// Encodes one eight-bit intra candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="candidate">The values and buffers of the plane that every candidate of the block shares.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <param name="sse">The residual energy of leaving the candidate uncoded, measured where its distortion was.</param>
    /// <returns>The normalized distortion in AV1 transform units.</returns>
    public static long EncodeIntraLossyCandidate(
        in Av1IntraCandidatePlane<byte> candidate,
        Av1PredictionMode mode,
        int angleDelta,
        Av1TransformType transformType,
        ref Av1EncoderTransformBlockState state,
        out long sse)
    {
        // The source block and the candidate reconstruction.
        Av1EncoderBlockWorkspace workspace = candidate.Workspace;
        ReadOnlySpan<byte> source = candidate.Source;
        int sourceStride = candidate.SourceStride;
        Point blockOrigin = candidate.BlockOrigin;
        Span<byte> reconstruction = candidate.Reconstruction;

        // The transform and the quantizer of the plane.
        Av1Plane plane = candidate.Plane;
        Av1TransformSize transformSize = candidate.TransformSize;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int qIndex = candidate.QIndex;
        bool lossless = candidate.Lossless;
        int dcDeltaQ = candidate.DcDeltaQ;
        int acDeltaQ = candidate.AcDeltaQ;
        (int Type, uint Threshold) distortionPolicy = candidate.DistortionPolicy;

        // The coefficient and workspace buffers, which the caller read once for all candidates.
        Span<int> quantizedCoefficients = candidate.QuantizedCoefficients;
        Span<short> residual = candidate.Residual;
        Span<int> transformCoefficients = candidate.TransformCoefficients;
        Span<int> dequantizedCoefficients = candidate.DequantizedCoefficients;
        Span<int> transformWorkspace = candidate.TransformWorkspace;

        PrepareIntraPrediction(
            transformWorkspace,
            source,
            sourceStride,
            reconstruction,
            width,
            candidate.Above,
            candidate.Left,
            candidate.HasLeft,
            candidate.HasAbove,
            mode,
            angleDelta,
            candidate.EnableIntraEdgeFilter,
            candidate.SmoothIntraEdges,
            residual,
            transformSize);

        // The prediction goes into the frame. This candidate is the last transform block of its plane block, so the frame keeps the prediction.
        // Reference: av1_predict_intra_block_facade() into pd->dst in block_rd_txfm(), and the last block test of recon_intra().
        WriteFrameSamples(candidate.Frame, candidate.FrameSamples, blockOrigin, reconstruction, width, width, height);

        // search_tx_type measures the residual energy of the visible samples and
        // selects transform-domain distortion when the speed policy and that energy allow it. A 64-point
        // transform keeps half of its coefficients, so its transform-domain error is not comparable.
        Size visible = workspace.GetVisibleSize(plane, blockOrigin, width, height);
        int visibleWidth = visible.Width;
        int visibleHeight = visible.Height;
        int predictDcLevel = GetPredictDcLevel(workspace);

        // A 64-point transform is excluded from skip prediction, because its DC coefficient carries no
        // scaling term. Its residual is then measured without a mean and a variance.
        bool predictDcBlock = predictDcLevel >= 1 && width != 64 && height != 64;
        long perPixelMean = 0;
        ulong blockVariance = 0;
        uint blockMseQ8;
        long residualEnergy = predictDcBlock
            ? GetBlockStatistics(
                residual, width, visibleWidth, visibleHeight, Av1BitDepth.EightBit, out blockMseQ8, out perPixelMean, out blockVariance)
            : GetBlockError(
                residual, width, visibleWidth, visibleHeight, Av1BitDepth.EightBit, out blockMseQ8);

        sse = residualEnergy;

        // predict_dc_only_block settles a block whose residual cannot survive
        // quantization. Its prediction stands as the reconstruction and it codes the all-zero flag alone.
        if (predictDcBlock && PredictSkippedBlock(
            transformSize,
            Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, Av1BitDepth.EightBit),
            Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, Av1BitDepth.EightBit),
            Av1BitDepth.EightBit,
            perPixelMean,
            blockVariance))
        {
            state.EndOfBlock = 0;
            state.CoefficientContext = 0;

            // The reference stores DCT_DCT for a predicted luma block. Chroma keeps the type derived from
            // the prediction mode, because no chroma transform type is signaled for a decoder to read.
            state.TransformType = plane == Av1Plane.Y ? Av1TransformType.DctDct : transformType;

            // Only the end of block is set: every reader of the coefficients stops at it, so the buffer is not cleared.
            return residualEnergy;
        }

        // This search holds one transform type, as a chroma search always does. A policy that measures
        // the winner in the pixel domain then has nothing left to compare, so it measures every candidate
        // there instead (search_tx_type).
        bool useTransformDomainDistortion = distortionPolicy.Type > 1 &&
            blockMseQ8 >= distortionPolicy.Threshold &&
            transformSize.GetSquareUpSize() != Av1TransformSize.Size64x64;

        // search_tx_type() subtracts with the border padding of the candidate type.
        PadBorderResidual(workspace, plane, blockOrigin, residual, width, width, height, transformType);

        EncodeLossyCandidate(
            workspace,
            candidate.Writer,
            candidate.Tables,
            transformCoefficients,
            dequantizedCoefficients,
            transformWorkspace,
            candidate.Context,
            residual,
            width,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            lossless,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
            candidate.RateMultiplier,
            false,
            candidate.UseChromaWeights,
            false,
            blockMseQ8,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.Reconstruct8Bit(
                dequantizedCoefficients,
                reconstruction,
                width,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                transformWorkspace);
        }

        if (useTransformDomainDistortion)
        {
            // An empty transform reconstructs the prediction, so its error is the residual energy.
            int codedCoefficientCount = transformSize.GetAdjusted().GetSize2d();
            return state.EndOfBlock == 0
                ? residualEnergy
                : GetTransformError(
                    transformCoefficients[..codedCoefficientCount],
                    dequantizedCoefficients[..codedCoefficientCount],
                    transformSize,
                    Av1BitDepth.EightBit,
                    out sse);
        }

        // Full transforms retain their padded samples; only the coded source extent contributes to distortion.
        long distortion = Av1ResidualBuilder.SumSquaredError(
            source,
            sourceStride,
            reconstruction,
            width,
            visibleWidth,
            visibleHeight);

        Av1ComponentType componentType = plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
        return BoundPixelDistortion(
            workspace,
            componentType,
            state.TransformType,
            distortion << 4,
            residualEnergy,
            state.EndOfBlock,
            transformCoefficients,
            dequantizedCoefficients,
            transformSize,
            Av1BitDepth.EightBit);
    }

    /// <summary>
    /// Reconstructs the eight-bit candidate most recently quantized into the workspace and measures its distortion.
    /// </summary>
    /// <remarks>
    /// Quantization and reconstruction are separate so that a transform search can compare the coefficient rate
    /// with its current winner first. A candidate whose rate alone already costs more cannot win, and then needs
    /// no inverse transform or pixel comparison.
    /// </remarks>
    /// <param name="workspace">The workspace supplying the visible extent of the plane.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
    /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
    /// <param name="source">The source transform block, from its top-left sample.</param>
    /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
    /// <param name="blockOrigin">The transform origin in plane samples, which gives the visible extent.</param>
    /// <param name="prediction">The prepared prediction surface.</param>
    /// <param name="inputStride">The number of prediction samples between rows.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="transformSize">The candidate transform dimensions.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible inverse transform.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long ReconstructPredictionLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Span<int> transformWorkspace,
        ReadOnlySpan<int> dequantized,
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point blockOrigin,
        ReadOnlySpan<byte> prediction,
        int inputStride,
        Span<byte> reconstruction,
        int reconstructionStride,
        Av1TransformSize transformSize,
        bool lossless,
        Av1Plane plane,
        Av1EncoderTransformBlockState state)
        => ReconstructPredictionLossyCandidateCore(
            workspace,
            transformWorkspace,
            dequantized,
            source,
            sourceStride,
            blockOrigin,
            prediction,
            inputStride,
            reconstruction,
            reconstructionStride,
            transformSize,
            lossless,
            plane,
            state);

    /// <summary>
    /// Reconstructs the eight-bit candidate and measures its distortion.
    /// </summary>
    /// <param name="workspace">The workspace supplying the visible extent of the plane.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
    /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
    /// <param name="source">The source transform block, from its top-left sample.</param>
    /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
    /// <param name="blockOrigin">The transform origin in plane samples, which gives the visible extent.</param>
    /// <param name="prediction">The prepared prediction surface.</param>
    /// <param name="inputStride">The number of prediction samples between rows.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="transformSize">The candidate transform dimensions.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible inverse transform.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long ReconstructPredictionLossyCandidateCore(
        Av1EncoderBlockWorkspace workspace,
        Span<int> transformWorkspace,
        ReadOnlySpan<int> dequantized,
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point blockOrigin,
        ReadOnlySpan<byte> prediction,
        int inputStride,
        Span<byte> reconstruction,
        int reconstructionStride,
        Av1TransformSize transformSize,
        bool lossless,
        Av1Plane plane,
        Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        if (state.EndOfBlock > 0 && !lossless && transformSize == Av1TransformSize.Size8x8 &&
            Av1TransformKernels.IsSupported)
        {
            // The kernel adds the residual to the prediction directly, as lowbd_write_buffer does, so the
            // prediction needs no copy.
            Av1InverseTransformer.Inverse8x8(
                dequantized,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                state.TransformType,
                state.EndOfBlock);
        }
        else if (state.EndOfBlock > 0 && !lossless && transformSize == Av1TransformSize.Size4x4 &&
            Av1TransformKernels.IsSupported)
        {
            Av1InverseTransformer.Inverse4x4(
                dequantized,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                state.TransformType);
        }
        else if (state.EndOfBlock > 0 && !lossless && transformSize == Av1TransformSize.Size16x16 && Av1TransformKernels.IsWideSupported)
        {
            Av1InverseTransformer.Inverse16x16(
                dequantized,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                state.TransformType,
                state.EndOfBlock);
        }
        else
        {
            // Each transform trial overwrites reconstruction but consumes the prepared prediction read-only.
            // Row copies preserve a larger candidate surface without materializing a second compact block.
            for (int row = 0; row < height; row++)
            {
                prediction.Slice(row * inputStride, width).CopyTo(reconstruction.Slice(row * reconstructionStride, width));
            }

            if (state.EndOfBlock > 0)
            {
                Av1InverseTransformer.Reconstruct8Bit(
                    dequantized,
                    reconstruction,
                    reconstructionStride,
                    transformSize,
                    state.TransformType,
                    (int)plane,
                    state.EndOfBlock,
                    lossless,
                    transformWorkspace);
            }
        }

        // Full transforms retain their padded samples; only the coded source extent contributes to distortion.
        Size visible = workspace.GetVisibleSize(plane, blockOrigin, width, height);
        long distortion = Av1ResidualBuilder.SumSquaredError(
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            visible.Width,
            visible.Height);

        return distortion << 4;
    }

    /// <summary>
    /// Encodes and reconstructs one high-bit-depth lossy DC intra block in contiguous encoder planes.
    /// </summary>
    /// <param name="workspace">The reusable residual, coefficient, and transform storage.</param>
    /// <param name="source">The coded source plane.</param>
    /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
    /// <param name="reconstruction">The coded reconstruction plane.</param>
    /// <param name="reconstructionSamples">The samples of the complete reconstruction plane, read once by the caller.</param>
    /// <param name="blockOrigin">The block origin in plane samples.</param>
    /// <param name="above">The contiguous top reference samples.</param>
    /// <param name="left">The contiguous left reference samples.</param>
    /// <param name="hasLeft">Whether the left reference is available.</param>
    /// <param name="hasAbove">Whether the top reference is available.</param>
    /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
    /// <param name="transformSize">The selected transform dimensions.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeIntraDcLossy(
        Av1EncoderBlockWorkspace workspace,
        Av1PlaneRegion<ushort> source,
        ReadOnlySpan<ushort> sourceSamples,
        Av1PlaneRegion<ushort> reconstruction,
        Span<ushort> reconstructionSamples,
        Point blockOrigin,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool hasLeft,
        bool hasAbove,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        EncodeIntraLossyContiguous(
            workspace,
            GetPlaneSpan(sourceSamples, source, blockOrigin),
            source.Stride,
            GetPlaneSpan(reconstructionSamples, reconstruction, blockOrigin),
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
            lossless,
            dcDeltaQ,
            acDeltaQ,
            plane,
            bitDepth,
            ref state);
    }

    /// <summary>
    /// Encodes one high-bit-depth intra candidate into contiguous decision scratch.
    /// </summary>
    /// <param name="candidate">The values and buffers of the plane that every candidate of the block shares.</param>
    /// <param name="mode">The intra prediction mode.</param>
    /// <param name="angleDelta">The signed directional-angle adjustment.</param>
    /// <param name="transformType">The selected compound transform type.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <param name="sse">The residual energy of leaving the candidate uncoded, measured where its distortion was.</param>
    /// <returns>The normalized distortion in AV1 transform units.</returns>
    public static long EncodeIntraLossyCandidate(
        in Av1IntraCandidatePlane<ushort> candidate,
        Av1PredictionMode mode,
        int angleDelta,
        Av1TransformType transformType,
        ref Av1EncoderTransformBlockState state,
        out long sse)
    {
        // The source block and the candidate reconstruction.
        Av1EncoderBlockWorkspace workspace = candidate.Workspace;
        ReadOnlySpan<ushort> source = candidate.Source;
        int sourceStride = candidate.SourceStride;
        Point blockOrigin = candidate.BlockOrigin;
        Span<ushort> reconstruction = candidate.Reconstruction;

        // The transform and the quantizer of the plane.
        Av1Plane plane = candidate.Plane;
        Av1TransformSize transformSize = candidate.TransformSize;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int qIndex = candidate.QIndex;
        bool lossless = candidate.Lossless;
        int dcDeltaQ = candidate.DcDeltaQ;
        int acDeltaQ = candidate.AcDeltaQ;
        Av1BitDepth bitDepth = candidate.BitDepth;
        (int Type, uint Threshold) distortionPolicy = candidate.DistortionPolicy;

        // The coefficient and workspace buffers, which the caller read once for all candidates.
        Span<int> quantizedCoefficients = candidate.QuantizedCoefficients;
        Span<short> residual = candidate.Residual;
        Span<int> transformCoefficients = candidate.TransformCoefficients;
        Span<int> dequantizedCoefficients = candidate.DequantizedCoefficients;
        Span<int> transformWorkspace = candidate.TransformWorkspace;

        PrepareIntraPrediction(
            transformWorkspace,
            source,
            sourceStride,
            reconstruction,
            width,
            candidate.Above,
            candidate.Left,
            candidate.HasLeft,
            candidate.HasAbove,
            mode,
            angleDelta,
            candidate.EnableIntraEdgeFilter,
            candidate.SmoothIntraEdges,
            residual,
            transformSize,
            bitDepth);

        // The prediction goes into the frame. This candidate is the last transform block of its plane block, so the
        // frame keeps the prediction.
        WriteFrameSamples(candidate.Frame, candidate.FrameSamples, blockOrigin, reconstruction, width, width, height);

        // search_tx_type measures the residual energy of the visible samples and
        // selects transform-domain distortion when the speed policy and that energy allow it. A 64-point
        // transform keeps half of its coefficients, so its transform-domain error is not comparable.
        Size visible = workspace.GetVisibleSize(plane, blockOrigin, width, height);
        int visibleWidth = visible.Width;
        int visibleHeight = visible.Height;
        int predictDcLevel = GetPredictDcLevel(workspace);

        // A 64-point transform is excluded from skip prediction, because its DC coefficient carries no
        // scaling term. Its residual is then measured without a mean and a variance.
        bool predictDcBlock = predictDcLevel >= 1 && width != 64 && height != 64;
        long perPixelMean = 0;
        ulong blockVariance = 0;
        uint blockMseQ8;
        long residualEnergy = predictDcBlock
            ? GetBlockStatistics(
                residual, width, visibleWidth, visibleHeight, bitDepth, out blockMseQ8, out perPixelMean, out blockVariance)
            : GetBlockError(
                residual, width, visibleWidth, visibleHeight, bitDepth, out blockMseQ8);

        sse = residualEnergy;

        // predict_dc_only_block settles a block whose residual cannot survive
        // quantization. Its prediction stands as the reconstruction and it codes the all-zero flag alone.
        if (predictDcBlock && PredictSkippedBlock(
            transformSize,
            Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth),
            Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth),
            bitDepth,
            perPixelMean,
            blockVariance))
        {
            state.EndOfBlock = 0;
            state.CoefficientContext = 0;

            // The reference stores DCT_DCT for a predicted luma block. Chroma keeps the type derived from
            // the prediction mode, because no chroma transform type is signaled for a decoder to read.
            state.TransformType = plane == Av1Plane.Y ? Av1TransformType.DctDct : transformType;

            // Only the end of block is set: every reader of the coefficients stops at it, so the buffer is not cleared.
            return residualEnergy;
        }

        // This search holds one transform type, as a chroma search always does. A policy that measures
        // the winner in the pixel domain then has nothing left to compare, so it measures every candidate
        // there instead (search_tx_type).
        bool useTransformDomainDistortion = distortionPolicy.Type > 1 &&
            blockMseQ8 >= distortionPolicy.Threshold &&
            transformSize.GetSquareUpSize() != Av1TransformSize.Size64x64;

        // search_tx_type() subtracts with the border padding of the candidate type.
        PadBorderResidual(workspace, plane, blockOrigin, residual, width, width, height, transformType);
        EncodeLossyCandidate(
            workspace,
            candidate.Writer,
            candidate.Tables,
            transformCoefficients,
            dequantizedCoefficients,
            transformWorkspace,
            candidate.Context,
            residual,
            width,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            lossless,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
            candidate.RateMultiplier,
            false,
            candidate.UseChromaWeights,
            false,
            blockMseQ8,
            ref state);

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.ReconstructHighBitDepth(
                dequantizedCoefficients,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                width,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                bitDepth,
                transformWorkspace);
        }

        if (useTransformDomainDistortion)
        {
            // An empty transform reconstructs the prediction, so its error is the residual energy.
            int codedCoefficientCount = transformSize.GetAdjusted().GetSize2d();
            return state.EndOfBlock == 0
                ? residualEnergy
                : GetTransformError(
                    transformCoefficients[..codedCoefficientCount],
                    dequantizedCoefficients[..codedCoefficientCount],
                    transformSize,
                    bitDepth,
                    out sse);
        }

        // Full transforms retain their padded samples; only the coded source extent contributes to distortion.
        long distortion = Av1ResidualBuilder.SumSquaredError(
            source,
            sourceStride,
            reconstruction,
            width,
            visibleWidth,
            visibleHeight);

        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        Av1ComponentType componentType = plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
        return BoundPixelDistortion(
            workspace,
            componentType,
            state.TransformType,
            normalizedDistortion << 4,
            residualEnergy,
            state.EndOfBlock,
            transformCoefficients,
            dequantizedCoefficients,
            transformSize,
            bitDepth);
    }

    /// <summary>
    /// Reconstructs the high-bit-depth candidate most recently quantized into the workspace and measures its distortion.
    /// </summary>
    /// <remarks>
    /// Quantization and reconstruction are separate so that a transform search can compare the coefficient rate
    /// with its current winner first. A candidate whose rate alone already costs more cannot win, and then needs
    /// no inverse transform or pixel comparison.
    /// </remarks>
    /// <param name="workspace">The workspace supplying the visible extent of the plane.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
    /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
    /// <param name="source">The source transform block, from its top-left sample.</param>
    /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
    /// <param name="blockOrigin">The transform origin in plane samples, which gives the visible extent.</param>
    /// <param name="prediction">The prepared prediction surface.</param>
    /// <param name="inputStride">The number of prediction samples between rows.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="transformSize">The candidate transform dimensions.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible inverse transform.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long ReconstructPredictionLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Span<int> transformWorkspace,
        ReadOnlySpan<int> dequantized,
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point blockOrigin,
        ReadOnlySpan<ushort> prediction,
        int inputStride,
        Span<ushort> reconstruction,
        int reconstructionStride,
        Av1TransformSize transformSize,
        bool lossless,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        Av1EncoderTransformBlockState state)
        => ReconstructPredictionLossyCandidateCore(
            workspace,
            transformWorkspace,
            dequantized,
            source,
            sourceStride,
            blockOrigin,
            prediction,
            inputStride,
            reconstruction,
            reconstructionStride,
            transformSize,
            lossless,
            plane,
            bitDepth,
            state);

    /// <summary>
    /// Reconstructs the high-bit-depth candidate and measures its distortion.
    /// </summary>
    /// <param name="workspace">The workspace supplying the visible extent of the plane.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
    /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
    /// <param name="source">The source transform block, from its top-left sample.</param>
    /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
    /// <param name="blockOrigin">The transform origin in plane samples, which gives the visible extent.</param>
    /// <param name="prediction">The prepared prediction surface.</param>
    /// <param name="inputStride">The number of prediction samples between rows.</param>
    /// <param name="reconstruction">The candidate reconstruction.</param>
    /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
    /// <param name="transformSize">The candidate transform dimensions.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible inverse transform.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
    public static long ReconstructPredictionLossyCandidateCore(
        Av1EncoderBlockWorkspace workspace,
        Span<int> transformWorkspace,
        ReadOnlySpan<int> dequantized,
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point blockOrigin,
        ReadOnlySpan<ushort> prediction,
        int inputStride,
        Span<ushort> reconstruction,
        int reconstructionStride,
        Av1TransformSize transformSize,
        bool lossless,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        Av1EncoderTransformBlockState state)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();

        // Each transform trial overwrites reconstruction but consumes the prepared prediction read-only.
        // Row copies preserve a larger candidate surface without materializing a second compact block.
        for (int row = 0; row < height; row++)
        {
            prediction.Slice(row * inputStride, width).CopyTo(reconstruction.Slice(row * reconstructionStride, width));
        }

        if (state.EndOfBlock > 0)
        {
            Av1InverseTransformer.ReconstructHighBitDepth(
                dequantized,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                reconstructionStride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                bitDepth,
                transformWorkspace);
        }

        // Full transforms retain their padded samples; only the coded source extent contributes to distortion.
        Size visible = workspace.GetVisibleSize(plane, blockOrigin, width, height);
        long distortion = Av1ResidualBuilder.SumSquaredError(
            source,
            sourceStride,
            reconstruction,
            reconstructionStride,
            visible.Width,
            visible.Height);

        int shift = (bitDepth.GetBitCount() - 8) * 2;
        long normalizedDistortion = shift == 0
            ? distortion
            : (distortion + (1L << (shift - 1))) >> shift;

        return normalizedDistortion << 4;
    }

    /// <summary>
    /// Builds an eight-bit intra prediction and its compact source residual.
    /// </summary>
    /// <param name="transformWorkspace">The transform workspace of the block, which serves as edge and prediction scratch.</param>
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
        Span<int> transformWorkspace,
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
            Span<byte> scratch = MemoryMarshal.AsBytes(transformWorkspace);
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
    /// <param name="transformWorkspace">The transform workspace of the block, which serves as edge and prediction scratch.</param>
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
        Span<int> transformWorkspace,
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
            Span<short> scratch = MemoryMarshal.Cast<int, short>(transformWorkspace);
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
    /// <param name="lossless">Whether the segment of the block codes losslessly.</param>
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
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        // The prediction scratch and the inverse transform share the transform workspace, read once.
        Span<int> transformWorkspace = workspace.TransformWorkspace;
        PrepareIntraPrediction(
            transformWorkspace,
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
            lossless,
            dcDeltaQ,
            acDeltaQ,
            Av1BitDepth.EightBit,
            plane,
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
                lossless,
                transformWorkspace);
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
    /// <param name="lossless">Whether the segment of the block codes losslessly.</param>
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
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1Plane plane,
        Av1BitDepth bitDepth,
        ref Av1EncoderTransformBlockState state)
    {
        // The prediction scratch and the inverse transform share the transform workspace, read once.
        Span<int> transformWorkspace = workspace.TransformWorkspace;
        PrepareIntraPrediction(
            transformWorkspace,
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
            lossless,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            plane,
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
                lossless,
                bitDepth,
                transformWorkspace);
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
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible transform.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The retained transform type and end-of-block syntax.</param>
    public static void EncodeLossy(
        Av1EncoderBlockWorkspace workspace,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
        => EncodeLossy(
            workspace,
            workspace.Residual,
            quantizedCoefficients,
            transformSize,
            transformType,
            qIndex,
            lossless,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            plane,
            ref state);

    /// <summary>
    /// Transforms and quantizes a prepared residual block.
    /// </summary>
    /// <param name="workspace">The coefficient and transform storage.</param>
    /// <param name="residual">The prepared residual samples in transform row order.</param>
    /// <param name="quantizedCoefficients">The destination entropy-coding coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible transform.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="plane">The component plane containing the block.</param>
    /// <param name="state">The resulting transform type and end-of-block position.</param>
    public static void EncodeLossy(
        Av1EncoderBlockWorkspace workspace,
        ReadOnlySpan<short> residual,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        Av1Plane plane,
        ref Av1EncoderTransformBlockState state)
    {
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = workspace.TransformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = workspace.DequantizedCoefficients[..coefficientCount];

        if (lossless)
        {
            // A coded-lossless frame fixes every transform block at 4x4 and uses the reversible transform. Its
            // quantizer removes only the transform's fixed scale, leaving reconstruction coefficients unchanged.
            Av1ForwardTransformer.TransformLossless4x4(residual, transformed, (uint)transformSize.GetWidth());
            state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossless(transformed, quantized, dequantized, bitDepth);
            state.TransformType = Av1TransformType.DctDct;
            state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                quantized, transformSize, state.TransformType, state.EndOfBlock);

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

        Av1ComponentType componentType = plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
        state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossy(
            transformed,
            quantized,
            dequantized,
            transformSize,
            transformType,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            workspace.EncoderOptions.Sharpness,
            workspace.GetQuantizationMatrix(componentType, transformSize, transformType),
            workspace.GetInverseQuantizationMatrix(componentType, transformSize, transformType));

        state.TransformType = transformType;
        state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
            quantized, transformSize, transformType, state.EndOfBlock);
    }

    /// <summary>
    /// Transforms and quantizes one candidate using the current coefficient-optimization policy.
    /// </summary>
    /// <param name="workspace">The block workspace, which holds the quantizer matrices and the coding stage.</param>
    /// <param name="writer">The tile symbol encoder that prices the coefficients.</param>
    /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
    /// <param name="transformCoefficients">The forward transform output buffer.</param>
    /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
    /// <param name="context">The neighboring coefficient contexts.</param>
    /// <param name="residual">The source-minus-prediction block.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible transform.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="componentType">The luminance or chroma component.</param>
    /// <param name="rateMultiplier">The block rate-distortion multiplier.</param>
    /// <param name="isInter">Whether the prediction uses an inter transform set.</param>
    /// <param name="useChromaWeights">Whether chroma uses its own coefficient refinement weights.</param>
    /// <param name="winnerEvaluation">Whether the candidate is the winner of its search, which selects the SATD gate.</param>
    /// <param name="blockMseQ8">The mean squared residual in Q8, which gates coefficient refinement.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <param name="dcOnly">Whether the block codes its residual mean as the DC coefficient alone.</param>
    /// <param name="perPixelMean">The signed transform-domain residual mean of a DC-only block.</param>
    public static void EncodeLossyCandidate(
        Av1EncoderBlockWorkspace workspace,
        Av1SymbolEncoder writer,
        in Av1CoefficientTables tables,
        Span<int> transformCoefficients,
        Span<int> dequantizedCoefficients,
        Span<int> transformWorkspace,
        Av1TransformBlockContext context,
        ReadOnlySpan<short> residual,
        int residualStride,
        Span<int> quantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        bool lossless,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        Av1ComponentType componentType,
        int rateMultiplier,
        bool isInter,
        bool useChromaWeights,
        bool winnerEvaluation,
        uint blockMseQ8,
        ref Av1EncoderTransformBlockState state,
        bool dcOnly = false,
        long perPixelMean = 0)
    {
        // The coefficient buffers of this block.
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = transformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = dequantizedCoefficients[..coefficientCount];

        if (lossless)
        {
            // Coded-lossless blocks use the reversible transform and lossless quantizer. Applying the lossy
            // energy gate would replace bit-exact coefficients with regular zero-bin quantization.
            Av1ForwardTransformer.TransformLossless4x4(residual, transformed, (uint)residualStride);
            state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossless(transformed, quantized, dequantized, bitDepth);
            state.TransformType = Av1TransformType.DctDct;
            state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                quantized, transformSize, state.TransformType, state.EndOfBlock);

            return;
        }

        if (dcOnly)
        {
            // A DC-only block replaces the forward transform with its scaled mean. Reference: av1_xform_dc_only().
            transformed.Clear();
            transformed[0] = (int)((perPixelMean * DcCoefficientScale[(int)transformSize]) >> 12);
        }
        else
        {
            Av1ForwardTransformer.Transform2d(
                residual,
                transformed,
                (uint)residualStride,
                transformType,
                transformSize,
                bitDepth.GetBitCount(),
                transformWorkspace);
        }

        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);
        Av1EncoderSpeedSettings speedSettings = workspace.SpeedSettings;
        (uint Distortion, uint Satd) thresholds = workspace.EvaluationStage switch
        {
            Av1EncoderEvaluationStage.Candidate => speedSettings.ModeCoefficientOptimizationThresholds,
            Av1EncoderEvaluationStage.Winner => speedSettings.WinnerCoefficientOptimizationThresholds,
            _ => speedSettings.DefaultCoefficientOptimizationThresholds
        };

        bool satdMeasured = false;
        bool optimize = speedSettings.EnableCoefficientOptimization && ShouldOptimizeCoefficients(
            blockMseQ8,
            transformed,
            transformSize,
            acDequantizer,
            bitDepth,
            thresholds,
            winnerEvaluation,
            out satdMeasured);

        // Fast quantization is paired with trellis refinement. When normalized residual energy or
        // transformed SATD disables refinement, regular quantization supplies the stronger zero-bin and
        // reciprocal correction that the unrefined candidate requires.
        // The SATD gate sets up the quantizer again, which drops the matrices of the candidate. Reference: the
        // av1_setup_quant() call of skip_trellis_opt_based_on_satd().
        workspace.CandidateMatricesDropped = satdMeasured;
        ReadOnlySpan<byte> weights = satdMeasured ? default : workspace.GetQuantizationMatrix(componentType, transformSize, transformType);
        ReadOnlySpan<byte> inverseWeights = satdMeasured ? default : workspace.GetInverseQuantizationMatrix(componentType, transformSize, transformType);
        state.EndOfBlock = optimize
            ? Av1ForwardQuantizer.QuantizeLossy(
                transformed, quantized, dequantized, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, workspace.EncoderOptions.Sharpness, weights, inverseWeights)
            : Av1ForwardQuantizer.QuantizeRegular(
                transformed, quantized, dequantized, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, workspace.EncoderOptions.Sharpness, weights, inverseWeights);

        if (optimize && state.EndOfBlock > 0)
        {
            state.EndOfBlock = writer.OptimizeCoefficients(
                in tables,
                transformed,
                quantized,
                dequantized,
                transformSize,
                transformType,
                componentType,
                context,
                dcDequantizer,
                acDequantizer,
                rateMultiplier,
                bitDepth,
                isInter,
                useChromaWeights,
                state.EndOfBlock,
                workspace.GetCoefficientOptimizationWeights(componentType, transformSize, transformType),
                out _);
        }

        state.TransformType = transformType;
        state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
            quantized, transformSize, transformType, state.EndOfBlock);
    }

    /// <summary>
    /// Transforms, quantizes, and costs one candidate of a transform-type search.
    /// </summary>
    /// <remarks>
    /// This is the loop body of <c>search_tx_type</c>. The caller decides trellis use once for the block from its
    /// residual energy; this method applies the per-type SATD gate of <c>skip_trellis_opt_based_on_satd</c>. The
    /// trellis pass returns the coefficient rate, so no second cost pass follows it.
    /// </remarks>
    /// <param name="trial">The values and buffers that every transform type of the block shares.</param>
    /// <param name="transformType">The transform type.</param>
    /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients. The search swaps this buffer with its best one.</param>
    /// <param name="dequantizedCoefficients">The candidate reconstruction coefficients. The search swaps this buffer with its best one.</param>
    /// <param name="state">The candidate transform type and end-of-block syntax.</param>
    /// <param name="matricesDropped">Whether the candidate was quantized without its quantization matrices.</param>
    /// <returns>The coefficient rate including the skip flag and the transform type.</returns>
    public static int EncodeTypeSearchCandidate(
        in Av1TransformTypeTrial trial,
        Av1TransformType transformType,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        ref Av1EncoderTransformBlockState state,
        out bool matricesDropped)
    {
        // The workspace, the writer and its rate tables.
        Av1EncoderBlockWorkspace workspace = trial.Workspace;
        Av1SymbolEncoder writer = trial.Writer;
        Av1CoefficientTables tables = trial.Tables;
        Av1TransformBlockContext context = trial.Context;

        // The residual and the transform buffers.
        ReadOnlySpan<short> residual = trial.Residual;
        int residualStride = trial.ResidualStride;
        Span<int> transformCoefficients = trial.TransformCoefficients;
        Span<int> transformWorkspace = trial.TransformWorkspace;

        // The transform, its syntax contexts and the quantizer.
        Av1TransformSize transformSize = trial.TransformSize;
        Av1PredictionMode intraDirection = trial.IntraDirection;
        Av1FilterIntraMode filterIntraMode = trial.FilterIntraMode;
        bool useReducedTransformSet = trial.UseReducedTransformSet;
        bool usesInterTransformSet = trial.UsesInterTransformSet;
        int qIndex = trial.QIndex;
        bool lossless = trial.Lossless;
        int dcDeltaQ = trial.DcDeltaQ;
        int acDeltaQ = trial.AcDeltaQ;
        int sharpness = trial.Sharpness;
        Av1BitDepth bitDepth = trial.BitDepth;
        Av1ComponentType componentType = trial.ComponentType;

        // The trellis policy of the block.
        int rateMultiplier = trial.RateMultiplier;
        bool isInter = trial.IsInter;
        bool useChromaWeights = trial.UseChromaWeights;
        bool skipTrellis = trial.SkipTrellis;
        uint satdThreshold = trial.SatdThreshold;
        bool dcOnly = trial.DcOnly;
        long perPixelMean = trial.PerPixelMean;

        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        Span<int> transformed = transformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = dequantizedCoefficients[..coefficientCount];
        bool optimize = !skipTrellis;

        if (lossless)
        {
            // Coded-lossless blocks use the reversible transform and lossless quantizer, and no refinement.
            Av1ForwardTransformer.TransformLossless4x4(residual, transformed, (uint)residualStride);
            state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossless(transformed, quantized, dequantized, bitDepth);
            state.TransformType = Av1TransformType.DctDct;
            state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                quantized, transformSize, state.TransformType, state.EndOfBlock);

            optimize = false;
        }
        else if (dcOnly)
        {
            // A DC-only block replaces the forward transform with its scaled mean. Reference: av1_xform_dc_only().
            transformed.Clear();
            transformed[0] = (int)((perPixelMean * DcCoefficientScale[(int)transformSize]) >> 12);
        }
        else
        {
            Av1ForwardTransformer.Transform2d(
                residual,
                transformed,
                (uint)residualStride,
                transformType,
                transformSize,
                bitDepth.GetBitCount(),
                transformWorkspace);
        }

        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);

        // The SATD gate sets up the quantizer again, which drops the matrices of the candidate for quantization
        // and transform-domain distortion. Coefficient optimization still reads them from the block. Reference:
        // the av1_setup_quant() call of skip_trellis_opt_based_on_satd().
        matricesDropped = optimize && satdThreshold != uint.MaxValue;
        workspace.CandidateMatricesDropped = matricesDropped;
        if (optimize && satdThreshold != uint.MaxValue)
        {
            // skip_trellis_opt_based_on_satd: the SATD of the coded coefficients, at transform scale one
            // and eight-bit precision, against the threshold times the quantizer step and sqrt(pixels).
            long satd = Av1CoefficientMeasures.SumAbsolute(transformed);

            int scaleShift = 1 - transformSize.GetScale();
            satd = scaleShift >= 0 ? satd >> scaleShift : satd << -scaleShift;
            satd >>= bitDepth.GetBitCount() - 8;
            int dequantShift = bitDepth == Av1BitDepth.EightBit ? 3 : bitDepth.GetBitCount() - 5;
            ulong qStep = (uint)(acDequantizer >> dequantShift);
            ReadOnlySpan<byte> squareRootPixels = [4, 8, 16, 32, 32, 6, 6, 12, 12, 23, 23, 32, 32, 8, 8, 16, 16, 23, 23];
            optimize = (ulong)satd <= satdThreshold * qStep * squareRootPixels[(int)transformSize];
        }

        if (!lossless)
        {
            // Fast quantization is paired with trellis refinement. Without refinement, regular quantization
            // supplies the stronger zero-bin and reciprocal correction that the unrefined candidate requires.
            ReadOnlySpan<byte> matrix = matricesDropped ? default : workspace.GetQuantizationMatrix(componentType, transformSize, transformType);
            ReadOnlySpan<byte> inverseMatrix = matricesDropped ? default : workspace.GetInverseQuantizationMatrix(componentType, transformSize, transformType);
            if (optimize)
            {
                state.EndOfBlock = Av1ForwardQuantizer.QuantizeLossy(
                    transformed, quantized, dequantized, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness, matrix, inverseMatrix);
            }
            else
            {
                state.EndOfBlock = Av1ForwardQuantizer.QuantizeRegular(
                    transformed, quantized, dequantized, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness, matrix, inverseMatrix);
            }

            state.TransformType = transformType;
        }

        if (!optimize || state.EndOfBlock == 0)
        {
            state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                quantized, transformSize, state.TransformType, state.EndOfBlock);

            return writer.GetCoefficientCost(
                in tables,
                transformSize,
                state.TransformType,
                intraDirection,
                quantized,
                componentType,
                context,
                state.EndOfBlock,
                useReducedTransformSet,
                filterIntraMode,
                usesInterTransformSet,
                lossless);
        }

        state.EndOfBlock = writer.OptimizeCoefficients(
            in tables,
            transformed,
            quantized,
            dequantized,
            transformSize,
            transformType,
            componentType,
            context,
            dcDequantizer,
            acDequantizer,
            rateMultiplier,
            bitDepth,
            isInter,
            useChromaWeights,
            state.EndOfBlock,
            workspace.GetCoefficientOptimizationWeights(componentType, transformSize, transformType),
            out int coefficientRate);

        state.CoefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
            quantized, transformSize, transformType, state.EndOfBlock);

        return Av1SymbolEncoder.GetOptimizedCoefficientCost(
            in tables,
            transformSize,
            transformType,
            intraDirection,
            componentType,
            context,
            state.EndOfBlock,
            coefficientRate,
            useReducedTransformSet,
            filterIntraMode,
            usesInterTransformSet,
            lossless);
    }

    /// <summary>
    /// Measures the residual energy of the visible part of one transform block.
    /// </summary>
    /// <remarks>
    /// This is <c>av1_pixel_diff_dist</c> followed by the scaling of <c>search_tx_type</c>: the error is
    /// normalized to eight-bit precision and multiplied by sixteen to match pixel-domain distortion units.
    /// </remarks>
    /// <param name="residual">The source-minus-prediction block.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="visibleWidth">The number of columns inside the frame.</param>
    /// <param name="visibleHeight">The number of rows inside the frame.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="blockMseQ8">The mean squared error in Q8, normalized to eight-bit precision.</param>
    /// <returns>The scaled sum of squared residual samples.</returns>
    public static long GetBlockError(
        ReadOnlySpan<short> residual,
        int residualStride,
        int visibleWidth,
        int visibleHeight,
        Av1BitDepth bitDepth,
        out uint blockMseQ8)
    {
        // Contiguous rows form one row, so a block narrower than a vector still fills the vectors.
        long sumOfSquares = residualStride == visibleWidth
            ? Av1ResidualBuilder.SumSquares(residual[..(visibleWidth * visibleHeight)])
            : Av1ResidualBuilder.SumSquares(residual, residualStride, visibleWidth, visibleHeight);

        blockMseQ8 = visibleWidth > 0 && visibleHeight > 0
            ? (uint)((256 * sumOfSquares) / (visibleWidth * visibleHeight))
            : 0;

        int precisionShift = 2 * (bitDepth.GetBitCount() - 8);
        if (precisionShift > 0)
        {
            long rounding = 1L << (precisionShift - 1);
            sumOfSquares = (sumOfSquares + rounding) >> precisionShift;
            blockMseQ8 = (uint)((blockMseQ8 + rounding) >> precisionShift);
        }

        return sumOfSquares * 16;
    }

    /// <summary>
    /// Measures the residual energy, mean, and variance of the visible part of one transform block.
    /// </summary>
    /// <remarks>
    /// This is <c>pixel_diff_stats</c> with the eight-bit normalization and the
    /// scaling that <c>search_tx_type</c> applies to its results. It replaces <see cref="GetBlockError"/>
    /// when the speed policy predicts skipped blocks, and normalizes the mean in a way that measurement
    /// does not: the mean moves to the transform domain, where a DC coefficient scale applies to it.
    /// </remarks>
    /// <param name="residual">The source-minus-prediction block.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="visibleWidth">The number of columns inside the frame.</param>
    /// <param name="visibleHeight">The number of rows inside the frame.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="blockMseQ8">The mean squared error in Q8, normalized to eight-bit precision.</param>
    /// <param name="perPixelMean">The signed mean residual sample, scaled to the transform domain.</param>
    /// <param name="blockVariance">The residual variance, normalized to eight-bit precision.</param>
    /// <returns>The scaled sum of squared residual samples.</returns>
    public static long GetBlockStatistics(
        ReadOnlySpan<short> residual,
        int residualStride,
        int visibleWidth,
        int visibleHeight,
        Av1BitDepth bitDepth,
        out uint blockMseQ8,
        out long perPixelMean,
        out ulong blockVariance)
    {
        // If the stride is the same as the width, the rows follow each other in memory with no gap. Then the block is
        // one long row, so a block narrower than a vector still fills complete vectors. The sums are exact integers,
        // so the order of the additions does not change the result.
        long sumOfSquares = residualStride == visibleWidth
            ? Av1ResidualBuilder.SumAndSumSquares(residual[..(visibleWidth * visibleHeight)], out long sum)
            : Av1ResidualBuilder.SumAndSumSquares(residual, residualStride, visibleWidth, visibleHeight, out sum);

        if (visibleWidth > 0 && visibleHeight > 0)
        {
            // The reference normalizes with a reciprocal in double precision here, where the plain energy
            // measurement of av1_pixel_diff_dist divides in integer precision.
            double normalization = 1.0 / (visibleWidth * visibleHeight);
            int signOfSum = sum > 0 ? 1 : -1;
            perPixelMean = signOfSum * ((long)(normalization * Math.Abs(sum)) << 7);
            blockMseQ8 = (uint)(normalization * (256 * sumOfSquares));
            blockVariance = (ulong)sumOfSquares - (ulong)(normalization * sum * sum);
        }
        else
        {
            perPixelMean = 0;
            blockMseQ8 = 0;
            blockVariance = 0;
        }

        int precisionShift = 2 * (bitDepth.GetBitCount() - 8);
        if (precisionShift > 0)
        {
            long rounding = 1L << (precisionShift - 1);
            sumOfSquares = (sumOfSquares + rounding) >> precisionShift;
            blockMseQ8 = (uint)((blockMseQ8 + rounding) >> precisionShift);
            blockVariance = (blockVariance + (ulong)rounding) >> precisionShift;
        }

        return sumOfSquares * 16;
    }

    /// <summary>
    /// Gets the transform-domain distortion policy of the current mode evaluation stage.
    /// </summary>
    /// <remarks>This is <c>set_tx_domain_dist_params</c>.</remarks>
    /// <param name="workspace">The workspace holding the speed settings and the evaluation stage.</param>
    /// <returns>The distortion type and its mean-error threshold.</returns>
    public static (int Type, uint Threshold) GetDistortionPolicy(Av1EncoderBlockWorkspace workspace)
        => GetDistortionPolicy(workspace, workspace.SpeedSettings);

    /// <summary>
    /// Gets the transform-domain distortion policy of the active evaluation stage. The QM-PSNR metric is measured
    /// in the transform domain, so it always uses the transform domain. Reference: set_tx_domain_dist_params().
    /// </summary>
    /// <param name="workspace">The workspace holding the evaluation stage and the encoder configuration.</param>
    /// <param name="speedSettings">The speed settings holding the stage policies.</param>
    /// <returns>The distortion type and the mean squared error threshold.</returns>
    public static (int Type, uint Threshold) GetDistortionPolicy(Av1EncoderBlockWorkspace workspace, Av1EncoderSpeedSettings speedSettings)
    {
        if (workspace.EncoderOptions.DistortionMetric == Av1DistortionMetric.QuantizationMatrixPsnr)
        {
            return (1, 0);
        }

        return workspace.EvaluationStage switch
        {
            Av1EncoderEvaluationStage.Candidate => speedSettings.ModeTransformDomainDistortion,
            Av1EncoderEvaluationStage.Winner => speedSettings.WinnerTransformDomainDistortion,
            _ => speedSettings.DefaultTransformDomainDistortion
        };
    }

    /// <summary>
    /// Measures the transform-domain distortion of a transform block, weighted by its quantization matrix under the
    /// QM-PSNR metric. Reference: dist_block_tx_domain().
    /// </summary>
    /// <param name="workspace">The workspace holding the encoder configuration and the matrix levels.</param>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="coefficients">The original transform coefficients.</param>
    /// <param name="dequantized">The reconstructed transform coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform type selecting the matrix and the scan.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="sumOfSquares">The normalized energy of the original coefficients.</param>
    /// <param name="useMatrix">Whether the candidate kept its quantization matrix.</param>
    /// <returns>The normalized squared quantization error.</returns>
    public static long GetTransformError(
        Av1EncoderBlockWorkspace workspace,
        Av1ComponentType componentType,
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<int> dequantized,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1BitDepth bitDepth,
        out long sumOfSquares,
        bool useMatrix = true)
    {
        if (useMatrix && workspace.EncoderOptions.DistortionMetric == Av1DistortionMetric.QuantizationMatrixPsnr)
        {
            ReadOnlySpan<byte> weights = workspace.GetDistortionWeights(componentType, transformSize, transformType);
            if (!weights.IsEmpty)
            {
                return GetWeightedTransformError(coefficients, dequantized, transformSize, bitDepth, weights, out sumOfSquares);
            }
        }

        return GetTransformError(coefficients, dequantized, transformSize, bitDepth, out sumOfSquares);
    }

    /// <summary>
    /// Fills the residual of a transform block beyond the frame edge when the picture pads its border.
    /// Reference: the do_border_pad path of av1_subtract_block().
    /// </summary>
    /// <param name="workspace">The workspace holding the border policy and the visible boundary.</param>
    /// <param name="plane">The plane of the block.</param>
    /// <param name="origin">The transform block origin in plane samples.</param>
    /// <param name="residual">The residual block.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="width">The transform width.</param>
    /// <param name="height">The transform height.</param>
    /// <param name="transformType">The transform type that will code the block.</param>
    /// <returns><see langword="true"/> when the block crosses the frame edge and was filled.</returns>
    public static bool PadBorderResidual(
        Av1EncoderBlockWorkspace workspace,
        Av1Plane plane,
        Point origin,
        Span<short> residual,
        int residualStride,
        int width,
        int height,
        Av1TransformType transformType)
    {
        if (!workspace.BorderPad)
        {
            return false;
        }

        Size visible = workspace.GetVisibleSize(plane, origin, width, height);
        if (visible.Width == width && visible.Height == height)
        {
            return false;
        }

        Av1ResidualBuilder.FillResidueOutsideFrame(residual, residualStride, width, height, visible.Width, visible.Height, transformType);
        return true;
    }

    /// <summary>
    /// Gets the skip prediction level of the current mode evaluation stage.
    /// </summary>
    /// <remarks>This is the <c>predict_dc_level</c> assignment of <c>set_mode_eval_params</c>.</remarks>
    /// <param name="workspace">The workspace holding the speed settings and the evaluation stage.</param>
    /// <returns>The aggressiveness of skip and DC-only block prediction.</returns>
    public static int GetPredictDcLevel(Av1EncoderBlockWorkspace workspace)
        => workspace.EvaluationStage switch
        {
            Av1EncoderEvaluationStage.Candidate => workspace.SpeedSettings.ModePredictDcLevel,
            Av1EncoderEvaluationStage.Winner => workspace.SpeedSettings.WinnerPredictDcLevel,
            _ => workspace.SpeedSettings.DefaultPredictDcLevel
        };

    /// <summary>
    /// Predicts whether one transform block codes no coefficients at all.
    /// </summary>
    /// <remarks>
    /// This is the skip branch of <c>predict_dc_only_block</c>. A residual whose
    /// variance stays below the quantizer step and whose transform-domain mean stays below the DC step
    /// quantizes to nothing, so the block keeps its prediction and costs only the all-zero flag. The
    /// remaining branch of the reference predicts DC-only blocks at level two, which the still-picture
    /// speed features never select.
    /// </remarks>
    /// <param name="transformSize">The transform dimensions scaling the mean.</param>
    /// <param name="dcDequantizer">The DC quantizer step of the plane.</param>
    /// <param name="acDequantizer">The AC quantizer step of the plane.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="perPixelMean">The signed mean residual sample, scaled to the transform domain.</param>
    /// <param name="blockVariance">The residual variance, normalized to eight-bit precision.</param>
    /// <returns><see langword="true"/> when the block is predicted to code no coefficients.</returns>
    public static bool PredictSkippedBlock(
        Av1TransformSize transformSize,
        int dcDequantizer,
        int acDequantizer,
        Av1BitDepth bitDepth,
        long perPixelMean,
        ulong blockVariance)
        => PredictSkippedBlock(transformSize, dcDequantizer, acDequantizer, bitDepth, perPixelMean, blockVariance, out _);

    /// <summary>
    /// Predicts whether one transform block codes no coefficients, or only its DC coefficient.
    /// </summary>
    /// <remarks>
    /// This is <c>predict_dc_only_block</c>. A residual whose variance stays below the quantizer step is either a
    /// skip block, when its transform-domain mean also stays below the DC step, or otherwise a DC-only candidate.
    /// The caller applies the DC-only result only at prediction level two and above, and only where the reference
    /// allows it: luma, or chroma of an inter block.
    /// </remarks>
    /// <param name="transformSize">The transform dimensions scaling the mean.</param>
    /// <param name="dcDequantizer">The DC quantizer step of the plane.</param>
    /// <param name="acDequantizer">The AC quantizer step of the plane.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="perPixelMean">The signed mean residual sample, scaled to the transform domain.</param>
    /// <param name="blockVariance">The residual variance, normalized to eight-bit precision.</param>
    /// <param name="dcOnly">Receives whether the low-variance block keeps its DC coefficient.</param>
    /// <returns><see langword="true"/> when the block is predicted to code no coefficients.</returns>
    public static bool PredictSkippedBlock(
        Av1TransformSize transformSize,
        int dcDequantizer,
        int acDequantizer,
        Av1BitDepth bitDepth,
        long perPixelMean,
        ulong blockVariance,
        out bool dcOnly)
    {
        dcOnly = false;
        int dequantShift = bitDepth == Av1BitDepth.EightBit ? 3 : bitDepth.GetBitCount() - 5;
        ulong quantizerStep = (ulong)(acDequantizer >> dequantShift);
        ulong varianceThreshold = (ulong)(1.8 * quantizerStep * quantizerStep);
        if (blockVariance >= varianceThreshold)
        {
            return false;
        }

        ulong dcQuantizerStep = (ulong)(dcDequantizer >> 3);
        if ((ulong)Math.Abs(perPixelMean) * DcCoefficientScale[(int)transformSize] < dcQuantizerStep << 12)
        {
            return true;
        }

        dcOnly = true;
        return false;
    }

    /// <summary>
    /// Determines whether coefficient refinement is useful for the current residual and transform.
    /// </summary>
    private static bool ShouldOptimizeCoefficients(
        uint blockMseQ8,
        ReadOnlySpan<int> transformed,
        Av1TransformSize transformSize,
        int acDequantizer,
        Av1BitDepth bitDepth,
        (uint Distortion, uint Satd) thresholds,
        bool winnerEvaluation,
        out bool satdMeasured)
    {
        satdMeasured = false;
        if (winnerEvaluation)
        {
            // Final encoding has fixed mode and transform choices. Its trellis pass is independent
            // of the energy thresholds used to reduce candidate-search work.
            return true;
        }

        uint distortionThreshold = thresholds.Distortion;
        uint satdThreshold = thresholds.Satd;
        int bitDepthShift = bitDepth.GetBitCount() - 8;

        // The visible residual energy of the caller decides first. Reference: perform_block_coeff_opt in
        // search_tx_type(), from the block_mse_q8 of av1_pixel_diff_dist() or pixel_diff_stats().
        int dequantShift = bitDepth == Av1BitDepth.EightBit ? 3 : bitDepth.GetBitCount() - 5;
        ulong qStep = (uint)(acDequantizer >> dequantShift);
        if ((ulong)blockMseQ8 > distortionThreshold * qStep * qStep)
        {
            return false;
        }

        if (satdThreshold == uint.MaxValue)
        {
            return true;
        }

        satdMeasured = true;
        long satd = Av1CoefficientMeasures.SumAbsolute(transformed);

        // skip_trellis_opt_based_on_satd scales by MAX_TX_SCALE (one) minus the transform scale, so a
        // 64-point transform shifts left. The table is ceil(sqrt(coded transform pixels)) in enum order.
        int scaleShift = 1 - transformSize.GetScale();
        satd = scaleShift >= 0 ? satd >> scaleShift : satd << -scaleShift;
        satd >>= bitDepthShift;
        ReadOnlySpan<byte> squareRootPixels = [4, 8, 16, 32, 32, 6, 6, 12, 12, 23, 23, 32, 32, 8, 8, 16, 16, 23, 23];
        return (ulong)satd <= satdThreshold * qStep * squareRootPixels[(int)transformSize];
    }

    /// <summary>
    /// Estimates the luma transform rate and distortion of a prepared inter prediction.
    /// </summary>
    /// <param name="transformCoefficients">The forward transform output, overwritten for each transform block.</param>
    /// <param name="dequantizedCoefficients">The dequantized coefficients, overwritten for each transform block.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
    /// <param name="residual">The padded source-minus-prediction block.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="quantizedCoefficients">Scratch for one transform's entropy-coding coefficients.</param>
    /// <param name="writer">The current tile probability state; estimation does not adapt it.</param>
    /// <param name="tables">The rate tables of the tile, which the caller read once.</param>
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
        Span<int> transformCoefficients,
        Span<int> dequantizedCoefficients,
        Span<int> transformWorkspace,
        ReadOnlySpan<short> residual,
        int residualStride,
        Span<int> quantizedCoefficients,
        Av1SymbolEncoder writer,
        in Av1CoefficientTables tables,
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
        Span<int> transformed = transformCoefficients[..coefficientCount];
        Span<int> quantized = quantizedCoefficients[..coefficientCount];
        Span<int> dequantized = dequantizedCoefficients[..coefficientCount];

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

                // Only the 4x4 transform uses reversible lifting. Larger transforms here are
                // estimates for prediction selection; final lossless residual coding still uses 4x4.
                if (lossless && transformSize == Av1TransformSize.Size4x4)
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
                        transformWorkspace);

                    endOfBlock = Av1ForwardQuantizer.QuantizeRegular(
                        transformed,
                        quantized,
                        dequantized,
                        transformSize,
                        Av1TransformType.DctDct,
                        qIndex,
                        dcDeltaQ,
                        0,
                        bitDepth,
                        sharpness);
                }

                int transformRate = writer.GetCoefficientCost(
                    in tables,
                    transformSize,
                    Av1TransformType.DctDct,
                    Av1PredictionMode.DC,
                    quantized,
                    Av1ComponentType.Luminance,
                    context,
                    endOfBlock,
                    useReducedTransformSet: false,
                    Av1FilterIntraMode.AllFilterIntraModes,
                    usesInterTransformSet: true,
                    lossless);

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
    /// Settles the pixel-domain distortion of a coded transform block. Reconstruction clamps samples, so a block
    /// whose residual averages at least a quarter of the largest sample energy can measure too small, and its
    /// transform-domain error bounds it from below. A 64x64 transform whose energy lies mostly outside its coded
    /// quadrant is measured in the transform domain, with the energy it cannot code added back.
    /// Reference: the pixel-domain branch of the distortion step of search_tx_type().
    /// </summary>
    /// <param name="workspace">The workspace holding the encoder configuration and the matrix levels.</param>
    /// <param name="componentType">The luma or chroma component.</param>
    /// <param name="transformType">The transform type of the candidate.</param>
    /// <param name="pixelDistortion">The pixel-domain distortion in AV1 transform units.</param>
    /// <param name="residualEnergy">The residual energy in the same units.</param>
    /// <param name="endOfBlock">The position after the final nonzero coefficient.</param>
    /// <param name="coefficients">The forward transform coefficients.</param>
    /// <param name="dequantized">The dequantized coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <returns>The distortion the reference records for the block.</returns>
    private static long BoundPixelDistortion(
        Av1EncoderBlockWorkspace workspace,
        Av1ComponentType componentType,
        Av1TransformType transformType,
        long pixelDistortion,
        long residualEnergy,
        int endOfBlock,
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<int> dequantized,
        Av1TransformSize transformSize,
        Av1BitDepth bitDepth)
    {
        bool isHighEnergy = residualEnergy >= 128L * 128 * transformSize.GetSize2d();
        bool is64x64 = transformSize == Av1TransformSize.Size64x64;
        if (endOfBlock == 0 || !(isHighEnergy || is64x64))
        {
            return pixelDistortion;
        }

        int codedCoefficientCount = transformSize.GetAdjusted().GetSize2d();

        // The transform-domain bound uses the matrix-weighted error of the QM-PSNR metric when the candidate kept its
        // matrices. Reference: the dist_block_tx_domain() calls of search_tx_type().
        long transformDistortion = GetTransformError(
            workspace,
            componentType,
            coefficients[..codedCoefficientCount],
            dequantized[..codedCoefficientCount],
            transformSize,
            transformType,
            bitDepth,
            out long transformEnergy,
            useMatrix: !workspace.CandidateMatricesDropped);

        long energyDifference = residualEnergy - transformEnergy;
        if (!is64x64 || !isHighEnergy || energyDifference * 2 < transformEnergy)
        {
            return isHighEnergy && pixelDistortion < transformDistortion ? transformDistortion : pixelDistortion;
        }

        return transformDistortion + energyDifference;
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
        => GetTransformErrorCore(coefficients, dequantized, transformSize, bitDepth, out sumOfSquares);

    public static long GetTransformErrorCore(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<int> dequantized,
        Av1TransformSize transformSize,
        Av1BitDepth bitDepth,
        out long sumOfSquares)
    {
        TransformError<TransformErrorOperator>.Accumulate(coefficients, dequantized, out long energy, out long error);

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

    /// <summary>
    /// Writes samples into the frame that is coded. Each trial of a block writes its prediction and reconstruction there.
    /// An empty plane gets no write. Reference: the writes of a search into pd->dst, which points into cur_frame.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="plane">The frame plane, or an empty plane for no write.</param>
    /// <param name="planeSamples">The samples of <paramref name="plane"/>, which the caller reads once outside its loops.</param>
    /// <param name="origin">The origin of the rectangle, in plane samples.</param>
    /// <param name="samples">The samples, row by row.</param>
    /// <param name="stride">The row stride of the samples.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    public static void WriteFrameSamples<TSample>(
        Av1PlaneRegion<TSample> plane,
        Span<TSample> planeSamples,
        Point origin,
        ReadOnlySpan<TSample> samples,
        int stride,
        int width,
        int height)
        where TSample : unmanaged
    {
        if (planeSamples.IsEmpty)
        {
            return;
        }

        Span<TSample> destination = planeSamples[plane.GetOffset(origin.X, origin.Y)..];
        for (int row = 0; row < height; row++)
        {
            samples.Slice(row * stride, width).CopyTo(destination.Slice(row * plane.Stride, width));
        }
    }

    /// <summary>
    /// Gets the plane samples from a block origin to the end of the plane. A caller in a loop reads
    /// <see cref="Av1PlaneRegion{TSample}.Samples"/> once and slices it at <see cref="Av1PlaneRegion{TSample}.GetOffset"/> instead.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="plane">The plane.</param>
    /// <param name="blockOrigin">The block origin, relative to the plane rectangle.</param>
    /// <returns>The samples from the block origin; rows are <see cref="Av1PlaneRegion{TSample}.Stride"/> apart.</returns>
    public static Span<TSample> GetPlaneSpan<TSample>(Av1PlaneRegion<TSample> plane, Point blockOrigin)
        where TSample : unmanaged
    {
        int offset =
            ((plane.Bounds.Y + blockOrigin.Y) * plane.Stride) +
            plane.Bounds.X +
            blockOrigin.X;

        // Encoder planes are single contiguous allocations, so the block keeps the physical stride without a row copy.
        return plane.Samples[offset..];
    }

    /// <summary>
    /// Gets the plane samples from a block origin to the end of the plane, from the plane samples that the caller read once.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="planeSamples">The samples of the complete plane, borders included, read once by the caller.</param>
    /// <param name="plane">The plane, which gives the position of the rectangle and the stride.</param>
    /// <param name="blockOrigin">The block origin, relative to the plane rectangle.</param>
    /// <returns>The samples from the block origin; rows are <see cref="Av1PlaneRegion{TSample}.Stride"/> apart.</returns>
    public static Span<TSample> GetPlaneSpan<TSample>(Span<TSample> planeSamples, Av1PlaneRegion<TSample> plane, Point blockOrigin)
        where TSample : unmanaged
        => planeSamples[plane.GetOffset(blockOrigin.X, blockOrigin.Y)..];

    /// <summary>
    /// Gets the read-only plane samples from a block origin to the end of the plane, from the plane samples that the caller read once.
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <param name="planeSamples">The samples of the complete plane, borders included, read once by the caller.</param>
    /// <param name="plane">The plane, which gives the position of the rectangle and the stride.</param>
    /// <param name="blockOrigin">The block origin, relative to the plane rectangle.</param>
    /// <returns>The samples from the block origin; rows are <see cref="Av1PlaneRegion{TSample}.Stride"/> apart.</returns>
    public static ReadOnlySpan<TSample> GetPlaneSpan<TSample>(ReadOnlySpan<TSample> planeSamples, Av1PlaneRegion<TSample> plane, Point blockOrigin)
        where TSample : unmanaged
        => planeSamples[plane.GetOffset(blockOrigin.X, blockOrigin.Y)..];
}

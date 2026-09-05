// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the finalized prediction, transform, quantization, and reconstruction block boundary.
/// </summary>
[Trait("Format", "Avif")]
public class Av1TransformBlockEncoderTests
{
    /// <summary>
    /// Verifies that the composed block path retains the exact outputs already established for its arithmetic stages.
    /// </summary>
    [Fact]
    public void LossyBlockEncodingMatchesTransformAndQuantizerContracts()
    {
        ValidateBlock(Av1TransformSize.Size4x4, Av1TransformType.DctDct, Av1BitDepth.EightBit, 1);
        ValidateBlock(Av1TransformSize.Size8x8, Av1TransformType.Identity, Av1BitDepth.TenBit, 73);
        ValidateBlock(Av1TransformSize.Size32x64, Av1TransformType.DctDct, Av1BitDepth.TenBit, 173);
        ValidateBlock(Av1TransformSize.Size64x64, Av1TransformType.DctDct, Av1BitDepth.TwelveBit, 255);
    }

    /// <summary>
    /// Verifies that the eight-bit block boundary preserves stage ordering, strides, padding, and retained syntax.
    /// </summary>
    [Fact]
    public void EightBitIntraDcBlockEncodingMatchesStageContracts()
    {
        const int SourceStride = 13;
        const int ReconstructionStride = 15;
        const byte PaddingSentinel = 176;
        Av1TransformSize transformSize = Av1TransformSize.Size8x8;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        byte[] source = new byte[SourceStride * height];
        byte[] expectedReconstruction = new byte[ReconstructionStride * height];
        byte[] actualReconstruction = new byte[ReconstructionStride * height];
        byte[] above = new byte[width];
        byte[] left = new byte[height];
        int[] expectedQuantized = new int[coefficientCount + 7];
        int[] actualQuantized = new int[coefficientCount + 7];
        using Av1EncoderFrameBuffer<byte> sourceFrame = new(
            Configuration.Default,
            width,
            height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstructionFrame = new(
            Configuration.Default,
            width,
            height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        reconstructionFrame.Luma.DangerousGetSingleSpan().Fill(PaddingSentinel);
        Buffer2DRegion<byte> sourcePlane = sourceFrame.Frame.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> reconstructionPlane = reconstructionFrame.Frame.CodedView.GetPlane(Av1Plane.Y);
        using Av1EncoderBlockWorkspace expectedWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace actualWorkspace = new(Configuration.Default);
        FillSource(source, SourceStride, width, height, byte.MaxValue);
        Array.Fill(expectedReconstruction, (byte)211);
        Array.Fill(actualReconstruction, (byte)211);
        Array.Fill(expectedQuantized, int.MinValue);
        Array.Fill(actualQuantized, int.MinValue);

        for (int y = 0; y < height; y++)
        {
            source.AsSpan(y * SourceStride, width).CopyTo(sourcePlane.DangerousGetRowSpan(y));
            reconstructionPlane.DangerousGetRowSpan(y).Fill(211);
        }

        for (int i = 0; i < above.Length; i++)
        {
            above[i] = (byte)(37 + (i * 11));
        }

        for (int i = 0; i < left.Length; i++)
        {
            left[i] = (byte)(19 + (i * 13));
        }

        Av1DcIntraPredictor.PredictScalar(
            true,
            true,
            expectedReconstruction,
            ReconstructionStride,
            above,
            left,
            width,
            height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                expectedWorkspace.Residual[(y * width) + x] =
                    (short)(source[(y * SourceStride) + x] - expectedReconstruction[(y * ReconstructionStride) + x]);
            }
        }

        Av1EncoderTransformBlockState expectedState = default;
        Av1TransformBlockEncoder.EncodeLossy(
            expectedWorkspace,
            expectedQuantized,
            transformSize,
            Av1TransformType.DctDct,
            73,
            -1,
            3,
            Av1BitDepth.EightBit,
            ref expectedState);

        if (expectedState.EndOfBlock > 0)
        {
            Av1InverseTransformer.Reconstruct8Bit(
                expectedWorkspace.DequantizedCoefficients,
                expectedReconstruction,
                ReconstructionStride,
                transformSize,
                Av1TransformType.DctDct,
                (int)Av1Plane.Y,
                expectedState.EndOfBlock,
                false,
                expectedWorkspace.TransformWorkspace);
        }

        Av1EncoderTransformBlockState actualState = default;
        Av1TransformBlockEncoder.EncodeIntraDcLossy(
            actualWorkspace,
            sourcePlane,
            reconstructionPlane,
            Point.Empty,
            above,
            left,
            true,
            true,
            actualQuantized,
            transformSize,
            Av1TransformType.DctDct,
            73,
            -1,
            3,
            Av1Plane.Y,
            ref actualState);

        for (int y = 0; y < height; y++)
        {
            reconstructionPlane.DangerousGetRowSpan(y).CopyTo(
                actualReconstruction.AsSpan(y * ReconstructionStride, width));
        }

        int physicalRow = reconstructionPlane.Bounds.Y;
        int physicalColumn = reconstructionPlane.Bounds.X;
        ReadOnlySpan<byte> completeRow = reconstructionFrame.Luma.DangerousGetRowSpan(physicalRow);
        Assert.Equal(expectedReconstruction, actualReconstruction);
        Assert.Equal(expectedQuantized, actualQuantized);
        Assert.Equal(expectedState.EndOfBlock, actualState.EndOfBlock);
        Assert.Equal(expectedState.TransformType, actualState.TransformType);
        Assert.Equal(PaddingSentinel, completeRow[physicalColumn - 1]);
        Assert.Equal(PaddingSentinel, completeRow[physicalColumn + width]);
    }

    /// <summary>
    /// Verifies that the high-bit-depth block boundary preserves stage ordering, strides, padding, and retained syntax.
    /// </summary>
    [Fact]
    public void HighBitDepthIntraDcBlockEncodingMatchesStageContracts()
    {
        const int SourceStride = 19;
        const int ReconstructionStride = 23;
        Av1TransformSize transformSize = Av1TransformSize.Size16x8;
        Av1BitDepth bitDepth = Av1BitDepth.TenBit;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        ushort[] source = new ushort[SourceStride * height];
        ushort[] expectedReconstruction = new ushort[ReconstructionStride * height];
        ushort[] actualReconstruction = new ushort[ReconstructionStride * height];
        ushort[] above = new ushort[width];
        ushort[] left = new ushort[height];
        int[] expectedQuantized = new int[coefficientCount + 7];
        int[] actualQuantized = new int[coefficientCount + 7];
        using Buffer2D<ushort> sourceBuffer = Buffer2D<ushort>.WrapMemory(source, SourceStride, height, SourceStride);
        using Buffer2D<ushort> reconstructionBuffer =
            Buffer2D<ushort>.WrapMemory(actualReconstruction, ReconstructionStride, height, ReconstructionStride);

        using Av1EncoderBlockWorkspace expectedWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace actualWorkspace = new(Configuration.Default);
        FillSource(source, SourceStride, width, height, (1 << bitDepth.GetBitCount()) - 1);
        Array.Fill(expectedReconstruction, (ushort)777);
        Array.Fill(actualReconstruction, (ushort)777);
        Array.Fill(expectedQuantized, int.MinValue);
        Array.Fill(actualQuantized, int.MinValue);

        for (int i = 0; i < above.Length; i++)
        {
            above[i] = (ushort)(173 + (i * 17));
        }

        for (int i = 0; i < left.Length; i++)
        {
            left[i] = (ushort)(91 + (i * 29));
        }

        Span<short> signedExpectedReconstruction = MemoryMarshal.Cast<ushort, short>(expectedReconstruction.AsSpan());
        Av1DcIntraPredictor.PredictScalar(
            true,
            false,
            signedExpectedReconstruction,
            ReconstructionStride,
            MemoryMarshal.Cast<ushort, short>(above),
            MemoryMarshal.Cast<ushort, short>(left),
            width,
            height,
            bitDepth.GetBitCount());

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                expectedWorkspace.Residual[(y * width) + x] =
                    (short)(source[(y * SourceStride) + x] - expectedReconstruction[(y * ReconstructionStride) + x]);
            }
        }

        Av1EncoderTransformBlockState expectedState = default;
        Av1TransformBlockEncoder.EncodeLossy(
            expectedWorkspace,
            expectedQuantized,
            transformSize,
            Av1TransformType.DctDct,
            117,
            -2,
            4,
            bitDepth,
            ref expectedState);

        if (expectedState.EndOfBlock > 0)
        {
            Av1InverseTransformer.ReconstructHighBitDepth(
                expectedWorkspace.DequantizedCoefficients,
                signedExpectedReconstruction,
                ReconstructionStride,
                transformSize,
                Av1TransformType.DctDct,
                (int)Av1Plane.U,
                expectedState.EndOfBlock,
                false,
                bitDepth,
                expectedWorkspace.TransformWorkspace);
        }

        Av1EncoderTransformBlockState actualState = default;
        Av1TransformBlockEncoder.EncodeIntraDcLossy(
            actualWorkspace,
            new Buffer2DRegion<ushort>(sourceBuffer),
            new Buffer2DRegion<ushort>(reconstructionBuffer),
            Point.Empty,
            above,
            left,
            true,
            false,
            actualQuantized,
            transformSize,
            Av1TransformType.DctDct,
            117,
            -2,
            4,
            Av1Plane.U,
            bitDepth,
            ref actualState);

        Assert.Equal(expectedReconstruction, actualReconstruction);
        Assert.Equal(expectedQuantized, actualQuantized);
        Assert.Equal(expectedState.EndOfBlock, actualState.EndOfBlock);
        Assert.Equal(expectedState.TransformType, actualState.TransformType);
    }

    /// <summary>
    /// Verifies that high-bit-depth candidate distortion follows the codec's pixel-domain normalization order.
    /// </summary>
    [Fact]
    public void TwelveBitCandidateNormalizesSseBeforeTransformScaling()
    {
        const int Width = 8;
        const int Height = 8;
        ushort[] source = new ushort[Width * Height];
        ushort[] reconstruction = new ushort[Width * Height];
        ushort[] above = new ushort[Width];
        ushort[] left = new ushort[Height];
        int[] quantized = new int[Width * Height];
        for (int x = 0; x < Width; x++)
        {
            above[x] = (ushort)(1000 + (x * 113));
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                source[(y * Width) + x] = (ushort)(above[x] + 1);
            }
        }

        using Buffer2D<ushort> sourceBuffer = Buffer2D<ushort>.WrapMemory(source, Width, Height, Width);
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default);
        Av1EncoderTransformBlockState state = default;
        long distortion = Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
            workspace,
            new Buffer2DRegion<ushort>(sourceBuffer),
            Point.Empty,
            reconstruction,
            above,
            left,
            hasLeft: false,
            hasAbove: true,
            Av1PredictionMode.Vertical,
            0,
            enableIntraEdgeFilter: false,
            smoothIntraEdges: false,
            quantized,
            Av1TransformSize.Size8x8,
            Av1TransformType.DctDct,
            qIndex: 255,
            dcDeltaQ: 0,
            acDeltaQ: 0,
            Av1Plane.Y,
            Av1BitDepth.TwelveBit,
            ref state);

        for (int y = 0; y < Height; y++)
        {
            Assert.True(above.AsSpan().SequenceEqual(reconstruction.AsSpan(y * Width, Width)));
        }

        // Rounding the 64-sample SSE before the transform-domain scale is observably different from scaling first.
        Assert.Equal((ushort)0, state.EndOfBlock);
        Assert.Equal(0, distortion);
    }

    /// <summary>
    /// Verifies that high-bit-depth directional candidates apply the selected syntax adjustment.
    /// </summary>
    /// <param name="angleDelta">The signed AV1 directional adjustment.</param>
    [Theory]
    [InlineData(-3)]
    [InlineData(3)]
    public void TwelveBitDirectionalCandidateAppliesAngleDelta(int angleDelta)
    {
        const int Width = 8;
        const int Height = 8;
        ushort[] source = new ushort[Width * Height];
        ushort[] reconstruction = new ushort[Width * Height];
        int[] quantized = new int[Width * Height];
        Span<ushort> aboveStorage = stackalloc ushort[17];
        Span<ushort> above = aboveStorage[1..];
        Span<ushort> leftStorage = stackalloc ushort[17];
        Span<ushort> left = leftStorage[1..];
        aboveStorage[0] = 2048;
        leftStorage[0] = 2048;
        for (int i = 0; i < 16; i++)
        {
            above[i] = (ushort)(512 + (i * 128));
            left[i] = (ushort)(3584 - (i * 128));
        }

        Span<short> signedSource = MemoryMarshal.Cast<ushort, short>(source.AsSpan());
        Span<short> signedAbove = MemoryMarshal.Cast<ushort, short>(above);
        Span<short> signedLeft = MemoryMarshal.Cast<ushort, short>(left);

        // Directional arithmetic has independent scalar-oracle coverage. This isolates the high-bit-depth
        // candidate boundary and proves that its signed syntax adjustment reaches prediction unchanged.
        Av1DirectionalIntraPredictor.PredictScalar(
            signedSource,
            Width,
            Av1TransformSize.Size8x8,
            signedAbove,
            signedLeft,
            false,
            false,
            Av1PredictionMode.Directional135Degrees.ToAngle() + (angleDelta * Av1Constants.AngleStep));

        using Buffer2D<ushort> sourceBuffer = Buffer2D<ushort>.WrapMemory(source, Width, Height, Width);
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default);
        Av1EncoderTransformBlockState state = default;
        long distortion = Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
            workspace,
            new Buffer2DRegion<ushort>(sourceBuffer),
            Point.Empty,
            reconstruction,
            above,
            left,
            hasLeft: true,
            hasAbove: true,
            Av1PredictionMode.Directional135Degrees,
            angleDelta,
            enableIntraEdgeFilter: false,
            smoothIntraEdges: false,
            quantized,
            Av1TransformSize.Size8x8,
            Av1TransformType.DctDct,
            qIndex: 255,
            dcDeltaQ: 0,
            acDeltaQ: 0,
            Av1Plane.Y,
            Av1BitDepth.TwelveBit,
            ref state);

        Assert.Equal(0, distortion);
        Assert.Equal((ushort)0, state.EndOfBlock);
        Assert.True(source.AsSpan().SequenceEqual(reconstruction));
    }

    /// <summary>
    /// Verifies that complete eight-bit and high-bit-depth DC block encoding uses only caller-owned storage.
    /// </summary>
    [Fact]
    public void IntraDcBlockEncodingDoesNotAllocate()
    {
        const int Stride = 8;
        Av1TransformSize transformSize = Av1TransformSize.Size8x8;
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        byte[] source8 = new byte[Stride * Stride];
        byte[] reconstruction8 = new byte[Stride * Stride];
        byte[] above8 = new byte[Stride];
        byte[] left8 = new byte[Stride];
        ushort[] source10 = new ushort[Stride * Stride];
        ushort[] reconstruction10 = new ushort[Stride * Stride];
        ushort[] above10 = new ushort[Stride];
        ushort[] left10 = new ushort[Stride];
        int[] quantized = new int[coefficientCount];
        using Buffer2D<byte> sourceBuffer8 = Buffer2D<byte>.WrapMemory(source8, Stride, Stride);
        using Buffer2D<byte> reconstructionBuffer8 = Buffer2D<byte>.WrapMemory(reconstruction8, Stride, Stride);
        using Buffer2D<ushort> sourceBuffer10 = Buffer2D<ushort>.WrapMemory(source10, Stride, Stride);
        using Buffer2D<ushort> reconstructionBuffer10 = Buffer2D<ushort>.WrapMemory(reconstruction10, Stride, Stride);
        Buffer2DRegion<byte> sourcePlane8 = new(sourceBuffer8);
        Buffer2DRegion<byte> reconstructionPlane8 = new(reconstructionBuffer8);
        Buffer2DRegion<ushort> sourcePlane10 = new(sourceBuffer10);
        Buffer2DRegion<ushort> reconstructionPlane10 = new(reconstructionBuffer10);
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default);
        FillSource(source8, Stride, Stride, Stride, byte.MaxValue);
        FillSource(source10, Stride, Stride, Stride, 1023);
        Array.Fill(above8, (byte)103);
        Array.Fill(left8, (byte)127);
        Array.Fill(above10, (ushort)503);
        Array.Fill(left10, (ushort)527);
        Av1EncoderTransformBlockState state = default;

        Av1TransformBlockEncoder.EncodeIntraDcLossy(
            workspace,
            sourcePlane8,
            reconstructionPlane8,
            Point.Empty,
            above8,
            left8,
            true,
            true,
            quantized,
            transformSize,
            Av1TransformType.DctDct,
            73,
            -1,
            3,
            Av1Plane.Y,
            ref state);

        Av1TransformBlockEncoder.EncodeIntraDcLossy(
            workspace,
            sourcePlane10,
            reconstructionPlane10,
            Point.Empty,
            above10,
            left10,
            true,
            true,
            quantized,
            transformSize,
            Av1TransformType.DctDct,
            73,
            -1,
            3,
            Av1Plane.Y,
            Av1BitDepth.TenBit,
            ref state);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 16; iteration++)
        {
            Av1TransformBlockEncoder.EncodeIntraDcLossy(
                workspace,
                sourcePlane8,
                reconstructionPlane8,
                Point.Empty,
                above8,
                left8,
                true,
                true,
                quantized,
                transformSize,
                Av1TransformType.DctDct,
                73,
                -1,
                3,
                Av1Plane.Y,
                ref state);

            Av1TransformBlockEncoder.EncodeIntraDcLossy(
                workspace,
                sourcePlane10,
                reconstructionPlane10,
                Point.Empty,
                above10,
                left10,
                true,
                true,
                quantized,
                transformSize,
                Av1TransformType.DctDct,
                73,
                -1,
                3,
                Av1Plane.Y,
                Av1BitDepth.TenBit,
                ref state);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>
    /// Verifies that repeated maximum-transform block encoding uses only caller-owned workspaces.
    /// </summary>
    [Fact]
    public void LossyBlockEncodingDoesNotAllocate()
    {
        Av1TransformSize transformSize = Av1TransformSize.Size64x64;
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        int[] quantized = new int[coefficientCount];
        using Av1EncoderBlockWorkspace workspace = new(Configuration.Default);
        FillResidual(workspace.Residual, width, height, 4095);
        Av1EncoderTransformBlockState state = default;

        // Cross tiered-compilation call thresholds before measuring the steady-state transform kernel.
        for (int iteration = 0; iteration < 64; iteration++)
        {
            Av1TransformBlockEncoder.EncodeLossy(
                workspace,
                quantized,
                transformSize,
                Av1TransformType.DctDct,
                73,
                -1,
                3,
                Av1BitDepth.TwelveBit,
                ref state);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 16; iteration++)
        {
            Av1TransformBlockEncoder.EncodeLossy(
                workspace,
                quantized,
                transformSize,
                Av1TransformType.DctDct,
                73,
                -1,
                3,
                Av1BitDepth.TwelveBit,
                ref state);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>
    /// Verifies that the block workspace uses one exact-size allocator owner and returns it exactly once.
    /// </summary>
    [Fact]
    public void BlockWorkspaceUsesOneExactSizeOwner()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1EncoderBlockWorkspace workspace = new(configuration))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(int), allocation.ElementType);
            Assert.Equal(Av1EncoderBlockWorkspace.StorageLength, allocation.Length);
            Assert.Equal(Av1EncoderBlockWorkspace.MaximumResidualCount, workspace.Residual.Length);
            Assert.Equal(Av1EncoderBlockWorkspace.MaximumCoefficientCount, workspace.TransformCoefficients.Length);
            Assert.Equal(Av1EncoderBlockWorkspace.MaximumCoefficientCount, workspace.DequantizedCoefficients.Length);
            Assert.Equal(Av1TransformWorkspace.MaximumLength, workspace.TransformWorkspace.Length);

            Av1EncoderModeDecisionWorkspace<ushort> modeWorkspace = workspace.GetModeDecisionWorkspace<ushort>();
            Av1EncoderPaletteWorkspace<ushort> paletteWorkspace = modeWorkspace.Palette;
            Av1EncoderInterPredictionWorkspace<ushort> intraBlockCopyWorkspace =
                workspace.GetInterPredictionWorkspace<ushort>();

            Assert.Equal(
                (2 * Av1Constants.MaxTransformSize) + 1,
                modeWorkspace.GetReferenceSamples(3).Length);

            Assert.Equal(Av1EncoderModeDecisionWorkspace<ushort>.MaximumSampleCount, modeWorkspace.GetCandidateReconstruction(1).Length);
            Assert.Equal(Av1EncoderModeDecisionWorkspace<ushort>.MaximumSampleCount, modeWorkspace.GetCandidateCoefficients(1).Length);
            Assert.Equal(Av1EncoderModeDecisionWorkspace<ushort>.MaximumTransformSampleCount, modeWorkspace.Prediction.Length);
            Assert.Equal(Av1EncoderModeDecisionWorkspace<ushort>.MaximumTransformSampleCount, modeWorkspace.Residual.Length);
            Assert.Equal(Av1EncoderModeDecisionWorkspace<ushort>.MaximumCandidateTransformBlockCount, modeWorkspace.CandidateTransformBlocks.Length);
            Assert.Equal(2048, modeWorkspace.CandidateTransformBlocks.Length);

            // CfL is unavailable above 32x32, so its scratch remains fixed while larger partitions are enabled.
            Assert.Equal(Av1ChromaFromLumaContext.BufferLength, modeWorkspace.ChromaFromLumaSamples.Length);

            Assert.Equal(Av1ChromaFromLumaMath.AlphaCandidateCount, modeWorkspace.GetChromaFromLumaRates(1).Length);
            Assert.Equal(Av1ChromaFromLumaMath.AlphaCandidateCount, modeWorkspace.GetChromaFromLumaDistortions(1).Length);
            int maximumPaletteSampleCount =
                Av1BlockSize.Block64x64.GetWidth() * Av1BlockSize.Block64x64.GetHeight();

            Assert.Equal(maximumPaletteSampleCount, paletteWorkspace.GetPrediction(1).Length);
            Assert.Equal(maximumPaletteSampleCount, paletteWorkspace.AlternateIndices.Length);

            // Conventional mode search and IBC are sequential, so their typed views intentionally alias one owner region.
            modeWorkspace.GetReferenceSamples(0)[0] = 123;
            Assert.Equal((ushort)123, intraBlockCopyWorkspace.SelectedLumaReconstruction[0]);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    private static void ValidateBlock(
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1BitDepth bitDepth,
        int qIndex)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        int sampleMaximum = (1 << bitDepth.GetBitCount()) - 1;
        int[] expectedTransformed = new int[coefficientCount + 7];
        int[] expectedQuantized = new int[coefficientCount + 7];
        int[] expectedDequantized = new int[coefficientCount + 7];
        int[] actualQuantized = new int[coefficientCount + 7];
        int[] expectedWorkspace = new int[Av1TransformWorkspace.MaximumLength];
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Array.Fill(expectedTransformed, int.MinValue);
        Array.Fill(expectedQuantized, int.MinValue);
        Array.Fill(expectedDequantized, int.MinValue);
        Array.Fill(actualQuantized, int.MinValue);
        FillResidual(blockWorkspace.Residual, width, height, sampleMaximum);

        Av1ForwardTransformer.Transform2d(
            blockWorkspace.Residual,
            expectedTransformed.AsSpan(0, coefficientCount),
            (uint)width,
            transformType,
            transformSize,
            bitDepth.GetBitCount(),
            expectedWorkspace);

        ushort expectedEndOfBlock = Av1ForwardQuantizer.QuantizeLossy(
            expectedTransformed,
            expectedQuantized,
            expectedDequantized,
            transformSize,
            transformType,
            qIndex,
            -1,
            3,
            bitDepth);

        Av1EncoderTransformBlockState actualState = default;
        Av1TransformBlockEncoder.EncodeLossy(
            blockWorkspace,
            actualQuantized,
            transformSize,
            transformType,
            qIndex,
            -1,
            3,
            bitDepth,
            ref actualState);

        AssertEqual(expectedTransformed, blockWorkspace.TransformCoefficients, coefficientCount);
        Assert.Equal(expectedQuantized, actualQuantized);
        AssertEqual(expectedDequantized, blockWorkspace.DequantizedCoefficients, coefficientCount);
        Assert.Equal(expectedEndOfBlock, actualState.EndOfBlock);
        Assert.Equal(transformType, actualState.TransformType);
    }

    private static void FillResidual(Span<short> residual, int width, int height, int sampleMaximum)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                residual[index] = (short)((index & 3) switch
                {
                    0 => sampleMaximum,
                    1 => -sampleMaximum,
                    2 => ((index * 73) % ((2 * sampleMaximum) + 1)) - sampleMaximum,
                    _ => 0,
                });
            }
        }
    }

    private static void FillSource(Span<byte> source, int stride, int width, int height, int sampleMaximum)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                source[(y * stride) + x] = (byte)(((y * 43) + (x * 71) + 29) % (sampleMaximum + 1));
            }
        }
    }

    private static void FillSource(Span<ushort> source, int stride, int width, int height, int sampleMaximum)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                source[(y * stride) + x] = (ushort)(((y * 181) + (x * 313) + 97) % (sampleMaximum + 1));
            }
        }
    }

    private static void AssertEqual(ReadOnlySpan<int> expected, ReadOnlySpan<int> actual, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }
}

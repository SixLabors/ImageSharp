// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
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
        using Av1EncoderBlockWorkspace expectedWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace actualWorkspace = new(Configuration.Default);
        FillSource(source, SourceStride, width, height, byte.MaxValue);
        Array.Fill(expectedReconstruction, (byte)211);
        Array.Fill(actualReconstruction, (byte)211);
        Array.Fill(expectedQuantized, int.MinValue);
        Array.Fill(actualQuantized, int.MinValue);

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
            source,
            SourceStride,
            actualReconstruction,
            ReconstructionStride,
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

        Assert.Equal(expectedReconstruction, actualReconstruction);
        Assert.Equal(expectedQuantized, actualQuantized);
        Assert.Equal(expectedState.EndOfBlock, actualState.EndOfBlock);
        Assert.Equal(expectedState.TransformType, actualState.TransformType);
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
            source,
            SourceStride,
            actualReconstruction,
            ReconstructionStride,
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
            source8,
            Stride,
            reconstruction8,
            Stride,
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
            source10,
            Stride,
            reconstruction10,
            Stride,
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
                source8,
                Stride,
                reconstruction8,
                Stride,
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
                source10,
                Stride,
                reconstruction10,
                Stride,
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

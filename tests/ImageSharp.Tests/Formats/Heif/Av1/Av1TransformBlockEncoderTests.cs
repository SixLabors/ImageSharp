// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the finalized forward-transform and quantization block boundary.
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

    private static void AssertEqual(ReadOnlySpan<int> expected, ReadOnlySpan<int> actual, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }
}

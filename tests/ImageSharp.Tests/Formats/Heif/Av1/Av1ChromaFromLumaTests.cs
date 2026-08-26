// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 chroma-from-luma storage, parameter derivation, and prediction.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ChromaFromLumaTests
{
    /// <summary>
    /// The hardware configurations required to exercise each SIMD tier and the complete scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies Q3 luma storage for every AV1 chroma-subsampling layout.
    /// </summary>
    [Theory]
    [InlineData(false, false, new short[] { 8, 16, 24, 32, 40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128 })]
    [InlineData(true, false, new short[] { 12, 28, 44, 60, 76, 92, 108, 124 })]
    [InlineData(true, true, new short[] { 28, 44, 92, 108 })]
    public void Store8BitMatchesLibaomSubsampling(bool subX, bool subY, short[] expected)
    {
        ObuColorConfig colorConfig = new() { SubSamplingX = subX, SubSamplingY = subY };
        Av1ChromaFromLumaContext context = new(colorConfig);
        byte[] input = Enumerable.Range(1, 16).Select(x => (byte)x).ToArray();

        context.Store(input, 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 0, 0);

        int width = 4 >> (subX ? 1 : 0);
        int height = 4 >> (subY ? 1 : 0);
        Assert.Equal(expected, GetBlock(context.Q3Buffer, width, height));
    }

    /// <summary>
    /// Verifies that Q3 storage retains the complete 12-bit sample range.
    /// </summary>
    [Fact]
    public void StoreHighBitDepthPreservesTwelveBitQ3Range()
    {
        ObuColorConfig colorConfig = new();
        Av1ChromaFromLumaContext context = new(colorConfig);
        short[] input = Enumerable.Repeat((short)4095, 16).ToArray();

        context.Store(input, 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 0, 0);

        Assert.All(GetBlock(context.Q3Buffer, 4, 4), value => Assert.Equal(32760, value));
    }

    /// <summary>
    /// Verifies that sub-8-by-8 luma blocks are combined before the shared average is removed.
    /// </summary>
    [Fact]
    public void StoreCombinesSub8x8LumaBeforeSubtractingAverage()
    {
        ObuColorConfig colorConfig = new() { SubSamplingX = true, SubSamplingY = true };
        Av1ChromaFromLumaContext context = new(colorConfig);

        context.Store(Enumerable.Repeat((byte)10, 16).ToArray(), 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 0, 0);
        context.Store(Enumerable.Repeat((byte)20, 16).ToArray(), 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 0, 1);
        context.Store(Enumerable.Repeat((byte)30, 16).ToArray(), 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 1, 0);
        context.Store(Enumerable.Repeat((byte)40, 16).ToArray(), 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 1, 1);

        context.ComputeParameters(Av1TransformSize.Size4x4);

        short[] expected =
        [
            -120, -120, -40, -40,
            -120, -120, -40, -40,
            40, 40, 120, 120,
            40, 40, 120, 120
        ];

        Assert.Equal(expected, GetBlock(context.Q3Buffer, 4, 4));
    }

    /// <summary>
    /// Verifies that frame-edge extension precedes average subtraction.
    /// </summary>
    [Fact]
    public void ComputeParametersPadsFrameEdgeBeforeSubtractingAverage()
    {
        ObuColorConfig colorConfig = new();
        Av1ChromaFromLumaContext context = new(colorConfig);
        byte[] input = Enumerable.Range(1, 16).Select(x => (byte)x).ToArray();
        context.Store(input, 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 0, 0);

        context.ComputeParameters(Av1TransformSize.Size8x8);

        short[] actual = GetBlock(context.Q3Buffer, 8, 8);
        Assert.Equal(-90, actual[0]);
        Assert.Equal(-66, actual[7]);
        Assert.Equal(6, actual[56]);
        Assert.Equal(30, actual[63]);
        Assert.Equal(0, actual.Sum(x => x));
    }

    /// <summary>
    /// Verifies 8-bit CfL scaling and clipping with known values.
    /// </summary>
    [Fact]
    public void Predict8BitAddsScaledLumaAndClips()
    {
        short[] lumaQ3 = new short[32 * 32];
        new short[] { -64, -32, 64, 64 }.CopyTo(lumaQ3, 0);
        byte[] destination = [128, 128, 128, 128];

        Av1ChromaFromLumaPredictor.Predict(lumaQ3, destination, 4, 8, 4, 1);

        Assert.Equal(new byte[] { 120, 124, 136, 136 }, destination);
    }

    /// <summary>
    /// Verifies high-bit-depth CfL scaling and clipping with known values.
    /// </summary>
    [Theory]
    [InlineData((int)Av1BitDepth.TenBit, 1023)]
    [InlineData((int)Av1BitDepth.TwelveBit, 4095)]
    public void PredictHighBitDepthAddsScaledLumaAndClips(int bitDepthIndex, short maximum)
    {
        short[] lumaQ3 = new short[32 * 32];
        new short[] { -128, -64, 64, 128 }.CopyTo(lumaQ3, 0);
        short dc = (short)(maximum / 2);
        short[] destination = [dc, dc, dc, dc];

        Av1ChromaFromLumaPredictor.Predict(lumaQ3, destination, 4, 16, ((Av1BitDepth)bitDepthIndex).GetBitCount(), 4, 1);

        Assert.Equal(new short[] { (short)(dc - 32), (short)(dc - 16), (short)(dc + 16), (short)(dc + 32) }, destination);
    }

    /// <summary>
    /// Verifies exact 8-, 10-, and 12-bit CfL output and padding preservation across all intrinsic tiers.
    /// </summary>
    [Fact]
    public void PredictMatchesIndependentDefinitionAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePredictors, PredictorConfigurations);

    /// <summary>
    /// Verifies exact luma subsampling and average subtraction across all intrinsic tiers.
    /// </summary>
    [Fact]
    public void ContextOperationsMatchIndependentDefinitionAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateContextOperations, PredictorConfigurations);

    /// <summary>
    /// Verifies the plane-specific alpha magnitude and joint-sign mapping.
    /// </summary>
    [Theory]
    [InlineData((int)Av1Plane.U, 3)]
    [InlineData((int)Av1Plane.V, 4)]
    public void AlphaIndexSelectsMagnitudeForRequestedChromaPlane(int planeIndex, int expected)
    {
        // U occupies the high nibble and V occupies the low nibble in the packed AV1 alpha index.
        const int alphaIndex = 0x23;
        const int bothPositiveJointSign = 7;

        int actual = Av1PredictionDecoder.ChromaFromLumaIndexToAlpha(
            alphaIndex,
            bothPositiveJointSign,
            (Av1Plane)planeIndex);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Exercises every valid CfL width, both alpha signs, clipping boundaries, and padded destination rows.
    /// </summary>
    private static void ValidatePredictors()
    {
        int[] widths = [4, 8, 16, 32];
        int[] alphaValues = [-16, -9, 0, 7, 16];
        foreach (int width in widths)
        {
            const int height = 8;
            int stride = width + 5;
            short[] lumaQ3 = CreateLumaSurface(height);

            foreach (int alphaQ3 in alphaValues)
            {
                byte[] expected = CreateByteDestination(stride, height, 137);
                byte[] actual = (byte[])expected.Clone();
                ApplyReference(lumaQ3, expected, stride, alphaQ3, width, height);
                Av1ChromaFromLumaPredictor.Predict(lumaQ3, actual, stride, alphaQ3, width, height);
                Assert.Equal(expected, actual);

                foreach (int bitDepth in new[] { 10, 12 })
                {
                    short dc = (short)((1 << (bitDepth - 1)) + 53);
                    short[] expectedHigh = CreateHighBitDepthDestination(stride, height, dc);
                    short[] actualHigh = (short[])expectedHigh.Clone();
                    ApplyReference(lumaQ3, expectedHigh, stride, alphaQ3, bitDepth, width, height);
                    Av1ChromaFromLumaPredictor.Predict(lumaQ3, actualHigh, stride, alphaQ3, bitDepth, width, height);
                    Assert.Equal(expectedHigh, actualHigh);
                }
            }
        }
    }

    /// <summary>
    /// Exercises every CfL sampling layout, source width, sample precision, and padded minimum block extent.
    /// </summary>
    private static void ValidateContextOperations()
    {
        int[] sourceSizes = [4, 8, 16, 32];
        int[] samplingLayouts = [0, 1, 3];
        foreach (int sampling in samplingLayouts)
        {
            bool subX = (sampling & 1) != 0;
            bool subY = (sampling & 2) != 0;
            ObuColorConfig colorConfig = new() { SubSamplingX = subX, SubSamplingY = subY };

            foreach (int sourceSize in sourceSizes)
            {
                int stride = sourceSize + 3;
                Av1TransformSize sourceTransform = GetTransformSize(sourceSize, sourceSize);
                int activeWidth = sourceSize >> (subX ? 1 : 0);
                int activeHeight = sourceSize >> (subY ? 1 : 0);
                int targetWidth = Math.Max(activeWidth, 4);
                int targetHeight = Math.Max(activeHeight, 4);
                Av1TransformSize targetTransform = GetTransformSize(targetWidth, targetHeight);
                byte[] input = CreateByteInput(stride, sourceSize);
                short[] expected = CreateStoredReference(input, stride, sourceSize, subX, subY);
                PadAndSubtractAverage(expected, activeWidth, activeHeight, targetWidth, targetHeight);
                Av1ChromaFromLumaContext context = new(colorConfig);
                context.Store(input, stride, 0, 0, sourceTransform, sourceTransform.ToBlockSize(), 0, 0);
                context.ComputeParameters(targetTransform);
                Assert.Equal(expected, context.Q3Buffer);

                short[] highInput = CreateHighBitDepthInput(stride, sourceSize);
                expected = CreateStoredReference(highInput, stride, sourceSize, subX, subY);
                PadAndSubtractAverage(expected, activeWidth, activeHeight, targetWidth, targetHeight);
                context = new Av1ChromaFromLumaContext(colorConfig);
                context.Store(highInput, stride, 0, 0, sourceTransform, sourceTransform.ToBlockSize(), 0, 0);
                context.ComputeParameters(targetTransform);
                Assert.Equal(expected, context.Q3Buffer);
            }
        }
    }

    /// <summary>
    /// Maps dimensions used by the CfL operation matrix to their AV1 transform identifier.
    /// </summary>
    private static Av1TransformSize GetTransformSize(int width, int height)
        => (width, height) switch
        {
            (4, 4) => Av1TransformSize.Size4x4,
            (8, 8) => Av1TransformSize.Size8x8,
            (16, 16) => Av1TransformSize.Size16x16,
            (32, 32) => Av1TransformSize.Size32x32,
            (4, 8) => Av1TransformSize.Size4x8,
            (8, 4) => Av1TransformSize.Size8x4,
            (8, 16) => Av1TransformSize.Size8x16,
            (16, 8) => Av1TransformSize.Size16x8,
            (16, 32) => Av1TransformSize.Size16x32,
            (32, 16) => Av1TransformSize.Size32x16,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>
    /// Creates deterministic 8-bit luma rows with padding that must not contribute to CfL.
    /// </summary>
    private static byte[] CreateByteInput(int stride, int height)
    {
        byte[] result = Enumerable.Repeat((byte)251, stride * height).ToArray();
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < stride - 3; column++)
            {
                result[(row * stride) + column] = (byte)(((row * 67) + (column * 29) + 11) & byte.MaxValue);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates deterministic 12-bit luma rows with padding that must not contribute to CfL.
    /// </summary>
    private static short[] CreateHighBitDepthInput(int stride, int height)
    {
        short[] result = Enumerable.Repeat((short)4095, stride * height).ToArray();
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < stride - 3; column++)
            {
                result[(row * stride) + column] = (short)(((row * 977) + (column * 353) + 101) & 4095);
            }
        }

        return result;
    }

    /// <summary>
    /// Produces the normative Q3 luma surface for 8-bit input.
    /// </summary>
    private static short[] CreateStoredReference(byte[] input, int stride, int size, bool subX, bool subY)
    {
        short[] result = new short[32 * 32];
        int rowStep = subY ? 2 : 1;
        int columnStep = subX ? 2 : 1;
        int shift = subX ? (subY ? 1 : 2) : 3;
        for (int row = 0; row < size; row += rowStep)
        {
            for (int column = 0; column < size; column += columnStep)
            {
                int sum = input[(row * stride) + column];
                if (subX)
                {
                    sum += input[(row * stride) + column + 1];
                }

                if (subY)
                {
                    sum += input[((row + 1) * stride) + column] + input[((row + 1) * stride) + column + 1];
                }

                result[((row / rowStep) * 32) + (column / columnStep)] = (short)(sum << shift);
            }
        }

        return result;
    }

    /// <summary>
    /// Produces the normative Q3 luma surface for high-bit-depth input.
    /// </summary>
    private static short[] CreateStoredReference(short[] input, int stride, int size, bool subX, bool subY)
    {
        short[] result = new short[32 * 32];
        int rowStep = subY ? 2 : 1;
        int columnStep = subX ? 2 : 1;
        int shift = subX ? (subY ? 1 : 2) : 3;
        for (int row = 0; row < size; row += rowStep)
        {
            for (int column = 0; column < size; column += columnStep)
            {
                int sum = input[(row * stride) + column];
                if (subX)
                {
                    sum += input[(row * stride) + column + 1];
                }

                if (subY)
                {
                    sum += input[((row + 1) * stride) + column] + input[((row + 1) * stride) + column + 1];
                }

                result[((row / rowStep) * 32) + (column / columnStep)] = (short)(sum << shift);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies CfL edge extension and rounded average subtraction to an independently stored Q3 surface.
    /// </summary>
    private static void PadAndSubtractAverage(short[] buffer, int activeWidth, int activeHeight, int width, int height)
    {
        for (int row = 0; row < activeHeight; row++)
        {
            buffer.AsSpan((row * 32) + activeWidth, width - activeWidth).Fill(buffer[(row * 32) + activeWidth - 1]);
        }

        for (int row = activeHeight; row < height; row++)
        {
            buffer.AsSpan((row - 1) * 32, width).CopyTo(buffer.AsSpan(row * 32, width));
        }

        int sum = (width * height) >> 1;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                sum += buffer[(row * 32) + column];
            }
        }

        short average = (short)(sum >> (BitOperations.Log2((uint)width) + BitOperations.Log2((uint)height)));
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                buffer[(row * 32) + column] -= average;
            }
        }
    }

    /// <summary>
    /// Creates a deterministic Q3 surface spanning the legal signed 12-bit CfL range.
    /// </summary>
    private static short[] CreateLumaSurface(int height)
    {
        short[] result = new short[32 * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < 32; column++)
            {
                result[(row * 32) + column] = (short)(((row * 7919) + (column * 4051)) % 65521 - 32760);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates an 8-bit DC prediction with non-image padding sentinels.
    /// </summary>
    private static byte[] CreateByteDestination(int stride, int height, byte dc)
    {
        byte[] result = Enumerable.Repeat((byte)203, stride * height).ToArray();
        for (int row = 0; row < height; row++)
        {
            result.AsSpan(row * stride, stride - 5).Fill(dc);
        }

        return result;
    }

    /// <summary>
    /// Creates a high-bit-depth DC prediction with non-image padding sentinels.
    /// </summary>
    private static short[] CreateHighBitDepthDestination(int stride, int height, short dc)
    {
        short[] result = Enumerable.Repeat((short)-1, stride * height).ToArray();
        for (int row = 0; row < height; row++)
        {
            result.AsSpan(row * stride, stride - 5).Fill(dc);
        }

        return result;
    }

    /// <summary>
    /// Applies the AV1 signed Q3 rounding definition to an 8-bit destination.
    /// </summary>
    private static void ApplyReference(short[] lumaQ3, byte[] destination, int stride, int alphaQ3, int width, int height)
    {
        int dc = destination[0];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int product = alphaQ3 * lumaQ3[(row * 32) + column];
                int scaled = product < 0 ? -((-product + 32) >> 6) : (product + 32) >> 6;
                destination[(row * stride) + column] = (byte)Math.Clamp(dc + scaled, byte.MinValue, byte.MaxValue);
            }
        }
    }

    /// <summary>
    /// Applies the AV1 signed Q3 rounding definition to a high-bit-depth destination.
    /// </summary>
    private static void ApplyReference(short[] lumaQ3, short[] destination, int stride, int alphaQ3, int bitDepth, int width, int height)
    {
        int dc = destination[0];
        int maximum = (1 << bitDepth) - 1;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int product = alphaQ3 * lumaQ3[(row * 32) + column];
                int scaled = product < 0 ? -((-product + 32) >> 6) : (product + 32) >> 6;
                destination[(row * stride) + column] = (short)Math.Clamp(dc + scaled, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Extracts the active rows from the fixed-stride CfL buffer.
    /// </summary>
    private static short[] GetBlock(short[] buffer, int width, int height)
    {
        short[] result = new short[width * height];
        for (int y = 0; y < height; y++)
        {
            buffer.AsSpan(y * 32, width).CopyTo(result.AsSpan(y * width, width));
        }

        return result;
    }
}

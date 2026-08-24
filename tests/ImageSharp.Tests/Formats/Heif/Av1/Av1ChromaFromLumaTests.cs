// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1ChromaFromLumaTests
{
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

    [Fact]
    public void StoreHighBitDepthPreservesTwelveBitQ3Range()
    {
        ObuColorConfig colorConfig = new();
        Av1ChromaFromLumaContext context = new(colorConfig);
        short[] input = Enumerable.Repeat((short)4095, 16).ToArray();

        context.Store(input, 4, 0, 0, Av1TransformSize.Size4x4, Av1BlockSize.Block4x4, 0, 0);

        Assert.All(GetBlock(context.Q3Buffer, 4, 4), value => Assert.Equal(32760, value));
    }

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

    [Fact]
    public void Predict8BitAddsScaledLumaAndClips()
    {
        short[] lumaQ3 = new short[32 * 32];
        new short[] { -64, -32, 64, 64 }.CopyTo(lumaQ3, 0);
        byte[] dcPrediction = [0, 128, 250, 255];
        byte[] destination = new byte[4];

        Av1PredictionDecoder.ChromaFromLumaPredict(lumaQ3, dcPrediction, 4, destination, 4, 8, Av1BitDepth.EightBit, 4, 1);

        Assert.Equal(new byte[] { 0, 124, 255, 255 }, destination);
    }

    [Theory]
    [InlineData((int)Av1BitDepth.TenBit, 1023)]
    [InlineData((int)Av1BitDepth.TwelveBit, 4095)]
    public void PredictHighBitDepthAddsScaledLumaAndClips(int bitDepthIndex, short maximum)
    {
        short[] lumaQ3 = new short[32 * 32];
        new short[] { -128, -64, 64, 128 }.CopyTo(lumaQ3, 0);
        short[] dcPrediction = [5, (short)(maximum / 2), (short)(maximum - 5), maximum];
        short[] destination = new short[4];

        Av1PredictionDecoder.ChromaFromLumaPredict(
            lumaQ3,
            dcPrediction,
            4,
            destination,
            4,
            16,
            (Av1BitDepth)bitDepthIndex,
            4,
            1);

        Assert.Equal(new short[] { 0, (short)((maximum / 2) - 16), maximum, maximum }, destination);
    }

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

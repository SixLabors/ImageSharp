// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1CoefficientsEntropyTests
{
    private const int BaseQIndex = 23;

    [Fact]
    public void RoundTripZeroEndOfBlock()
    {
        // Assign
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        Av1TransformType transformType = Av1TransformType.Identity;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        ushort endOfBlock = 0;
        Av1BlockModeInfo modeInfo = new(Av1Constants.MaxPlanes, blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5];
        Span<int> expected = new int[16];
        Span<int> actuals = new int[16];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        decoder.ReadCoefficients(modeInfo, new Point(0, 0), aboveContexts, leftContexts, 0, 0, 0, 1, 1, transformBlockContext, transformSize, false, true, transformInfo, 0, 0, actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
        Assert.Equal(expected, actuals);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void RoundTripFullBlock(ushort endOfBlock)
    {
        // Assign
        const Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1TransformType transformType = Av1TransformType.Identity;
        const Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        const Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        Av1BlockModeInfo modeInfo = new(Av1Constants.MaxPlanes, blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        Span<int> actuals = new int[16 + 1];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        int plane = Math.Min((int)componentType, 1);
        decoder.ReadCoefficients(modeInfo, new Point(0, 0), aboveContexts, leftContexts, 0, 0, plane, 1, 1, transformBlockContext, transformSize, false, true, transformInfo, 0, 0, actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
    }

    [Theory]
    [MemberData(nameof(GetBlockSize4x4Data))]
    public void RoundTripFullCoefficientsYSize4x4(int bSize, int txSize, int txType)
    {
        // Assign
        const ushort endOfBlock = 16;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1BlockSize blockSize = (Av1BlockSize)bSize;
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        Av1BlockModeInfo modeInfo = new(Av1Constants.MaxPlanes, blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[transformSize.Get4x4WideCount()];
        int[] leftContexts = new int[transformSize.Get4x4HighCount()];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = Enumerable.Range(0, blockSize.GetHeight() * blockSize.GetWidth()).ToArray();
        Span<int> actuals = new int[16 + 1];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        int plane = Math.Min((int)componentType, 1);
        decoder.ReadCoefficients(modeInfo, new Point(0, 0), aboveContexts, leftContexts, 0, 0, plane, 1, 1, transformBlockContext, transformSize, false, true, transformInfo, 0, 0, actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
        Assert.Equal(coefficientsBuffer[..endOfBlock], actuals[1..(endOfBlock + 1)]);
    }

    public static TheoryData<int, int, int> GetBlockSize4x4Data()
    {
        TheoryData<int, int, int> result = [];
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
        for (Av1TransformType transformType = Av1TransformType.DctDct; transformType < Av1TransformType.VerticalDct; transformType++)
        {
            result.Add((int)blockSize, (int)transformSize, (int)transformType);
        }

        return result;
    }
}

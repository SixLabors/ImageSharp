// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using Microsoft.VisualBasic;
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
        const short transformBlockSkipContext = 0;
        const short dcSignContext = 0;
        const int txbIndex = 0;
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        Av1TransformType transformType = Av1TransformType.Identity;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        ushort endOfBlock = 16;
        Av1BlockModeInfo modeInfo = new(Av1Constants.MaxPlanes, blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = new();
        Configuration configuration = Configuration.Default;
        Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        Span<int> expected = new int[16];
        Span<int> actuals = new int[16];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, txbIndex, intraDirection, coefficientsBuffer, componentType, transformBlockSkipContext, dcSignContext, endOfBlock, true, BaseQIndex, filterIntraMode);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0);
        Av1SymbolReader reader = new(encoded.GetSpan());
        decoder.ReadCoefficients(modeInfo, new Point(0, 0), aboveContexts, leftContexts, 0, 0, 0, 1, 1, transformBlockContext, transformSize, false, true, transformInfo, 0, 0, actuals);

        // Assert
        Assert.Equal(expected, actuals);
    }

}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the range-decoder alignment used by AV1 motion-mode syntax.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionModeEntropyTests
{
    /// <summary>
    /// Verifies that both motion-mode alphabets leave the range decoder aligned for the immediately following filter symbol.
    /// </summary>
    /// <param name="allowWarpedMotion">Whether the motion-mode symbol uses the ternary rather than binary distribution.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesFollowingInterpolationSymbol(bool allowWarpedMotion)
    {
        Av1BlockSize blockSize = Av1BlockSize.Block16x16;
        const int interpolationContext = 3;
        Av1Distribution motionModeDistribution = allowWarpedMotion
            ? Av1DefaultDistributions.MotionMode[(int)blockSize]
            : Av1DefaultDistributions.Obmc[(int)blockSize];

        using Av1SymbolWriter writer = new(Configuration.Default, 2, updateCdf: true);
        writer.WriteSymbol((int)Av1MotionMode.SimpleTranslation, motionModeDistribution);
        writer.WriteSymbol(
            (int)Av1InterpolationFilter.Sharp,
            Av1DefaultDistributions.SwitchableInterpolation[interpolationContext]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        Assert.Equal(Av1MotionMode.SimpleTranslation, decoder.ReadMotionMode(blockSize, allowWarpedMotion));
        Assert.Equal(Av1InterpolationFilter.Sharp, decoder.ReadSwitchableInterpolationFilter(interpolationContext));
    }
}

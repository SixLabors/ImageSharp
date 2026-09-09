// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the entropy defaults, lifecycle, and range-decoder alignment used by AV1 motion-mode syntax.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionModeEntropyTests
{
    /// <summary>
    /// Gets the reference decoder's forward Q15 Simple Translation, OBMC, and Warped thresholds in block-size order.
    /// </summary>
    private static ReadOnlySpan<ushort> MotionModeForwardThresholds =>
    [
        10923, 21845,
        10923, 21845,
        10923, 21845,
        7651, 24760,
        4738, 24765,
        5391, 25528,
        19419, 26810,
        5123, 23606,
        11606, 24308,
        26260, 29116,
        20360, 28062,
        21679, 26830,
        29516, 30701,
        28898, 30397,
        30878, 31335,
        32507, 32558,
        10923, 21845,
        10923, 21845,
        28799, 31390,
        26431, 30774,
        28973, 31594,
        29742, 31203,
    ];

    /// <summary>
    /// Gets the reference decoder's forward Q15 Simple Translation and OBMC thresholds in block-size order.
    /// </summary>
    private static ReadOnlySpan<ushort> ObmcForwardThresholds =>
    [
        16384, 16384, 16384, 10437, 9371, 9301, 17432, 14423, 15142, 25817, 22823,
        22083, 30128, 31014, 31560, 32638, 16384, 16384, 23664, 20901, 24008, 26879,
    ];

    /// <summary>
    /// Verifies all twenty-two ternary and binary motion-mode distributions against the reference decoder's forward Q15 defaults.
    /// </summary>
    [Fact]
    public void DefaultsMatchReference()
    {
        const int blockSizeCount = (int)Av1BlockSize.AllSizes;
        const int ternaryThresholdCount = 2;
        ReadOnlySpan<ushort> motionModeForwardThresholds = MotionModeForwardThresholds;
        ReadOnlySpan<ushort> obmcForwardThresholds = ObmcForwardThresholds;
        Av1Distribution[] motionMode = Av1DefaultDistributions.MotionMode;
        Av1Distribution[] obmc = Av1DefaultDistributions.Obmc;

        Assert.Equal(blockSizeCount * ternaryThresholdCount, motionModeForwardThresholds.Length);
        Assert.Equal(blockSizeCount, obmcForwardThresholds.Length);
        Assert.Equal(blockSizeCount, motionMode.Length);
        Assert.Equal(blockSizeCount, obmc.Length);

        for (int blockSize = 0; blockSize < blockSizeCount; blockSize++)
        {
            Assert.Equal(3, motionMode[blockSize].NumberOfSymbols);
            Assert.Equal(2, obmc[blockSize].NumberOfSymbols);

            for (int threshold = 0; threshold < ternaryThresholdCount; threshold++)
            {
                // Av1Distribution stores inverse cumulative thresholds, so complement the reference decoder's published forward
                // Q15 values before comparing the exact state consumed by the range decoder.
                uint expected = (uint)Av1Distribution.ProbabilityTop -
                    motionModeForwardThresholds[(blockSize * ternaryThresholdCount) + threshold];

                Assert.Equal(expected, motionMode[blockSize][threshold]);
            }

            uint expectedObmc = (uint)Av1Distribution.ProbabilityTop - obmcForwardThresholds[blockSize];

            Assert.Equal(expectedObmc, obmc[blockSize][0]);
        }
    }

    /// <summary>
    /// Verifies that newly created frame contexts and explicit copies own independent motion-mode distributions.
    /// </summary>
    [Fact]
    public void FrameEntropyContextsDeepCopyAndCopyFromMotionModeState()
    {
        int blockSize = (int)Av1BlockSize.Block16x16;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);

        Assert.NotSame(source.MotionMode[blockSize], destination.MotionMode[blockSize]);
        Assert.NotSame(source.Obmc[blockSize], destination.Obmc[blockSize]);

        source.MotionMode[blockSize].Update((int)Av1MotionMode.Warped);
        source.Obmc[blockSize].Update((int)Av1MotionMode.Obmc);

        Assert.NotEqual(source.MotionMode[blockSize][0], destination.MotionMode[blockSize][0]);
        Assert.NotEqual(source.Obmc[blockSize][0], destination.Obmc[blockSize][0]);

        destination.CopyFrom(source);

        Assert.Equal(source.MotionMode[blockSize][0], destination.MotionMode[blockSize][0]);
        Assert.Equal(source.MotionMode[blockSize][1], destination.MotionMode[blockSize][1]);
        Assert.Equal(source.Obmc[blockSize][0], destination.Obmc[blockSize][0]);

        source.MotionMode[blockSize].Update((int)Av1MotionMode.SimpleTranslation);
        source.Obmc[blockSize].Update((int)Av1MotionMode.SimpleTranslation);

        Assert.NotEqual(source.MotionMode[blockSize][0], destination.MotionMode[blockSize][0]);
        Assert.NotEqual(source.Obmc[blockSize][0], destination.Obmc[blockSize][0]);
    }

    /// <summary>
    /// Verifies that resetting a frame context restores both motion-mode thresholds and adaptation history.
    /// </summary>
    [Fact]
    public void FrameEntropyResetRestoresMotionModeState()
    {
        const int updateCount = 20;
        int blockSize = (int)Av1BlockSize.Block32x16;
        Av1FrameEntropyContext context = new(0);
        Av1FrameEntropyContext expected = new(0);

        for (int update = 0; update < updateCount; update++)
        {
            context.MotionMode[blockSize].Update((int)Av1MotionMode.Warped);
            context.Obmc[blockSize].Update((int)Av1MotionMode.Obmc);
        }

        context.ResetToDefaults(0);

        Assert.Equal(expected.MotionMode[blockSize][0], context.MotionMode[blockSize][0]);
        Assert.Equal(expected.MotionMode[blockSize][1], context.MotionMode[blockSize][1]);
        Assert.Equal(expected.Obmc[blockSize][0], context.Obmc[blockSize][0]);

        // Equal thresholds can still carry different observation counts. Applying the same next symbols proves that
        // ResetToDefaults restored the update-rate history as well as the visible probability thresholds.
        context.MotionMode[blockSize].Update((int)Av1MotionMode.Obmc);
        expected.MotionMode[blockSize].Update((int)Av1MotionMode.Obmc);
        context.Obmc[blockSize].Update((int)Av1MotionMode.SimpleTranslation);
        expected.Obmc[blockSize].Update((int)Av1MotionMode.SimpleTranslation);

        Assert.Equal(expected.MotionMode[blockSize][0], context.MotionMode[blockSize][0]);
        Assert.Equal(expected.MotionMode[blockSize][1], context.MotionMode[blockSize][1]);
        Assert.Equal(expected.Obmc[blockSize][0], context.Obmc[blockSize][0]);
    }

    /// <summary>
    /// Verifies that a published frame snapshot retains adapted motion-mode thresholds but resets update counts.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsMotionModeUpdateCounts()
    {
        const int updateCount = 20;
        int blockSize = (int)Av1BlockSize.Block16x32;
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);

        for (int update = 0; update < updateCount; update++)
        {
            source.MotionMode[blockSize].Update((int)Av1MotionMode.Warped);
            source.Obmc[blockSize].Update((int)Av1MotionMode.Obmc);
        }

        source.SnapshotTo(snapshot);

        Assert.Equal(source.MotionMode[blockSize][0], snapshot.MotionMode[blockSize][0]);
        Assert.Equal(source.MotionMode[blockSize][1], snapshot.MotionMode[blockSize][1]);
        Assert.Equal(source.Obmc[blockSize][0], snapshot.Obmc[blockSize][0]);

        // The source retains twenty observations while the snapshot restarts at zero. Identical next observations
        // therefore move their equal starting thresholds by different update rates.
        source.MotionMode[blockSize].Update((int)Av1MotionMode.SimpleTranslation);
        snapshot.MotionMode[blockSize].Update((int)Av1MotionMode.SimpleTranslation);
        source.Obmc[blockSize].Update((int)Av1MotionMode.SimpleTranslation);
        snapshot.Obmc[blockSize].Update((int)Av1MotionMode.SimpleTranslation);

        Assert.NotEqual(source.MotionMode[blockSize][0], snapshot.MotionMode[blockSize][0]);
        Assert.NotEqual(source.Obmc[blockSize][0], snapshot.Obmc[blockSize][0]);
    }

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

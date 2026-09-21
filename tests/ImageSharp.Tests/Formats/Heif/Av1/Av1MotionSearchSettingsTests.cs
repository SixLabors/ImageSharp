// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1MotionSearchSettingsTests
{
    [Theory]
    [InlineData(HeifEncodingSpeed.Level0, false, 719, 70, Av1BlockSize.Block8x8, FullPixelSearchMethod.NStep)]
    [InlineData(HeifEncodingSpeed.Level0, false, 719, 71, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level0, false, 719, 200, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level0, false, 719, 201, Av1BlockSize.Block8x8, FullPixelSearchMethod.ClampedDiamond)]
    [InlineData(HeifEncodingSpeed.Level0, false, 720, 200, Av1BlockSize.Block8x8, FullPixelSearchMethod.NStep)]
    [InlineData(HeifEncodingSpeed.Level0, false, 720, 201, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level1, false, 719, 50, Av1BlockSize.Block8x8, FullPixelSearchMethod.NStep)]
    [InlineData(HeifEncodingSpeed.Level1, false, 719, 51, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level1, false, 720, 0, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level2, false, 719, 40, Av1BlockSize.Block8x8, FullPixelSearchMethod.NStep)]
    [InlineData(HeifEncodingSpeed.Level2, false, 719, 41, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level2, false, 719, 170, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level2, false, 719, 171, Av1BlockSize.Block8x8, FullPixelSearchMethod.ClampedDiamond)]
    [InlineData(HeifEncodingSpeed.Level2, false, 720, 200, Av1BlockSize.Block8x8, FullPixelSearchMethod.EightPointNStep)]
    [InlineData(HeifEncodingSpeed.Level2, false, 720, 201, Av1BlockSize.Block8x8, FullPixelSearchMethod.Diamond)]
    [InlineData(HeifEncodingSpeed.Level6, false, 719, 120, Av1BlockSize.Block32x64, FullPixelSearchMethod.Diamond)]
    [InlineData(HeifEncodingSpeed.Level6, false, 719, 120, Av1BlockSize.Block64x64, FullPixelSearchMethod.BigDiamond)]
    [InlineData(HeifEncodingSpeed.Level6, false, 720, 120, Av1BlockSize.Block64x128, FullPixelSearchMethod.Diamond)]
    [InlineData(HeifEncodingSpeed.Level6, false, 720, 120, Av1BlockSize.Block128x128, FullPixelSearchMethod.BigDiamond)]
    [InlineData(HeifEncodingSpeed.Level2, true, 719, 171, Av1BlockSize.Block8x8, FullPixelSearchMethod.ClampedDiamond)]
    [InlineData(HeifEncodingSpeed.Level6, true, 719, 120, Av1BlockSize.Block16x32, FullPixelSearchMethod.Diamond)]
    [InlineData(HeifEncodingSpeed.Level6, true, 719, 120, Av1BlockSize.Block32x32, FullPixelSearchMethod.BigDiamond)]
    public void FullPixelPatternUsesQuantizerResolutionAndBlockGeometry(
        HeifEncodingSpeed speed,
        bool intraOnly,
        int minimumDimension,
        int qIndex,
        int blockSize,
        int expected)
    {
        Av1MotionSearchSettings landscape = new(speed, intraOnly, new Size(1920, minimumDimension), qIndex, false, false);
        Av1MotionSearchSettings portrait = new(speed, intraOnly, new Size(minimumDimension, 1920), qIndex, false, false);

        Assert.Equal((FullPixelSearchMethod)expected, landscape.GetFullPixelMethod((Av1BlockSize)blockSize));
        Assert.Equal((FullPixelSearchMethod)expected, portrait.GetFullPixelMethod((Av1BlockSize)blockSize));
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level1, 719, false, CandidateSelection.RateDistortion, 0)]
    [InlineData(HeifEncodingSpeed.Level2, 719, false, CandidateSelection.Variance, 2)]
    [InlineData(HeifEncodingSpeed.Level3, 719, false, CandidateSelection.Variance, 2)]
    [InlineData(HeifEncodingSpeed.Level9, 719, true, CandidateSelection.RateDistortion, 1)]
    [InlineData(HeifEncodingSpeed.Level2, 720, false, CandidateSelection.FirstOnly, 1)]
    [InlineData(HeifEncodingSpeed.Level3, 720, true, CandidateSelection.RateDistortion, 1)]
    [InlineData(HeifEncodingSpeed.Level9, 720, true, CandidateSelection.RateDistortion, 1)]
    public void ResolutionAndFrameRoleOverrideInitialSecondCandidatePolicy(
        HeifEncodingSpeed speed,
        int minimumDimension,
        bool boostedFrame,
        int expectedSelection,
        int expectedAutomaticStepSize)
    {
        Av1MotionSearchSettings settings = new(speed, false, new Size(1920, minimumDimension), 120, boostedFrame, false);

        Assert.Equal((CandidateSelection)expectedSelection, settings.SecondCandidateSelection);
        Assert.Equal(expectedAutomaticStepSize, settings.AutomaticStepSizeLevel);
    }

    [Fact]
    public void IndependentFrameAndSequenceModesRetainDifferentMotionPolicies()
    {
        Av1MotionSearchSettings intraOnly = new(HeifEncodingSpeed.Level9, true, new Size(256, 256), 120, true, true);
        Av1MotionSearchSettings sequence = new(HeifEncodingSpeed.Level9, false, new Size(256, 256), 120, true, true);

        Assert.True(intraOnly.AllowIntraBlockCopy);
        Assert.True(intraOnly.PruneIntraBlockCopyHashCandidates);
        Assert.True(intraOnly.UseFastIntraBlockCopySearch);
        Assert.True(intraOnly.LimitIntraBlockCopyHashBlockSize);
        Assert.True(intraOnly.LimitFullPixelStartingCandidates);
        // Real-time keeps block copy available for screen content (rt_use_intrabc),
        // where good quality drops it from speed three (use_intrabc, L1312).
        Assert.True(sequence.AllowIntraBlockCopy);
        Assert.False(sequence.LimitFullPixelStartingCandidates);
        // All-intra keeps the eight-tap subpel filter of init_mv_sf; real-time drops to two taps
        // (use_accurate_subpel_search).
        Assert.Equal(8, intraOnly.FractionalInterpolationTaps);
        Assert.Equal(2, sequence.FractionalInterpolationTaps);
        Assert.Equal(2, intraOnly.FractionalIterationsPerStep);
        Assert.Equal(1, sequence.FractionalIterationsPerStep);
        // All-intra stops the simple motion search at half a sample from speed four; real-time keeps the
        // eighth-sample default (simple_motion_subpel_force_stop).
        Assert.Equal(SearchPrecision.HalfSample, intraOnly.SimpleMotionPrecision);
        Assert.Equal(SearchPrecision.EighthSample, sequence.SimpleMotionPrecision);
        // Screen content halves the all-intra mesh threshold and speed one doubles it again; real-time
        // never searches the mesh (exhaustive_searches_thresh).
        Assert.Equal(2_097_152, intraOnly.MeshErrorThreshold);
        Assert.Equal(int.MaxValue, sequence.MeshErrorThreshold);
    }

    [Theory]
    [InlineData(HeifEncodingSpeed.Level0, 0, 0, 2, FractionalSearchMethod.TwoLevelTree)]
    [InlineData(HeifEncodingSpeed.Level1, 0, 0, 2, FractionalSearchMethod.TwoLevelTree)]
    [InlineData(HeifEncodingSpeed.Level2, 0, 0, 1, FractionalSearchMethod.TwoLevelTree)]
    [InlineData(HeifEncodingSpeed.Level3, 1, 0, 1, FractionalSearchMethod.PrunedTree)]
    [InlineData(HeifEncodingSpeed.Level4, 2, 1, 1, FractionalSearchMethod.MorePrunedTree)]
    [InlineData(HeifEncodingSpeed.Level5, 2, 1, 1, FractionalSearchMethod.MorePrunedTree)]
    [InlineData(HeifEncodingSpeed.Level6, 2, 2, 1, FractionalSearchMethod.MorePrunedTree)]
    public void SequenceSearchStagesUseTheSelectedSpeedPolicy(
        HeifEncodingSpeed speed,
        int expectedMeshPruning,
        int expectedStartPruning,
        int expectedIterations,
        int expectedFractionalMethod)
    {
        Av1MotionSearchSettings settings = new(speed, false, new Size(256, 256), 120, false, false);

        Assert.Equal(expectedMeshPruning, settings.MeshPruningLevel);
        Assert.Equal(expectedStartPruning, settings.StartCandidatePruningLevel);
        Assert.Equal(expectedIterations, settings.FractionalIterationsPerStep);
        Assert.Equal((FractionalSearchMethod)expectedFractionalMethod, settings.FractionalMethod);
    }
}

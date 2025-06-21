// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossy;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class Vp8ModeScoreTests
{
    [Fact]
    public void InitScore_Works()
    {
        Vp8ModeScore score = new Vp8ModeScore();
        score.InitScore();
        Assert.Equal(0, score.D);
        Assert.Equal(0, score.SD);
        Assert.Equal(0, score.R);
        Assert.Equal(0, score.H);
        Assert.Equal(0u, score.Nz);
        Assert.Equal(Vp8ModeScore.MaxCost, score.Score);
    }

    [Fact]
    public void CopyScore_Works()
    {
        // arrange
        Vp8ModeScore score1 = new Vp8ModeScore
        {
            Score = 123,
            Nz = 1,
            D = 2,
            H = 3,
            ModeI16 = 4,
            ModeUv = 5,
            R = 6,
            SD = 7
        };
        Vp8ModeScore score2 = new Vp8ModeScore();
        score2.InitScore();

        // act
        score2.CopyScore(score1);

        // assert
        Assert.Equal(score1.D, score2.D);
        Assert.Equal(score1.SD, score2.SD);
        Assert.Equal(score1.R, score2.R);
        Assert.Equal(score1.H, score2.H);
        Assert.Equal(score1.Nz, score2.Nz);
        Assert.Equal(score1.Score, score2.Score);
    }

    [Fact]
    public void AddScore_Works()
    {
        // arrange
        Vp8ModeScore score1 = new Vp8ModeScore
        {
            Score = 123,
            Nz = 1,
            D = 2,
            H = 3,
            ModeI16 = 4,
            ModeUv = 5,
            R = 6,
            SD = 7
        };
        Vp8ModeScore score2 = new Vp8ModeScore
        {
            Score = 123,
            Nz = 1,
            D = 2,
            H = 3,
            ModeI16 = 4,
            ModeUv = 5,
            R = 6,
            SD = 7
        };

        // act
        score2.AddScore(score1);

        // assert
        Assert.Equal(4, score2.D);
        Assert.Equal(14, score2.SD);
        Assert.Equal(12, score2.R);
        Assert.Equal(6, score2.H);
        Assert.Equal(1u, score2.Nz);
        Assert.Equal(246, score2.Score);
    }
}

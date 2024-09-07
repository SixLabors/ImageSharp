// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests various companding algorithms. Expanded numbers are hand calculated from formulas online.
/// </summary>
public class CompandingTests
{
    private static readonly ApproximateFloatComparer Comparer = new(.000001F);

    [Fact]
    public void Rec2020Companding_IsCorrect()
    {
        Vector4 input = new(.667F);
        Vector4 e = Rec2020Companding.Expand(input);
        Vector4 c = Rec2020Companding.Compress(e);
        CompandingIsCorrectImpl(e, c, .44847462F, input);
    }

    [Fact]
    public void Rec709Companding_IsCorrect()
    {
        Vector4 input = new(.667F);
        Vector4 e = Rec709Companding.Expand(input);
        Vector4 c = Rec709Companding.Compress(e);
        CompandingIsCorrectImpl(e, c, .4483577F, input);
    }

    [Fact]
    public void SRgbCompanding_IsCorrect()
    {
        Vector4 input = new(.667F);
        Vector4 e = SRgbCompanding.Expand(input);
        Vector4 c = SRgbCompanding.Compress(e);
        CompandingIsCorrectImpl(e, c, .40242353F, input);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    public void SRgbCompanding_Expand_VectorSpan(int length)
    {
        Random rnd = new(42);
        Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
        Vector4[] expected = new Vector4[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            expected[i] = SRgbCompanding.Expand(source[i]);
        }

        SRgbCompanding.Expand(source);

        Assert.Equal(expected, source, Comparer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    public void SRgbCompanding_Compress_VectorSpan(int length)
    {
        Random rnd = new(42);
        Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
        Vector4[] expected = new Vector4[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            expected[i] = SRgbCompanding.Compress(source[i]);
        }

        SRgbCompanding.Compress(source);

        Assert.Equal(expected, source, Comparer);
    }

    [Fact]
    public void GammaCompanding_IsCorrect()
    {
        const double gamma = 2.2;
        Vector4 input = new(.667F);
        Vector4 e = GammaCompanding.Expand(input, gamma);
        Vector4 c = GammaCompanding.Compress(e, gamma);
        CompandingIsCorrectImpl(e, c, .41027668F, input);
    }

    [Fact]
    public void LCompanding_IsCorrect()
    {
        Vector4 input = new(.667F);
        Vector4 e = LCompanding.Expand(input);
        Vector4 c = LCompanding.Compress(e);
        CompandingIsCorrectImpl(e, c, .36236193F, input);
    }

    private static void CompandingIsCorrectImpl(Vector4 e, Vector4 c, float expanded, Vector4 compressed)
    {
        // W (alpha) is already the linear representation of the color.
        Assert.Equal(new(expanded, expanded, expanded, e.W), e, Comparer);
        Assert.Equal(compressed, c, Comparer);
    }
}

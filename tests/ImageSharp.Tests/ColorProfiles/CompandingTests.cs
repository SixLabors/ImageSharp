// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Companding;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests various companding algorithms. Expanded numbers are hand calculated from formulas online.
/// </summary>
public partial class CompandingTests
{
    // The recorded native power comparison covers 524,424 input/exponent pairs at each execution width.
    // Its largest relative error against the power formula is below 0.000464. Use 0.0005 for direct formula checks.
    // Extended-value tests scale by max(1, abs(expected)) to apply this bound to both SDR and HDR values.
    private const float DirectApproximationTolerance = 5E-4F;

    private static readonly ApproximateFloatComparer DirectComparer = new(DirectApproximationTolerance);

    // A round trip evaluates two approximate transfer functions. Allow the sum of their individual error bounds.
    private static readonly ApproximateFloatComparer RoundTripComparer = new(2F * DirectApproximationTolerance);

    // Keep scalar/SIMD agreement and sixteen-bit table accuracy separate from direct approximation accuracy.
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

    /// <summary>
    /// Checks published curve values and round trips with the error bounds for direct approximation.
    /// </summary>
    /// <param name="e">The expanded color.</param>
    /// <param name="c">The color after expansion and compression.</param>
    /// <param name="expanded">The expanded component calculated from the curve formula.</param>
    /// <param name="compressed">The original nonlinear color.</param>
    private static void CompandingIsCorrectImpl(Vector4 e, Vector4 c, float expanded, Vector4 compressed)
    {
        Assert.Equal(new Vector4(expanded, expanded, expanded, compressed.W), e, DirectComparer);
        Assert.Equal(compressed, c, RoundTripComparer);

        // Transfer functions change RGB only. Approximation error must never permit a change to alpha.
        Assert.Equal(compressed.W, e.W);
        Assert.Equal(compressed.W, c.W);
    }
}

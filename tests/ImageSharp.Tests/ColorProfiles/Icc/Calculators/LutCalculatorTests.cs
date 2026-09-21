// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

namespace SixLabors.ImageSharp.Tests.ColorProfiles.Icc.Calculators;

/// <summary>
/// Tests ICC <see cref="LutCalculator"/>
/// </summary>
[Trait("Color", "Conversion")]
public class LutCalculatorTests
{
    /// <summary>
    /// Verifies that forward lookup saturates at the table endpoints outside its normalized domain.
    /// </summary>
    /// <param name="input">The normalized lookup input.</param>
    /// <param name="expected">The expected table value.</param>
    [Theory]
    [InlineData(float.NaN, 0.25F)]
    [InlineData(float.NegativeInfinity, 0.25F)]
    [InlineData(float.MinValue, 0.25F)]
    [InlineData(-0.0001F, 0.25F)]
    [InlineData(0F, 0.25F)]
    [InlineData(0.5F, 0.5F)]
    [InlineData(1F, 0.75F)]
    [InlineData(1.0001F, 0.75F)]
    [InlineData(2F, 0.75F)]
    [InlineData(float.MaxValue, 0.75F)]
    [InlineData(float.PositiveInfinity, 0.75F)]
    public void ForwardLookup_ClampsToTableDomain(float input, float expected)
    {
        // Nonzero and nonunit endpoints distinguish table saturation from clamping the output to 0 or 1.
        LutCalculator calculator = new([0.25F, 0.5F, 0.75F], inverse: false);

        Assert.Equal(expected, calculator.Calculate(input));
    }

    [Theory]
    [MemberData(nameof(IccConversionDataLut.LutConversionTestData), MemberType = typeof(IccConversionDataLut))]
    internal void LutCalculator_WithLut_ReturnsResult(float[] lut, bool inverted, float input, float expected)
    {
        LutCalculator calculator = new(lut, inverted);

        float result = calculator.Calculate(input);

        Assert.Equal(expected, result, 4f);
    }
}

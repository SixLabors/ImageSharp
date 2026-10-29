// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Encoder.Noise;

/// <summary>
/// Tests noise-curve fitting for the JPEG XL encoder.
/// </summary>
public class NoiseEncoderTests
{
    /// <summary>
    /// Checks that an already optimal constant curve retains every lookup value.
    /// </summary>
    /// <param name="noiseLevel">The constant measured noise level.</param>
    [Theory]
    [InlineData(0F)]
    [InlineData(0.125F)]
    public void OptimizeNoiseParameters_ConstantMeasurements_PreservesFlatCurve(float noiseLevel)
    {
        // These intensities produce exact lookup positions or half-way fractions. A flat curve has zero fitting and smoothing error.
        // Its gradient is zero before the first search step, so the optimizer must not divide by the direction's zero length.
        List<JxlNoiseLevel> measurements =
        [
            new(noiseLevel, 0F),
            new(noiseLevel, 0.25F),
            new(noiseLevel, 0.5F),
            new(noiseLevel, 0.75F),
            new(noiseLevel, 1F),
        ];

        JxlNoiseParameters parameters = new();
        JxlNoiseEncoder.OptimizeNoiseParameters(measurements, parameters, 1F);

        foreach (float value in parameters.Lookup)
        {
            Assert.Equal(noiseLevel, value);
        }
    }
}

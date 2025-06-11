// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

/// <summary>
/// Represents an ICC calculator with a single floating point value and result
/// </summary>
internal interface ISingleCalculator
{
    /// <summary>
    /// Calculates a result from the given value
    /// </summary>
    /// <param name="value">The input value</param>
    /// <returns>The calculated result</returns>
    float Calculate(float value);
}

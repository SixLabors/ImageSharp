// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

/// <summary>
/// Represents an ICC calculator with <see cref="Vector4"/> values and results
/// </summary>
internal interface IVector4Calculator
{
    /// <summary>
    /// Calculates a result from the given values
    /// </summary>
    /// <param name="value">The input values</param>
    /// <returns>The calculated result</returns>
    Vector4 Calculate(Vector4 value);
}

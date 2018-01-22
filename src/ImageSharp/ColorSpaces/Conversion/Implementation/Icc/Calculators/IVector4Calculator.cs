// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators
{
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
}

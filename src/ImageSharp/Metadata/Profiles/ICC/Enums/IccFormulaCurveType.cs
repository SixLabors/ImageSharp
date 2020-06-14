// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Formula curve segment type
    /// </summary>
    internal enum IccFormulaCurveType : ushort
    {
        /// <summary>
        /// Type 1: Y = (a * X + b)^γ + c
        /// </summary>
        Type1 = 0,

        /// <summary>
        /// Type 1: Y = a * log10 (b * X^γ + c) + d
        /// </summary>
        Type2 = 1,

        /// <summary>
        /// Type 3: Y = a * b^(c * X + d) + e
        /// </summary>
        Type3 = 2
    }
}

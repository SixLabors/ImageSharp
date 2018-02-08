// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Color represented as a vector in its color space
    /// </summary>
    internal interface IColorVector
    {
        /// <summary>
        /// Gets the vector representation of the color
        /// </summary>
        Vector3 Vector { get; }
    }
}
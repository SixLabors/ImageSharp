// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A <see langword="delegate"/> representing a user defined processing delegate to use to modify image rows.
    /// </summary>
    /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
    /// <remarks>The <see cref="Vector4.X"/>, <see cref="Vector4.Y"/>, <see cref="Vector4.Z"/>, and <see cref="Vector4.W"/> fields map the RGBA channels respectively.</remarks>
    public delegate void PixelRowOperation(Span<Vector4> span);

    /// <summary>
    /// A <see langword="delegate"/> representing a user defined processing delegate to use to modify image rows.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the parameter of the method that this delegate encapsulates.
    /// This type parameter is contravariant.That is, you can use either the type you specified or any type that is less derived.
    /// </typeparam>
    /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
    /// <param name="value">The parameter of the method that this delegate encapsulates.</param>
    /// <remarks>The <see cref="Vector4.X"/>, <see cref="Vector4.Y"/>, <see cref="Vector4.Z"/>, and <see cref="Vector4.W"/> fields map the RGBA channels respectively.</remarks>
    public delegate void PixelRowOperation<in T>(Span<Vector4> span, T value);
}

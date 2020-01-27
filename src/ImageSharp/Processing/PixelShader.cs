// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A <see langword="delegate"/> representing a user defined pixel shader.
    /// </summary>
    /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
    /// <remarks>The <see cref="Vector4.X"/>, <see cref="Vector4.Y"/>, <see cref="Vector4.Z"/>, and <see cref="Vector4.W"/> fields map the RGBA channels respectively.</remarks>
    public delegate void PixelShader(Span<Vector4> span);
}

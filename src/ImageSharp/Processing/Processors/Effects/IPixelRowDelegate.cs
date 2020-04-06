// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// An <see langword="interface"/> used by the row delegates for a given <see cref="PixelRowDelegateProcessor{TPixel,TDelegate}"/> instance
    /// </summary>
    public interface IPixelRowDelegate
    {
        /// <summary>
        /// Applies the current pixel row delegate to a target row of preprocessed pixels.
        /// </summary>
        /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
        /// <remarks>The <see cref="Vector4.X"/>, <see cref="Vector4.Y"/>, <see cref="Vector4.Z"/>, and <see cref="Vector4.W"/> fields map the RGBA channels respectively.</remarks>
        void Invoke(Span<Vector4> span);
    }
}
